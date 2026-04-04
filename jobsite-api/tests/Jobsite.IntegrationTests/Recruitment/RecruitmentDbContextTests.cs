using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.IntegrationTests.Recruitment;

/// <summary>
/// Integration tests validating RecruitmentDbContext schema creation, table mapping,
/// CHECK constraints, indexes, and entity persistence against a real PostgreSQL container.
/// </summary>
[Collection("Recruitment")]
public sealed class RecruitmentDbContextTests : IAsyncLifetime
{
    private readonly RecruitmentIntegrationFixture _fixture;

    public RecruitmentDbContextTests(RecruitmentIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Schema ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Schema_RecruitmentSchemaExists()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'recruitment'";
        object? result = await cmd.ExecuteScalarAsync();
        string? schemaName = result?.ToString();

        // Assert
        schemaName.Should().Be("recruitment");
    }

    // ─── ClientCompany persistence ───────────────────────────────────────

    [Fact]
    public async Task ClientCompany_Persists_AllFieldsCorrectly()
    {
        // Arrange
        ClientCompany company = new()
        {
            Name = "Acme Corporation",
            DisplayName = "Acme Corp",
            IsAnonymous = false,
            Industry = Industry.Technology,
            Website = "https://acme.example.com",
            ContactName = "Jane Doe",
            ContactEmail = "jane@acme.example.com",
            ContactPhone = "+1234567890",
            Notes = "Premium client",
            Status = ClientCompanyStatus.Active
        };

        // Act
        _fixture.DbContext.ClientCompanies.Add(company);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ClientCompany? persisted = await _fixture.DbContext.ClientCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == "Acme Corporation");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.DisplayName.Should().Be("Acme Corp");
        persisted.IsAnonymous.Should().BeFalse();
        persisted.Industry.Should().Be(Industry.Technology);
        persisted.Website.Should().Be("https://acme.example.com");
        persisted.ContactName.Should().Be("Jane Doe");
        persisted.ContactEmail.Should().Be("jane@acme.example.com");
        persisted.ContactPhone.Should().Be("+1234567890");
        persisted.Notes.Should().Be("Premium client");
        persisted.Status.Should().Be(ClientCompanyStatus.Active);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ClientCompany_CheckConstraint_RejectsInvalidStatus()
    {
        // Arrange
        ClientCompany company = new()
        {
            Name = "Bad Status Corp",
            Status = "Suspended"
        };

        // Act
        _fixture.DbContext.ClientCompanies.Add(company);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── JobPosting persistence ──────────────────────────────────────────

    [Fact]
    public async Task JobPosting_Persists_AllFieldsCorrectly()
    {
        // Arrange
        Guid postedBy = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        JobPosting posting = new()
        {
            Title = "Senior .NET Developer",
            Description = "Build modular monoliths with .NET 10",
            Requirements = "5+ years C# experience",
            LocationType = LocationType.Hybrid,
            City = "Manila",
            Country = "Philippines",
            EmploymentType = EmploymentType.FullTime,
            SalaryMin = 80000m,
            SalaryMax = 120000m,
            SalaryCurrency = "USD",
            Department = "Engineering",
            Status = JobPostingStatus.Published,
            PostedBy = postedBy,
            PublishedAt = now
        };

        // Act
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        JobPosting? persisted = await _fixture.DbContext.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Title == "Senior .NET Developer");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Description.Should().Contain(".NET 10");
        persisted.Requirements.Should().Contain("C#");
        persisted.LocationType.Should().Be(LocationType.Hybrid);
        persisted.City.Should().Be("Manila");
        persisted.Country.Should().Be("Philippines");
        persisted.EmploymentType.Should().Be(EmploymentType.FullTime);
        persisted.SalaryMin.Should().Be(80000m);
        persisted.SalaryMax.Should().Be(120000m);
        persisted.SalaryCurrency.Should().Be("USD");
        persisted.Department.Should().Be("Engineering");
        persisted.Status.Should().Be(JobPostingStatus.Published);
        persisted.PostedBy.Should().Be(postedBy);
        persisted.PublishedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task JobPosting_DefaultValues_AppliedByDatabase()
    {
        // Arrange — minimal required fields
        JobPosting posting = new()
        {
            Title = "Default Test Job",
            Description = "Test description",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.Contract,
            PostedBy = Guid.NewGuid()
        };

        // Act
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        JobPosting? persisted = await _fixture.DbContext.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Title == "Default Test Job");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(JobPostingStatus.Draft);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.SalaryMin.Should().BeNull();
        persisted.SalaryMax.Should().BeNull();
        persisted.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task JobPosting_CheckConstraint_RejectsInvalidStatus()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Bad Status Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            Status = "Archived",
            PostedBy = Guid.NewGuid()
        };

