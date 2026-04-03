namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>Skill entry for the applicant's self-reported skills.</summary>
public sealed class SkillDto
{
    /// <summary>Skill name (e.g. "C#", "PostgreSQL").</summary>
    public required string Name { get; init; }

    /// <summary>Proficiency level: Beginner, Intermediate, Advanced, Expert.</summary>
    public string? Level { get; init; }

    /// <summary>Years of experience with this skill.</summary>
    public int? Years { get; init; }
}
