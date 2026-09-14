using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiWorkbench.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260914230000_ModuleApiAuth")]
    public partial class ModuleApiAuth : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "service_token_urls",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropIndex(
                name: "ix_service_token_urls_service_id_environment_region_code",
                table: "service_token_urls");

            migrationBuilder.CreateIndex(
                name: "ix_service_token_urls_service_id_module_environment_region_code",
                table: "service_token_urls",
                columns: new[] { "service_id", "module", "environment", "region_code" },
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "api_auth_type",
                table: "service_swagger_sources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cert_path",
                table: "service_swagger_sources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cert_base64",
                table: "service_swagger_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cert_vault_path",
                table: "service_swagger_sources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cert_password",
                table: "service_swagger_sources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_field",
                table: "service_swagger_sources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_token_urls_service_id_module_environment_region_code",
                table: "service_token_urls");

            migrationBuilder.DropColumn(name: "module", table: "service_token_urls");

            migrationBuilder.CreateIndex(
                name: "ix_service_token_urls_service_id_environment_region_code",
                table: "service_token_urls",
                columns: new[] { "service_id", "environment", "region_code" },
                unique: true);

            migrationBuilder.DropColumn(name: "api_auth_type", table: "service_swagger_sources");
            migrationBuilder.DropColumn(name: "cert_path", table: "service_swagger_sources");
            migrationBuilder.DropColumn(name: "cert_base64", table: "service_swagger_sources");
            migrationBuilder.DropColumn(name: "cert_vault_path", table: "service_swagger_sources");
            migrationBuilder.DropColumn(name: "cert_password", table: "service_swagger_sources");
            migrationBuilder.DropColumn(name: "token_field", table: "service_swagger_sources");
        }
    }
}
