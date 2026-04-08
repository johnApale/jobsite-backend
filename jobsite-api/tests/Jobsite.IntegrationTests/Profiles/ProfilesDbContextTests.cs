using FluentAssertions;
using Jobsite.Modules.Profiles.Domain.Constants;
using Jobsite.Modules.Profiles.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Profiles;

/// <summary>
/// Integration tests validating ProfilesDbContext schema creation, table mapping,
/// CHECK constraints, indexes, and cascade behavior against a real PostgreSQL container.
/// </summary>
[Collection("Profiles")]
public sealed class ProfilesDbContextTests : IAsyncLifetime
{
    private readonly ProfilesIntegrationFixture _fixture;

    public ProfilesDbContextTests(ProfilesIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Schema ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Schema_ProfilesSchemaExists()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'profiles'";
        object? result = await cmd.ExecuteScalarAsync();
        string? schemaName = result?.ToString();

        // Assert
        schemaName.Should().Be("profiles");
    }

    // ─── ApplicantProfile persistence ────────────────────────────────────

    [Fact]
    public async Task ApplicantProfile_Persists_AllFieldsCorrectly()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        DateTime completedAt = DateTime.UtcNow;

        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Jane",
            LastName = "Doe",
            Phone = "+1-555-0100",
            City = "New York",
            Country = "US",
            Skills = """[{"name": "C#", "level": "Advanced", "years": 7}]""",
            SocialLinks = """{"linkedin": "https://linkedin.com/in/janedoe", "github": "https://github.com/janedoe"}""",
            Documents = """[{"type": "CoverLetter", "url": "https://cdn.example.com/cl.pdf", "filename": "cover.pdf"}]""",
            ProfileCompletedAt = completedAt
        };

        // Act
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ApplicantProfile? persisted = await _fixture.DbContext.ApplicantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(userId);
        persisted.FirstName.Should().Be("Jane");
        persisted.LastName.Should().Be("Doe");
        persisted.Phone.Should().Be("+1-555-0100");
        persisted.City.Should().Be("New York");
        persisted.Country.Should().Be("US");
        persisted.Skills.Should().Contain("C#");
        persisted.SocialLinks.Should().Contain("linkedin");
        persisted.Documents.Should().Contain("CoverLetter");
        persisted.ProfileCompletedAt.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ApplicantProfile_DefaultValues_AppliedByDatabase()
    {
        // Arrange — minimal required fields only
        ApplicantProfile profile = new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Min",
            LastName = "Profile"
        };

        // Act
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ApplicantProfile? persisted = await _fixture.DbContext.ApplicantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == profile.Id);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Phone.Should().BeNull();
        persisted.City.Should().BeNull();
        persisted.Country.Should().BeNull();
        persisted.Skills.Should().BeNull();
        persisted.SocialLinks.Should().BeNull();
        persisted.Documents.Should().BeNull();
        persisted.ProfileCompletedAt.Should().BeNull();
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    // ─── Resume persistence ──────────────────────────────────────────────

    [Fact]
    public async Task Resume_Persists_AllFieldsCorrectly()
    {
        // Arrange — need an ApplicantProfile first (FK constraint)
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User"
        };
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        DateTime parsedAt = DateTime.UtcNow;

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/resumes/test.pdf",
            OriginalFilename = "my_resume.pdf",
            FileSizeBytes = 524288,
            FileType = FileType.Pdf,
            IsLatest = true,
            IsParsed = true,
            ParsedText = "Full extracted text content here",
            ExtractedSkills = """[{"name": ".NET", "years": 5, "confidence": 0.95}]""",
            AiParsedContent = """{"skills": [".NET", "C#"], "education": ["BS CS"]}""",
            ParsedAt = parsedAt
        };

        // Act
        _fixture.DbContext.Resumes.Add(resume);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        Resume? persisted = await _fixture.DbContext.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.FileUrl.Should().Be("https://blob.storage/resumes/test.pdf");
        persisted.OriginalFilename.Should().Be("my_resume.pdf");
        persisted.FileSizeBytes.Should().Be(524288);
        persisted.FileType.Should().Be(FileType.Pdf);
        persisted.IsLatest.Should().BeTrue();
        persisted.IsParsed.Should().BeTrue();
        persisted.ParsedText.Should().Be("Full extracted text content here");
        persisted.ExtractedSkills.Should().Contain(".NET");
        persisted.AiParsedContent.Should().Contain("C#");
        persisted.ParsedAt.Should().BeCloseTo(parsedAt, TimeSpan.FromSeconds(1));
        persisted.Id.Should().NotBe(Guid.Empty);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Resume_DefaultValues_AppliedByDatabase()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User"
        };
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/resumes/min.pdf",
            OriginalFilename = "min.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };

        // Act
        _fixture.DbContext.Resumes.Add(resume);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        Resume? persisted = await _fixture.DbContext.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.IsLatest.Should().BeFalse();
        persisted.IsParsed.Should().BeFalse();
        persisted.ParsedText.Should().BeNull();
        persisted.ExtractedSkills.Should().BeNull();
        persisted.AiParsedContent.Should().BeNull();
        persisted.ParseError.Should().BeNull();
        persisted.ParsedAt.Should().BeNull();
    }

    // ─── CHECK constraints ───────────────────────────────────────────────

    [Fact]
    public async Task Resume_CheckConstraint_RejectsInvalidFileType()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User"
        };
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/resumes/test.exe",
            OriginalFilename = "malware.exe",
            FileSizeBytes = 1024,
            FileType = "EXE"
        };

        // Act
        _fixture.DbContext.Resumes.Add(resume);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Resume_CheckConstraint_AcceptsValidFileTypes()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Test",
            LastName = "User"
        };
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        Resume pdfResume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/resumes/test.pdf",
            OriginalFilename = "resume.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };

        Resume docxResume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/resumes/test.docx",
            OriginalFilename = "resume.docx",
            FileSizeBytes = 2048,
            FileType = FileType.Docx
        };

        // Act
        _fixture.DbContext.Resumes.Add(pdfResume);
        _fixture.DbContext.Resumes.Add(docxResume);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        List<Resume> resumes = await _fixture.DbContext.Resumes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();

        // Assert
        resumes.Should().HaveCount(2);
        resumes.Select(r => r.FileType).Should().BeEquivalentTo([FileType.Pdf, FileType.Docx]);
    }

    // ─── Cascade delete ──────────────────────────────────────────────────

    [Fact]
    public async Task CascadeDelete_DeletingProfile_DeletesResumes()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Cascade",
            LastName = "Test"
        };
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        Resume resume1 = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/r1.pdf",
            OriginalFilename = "r1.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };
        Resume resume2 = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/r2.docx",
            OriginalFilename = "r2.docx",
            FileSizeBytes = 2048,
            FileType = FileType.Docx
        };
        _fixture.DbContext.Resumes.AddRange(resume1, resume2);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act — delete the profile
        ApplicantProfile? tracked = await _fixture.DbContext.ApplicantProfiles
            .FirstOrDefaultAsync(p => p.Id == userId);
        _fixture.DbContext.ApplicantProfiles.Remove(tracked!);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Assert — resumes should be cascade-deleted
        List<Resume> orphanedResumes = await _fixture.DbContext.Resumes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();
        orphanedResumes.Should().BeEmpty();
    }

    // ─── Indexes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplicantProfiles_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'profiles' AND tablename = 'applicant_profiles'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_applicant_profiles_city_country");
    }

    [Fact]
    public async Task Resumes_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'profiles' AND tablename = 'resumes'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_resumes_user_id");
        indexes.Should().Contain("ix_resumes_is_parsed");
        indexes.Should().Contain("ix_resumes_user_id_is_latest");
    }
}
