using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentPricesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_instruments_isin",
                table: "instruments",
                column: "isin");

            migrationBuilder.CreateTable(
                name: "instrument_prices",
                columns: table => new
                {
                    isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    valuation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    change = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    change_percent = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    previous_close = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    open = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    high = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    low = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    volume = table.Column<long>(type: "bigint", nullable: true),
                    market = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    market_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    price_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrument_prices", x => new { x.isin, x.valuation_date });
                    table.ForeignKey(
                        name: "FK_instrument_prices_instruments_isin",
                        column: x => x.isin,
                        principalTable: "instruments",
                        principalColumn: "isin",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_isin",
                table: "instrument_prices",
                column: "isin");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_symbol",
                table: "instrument_prices",
                column: "symbol");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_valuation_date",
                table: "instrument_prices",
                column: "valuation_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instrument_prices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_instruments_isin",
                table: "instruments");
        }
    }
}
