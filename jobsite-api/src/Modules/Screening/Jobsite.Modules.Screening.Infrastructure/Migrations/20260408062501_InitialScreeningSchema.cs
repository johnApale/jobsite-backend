using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobsite.Modules.Screening.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialScreeningSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "screening");

        migrationBuilder.CreateTable(
            name: "screening_question_responses",
            schema: "screening",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                application_id = table.Column<Guid>(type: "uuid", nullable: false),
                question_id = table.Column<Guid>(type: "uuid", nullable: false),
                response_text = table.Column<string>(type: "text", nullable: true),
                response_data = table.Column<string>(type: "jsonb", nullable: true),
                score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                score_result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                score_reasoning = table.Column<string>(type: "text", nullable: true),
                submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                scored_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_screening_question_responses", x => x.id);
                table.CheckConstraint("chk_question_responses_score_result", "score_result IS NULL OR score_result IN ('MeetsRequirement', 'PartialMatch', 'Missing')");
            });

        migrationBuilder.CreateTable(
            name: "screening_results",
            schema: "screening",
            columns: table => new
            {
                application_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                overall_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                match_strength = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                criteria_score_breakdown = table.Column<string>(type: "jsonb", nullable: true),
                ai_criteria_score_breakdown = table.Column<string>(type: "jsonb", nullable: true),
                ai_overall_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                question_score_breakdown = table.Column<string>(type: "jsonb", nullable: true),
                assessment_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                candidate_feedback = table.Column<string>(type: "jsonb", nullable: true),
                auto_advance_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                auto_reject_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                review_notes = table.Column<string>(type: "text", nullable: true),
                failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_screening_results", x => x.application_id);
                table.CheckConstraint("chk_screening_results_match_strength", "match_strength IS NULL OR match_strength IN ('Strong', 'Good', 'Moderate', 'Weak')");
                table.CheckConstraint("chk_screening_results_outcome", "outcome IS NULL OR outcome IN ('AutoAdvanced', 'AutoRejected', 'ManualReview', 'ManuallyAdvanced', 'ManuallyRejected')");
                table.CheckConstraint("chk_screening_results_status", "status IN ('Pending', 'InProgress', 'Completed', 'Failed')");
            });

        migrationBuilder.CreateIndex(
            name: "ix_question_responses_application_id",
            schema: "screening",
            table: "screening_question_responses",
            column: "application_id");

        migrationBuilder.CreateIndex(
            name: "uq_question_responses_app_question",
            schema: "screening",
            table: "screening_question_responses",
            columns: new[] { "application_id", "question_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_screening_results_match_strength",
            schema: "screening",
            table: "screening_results",
            column: "match_strength");

        migrationBuilder.CreateIndex(
            name: "ix_screening_results_outcome",
            schema: "screening",
            table: "screening_results",
            column: "outcome");

        migrationBuilder.CreateIndex(
            name: "ix_screening_results_overall_score",
            schema: "screening",
            table: "screening_results",
            column: "overall_score");

        migrationBuilder.CreateIndex(
            name: "ix_screening_results_status",
            schema: "screening",
            table: "screening_results",
            column: "status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "screening_question_responses",
            schema: "screening");

        migrationBuilder.DropTable(
            name: "screening_results",
            schema: "screening");
    }
}
