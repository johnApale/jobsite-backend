using FluentAssertions;
using Jobsite.Modules.Profiles.Domain.Constants;

namespace Jobsite.UnitTests.Profiles;

public sealed class ProfileConstantsTests
{
    // ── FileType ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("PDF")]
    [InlineData("DOCX")]
    public void FileType_IsValid_ValidType_ReturnsTrue(string fileType)
    {
        bool result = FileType.IsValid(fileType);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("DOC")]
    [InlineData("TXT")]
    [InlineData("pdf")]
    [InlineData("")]
    public void FileType_IsValid_InvalidType_ReturnsFalse(string fileType)
    {
        bool result = FileType.IsValid(fileType);

        result.Should().BeFalse();
    }

    // ── SkillLevel ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Beginner")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    [InlineData("Expert")]
    public void SkillLevel_IsValid_ValidLevel_ReturnsTrue(string level)
    {
        bool result = SkillLevel.IsValid(level);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("beginner")]
    [InlineData("EXPERT")]
    [InlineData("Senior")]
    [InlineData("")]
    public void SkillLevel_IsValid_InvalidLevel_ReturnsFalse(string level)
    {
        bool result = SkillLevel.IsValid(level);

        result.Should().BeFalse();
    }
}
