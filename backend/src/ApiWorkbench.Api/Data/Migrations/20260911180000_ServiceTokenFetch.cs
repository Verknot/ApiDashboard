using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApiWorkbench.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260911180000_ServiceTokenFetch")]
    public partial class ServiceTokenFetch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "token_body",
                table: "services",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_field",
                table: "services",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "accessToken");

            migrationBuilder.AddColumn<string>(
                name: "token_password",
                table: "services",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_username",
                table: "services",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "token_vault_base64",
                table: "services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "token_vault_password_path",
                table: "services",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_vault_path",
                table: "services",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_vault_username_path",
                table: "services",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_token_urls",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    region_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_token_urls", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_token_urls_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_token_urls_service_id_environment_region_code",
                table: "service_token_urls",
                columns: new[] { "service_id", "environment", "region_code" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_token_urls");

            migrationBuilder.DropColumn(name: "token_body", table: "services");
            migrationBuilder.DropColumn(name: "token_field", table: "services");
            migrationBuilder.DropColumn(name: "token_password", table: "services");
            migrationBuilder.DropColumn(name: "token_username", table: "services");
            migrationBuilder.DropColumn(name: "token_vault_base64", table: "services");
            migrationBuilder.DropColumn(name: "token_vault_password_path", table: "services");
            migrationBuilder.DropColumn(name: "token_vault_path", table: "services");
            migrationBuilder.DropColumn(name: "token_vault_username_path", table: "services");
        }
    }
}
