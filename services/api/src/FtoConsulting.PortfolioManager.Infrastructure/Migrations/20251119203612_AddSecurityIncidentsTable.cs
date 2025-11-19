using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityIncidentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_incidents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    violation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    context = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    threat_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    additional_data = table.Column<string>(type: "jsonb", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_security_incidents_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "app",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_account_id",
                schema: "app",
                table: "security_incidents",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_account_id_created_at",
                schema: "app",
                table: "security_incidents",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_created_at",
                schema: "app",
                table: "security_incidents",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_is_resolved",
                schema: "app",
                table: "security_incidents",
                column: "is_resolved");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_severity",
                schema: "app",
                table: "security_incidents",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_threat_level",
                schema: "app",
                table: "security_incidents",
                column: "threat_level");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_threat_level_resolved",
                schema: "app",
                table: "security_incidents",
                columns: new[] { "threat_level", "is_resolved" });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_violation_type",
                schema: "app",
                table: "security_incidents",
                column: "violation_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_incidents",
                schema: "app");
        }
    }
}
