using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobsite.Modules.Recruitment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRecruitmentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recruitment");

            migrationBuilder.CreateTable(
                name: "client_companies",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_companies", x => x.id);
                    table.CheckConstraint("chk_client_companies_status", "status IN ('Active', 'Inactive')");
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    client_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    requirements = table.Column<string>(type: "text", nullable: true),
                    location_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    employment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    salary_min = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    salary_max = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closes_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_postings", x => x.id);
                    table.CheckConstraint("chk_job_postings_employment_type", "employment_type IN ('FullTime', 'PartTime', 'Contract', 'Temporary', 'Internship')");
                    table.CheckConstraint("chk_job_postings_location_type", "location_type IN ('OnSite', 'Remote', 'Hybrid')");
                    table.CheckConstraint("chk_job_postings_status", "status IN ('Draft', 'Published', 'Closed')");
                    table.ForeignKey(
                        name: "fk_job_postings_client_companies_client_company_id",
                        column: x => x.client_company_id,
                        principalSchema: "recruitment",
                        principalTable: "client_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resume_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cover_letter_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rejected_at_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("chk_applications_rejected_at_stage", "rejected_at_stage IS NULL OR rejected_at_stage IN ('Screening', 'Assessment', 'Shortlisted', 'FinalInterview', 'Offered')");
                    table.CheckConstraint("chk_applications_status", "status IN ('Submitted', 'Screening', 'Assessment', 'Shortlisted', 'FinalInterview', 'Offered', 'Hired', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "fk_applications_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalSchema: "recruitment",
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_evaluation_criteria",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evaluation_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_evaluation_criteria", x => x.id);
                    table.CheckConstraint("chk_criteria_category", "category IN ('Skill', 'Experience', 'Certification', 'Education', 'Location', 'Custom')");
                    table.CheckConstraint("chk_criteria_evaluation_method", "evaluation_method IN ('ExactMatch', 'RangeMatch', 'SemanticSimilarity')");
                    table.ForeignKey(
                        name: "fk_job_evaluation_criteria_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalSchema: "recruitment",
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_screening_questions",
                schema: "recruitment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    question_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    timing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    expected_answer = table.Column<string>(type: "jsonb", nullable: true),
                    options = table.Column<string>(type: "jsonb", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_screening_questions", x => x.id);
                    table.CheckConstraint("chk_questions_question_type", "question_type IN ('FreeText', 'MultipleChoice', 'YesNo')");
                    table.CheckConstraint("chk_questions_timing", "timing IN ('AtApplication', 'AfterScreening')");
                    table.ForeignKey(
                        name: "fk_job_screening_questions_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalSchema: "recruitment",
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_applicant_id",
                schema: "recruitment",
                table: "applications",
                column: "applicant_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_job_posting_id",
                schema: "recruitment",
                table: "applications",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_status",
                schema: "recruitment",
                table: "applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_applications_submitted_at",
                schema: "recruitment",
                table: "applications",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "uq_applications_applicant_job",
                schema: "recruitment",
                table: "applications",
                columns: new[] { "applicant_id", "job_posting_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_companies_name",
                schema: "recruitment",
                table: "client_companies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_client_companies_status",
                schema: "recruitment",
                table: "client_companies",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_criteria_category",
                schema: "recruitment",
                table: "job_evaluation_criteria",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_criteria_job_posting_id",
                schema: "recruitment",
                table: "job_evaluation_criteria",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_client_company",
                schema: "recruitment",
                table: "job_postings",
                column: "client_company_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_location",
                schema: "recruitment",
                table: "job_postings",
                columns: new[] { "city", "country" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_posted_by",
                schema: "recruitment",
                table: "job_postings",
                column: "posted_by");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_published_at",
                schema: "recruitment",
                table: "job_postings",
                column: "published_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_status",
                schema: "recruitment",
                table: "job_postings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_questions_job_posting_id",
                schema: "recruitment",
                table: "job_screening_questions",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_timing",
                schema: "recruitment",
                table: "job_screening_questions",
                column: "timing");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "job_evaluation_criteria",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "job_screening_questions",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "job_postings",
                schema: "recruitment");

            migrationBuilder.DropTable(
                name: "client_companies",
                schema: "recruitment");
        }
    }
}
