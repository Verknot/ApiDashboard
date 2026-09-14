using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiWorkbench.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260911151000_ServiceUrlModule")]
    public partial class ServiceUrlModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_urls_service_id_environment_region_code",
                table: "service_urls");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "service_urls",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_service_urls_service_id_module_environment_region_code",
                table: "service_urls",
                columns: new[] { "service_id", "module", "environment", "region_code" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_service_urls_service_id_module_environment_region_code",
                table: "service_urls");

            migrationBuilder.DropColumn(
                name: "module",
                table: "service_urls");

            migrationBuilder.CreateIndex(
                name: "ix_service_urls_service_id_environment_region_code",
                table: "service_urls",
                columns: new[] { "service_id", "environment", "region_code" },
                unique: true);
        }
    }
}
