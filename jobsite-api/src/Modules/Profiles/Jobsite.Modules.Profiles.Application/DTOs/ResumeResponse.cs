namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>Response body for resume endpoints.</summary>
public sealed class ResumeResponse
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string FileUrl { get; init; }
    public required string OriginalFilename { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string FileType { get; init; }
    public required bool IsLatest { get; init; }
    public required bool IsParsed { get; init; }
    public string? ParseError { get; init; }
    public DateTime? ParsedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
