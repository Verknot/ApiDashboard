using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiWorkbench.Api.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260916190000_TemplateParamsAndFavorites")]
    public partial class TemplateParamsAndFavorites : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "param_values",
                table: "request_templates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_favorite_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    endpoint_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    param_values = table.Column<string>(type: "jsonb", nullable: true),
                    request_body = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_favorite_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_favorite_requests_endpoints_endpoint_id",
                        column: x => x.endpoint_id,
                        principalTable: "endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_favorite_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_favorite_requests_endpoint_id",
                table: "user_favorite_requests",
                column: "endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_favorite_requests_user_created",
                table: "user_favorite_requests",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "user_favorite_requests");
            migrationBuilder.DropColumn(name: "param_values", table: "request_templates");
        }
    }
}
