using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence;

/// <summary>
/// Per-tenant DbContext for the Recruitment module.
/// Manages client companies, job postings, applications, criteria, and questions
/// in the <c>recruitment</c> schema.
/// </summary>
public sealed class RecruitmentDbContext : TenantDbContext
{
    public RecruitmentDbContext(DbContextOptions<RecruitmentDbContext> options,
        IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher)
    {
    }

    public DbSet<ClientCompany> ClientCompanies => Set<ClientCompany>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<JobEvaluationCriteria> JobEvaluationCriteria => Set<JobEvaluationCriteria>();
    public DbSet<JobScreeningQuestion> JobScreeningQuestions => Set<JobScreeningQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecruitmentDbContext).Assembly);
    }
}
