using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.UnitTests.Recruitment;

public sealed class RecruitmentConstantsTests
{
    // ── JobPostingStatus ─────────────────────────────────────────────────

    [Fact]
    public void JobPostingStatus_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        JobPostingStatus.IsValid(JobPostingStatus.Draft).Should().BeTrue();
        JobPostingStatus.IsValid(JobPostingStatus.Published).Should().BeTrue();
        JobPostingStatus.IsValid(JobPostingStatus.Closed).Should().BeTrue();
    }

    [Fact]
    public void JobPostingStatus_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        JobPostingStatus.IsValid("draft").Should().BeFalse();
        JobPostingStatus.IsValid("Active").Should().BeFalse();
        JobPostingStatus.IsValid("").Should().BeFalse();
    }

    // ── ApplicationStatus ────────────────────────────────────────────────

    [Fact]
    public void ApplicationStatus_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        ApplicationStatus.IsValid(ApplicationStatus.Submitted).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Screening).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Assessment).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Shortlisted).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.FinalInterview).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Offered).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Hired).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Rejected).Should().BeTrue();
        ApplicationStatus.IsValid(ApplicationStatus.Withdrawn).Should().BeTrue();
    }

    [Fact]
    public void ApplicationStatus_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        ApplicationStatus.IsValid("submitted").Should().BeFalse();
        ApplicationStatus.IsValid("Pending").Should().BeFalse();
        ApplicationStatus.IsValid("").Should().BeFalse();
    }

    // ── LocationType ─────────────────────────────────────────────────────

    [Fact]
    public void LocationType_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        LocationType.IsValid(LocationType.OnSite).Should().BeTrue();
        LocationType.IsValid(LocationType.Remote).Should().BeTrue();
        LocationType.IsValid(LocationType.Hybrid).Should().BeTrue();
    }

    [Fact]
    public void LocationType_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        LocationType.IsValid("onsite").Should().BeFalse();
        LocationType.IsValid("WFH").Should().BeFalse();
        LocationType.IsValid("").Should().BeFalse();
    }

    // ── EmploymentType ───────────────────────────────────────────────────

    [Fact]
    public void EmploymentType_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        EmploymentType.IsValid(EmploymentType.FullTime).Should().BeTrue();
        EmploymentType.IsValid(EmploymentType.PartTime).Should().BeTrue();
        EmploymentType.IsValid(EmploymentType.Contract).Should().BeTrue();
        EmploymentType.IsValid(EmploymentType.Temporary).Should().BeTrue();
        EmploymentType.IsValid(EmploymentType.Internship).Should().BeTrue();
    }

    [Fact]
    public void EmploymentType_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        EmploymentType.IsValid("fulltime").Should().BeFalse();
        EmploymentType.IsValid("Freelance").Should().BeFalse();
        EmploymentType.IsValid("").Should().BeFalse();
    }

    // ── CriteriaCategory ────────────────────────────────────────────────

    [Fact]
    public void CriteriaCategory_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        CriteriaCategory.IsValid(CriteriaCategory.Skill).Should().BeTrue();
        CriteriaCategory.IsValid(CriteriaCategory.Experience).Should().BeTrue();
        CriteriaCategory.IsValid(CriteriaCategory.Certification).Should().BeTrue();
        CriteriaCategory.IsValid(CriteriaCategory.Education).Should().BeTrue();
        CriteriaCategory.IsValid(CriteriaCategory.Location).Should().BeTrue();
        CriteriaCategory.IsValid(CriteriaCategory.Custom).Should().BeTrue();
    }

    [Fact]
    public void CriteriaCategory_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        CriteriaCategory.IsValid("skill").Should().BeFalse();
        CriteriaCategory.IsValid("Language").Should().BeFalse();
        CriteriaCategory.IsValid("").Should().BeFalse();
    }

    // ── EvaluationMethod ────────────────────────────────────────────────

    [Fact]
    public void EvaluationMethod_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        EvaluationMethod.IsValid(EvaluationMethod.ExactMatch).Should().BeTrue();
        EvaluationMethod.IsValid(EvaluationMethod.RangeMatch).Should().BeTrue();
        EvaluationMethod.IsValid(EvaluationMethod.SemanticSimilarity).Should().BeTrue();
    }

    [Fact]
    public void EvaluationMethod_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        EvaluationMethod.IsValid("exactmatch").Should().BeFalse();
        EvaluationMethod.IsValid("Fuzzy").Should().BeFalse();
        EvaluationMethod.IsValid("").Should().BeFalse();
    }

    // ── QuestionType ────────────────────────────────────────────────────

    [Fact]
    public void QuestionType_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        QuestionType.IsValid(QuestionType.FreeText).Should().BeTrue();
        QuestionType.IsValid(QuestionType.MultipleChoice).Should().BeTrue();
        QuestionType.IsValid(QuestionType.YesNo).Should().BeTrue();
    }

    [Fact]
    public void QuestionType_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        QuestionType.IsValid("freetext").Should().BeFalse();
        QuestionType.IsValid("Rating").Should().BeFalse();
        QuestionType.IsValid("").Should().BeFalse();
    }

    // ── QuestionTiming ──────────────────────────────────────────────────

    [Fact]
    public void QuestionTiming_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        QuestionTiming.IsValid(QuestionTiming.AtApplication).Should().BeTrue();
        QuestionTiming.IsValid(QuestionTiming.AfterScreening).Should().BeTrue();
    }

    [Fact]
    public void QuestionTiming_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        QuestionTiming.IsValid("atapplication").Should().BeFalse();
        QuestionTiming.IsValid("Before").Should().BeFalse();
        QuestionTiming.IsValid("").Should().BeFalse();
    }

    // ── RejectedAtStage ─────────────────────────────────────────────────

    [Fact]
    public void RejectedAtStage_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        RejectedAtStage.IsValid(RejectedAtStage.Screening).Should().BeTrue();
        RejectedAtStage.IsValid(RejectedAtStage.Assessment).Should().BeTrue();
        RejectedAtStage.IsValid(RejectedAtStage.Shortlisted).Should().BeTrue();
        RejectedAtStage.IsValid(RejectedAtStage.FinalInterview).Should().BeTrue();
        RejectedAtStage.IsValid(RejectedAtStage.Offered).Should().BeTrue();
    }

    [Fact]
    public void RejectedAtStage_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        RejectedAtStage.IsValid("screening").Should().BeFalse();
        RejectedAtStage.IsValid("Initial").Should().BeFalse();
        RejectedAtStage.IsValid("").Should().BeFalse();
    }

    // ── ClientCompanyStatus ─────────────────────────────────────────────

    [Fact]
    public void ClientCompanyStatus_IsValid_ValidValues_ReturnsTrue()
    {
        // Arrange & Act & Assert
        ClientCompanyStatus.IsValid(ClientCompanyStatus.Active).Should().BeTrue();
        ClientCompanyStatus.IsValid(ClientCompanyStatus.Inactive).Should().BeTrue();
    }

    [Fact]
    public void ClientCompanyStatus_IsValid_InvalidValues_ReturnsFalse()
    {
        // Arrange & Act & Assert
        ClientCompanyStatus.IsValid("active").Should().BeFalse();
        ClientCompanyStatus.IsValid("Suspended").Should().BeFalse();
        ClientCompanyStatus.IsValid("").Should().BeFalse();
    }
}