        // Act
        _fixture.DbContext.JobPostings.Add(posting);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task JobPosting_CheckConstraint_RejectsInvalidLocationType()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Bad Location Job",
            Description = "Test",
            LocationType = "InOffice",
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };

        // Act
        _fixture.DbContext.JobPostings.Add(posting);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task JobPosting_CheckConstraint_RejectsInvalidEmploymentType()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Bad Employment Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = "Freelance",
            PostedBy = Guid.NewGuid()
        };

        // Act
        _fixture.DbContext.JobPostings.Add(posting);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── Application persistence ─────────────────────────────────────────

    [Fact]
    public async Task Application_Persists_AllFieldsCorrectly()
    {
        // Arrange — need a JobPosting first (FK constraint)
        JobPosting posting = new()
        {
            Title = "App Test Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        Guid applicantId = Guid.NewGuid();
        Guid resumeId = Guid.NewGuid();
        DateTime submittedAt = DateTime.UtcNow;

        ApplicationEntity application = new()
        {
            JobPostingId = posting.Id,
            ApplicantId = applicantId,
            Status = ApplicationStatus.Submitted,
            ResumeId = resumeId,
            CoverLetterUrl = "https://storage.example.com/cover.pdf",
            SubmittedAt = submittedAt
        };

        // Act
        _fixture.DbContext.Applications.Add(application);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ApplicationEntity? persisted = await _fixture.DbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ApplicantId == applicantId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.JobPostingId.Should().Be(posting.Id);
        persisted.Status.Should().Be(ApplicationStatus.Submitted);
        persisted.ResumeId.Should().Be(resumeId);
        persisted.CoverLetterUrl.Should().Be("https://storage.example.com/cover.pdf");
        persisted.SubmittedAt.Should().BeCloseTo(submittedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Application_CheckConstraint_RejectsInvalidStatus()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "App Check Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        ApplicationEntity application = new()
        {
            JobPostingId = posting.Id,
            ApplicantId = Guid.NewGuid(),
            Status = "InReview",
            ResumeId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.Applications.Add(application);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Application_UniqueConstraint_PreventsDuplicateApplicantJob()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Unique Constraint Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        Guid applicantId = Guid.NewGuid();

        ApplicationEntity first = new()
        {
            JobPostingId = posting.Id,
            ApplicantId = applicantId,
            ResumeId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow
        };
        _fixture.DbContext.Applications.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        ApplicationEntity duplicate = new()
        {
            JobPostingId = posting.Id,
            ApplicantId = applicantId,
            ResumeId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.Applications.Add(duplicate);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── JobEvaluationCriteria persistence ───────────────────────────────

    [Fact]
    public async Task JobEvaluationCriteria_Persists_AllFieldsCorrectly()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Criteria Test Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = posting.Id,
            Name = "C# Proficiency",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.SemanticSimilarity,
            IsRequired = true,
            Weight = 30.00m,
            Configuration = """{"min_years": 5, "keywords": ["C#", ".NET"]}""",
            DisplayOrder = 1
        };

        // Act
        _fixture.DbContext.JobEvaluationCriteria.Add(criteria);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        JobEvaluationCriteria? persisted = await _fixture.DbContext.JobEvaluationCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == "C# Proficiency");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.JobPostingId.Should().Be(posting.Id);
        persisted.Category.Should().Be(CriteriaCategory.Skill);
        persisted.EvaluationMethod.Should().Be(EvaluationMethod.SemanticSimilarity);
        persisted.IsRequired.Should().BeTrue();
        persisted.Weight.Should().Be(30.00m);
        persisted.Configuration.Should().Contain("C#");
        persisted.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task JobEvaluationCriteria_CheckConstraint_RejectsInvalidCategory()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Criteria Check Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = posting.Id,
            Name = "Invalid Criteria",
            Category = "Personality",
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = 10m,
            Configuration = "{}",
            DisplayOrder = 0
        };

        // Act
        _fixture.DbContext.JobEvaluationCriteria.Add(criteria);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task JobEvaluationCriteria_CheckConstraint_RejectsInvalidEvaluationMethod()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Method Check Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = posting.Id,
            Name = "Invalid Method",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = "FuzzyMatch",
            Weight = 10m,
            Configuration = "{}",
            DisplayOrder = 0
        };

        // Act
        _fixture.DbContext.JobEvaluationCriteria.Add(criteria);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task JobEvaluationCriteria_JsonbConfiguration_PersistsCorrectly()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "JSONB Criteria Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        string jsonConfig = """{"min_years": 3, "max_years": 10, "keywords": ["Docker", "Kubernetes", "CI/CD"]}""";
        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = posting.Id,
            Name = "DevOps Experience",
            Category = CriteriaCategory.Experience,
            EvaluationMethod = EvaluationMethod.RangeMatch,
            Weight = 25m,
            Configuration = jsonConfig,
            DisplayOrder = 2
        };

        // Act
        _fixture.DbContext.JobEvaluationCriteria.Add(criteria);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        JobEvaluationCriteria? persisted = await _fixture.DbContext.JobEvaluationCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == "DevOps Experience");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Configuration.Should().Contain("Docker");
        persisted.Configuration.Should().Contain("Kubernetes");
        persisted.Configuration.Should().Contain("CI/CD");
    }

    // ─── JobScreeningQuestion persistence ────────────────────────────────

    [Fact]
    public async Task JobScreeningQuestion_Persists_AllFieldsCorrectly()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Question Test Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobScreeningQuestion question = new()
        {
            JobPostingId = posting.Id,
            QuestionText = "Are you authorized to work in the Philippines?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            IsRequired = true,
            Weight = 50m,
            ExpectedAnswer = """{"correct": true}""",
            DisplayOrder = 1
        };

        // Act
        _fixture.DbContext.JobScreeningQuestions.Add(question);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        JobScreeningQuestion? persisted = await _fixture.DbContext.JobScreeningQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.QuestionText == "Are you authorized to work in the Philippines?");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.QuestionType.Should().Be(QuestionType.YesNo);
        persisted.Timing.Should().Be(QuestionTiming.AtApplication);
        persisted.IsRequired.Should().BeTrue();
        persisted.Weight.Should().Be(50m);
        persisted.ExpectedAnswer.Should().Contain("correct");
        persisted.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task JobScreeningQuestion_CheckConstraint_RejectsInvalidQuestionType()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "QType Check Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobScreeningQuestion question = new()
        {
            JobPostingId = posting.Id,
            QuestionText = "Invalid type question",
            QuestionType = "Essay",
            Timing = QuestionTiming.AtApplication,
            Weight = 10m,
            DisplayOrder = 0
        };

        // Act
        _fixture.DbContext.JobScreeningQuestions.Add(question);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task JobScreeningQuestion_CheckConstraint_RejectsInvalidTiming()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Timing Check Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobScreeningQuestion question = new()
        {
            JobPostingId = posting.Id,
            QuestionText = "Invalid timing question",
            QuestionType = QuestionType.FreeText,
            Timing = "DuringInterview",
            Weight = 10m,
            DisplayOrder = 0
        };

        // Act
        _fixture.DbContext.JobScreeningQuestions.Add(question);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task JobScreeningQuestion_JsonbOptions_PersistsCorrectly()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "JSONB Question Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        string options = """["ASP.NET", "Spring Boot", "Django", "Express"]""";
        JobScreeningQuestion question = new()
        {
            JobPostingId = posting.Id,
            QuestionText = "Which framework do you prefer?",
            QuestionType = QuestionType.MultipleChoice,
            Timing = QuestionTiming.AtApplication,
            Weight = 15m,
            Options = options,
            ExpectedAnswer = """{"correct_options": [0, 1], "partial_credit": true}""",
            DisplayOrder = 2
        };

        // Act
        _fixture.DbContext.JobScreeningQuestions.Add(question);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        JobScreeningQuestion? persisted = await _fixture.DbContext.JobScreeningQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.QuestionText == "Which framework do you prefer?");

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Options.Should().Contain("ASP.NET");
        persisted.Options.Should().Contain("Django");
        persisted.ExpectedAnswer.Should().Contain("partial_credit");
    }

    // ─── Indexes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task JobPostings_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'recruitment' AND tablename = 'job_postings'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_job_postings_status");
        indexes.Should().Contain("ix_job_postings_posted_by");
        indexes.Should().Contain("ix_job_postings_published_at");
        indexes.Should().Contain("ix_job_postings_client_company");
        indexes.Should().Contain("ix_job_postings_location");
    }

    [Fact]
    public async Task Applications_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'recruitment' AND tablename = 'applications'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_applications_job_posting_id");
        indexes.Should().Contain("ix_applications_applicant_id");
        indexes.Should().Contain("ix_applications_status");
        indexes.Should().Contain("ix_applications_submitted_at");
    }

    // ─── Cascade delete ─────────────────────────────────────────────────

    [Fact]
    public async Task JobPosting_CascadeDelete_RemovesChildEntities()
    {
        // Arrange
        JobPosting posting = new()
        {
            Title = "Cascade Delete Job",
            Description = "Test cascade",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            PostedBy = Guid.NewGuid()
        };
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = posting.Id,
            Name = "Cascade Criterion",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = 10m,
            Configuration = "{}",
            DisplayOrder = 0
        };
        JobScreeningQuestion question = new()
        {
            JobPostingId = posting.Id,
            QuestionText = "Cascade question?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            Weight = 10m,
            DisplayOrder = 0
        };
        _fixture.DbContext.JobEvaluationCriteria.Add(criteria);
        _fixture.DbContext.JobScreeningQuestions.Add(question);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        _fixture.DbContext.JobPostings.Remove(posting);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();

        // Assert
        JobEvaluationCriteria? deletedCriteria = await _fixture.DbContext.JobEvaluationCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == criteria.Id);
        JobScreeningQuestion? deletedQuestion = await _fixture.DbContext.JobScreeningQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == question.Id);

        deletedCriteria.Should().BeNull();
        deletedQuestion.Should().BeNull();
    }
}
