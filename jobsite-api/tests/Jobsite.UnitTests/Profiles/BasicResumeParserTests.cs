using FluentAssertions;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Constants;
using Jobsite.Modules.Profiles.Infrastructure.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UglyToad.PdfPig.Writer;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Jobsite.UnitTests.Profiles;

public sealed class BasicResumeParserTests : IDisposable
{
    private readonly string _uploadRoot;
    private readonly BasicResumeParser _sut;

    public BasicResumeParserTests()
    {
        _uploadRoot = Path.Combine(Path.GetTempPath(), $"resume_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_uploadRoot);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:FileStorage:UploadPath"] = _uploadRoot
            })
            .Build();

        ILogger<BasicResumeParser> logger = Substitute.For<ILogger<BasicResumeParser>>();
        _sut = new BasicResumeParser(configuration, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_uploadRoot))
            Directory.Delete(_uploadRoot, recursive: true);
    }

    private string CreateTestPdf(string text, string relativePath = "test.pdf")
    {
        string fullPath = Path.Combine(_uploadRoot, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        PdfDocumentBuilder builder = new();
        PdfPageBuilder page = builder.AddPage(595, 842); // A4 dimensions in points
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        byte[] bytes = builder.Build();
        File.WriteAllBytes(fullPath, bytes);
        return relativePath;
    }

    private string CreateTestDocx(string text, string relativePath = "test.docx")
    {
        string fullPath = Path.Combine(_uploadRoot, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        using WordprocessingDocument doc = WordprocessingDocument.Create(fullPath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(
            new Body(
                new Paragraph(
                    new Run(
                        new Text(text)))));
        doc.Save();
        return relativePath;
    }

    // ── PDF Parsing ──────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ValidPdf_ExtractsText()
    {
        // Arrange
        string relativePath = CreateTestPdf("Experienced software developer with C# and Python skills");

        // Act
        ResumeParseResult result = await _sut.ParseAsync(relativePath, FileType.Pdf, CancellationToken.None);

        // Assert
        result.ParsedText.Should().Contain("Experienced software developer");
    }

    [Fact]
    public async Task ParseAsync_ValidDocx_ExtractsText()
    {
        // Arrange
        string relativePath = CreateTestDocx("Senior engineer specializing in cloud architecture");

        // Act
        ResumeParseResult result = await _sut.ParseAsync(relativePath, FileType.Docx, CancellationToken.None);

        // Assert
        result.ParsedText.Should().Contain("Senior engineer");
    }

    // ── Skill Extraction ─────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_TextWithKnownSkills_ExtractsMatchingSkills()
    {
        // Arrange
        string relativePath = CreateTestPdf("Expert in C# and Python with SQL experience");

        // Act
        ResumeParseResult result = await _sut.ParseAsync(relativePath, FileType.Pdf, CancellationToken.None);

        // Assert
        result.ExtractedSkills.Should().NotBeNull();
        result.ExtractedSkills.Should().Contain("C#");
        result.ExtractedSkills.Should().Contain("Python");
        result.ExtractedSkills.Should().Contain("SQL");
    }

    [Fact]
    public async Task ParseAsync_TextWithNoSkills_ReturnsNullSkills()
    {
        // Arrange
        string relativePath = CreateTestPdf("Hello world this is a basic document");

        // Act
        ResumeParseResult result = await _sut.ParseAsync(relativePath, FileType.Pdf, CancellationToken.None);

        // Assert
        result.ExtractedSkills.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_SkillMatchingIsCaseInsensitive()
    {
        // Arrange — "python" lowercase should still match "Python" in KnownSkills
        string relativePath = CreateTestDocx("Proficient in python and docker");

        // Act
        ResumeParseResult result = await _sut.ParseAsync(relativePath, FileType.Docx, CancellationToken.None);

        // Assert
        result.ExtractedSkills.Should().NotBeNull();
        result.ExtractedSkills.Should().Contain("Python");
        result.ExtractedSkills.Should().Contain("Docker");
    }

    // ── Error Cases ──────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_UnsupportedFileType_ThrowsNotSupportedException()
    {
        // Arrange — create a real file so the existence check passes
        string relativePath = "test.txt";
        string fullPath = Path.Combine(_uploadRoot, relativePath);
        await File.WriteAllTextAsync(fullPath, "some content");

        // Act
        Func<Task> act = () => _sut.ParseAsync(relativePath, "TXT", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*TXT*");
    }

    [Fact]
    public async Task ParseAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        string relativePath = "nonexistent/resume.pdf";

        // Act
        Func<Task> act = () => _sut.ParseAsync(relativePath, FileType.Pdf, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ParseAsync_DocxFile_ExtractsSkillsFromDocx()
    {
        // Arrange
        string relativePath = CreateTestDocx("Developer with experience in React and TypeScript and Node.js");

        // Act
        ResumeParseResult result = await _sut.ParseAsync(relativePath, FileType.Docx, CancellationToken.None);

        // Assert
        result.ExtractedSkills.Should().NotBeNull();
        result.ExtractedSkills.Should().Contain("React");
        result.ExtractedSkills.Should().Contain("TypeScript");
    }
}
