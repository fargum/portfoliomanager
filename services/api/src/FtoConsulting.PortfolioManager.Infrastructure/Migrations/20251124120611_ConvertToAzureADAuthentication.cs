using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertToAzureADAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_user_name",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "user_name",
                schema: "app",
                table: "accounts");

            migrationBuilder.RenameColumn(
                name: "password",
                schema: "app",
                table: "accounts",
                newName: "external_user_id");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "app",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "app",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "app",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                schema: "app",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_email",
                schema: "app",
                table: "accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_external_user_id",
                schema: "app",
                table: "accounts",
                column: "external_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_is_active",
                schema: "app",
                table: "accounts",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_email",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_external_user_id",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_is_active",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "display_name",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                schema: "app",
                table: "accounts");

            migrationBuilder.RenameColumn(
                name: "external_user_id",
                schema: "app",
                table: "accounts",
                newName: "password");

            migrationBuilder.AddColumn<string>(
                name: "user_name",
                schema: "app",
                table: "accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_user_name",
                schema: "app",
                table: "accounts",
                column: "user_name",
                unique: true);
        }
    }
}
