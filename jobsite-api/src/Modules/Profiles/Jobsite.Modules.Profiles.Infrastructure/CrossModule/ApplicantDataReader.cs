using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Profiles.Infrastructure.CrossModule;

/// <summary>
/// Provides applicant profile and resume data to the Screening module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class ApplicantDataReader : IApplicantDataReader
{
    private readonly ProfilesDbContext _db;

    public ApplicantDataReader(ProfilesDbContext db) => _db = db;

    public async Task<ApplicantDataSnapshot?> GetApplicantDataAsync(
        Guid applicantUserId, Guid? resumeId, CancellationToken ct = default)
    {
        string? profileSkills = await _db.ApplicantProfiles
            .AsNoTracking()
            .Where(p => p.Id == applicantUserId)
            .Select(p => p.Skills)
            .FirstOrDefaultAsync(ct);

        string? resumeParsedText = null;
        string? resumeExtractedSkills = null;
        string? aiParsedContent = null;

        if (resumeId is not null)
        {
            var resumeData = await _db.Resumes
                .AsNoTracking()
                .Where(r => r.Id == resumeId.Value && r.UserId == applicantUserId)
                .Select(r => new
                {
                    r.ParsedText,
                    r.ExtractedSkills,
                    r.AiParsedContent
                })
                .FirstOrDefaultAsync(ct);

            if (resumeData is not null)
            {
                resumeParsedText = resumeData.ParsedText;
                resumeExtractedSkills = resumeData.ExtractedSkills;
                aiParsedContent = resumeData.AiParsedContent;
            }
        }

        if (profileSkills is null && resumeParsedText is null && aiParsedContent is null)
            return null;

        return new ApplicantDataSnapshot
        {
            UserId = applicantUserId,
            ProfileSkills = profileSkills,
            ResumeParsedText = resumeParsedText,
            ResumeExtractedSkills = resumeExtractedSkills,
            AiParsedContent = aiParsedContent
        };
    }
}
