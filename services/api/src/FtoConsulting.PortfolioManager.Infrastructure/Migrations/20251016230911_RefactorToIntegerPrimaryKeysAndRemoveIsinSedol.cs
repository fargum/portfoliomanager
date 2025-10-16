using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorToIntegerPrimaryKeysAndRemoveIsinSedol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_instrument_prices_instruments_isin",
                table: "instrument_prices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_instruments_isin",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ix_instruments_isin",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ix_instruments_sedol",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ix_instruments_ticker",
                table: "instruments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_instrument_prices",
                table: "instrument_prices");

            migrationBuilder.DropIndex(
                name: "ix_instrument_prices_isin",
                table: "instrument_prices");

            migrationBuilder.DropColumn(
                name: "isin",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "sedol",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "isin",
                table: "instrument_prices");

            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.RenameTable(
                name: "portfolios",
                newName: "portfolios",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "platforms",
                newName: "platforms",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "instruments",
                newName: "instruments",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "instrument_types",
                newName: "instrument_types",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "instrument_prices",
                newName: "instrument_prices",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "holdings",
                newName: "holdings",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "accounts",
                newName: "accounts",
                newSchema: "app");

            migrationBuilder.RenameColumn(
                name: "symbol",
                schema: "app",
                table: "instrument_prices",
                newName: "ticker");

            migrationBuilder.RenameIndex(
                name: "ix_instrument_prices_symbol",
                schema: "app",
                table: "instrument_prices",
                newName: "ix_instrument_prices_ticker");

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                schema: "app",
                table: "portfolios",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "app",
                table: "portfolios",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "app",
                table: "platforms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "ticker",
                schema: "app",
                table: "instruments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "instrument_type_id",
                schema: "app",
                table: "instruments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "app",
                table: "instruments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "app",
                table: "instrument_types",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "app",
                table: "instrument_prices",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "instrument_id",
                schema: "app",
                table: "instrument_prices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "portfolio_id",
                schema: "app",
                table: "holdings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "platform_id",
                schema: "app",
                table: "holdings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "instrument_id",
                schema: "app",
                table: "holdings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "app",
                table: "holdings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "app",
                table: "accounts",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_instrument_prices",
                schema: "app",
                table: "instrument_prices",
                columns: new[] { "instrument_id", "valuation_date" });

            migrationBuilder.CreateIndex(
                name: "ix_instruments_ticker",
                schema: "app",
                table: "instruments",
                column: "ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_instrument_id",
                schema: "app",
                table: "instrument_prices",
                column: "instrument_id");

            migrationBuilder.AddForeignKey(
                name: "FK_instrument_prices_instruments_instrument_id",
                schema: "app",
                table: "instrument_prices",
                column: "instrument_id",
                principalSchema: "app",
                principalTable: "instruments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_instrument_prices_instruments_instrument_id",
                schema: "app",
                table: "instrument_prices");

            migrationBuilder.DropIndex(
                name: "ix_instruments_ticker",
                schema: "app",
                table: "instruments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_instrument_prices",
                schema: "app",
                table: "instrument_prices");

            migrationBuilder.DropIndex(
                name: "ix_instrument_prices_instrument_id",
                schema: "app",
                table: "instrument_prices");

            migrationBuilder.DropColumn(
                name: "instrument_id",
                schema: "app",
                table: "instrument_prices");

            migrationBuilder.RenameTable(
                name: "portfolios",
                schema: "app",
                newName: "portfolios");

            migrationBuilder.RenameTable(
                name: "platforms",
                schema: "app",
                newName: "platforms");

            migrationBuilder.RenameTable(
                name: "instruments",
                schema: "app",
                newName: "instruments");

            migrationBuilder.RenameTable(
                name: "instrument_types",
                schema: "app",
                newName: "instrument_types");

            migrationBuilder.RenameTable(
                name: "instrument_prices",
                schema: "app",
                newName: "instrument_prices");

            migrationBuilder.RenameTable(
                name: "holdings",
                schema: "app",
                newName: "holdings");

            migrationBuilder.RenameTable(
                name: "accounts",
                schema: "app",
                newName: "accounts");

            migrationBuilder.RenameColumn(
                name: "ticker",
                table: "instrument_prices",
                newName: "symbol");

            migrationBuilder.RenameIndex(
                name: "ix_instrument_prices_ticker",
                table: "instrument_prices",
                newName: "ix_instrument_prices_symbol");

            migrationBuilder.AlterColumn<Guid>(
                name: "account_id",
                table: "portfolios",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "portfolios",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "platforms",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "ticker",
                table: "instruments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "instrument_type_id",
                table: "instruments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "instruments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "isin",
                table: "instruments",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sedol",
                table: "instruments",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "instrument_types",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "instrument_prices",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "isin",
                table: "instrument_prices",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "portfolio_id",
                table: "holdings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "platform_id",
                table: "holdings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "instrument_id",
                table: "holdings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "holdings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "accounts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_instruments_isin",
                table: "instruments",
                column: "isin");

            migrationBuilder.AddPrimaryKey(
                name: "PK_instrument_prices",
                table: "instrument_prices",
                columns: new[] { "isin", "valuation_date" });

            migrationBuilder.CreateIndex(
                name: "ix_instruments_isin",
                table: "instruments",
                column: "isin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_sedol",
                table: "instruments",
                column: "sedol");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_ticker",
                table: "instruments",
                column: "ticker");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_isin",
                table: "instrument_prices",
                column: "isin");

            migrationBuilder.AddForeignKey(
                name: "FK_instrument_prices_instruments_isin",
                table: "instrument_prices",
                column: "isin",
                principalTable: "instruments",
                principalColumn: "isin",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
