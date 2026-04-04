using FluentAssertions;
using Jobsite.Modules.Profiles.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Profiles;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _uploadRoot;
    private readonly LocalFileStorage _sut;

    public LocalFileStorageTests()
    {
        _uploadRoot = Path.Combine(Path.GetTempPath(), $"storage_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_uploadRoot);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:FileStorage:UploadPath"] = _uploadRoot
            })
            .Build();

        ILogger<LocalFileStorage> logger = Substitute.For<ILogger<LocalFileStorage>>();
        _sut = new LocalFileStorage(configuration, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_uploadRoot))
            Directory.Delete(_uploadRoot, recursive: true);
    }

    // ── UploadAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_ValidFile_WritesToDisk()
    {
        // Arrange
        byte[] content = [0x50, 0x44, 0x46, 0x2D]; // PDF magic bytes
        using MemoryStream stream = new(content);

        // Act
        string relativePath = await _sut.UploadAsync(stream, "resume.pdf", "application/pdf", CancellationToken.None);

        // Assert
        string fullPath = Path.Combine(_uploadRoot, relativePath);
        File.Exists(fullPath).Should().BeTrue();
        byte[] written = await File.ReadAllBytesAsync(fullPath);
        written.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task UploadAsync_SanitizesFilename_RemovesInvalidChars()
    {
        // Arrange
        using MemoryStream stream = new([0x01]);
        string unsafeFileName = "bad<file>name.pdf";

        // Act
        string relativePath = await _sut.UploadAsync(stream, unsafeFileName, "application/pdf", CancellationToken.None);

        // Assert
        string fileName = Path.GetFileName(relativePath);
        char[] invalidChars = Path.GetInvalidFileNameChars();
        fileName.Should().NotContainAny(invalidChars.Select(c => c.ToString()).ToArray());
    }

    [Fact]
    public async Task UploadAsync_LongFilename_TruncatesTo200Chars()
    {
        // Arrange
        using MemoryStream stream = new([0x01]);
        string longName = new string('a', 300) + ".pdf";

        // Act
        string relativePath = await _sut.UploadAsync(stream, longName, "application/pdf", CancellationToken.None);

        // Assert
        string fullPath = Path.Combine(_uploadRoot, relativePath);
        File.Exists(fullPath).Should().BeTrue();
        // The sanitized part (after GUID prefix) should be at most 200 chars
        string fileName = Path.GetFileName(relativePath);
        // Format: {guid}_{sanitizedName} — guid is 32 chars + underscore = 33
        string sanitizedPart = fileName[(fileName.IndexOf('_') + 1)..];
        sanitizedPart.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task UploadAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        using MemoryStream stream = new([0x01]);

        // Act — the "resumes" subdirectory should be auto-created
        string relativePath = await _sut.UploadAsync(stream, "test.pdf", "application/pdf", CancellationToken.None);

        // Assert
        string resumesDir = Path.Combine(_uploadRoot, "resumes");
        Directory.Exists(resumesDir).Should().BeTrue();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesFromDisk()
    {
        // Arrange
        using MemoryStream stream = new([0x01]);
        string relativePath = await _sut.UploadAsync(stream, "to-delete.pdf", "application/pdf", CancellationToken.None);
        string fullPath = Path.Combine(_uploadRoot, relativePath);
        File.Exists(fullPath).Should().BeTrue();

        // Act
        await _sut.DeleteAsync(relativePath, CancellationToken.None);

        // Assert
        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentFile_DoesNotThrow()
    {
        // Arrange
        string fakePath = "resumes/nonexistent.pdf";

        // Act
        Func<Task> act = () => _sut.DeleteAsync(fakePath, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
