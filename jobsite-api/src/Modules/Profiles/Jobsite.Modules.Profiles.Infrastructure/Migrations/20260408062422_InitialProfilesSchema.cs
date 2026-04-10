using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobsite.Modules.Profiles.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialProfilesSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "profiles");

        migrationBuilder.CreateTable(
            name: "applicant_profiles",
            schema: "profiles",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                skills = table.Column<string>(type: "jsonb", nullable: true),
                social_links = table.Column<string>(type: "jsonb", nullable: true),
                documents = table.Column<string>(type: "jsonb", nullable: true),
                profile_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_applicant_profiles", x => x.user_id);
            });

        migrationBuilder.CreateTable(
            name: "resumes",
            schema: "profiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                file_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                original_filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                file_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                is_latest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_parsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                parsed_text = table.Column<string>(type: "text", nullable: true),
                extracted_skills = table.Column<string>(type: "jsonb", nullable: true),
                ai_parsed_content = table.Column<string>(type: "jsonb", nullable: true),
                parse_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                parsed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_resumes", x => x.id);
                table.CheckConstraint("chk_resumes_file_type", "file_type IN ('PDF', 'DOCX')");
                table.ForeignKey(
                    name: "fk_resumes_applicant_profiles_user_id",
                    column: x => x.user_id,
                    principalSchema: "profiles",
                    principalTable: "applicant_profiles",
                    principalColumn: "user_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_applicant_profiles_city_country",
            schema: "profiles",
            table: "applicant_profiles",
            columns: new[] { "city", "country" });

        migrationBuilder.CreateIndex(
            name: "ix_resumes_is_parsed",
            schema: "profiles",
            table: "resumes",
            column: "is_parsed");

        migrationBuilder.CreateIndex(
            name: "ix_resumes_user_id",
            schema: "profiles",
            table: "resumes",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_resumes_user_id_is_latest",
            schema: "profiles",
            table: "resumes",
            columns: new[] { "user_id", "is_latest" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "resumes",
            schema: "profiles");

        migrationBuilder.DropTable(
            name: "applicant_profiles",
            schema: "profiles");
    }
}
