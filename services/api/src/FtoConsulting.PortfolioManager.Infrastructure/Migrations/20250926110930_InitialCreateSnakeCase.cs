using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentTypes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instrument_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Platforms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platforms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolios", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolios_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "Accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    sedol = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    instrument_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instruments", x => x.id);
                    table.ForeignKey(
                        name: "fk_instruments_instrument_types_instrument_type_id",
                        column: x => x.instrument_type_id,
                        principalTable: "InstrumentTypes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_id = table.Column<Guid>(type: "uuid", nullable: false),
                    portfolio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    bought_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    daily_profit_loss = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    daily_profit_loss_percentage = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holdings", x => x.id);
                    table.ForeignKey(
                        name: "fk_holdings_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalTable: "Instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_holdings_platforms_platform_id",
                        column: x => x.platform_id,
                        principalTable: "Platforms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_holdings_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalTable: "Portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserName",
                table: "Accounts",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_holdings_instrument_id",
                table: "Holdings",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_holdings_platform_id",
                table: "Holdings",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_Portfolio_Instrument_Date",
                table: "Holdings",
                columns: new[] { "portfolio_id", "instrument_id", "valuation_date" });

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_PortfolioId",
                table: "Holdings",
                column: "portfolio_id");

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_ValuationDate",
                table: "Holdings",
                column: "valuation_date");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_instrument_type_id",
                table: "Instruments",
                column: "instrument_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_ISIN",
                table: "Instruments",
                column: "isin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Name",
                table: "Instruments",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_SEDOL",
                table: "Instruments",
                column: "sedol");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentTypes_Name",
                table: "InstrumentTypes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_Name",
                table: "Platforms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_AccountId_Name",
                table: "Portfolios",
                columns: new[] { "account_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holdings");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "Platforms");

            migrationBuilder.DropTable(
                name: "Portfolios");

            migrationBuilder.DropTable(
                name: "InstrumentTypes");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
