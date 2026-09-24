using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApiWorkbench.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    swagger_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    swagger_auth_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "none"),
                    swagger_vault_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    swagger_vault_username_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    swagger_vault_password_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    swagger_vault_base64 = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    swagger_basic_username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    swagger_basic_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "none"),
                    cert_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_base64 = table.Column<string>(type: "text", nullable: true),
                    cert_vault_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    token_username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    token_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    token_vault_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    token_vault_username_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    token_vault_password_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    token_vault_base64 = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    token_body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    token_field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "accessToken"),
                    proxy = table.Column<bool>(type: "boolean", nullable: false),
                    splunk_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_regional = table.Column<bool>(type: "boolean", nullable: false),
                    default_region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_first_login = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_snapshots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    raw_json = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_contract_snapshots_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "endpoints",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    operation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_schema = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    response_schema = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    parameters = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    user_tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "fk_endpoints_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_regions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_regions", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_regions_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    basic_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    insecure = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    api_auth_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cert_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_base64 = table.Column<string>(type: "text", nullable: true),
                    cert_vault_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cert_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    token_field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "service_token_urls",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    region_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
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

            migrationBuilder.CreateTable(
                name: "service_urls",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_id = table.Column<int>(type: "integer", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    region_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_urls", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_urls_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_audit_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    changed_by_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: true),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_audit_log_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_role_audit_log_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_role_audit_log_users_changed_by_id",
                        column: x => x.changed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_audit_log_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_pins",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_pins", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_pins_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_pins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    granted_by_id = table.Column<int>(type: "integer", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_role_assignments_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_role_assignments_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_role_assignments_users_granted_by_id",
                        column: x => x.granted_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_role_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    endpoint_id = table.Column<int>(type: "integer", nullable: true),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    region_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    request_headers = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    request_body = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    response_headers = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    response_truncated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    response_time_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_history_endpoints_endpoint_id",
                        column: x => x.endpoint_id,
                        principalTable: "endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_request_history_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_request_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    endpoint_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    template_body = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    param_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_templates_endpoints_endpoint_id",
                        column: x => x.endpoint_id,
                        principalTable: "endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_request_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_favorite_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    endpoint_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    param_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    request_body = table.Column<JsonDocument>(type: "jsonb", nullable: true),
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
                name: "idx_contract_snapshots_service_fetched",
                table: "contract_snapshots",
                columns: new[] { "service_id", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_endpoints_service_id_module_path_method",
                table: "endpoints",
                columns: new[] { "service_id", "module", "path", "method" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_request_history_user_created",
                table: "request_history",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_request_history_endpoint_id",
                table: "request_history",
                column: "endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_history_service_id",
                table: "request_history",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_templates_endpoint_id",
                table: "request_templates",
                column: "endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_templates_user_id",
                table: "request_templates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_audit_log_changed_by_id",
                table: "role_audit_log",
                column: "changed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_audit_log_role_id",
                table: "role_audit_log",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_audit_log_service_id",
                table: "role_audit_log",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_audit_log_user_id",
                table: "role_audit_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_regions_service_id_code",
                table: "service_regions",
                columns: new[] { "service_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_swagger_sources_service_id_name",
                table: "service_swagger_sources",
                columns: new[] { "service_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_token_urls_service_id_module_environment_region_code",
                table: "service_token_urls",
                columns: new[] { "service_id", "module", "environment", "region_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_urls_service_id_module_environment_region_code",
                table: "service_urls",
                columns: new[] { "service_id", "module", "environment", "region_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_services_name",
                table: "services",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_favorite_requests_endpoint_id",
                table: "user_favorite_requests",
                column: "endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_favorite_requests_user_created",
                table: "user_favorite_requests",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_pins_service_id",
                table: "user_pins",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_pins_user_alias",
                table: "user_pins",
                columns: new[] { "user_id", "alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_pins_user_updated",
                table: "user_pins",
                columns: new[] { "user_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_role_assignments_granted_by_id",
                table: "user_role_assignments",
                column: "granted_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_assignments_role_id",
                table: "user_role_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_assignments_service_id",
                table: "user_role_assignments",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_role_global",
                table: "user_role_assignments",
                columns: new[] { "user_id", "role_id" },
                unique: true,
                filter: "service_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_user_role_service",
                table: "user_role_assignments",
                columns: new[] { "user_id", "role_id", "service_id" },
                unique: true,
                filter: "service_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_snapshots");

            migrationBuilder.DropTable(
                name: "request_history");

            migrationBuilder.DropTable(
                name: "request_templates");

            migrationBuilder.DropTable(
                name: "role_audit_log");

            migrationBuilder.DropTable(
                name: "service_regions");

            migrationBuilder.DropTable(
                name: "service_swagger_sources");

            migrationBuilder.DropTable(
                name: "service_token_urls");

            migrationBuilder.DropTable(
                name: "service_urls");

            migrationBuilder.DropTable(
                name: "user_favorite_requests");

            migrationBuilder.DropTable(
                name: "user_pins");

            migrationBuilder.DropTable(
                name: "user_role_assignments");

            migrationBuilder.DropTable(
                name: "endpoints");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "services");
        }
    }
}
