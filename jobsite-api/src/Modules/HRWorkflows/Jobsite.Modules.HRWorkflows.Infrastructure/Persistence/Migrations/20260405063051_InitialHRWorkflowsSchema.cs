using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHRWorkflowsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr_workflows");

            migrationBuilder.CreateTable(
                name: "final_interviews",
                schema: "hr_workflows",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    interview_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    scheduled_by = table.Column<Guid>(type: "uuid", nullable: false),
                    overall_recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    decision_notes = table.Column<string>(type: "text", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_interviews", x => x.application_id);
                    table.CheckConstraint("chk_final_interviews_interview_type", "interview_type IN ('InPerson', 'Video', 'Phone')");
                    table.CheckConstraint("chk_final_interviews_recommendation", "overall_recommendation IS NULL OR overall_recommendation IN ('StrongHire', 'Hire', 'NoHire', 'StrongNoHire')");
                    table.CheckConstraint("chk_final_interviews_status", "status IN ('Scheduled', 'InProgress', 'Completed', 'Cancelled', 'NoShow')");
                });

            migrationBuilder.CreateTable(
                name: "job_offers",
                schema: "hr_workflows",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    salary = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    salary_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    employment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    benefits = table.Column<string>(type: "text", nullable: true),
                    additional_terms = table.Column<string>(type: "text", nullable: true),
                    offer_letter_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    extended_by = table.Column<Guid>(type: "uuid", nullable: false),
                    extended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    withdrawal_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_offers", x => x.application_id);
                    table.CheckConstraint("chk_job_offers_employment_type", "employment_type IN ('FullTime', 'PartTime', 'Contract', 'Temporary')");
                    table.CheckConstraint("chk_job_offers_salary_period", "salary_period IN ('Annual', 'Monthly', 'Hourly')");
                    table.CheckConstraint("chk_job_offers_status", "status IN ('Draft', 'Pending', 'Accepted', 'Declined', 'Withdrawn', 'Expired')");
                });

            migrationBuilder.CreateTable(
                name: "interview_panelists",
                schema: "hr_workflows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<decimal>(type: "numeric(3,1)", nullable: true),
                    recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    strengths = table.Column<string>(type: "text", nullable: true),
                    concerns = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    feedback_submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_panelists", x => x.id);
                    table.CheckConstraint("chk_panelists_rating", "rating IS NULL OR (rating >= 1.0 AND rating <= 5.0)");
                    table.CheckConstraint("chk_panelists_recommendation", "recommendation IS NULL OR recommendation IN ('StrongHire', 'Hire', 'NoHire', 'StrongNoHire')");
                    table.ForeignKey(
                        name: "fk_interview_panelists_final_interviews_interview_id",
                        column: x => x.interview_id,
                        principalSchema: "hr_workflows",
                        principalTable: "final_interviews",
                        principalColumn: "application_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_final_interviews_recommendation",
                schema: "hr_workflows",
                table: "final_interviews",
                column: "overall_recommendation");

            migrationBuilder.CreateIndex(
                name: "ix_final_interviews_scheduled_at",
                schema: "hr_workflows",
                table: "final_interviews",
                column: "scheduled_at");

            migrationBuilder.CreateIndex(
                name: "ix_final_interviews_scheduled_by",
                schema: "hr_workflows",
                table: "final_interviews",
                column: "scheduled_by");

            migrationBuilder.CreateIndex(
                name: "ix_final_interviews_status",
                schema: "hr_workflows",
                table: "final_interviews",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_panelists_feedback_pending",
                schema: "hr_workflows",
                table: "interview_panelists",
                columns: new[] { "interview_id", "feedback_submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_panelists_interview_id",
                schema: "hr_workflows",
                table: "interview_panelists",
                column: "interview_id");

            migrationBuilder.CreateIndex(
                name: "ix_panelists_interviewer_id",
                schema: "hr_workflows",
                table: "interview_panelists",
                column: "interviewer_id");

            migrationBuilder.CreateIndex(
                name: "uq_panelists_interview_interviewer",
                schema: "hr_workflows",
                table: "interview_panelists",
                columns: new[] { "interview_id", "interviewer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_expires_at",
                schema: "hr_workflows",
                table: "job_offers",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_extended_by",
                schema: "hr_workflows",
                table: "job_offers",
                column: "extended_by");

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_status",
                schema: "hr_workflows",
                table: "job_offers",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interview_panelists",
                schema: "hr_workflows");

            migrationBuilder.DropTable(
                name: "job_offers",
                schema: "hr_workflows");

            migrationBuilder.DropTable(
                name: "final_interviews",
                schema: "hr_workflows");
        }
    }
}
