using Jobsite.Modules.Profiles.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files to the local filesystem.
/// Configured upload root via <c>App:FileStorage:UploadPath</c>.
/// Returns relative URL paths suitable for serving via static files middleware.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _uploadRoot;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        _uploadRoot = configuration["App:FileStorage:UploadPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream file, string fileName, string contentType, CancellationToken ct = default)
    {
        string sanitizedFileName = SanitizeFileName(fileName);
        string uniqueName = $"{Guid.NewGuid():N}_{sanitizedFileName}";
        string relativePath = Path.Combine("resumes", uniqueName);
        string fullPath = Path.Combine(_uploadRoot, relativePath);

        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await using FileStream stream = new(fullPath, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream, ct);

        _logger.LogInformation("Stored file {FileName} at {Path}", sanitizedFileName, relativePath);

        return relativePath;
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        string fullPath = Path.Combine(_uploadRoot, fileUrl);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted file at {Path}", fileUrl);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
