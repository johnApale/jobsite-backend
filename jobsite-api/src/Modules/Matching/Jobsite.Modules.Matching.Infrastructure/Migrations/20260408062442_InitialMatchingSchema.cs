using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobsite.Modules.Matching.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMatchingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "matching");

            migrationBuilder.CreateTable(
                name: "candidate_matches",
                schema: "matching",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    screening_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    assessment_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    composite_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    match_strength = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    screening_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assessment_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_matches", x => x.application_id);
                    table.CheckConstraint("chk_matches_match_strength", "match_strength IS NULL OR match_strength IN ('Strong', 'Good', 'Moderate', 'Weak')");
                });

            migrationBuilder.CreateTable(
                name: "shortlists",
                schema: "matching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    total_candidates = table.Column<int>(type: "integer", nullable: false),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shortlists", x => x.id);
                    table.CheckConstraint("chk_shortlists_status", "status IN ('Draft', 'Finalized')");
                });

            migrationBuilder.CreateTable(
                name: "shortlist_candidates",
                schema: "matching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    shortlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    composite_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shortlist_candidates", x => x.id);
                    table.CheckConstraint("chk_shortlist_candidates_source", "source IN ('Algorithm', 'Manual')");
                    table.ForeignKey(
                        name: "fk_shortlist_candidates_shortlists_shortlist_id",
                        column: x => x.shortlist_id,
                        principalSchema: "matching",
                        principalTable: "shortlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_matches_applicant_user_id",
                schema: "matching",
                table: "candidate_matches",
                column: "applicant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_matches_composite_score",
                schema: "matching",
                table: "candidate_matches",
                column: "composite_score");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_matches_job_posting_id",
                schema: "matching",
                table: "candidate_matches",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_matches_match_strength",
                schema: "matching",
                table: "candidate_matches",
                column: "match_strength");

            migrationBuilder.CreateIndex(
                name: "ix_shortlist_candidates_application_id",
                schema: "matching",
                table: "shortlist_candidates",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "uq_shortlist_candidates_shortlist_app",
                schema: "matching",
                table: "shortlist_candidates",
                columns: new[] { "shortlist_id", "application_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shortlists_job_posting_id",
                schema: "matching",
                table: "shortlists",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_shortlists_status",
                schema: "matching",
                table: "shortlists",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_matches",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "shortlist_candidates",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "shortlists",
                schema: "matching");
        }
    }
}
