using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.Json;

namespace Jobsite.Modules.Profiles.Infrastructure.Parsing;

/// <summary>
/// Extracts plain text from PDF (via PdfPig) and DOCX (via OpenXml) files,
/// then matches keywords against a known skills list.
/// </summary>
public sealed class BasicResumeParser : IResumeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly HashSet<string> KnownSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "C#", ".NET", "ASP.NET", "Entity Framework", "EF Core", "SQL", "PostgreSQL",
        "SQL Server", "MySQL", "MongoDB", "Redis", "Docker", "Kubernetes", "Azure",
        "AWS", "GCP", "Git", "CI/CD", "REST", "GraphQL", "gRPC", "RabbitMQ",
        "Kafka", "MassTransit", "SignalR", "Blazor", "React", "Angular", "Vue",
        "TypeScript", "JavaScript", "Node.js", "Python", "Java", "Go", "Rust",
        "HTML", "CSS", "Tailwind", "Bootstrap", "SASS", "LESS",
        "Agile", "Scrum", "TDD", "DDD", "CQRS", "Microservices",
        "Linux", "Terraform", "Ansible", "Jenkins", "GitHub Actions",
        "Unit Testing", "Integration Testing", "xUnit", "NUnit", "Jest",
        "Machine Learning", "AI", "Data Science", "Power BI", "Tableau",
        "Excel", "Word", "PowerPoint", "Figma", "Photoshop",
        "Communication", "Leadership", "Problem Solving", "Teamwork",
        "Project Management", "JIRA", "Confluence"
    };

    private readonly string _uploadRoot;
    private readonly ILogger<BasicResumeParser> _logger;

    public BasicResumeParser(IConfiguration configuration, ILogger<BasicResumeParser> logger)
    {
        _uploadRoot = configuration["App:FileStorage:UploadPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _logger = logger;
    }

    public async Task<ResumeParseResult> ParseAsync(
        string fileUrl, string fileType, CancellationToken ct = default)
    {
        string fullPath = Path.Combine(_uploadRoot, fileUrl);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resume file not found at {fileUrl}");

        string text = fileType switch
        {
            FileType.Pdf => await ExtractTextFromPdfAsync(fullPath, ct),
            FileType.Docx => await ExtractTextFromDocxAsync(fullPath, ct),
            _ => throw new NotSupportedException($"Unsupported file type: {fileType}")
        };

        List<string> matchedSkills = ExtractSkills(text);
        string? extractedSkillsJson = matchedSkills.Count > 0
            ? JsonSerializer.Serialize(matchedSkills, JsonOptions)
            : null;

        _logger.LogInformation(
            "Parsed resume {FileUrl}: {TextLength} chars, {SkillCount} skills found",
            fileUrl, text.Length, matchedSkills.Count);

        return new ResumeParseResult
        {
            ParsedText = text,
            ExtractedSkills = extractedSkillsJson
        };
    }

    private static Task<string> ExtractTextFromPdfAsync(string path, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            StringBuilder sb = new();
            using PdfDocument document = PdfDocument.Open(path);

            foreach (Page page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                sb.AppendLine(page.Text);
            }

            return sb.ToString().Trim();
        }, ct);
    }

    private static Task<string> ExtractTextFromDocxAsync(string path, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
                return string.Empty;

            StringBuilder sb = new();
            foreach (Paragraph paragraph in body.Elements<Paragraph>())
            {
                ct.ThrowIfCancellationRequested();
                sb.AppendLine(paragraph.InnerText);
            }

            return sb.ToString().Trim();
        }, ct);
    }

    private static List<string> ExtractSkills(string text)
    {
        List<string> matched = [];

        foreach (string skill in KnownSkills)
        {
            if (text.Contains(skill, StringComparison.OrdinalIgnoreCase))
                matched.Add(skill);
        }

        matched.Sort(StringComparer.OrdinalIgnoreCase);
        return matched;
    }
}
