using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApiWorkbench.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260911140000_SwaggerModules")]
    public partial class SwaggerModules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "endpoints",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "contract_snapshots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropIndex(
                name: "ix_endpoints_service_id_path_method",
                table: "endpoints");

            migrationBuilder.CreateIndex(
                name: "ix_endpoints_service_id_module_path_method",
                table: "endpoints",
                columns: new[] { "service_id", "module", "path", "method" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "service_swagger_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    auth_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "none"),
                    vault_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    vault_username_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    vault_password_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    vault_base64 = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    basic_username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    basic_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_swagger_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_swagger_sources_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_swagger_sources_service_id_name",
                table: "service_swagger_sources",
                columns: new[] { "service_id", "name" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO service_swagger_sources (
                    service_id, name, sort_order, url, auth_type,
                    vault_path, vault_username_path, vault_password_path, vault_base64,
                    basic_username, basic_password)
                SELECT
                    id, '', 0, swagger_url, COALESCE(swagger_auth_type, 'none'),
                    swagger_vault_path, swagger_vault_username_path, swagger_vault_password_path,
                    COALESCE(swagger_vault_base64, false),
                    swagger_basic_username, swagger_basic_password
                FROM services
                WHERE swagger_url IS NOT NULL AND btrim(swagger_url) <> '';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_swagger_sources");

            migrationBuilder.DropIndex(
                name: "ix_endpoints_service_id_module_path_method",
                table: "endpoints");

            migrationBuilder.DropColumn(
                name: "module",
                table: "endpoints");

            migrationBuilder.DropColumn(
                name: "module",
                table: "contract_snapshots");

            migrationBuilder.CreateIndex(
                name: "ix_endpoints_service_id_path_method",
                table: "endpoints",
                columns: new[] { "service_id", "path", "method" },
                unique: true);
        }
    }
}
