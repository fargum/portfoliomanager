using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExplicitSnakeCaseMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_holdings_instruments_instrument_id",
                table: "Holdings");

            migrationBuilder.DropForeignKey(
                name: "fk_holdings_platforms_platform_id",
                table: "Holdings");

            migrationBuilder.DropForeignKey(
                name: "fk_holdings_portfolios_portfolio_id",
                table: "Holdings");

            migrationBuilder.DropForeignKey(
                name: "fk_instruments_instrument_types_instrument_type_id",
                table: "Instruments");

            migrationBuilder.DropForeignKey(
                name: "fk_portfolios_accounts_account_id",
                table: "Portfolios");

            migrationBuilder.DropPrimaryKey(
                name: "pk_portfolios",
                table: "Portfolios");

            migrationBuilder.DropPrimaryKey(
                name: "pk_platforms",
                table: "Platforms");

            migrationBuilder.DropPrimaryKey(
                name: "pk_instruments",
                table: "Instruments");

            migrationBuilder.DropPrimaryKey(
                name: "pk_holdings",
                table: "Holdings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_accounts",
                table: "Accounts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_instrument_types",
                table: "InstrumentTypes");

            migrationBuilder.RenameTable(
                name: "Portfolios",
                newName: "portfolios");

            migrationBuilder.RenameTable(
                name: "Platforms",
                newName: "platforms");

            migrationBuilder.RenameTable(
                name: "Instruments",
                newName: "instruments");

            migrationBuilder.RenameTable(
                name: "Holdings",
                newName: "holdings");

            migrationBuilder.RenameTable(
                name: "Accounts",
                newName: "accounts");

            migrationBuilder.RenameTable(
                name: "InstrumentTypes",
                newName: "instrument_types");

            migrationBuilder.RenameIndex(
                name: "IX_Portfolios_AccountId_Name",
                table: "portfolios",
                newName: "ix_portfolios_account_id_name");

            migrationBuilder.RenameIndex(
                name: "IX_Platforms_Name",
                table: "platforms",
                newName: "ix_platforms_name");

            migrationBuilder.RenameIndex(
                name: "IX_Instruments_SEDOL",
                table: "instruments",
                newName: "ix_instruments_sedol");

            migrationBuilder.RenameIndex(
                name: "IX_Instruments_Name",
                table: "instruments",
                newName: "ix_instruments_name");

            migrationBuilder.RenameIndex(
                name: "IX_Instruments_ISIN",
                table: "instruments",
                newName: "ix_instruments_isin");

            migrationBuilder.RenameIndex(
                name: "ix_instruments_instrument_type_id",
                table: "instruments",
                newName: "IX_instruments_instrument_type_id");

            migrationBuilder.RenameIndex(
                name: "IX_Holdings_Portfolio_Instrument_Date",
                table: "holdings",
                newName: "ix_holdings_portfolio_instrument_date");

            migrationBuilder.RenameIndex(
                name: "ix_holdings_platform_id",
                table: "holdings",
                newName: "IX_holdings_platform_id");

            migrationBuilder.RenameIndex(
                name: "ix_holdings_instrument_id",
                table: "holdings",
                newName: "IX_holdings_instrument_id");

            migrationBuilder.RenameIndex(
                name: "IX_Holdings_ValuationDate",
                table: "holdings",
                newName: "ix_holdings_valuation_date");

            migrationBuilder.RenameIndex(
                name: "IX_Holdings_PortfolioId",
                table: "holdings",
                newName: "ix_holdings_portfolio_id");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_UserName",
                table: "accounts",
                newName: "ix_accounts_user_name");

            migrationBuilder.RenameIndex(
                name: "IX_InstrumentTypes_Name",
                table: "instrument_types",
                newName: "ix_instrument_types_name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_portfolios",
                table: "portfolios",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_platforms",
                table: "platforms",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_instruments",
                table: "instruments",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_holdings",
                table: "holdings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_accounts",
                table: "accounts",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_instrument_types",
                table: "instrument_types",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_holdings_instruments_instrument_id",
                table: "holdings",
                column: "instrument_id",
                principalTable: "instruments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_holdings_platforms_platform_id",
                table: "holdings",
                column: "platform_id",
                principalTable: "platforms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_holdings_portfolios_portfolio_id",
                table: "holdings",
                column: "portfolio_id",
                principalTable: "portfolios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_instruments_instrument_types_instrument_type_id",
                table: "instruments",
                column: "instrument_type_id",
                principalTable: "instrument_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_portfolios_accounts_account_id",
                table: "portfolios",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_holdings_instruments_instrument_id",
                table: "holdings");

            migrationBuilder.DropForeignKey(
                name: "FK_holdings_platforms_platform_id",
                table: "holdings");

            migrationBuilder.DropForeignKey(
                name: "FK_holdings_portfolios_portfolio_id",
                table: "holdings");

            migrationBuilder.DropForeignKey(
                name: "FK_instruments_instrument_types_instrument_type_id",
                table: "instruments");

            migrationBuilder.DropForeignKey(
                name: "FK_portfolios_accounts_account_id",
                table: "portfolios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_portfolios",
                table: "portfolios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_platforms",
                table: "platforms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_instruments",
                table: "instruments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_holdings",
                table: "holdings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_accounts",
                table: "accounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_instrument_types",
                table: "instrument_types");

            migrationBuilder.RenameTable(
                name: "portfolios",
                newName: "Portfolios");

            migrationBuilder.RenameTable(
                name: "platforms",
                newName: "Platforms");

            migrationBuilder.RenameTable(
                name: "instruments",
                newName: "Instruments");

            migrationBuilder.RenameTable(
                name: "holdings",
                newName: "Holdings");

            migrationBuilder.RenameTable(
                name: "accounts",
                newName: "Accounts");

            migrationBuilder.RenameTable(
                name: "instrument_types",
                newName: "InstrumentTypes");

            migrationBuilder.RenameIndex(
                name: "ix_portfolios_account_id_name",
                table: "Portfolios",
                newName: "IX_Portfolios_AccountId_Name");

            migrationBuilder.RenameIndex(
                name: "ix_platforms_name",
                table: "Platforms",
                newName: "IX_Platforms_Name");

            migrationBuilder.RenameIndex(
                name: "ix_instruments_sedol",
                table: "Instruments",
                newName: "IX_Instruments_SEDOL");

            migrationBuilder.RenameIndex(
                name: "ix_instruments_name",
                table: "Instruments",
                newName: "IX_Instruments_Name");

            migrationBuilder.RenameIndex(
                name: "ix_instruments_isin",
                table: "Instruments",
                newName: "IX_Instruments_ISIN");

            migrationBuilder.RenameIndex(
                name: "IX_instruments_instrument_type_id",
                table: "Instruments",
                newName: "ix_instruments_instrument_type_id");

            migrationBuilder.RenameIndex(
                name: "ix_holdings_portfolio_instrument_date",
                table: "Holdings",
                newName: "IX_Holdings_Portfolio_Instrument_Date");

            migrationBuilder.RenameIndex(
                name: "IX_holdings_platform_id",
                table: "Holdings",
                newName: "ix_holdings_platform_id");

            migrationBuilder.RenameIndex(
                name: "IX_holdings_instrument_id",
                table: "Holdings",
                newName: "ix_holdings_instrument_id");

            migrationBuilder.RenameIndex(
                name: "ix_holdings_valuation_date",
                table: "Holdings",
                newName: "IX_Holdings_ValuationDate");

            migrationBuilder.RenameIndex(
                name: "ix_holdings_portfolio_id",
                table: "Holdings",
                newName: "IX_Holdings_PortfolioId");

            migrationBuilder.RenameIndex(
                name: "ix_accounts_user_name",
                table: "Accounts",
                newName: "IX_Accounts_UserName");

            migrationBuilder.RenameIndex(
                name: "ix_instrument_types_name",
                table: "InstrumentTypes",
                newName: "IX_InstrumentTypes_Name");

            migrationBuilder.AddPrimaryKey(
                name: "pk_portfolios",
                table: "Portfolios",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_platforms",
                table: "Platforms",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_instruments",
                table: "Instruments",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_holdings",
                table: "Holdings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_accounts",
                table: "Accounts",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_instrument_types",
                table: "InstrumentTypes",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_holdings_instruments_instrument_id",
                table: "Holdings",
                column: "instrument_id",
                principalTable: "Instruments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_holdings_platforms_platform_id",
                table: "Holdings",
                column: "platform_id",
                principalTable: "Platforms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_holdings_portfolios_portfolio_id",
                table: "Holdings",
                column: "portfolio_id",
                principalTable: "Portfolios",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_instruments_instrument_types_instrument_type_id",
                table: "Instruments",
                column: "instrument_type_id",
                principalTable: "InstrumentTypes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_portfolios_accounts_account_id",
                table: "Portfolios",
                column: "account_id",
                principalTable: "Accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
