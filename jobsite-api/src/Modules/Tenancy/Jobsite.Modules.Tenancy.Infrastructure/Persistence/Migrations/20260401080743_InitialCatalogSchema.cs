using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobsite.Modules.Tenancy.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCatalogSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.CreateTable(
            name: "tenants",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                subdomain = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                connection_string = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                owner_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                provisioned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deactivated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenants", x => x.id);
                table.CheckConstraint("chk_tenants_status", "status IN ('Provisioning', 'Active', 'Suspended', 'Deactivated')");
            });

        migrationBuilder.CreateTable(
            name: "tenant_brandings",
            schema: "catalog",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                favicon_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                primary_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                secondary_color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                tagline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenant_brandings", x => x.tenant_id);
                table.ForeignKey(
                    name: "fk_tenant_brandings_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalSchema: "catalog",
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_tenants_name",
            schema: "catalog",
            table: "tenants",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_tenants_status",
            schema: "catalog",
            table: "tenants",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_tenants_subdomain",
            schema: "catalog",
            table: "tenants",
            column: "subdomain",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tenant_brandings",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "tenants",
            schema: "catalog");
    }
}
