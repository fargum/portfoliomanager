using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoAccountOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "external_user_id",
                schema: "app",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "app",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<int>(
                name: "mode",
                schema: "app",
                table: "accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "owner_account_id",
                schema: "app",
                table: "accounts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_owner_account_id",
                schema: "app",
                table: "accounts",
                column: "owner_account_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_identity_mode",
                schema: "app",
                table: "accounts",
                sql: "(mode = 0 AND owner_account_id IS NULL AND external_user_id IS NOT NULL AND email IS NOT NULL) OR (mode = 1 AND owner_account_id IS NOT NULL AND owner_account_id <> id AND external_user_id IS NULL AND email IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_accounts_owner_account_id",
                schema: "app",
                table: "accounts",
                column: "owner_account_id",
                principalSchema: "app",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never turn identity-less demo data into a personal account on rollback.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM app.accounts WHERE mode = 1) THEN
                        RAISE EXCEPTION 'Remove demo accounts and their dependent data explicitly before reverting AddDemoAccountOwnership.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_accounts_accounts_owner_account_id",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_owner_account_id",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_identity_mode",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "mode",
                schema: "app",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_account_id",
                schema: "app",
                table: "accounts");

            migrationBuilder.AlterColumn<string>(
                name: "external_user_id",
                schema: "app",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "app",
                table: "accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
