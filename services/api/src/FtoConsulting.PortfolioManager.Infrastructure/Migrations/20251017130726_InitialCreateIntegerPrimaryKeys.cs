using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateIntegerPrimaryKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instrument_types",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrument_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platforms",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platforms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portfolios",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolios", x => x.id);
                    table.ForeignKey(
                        name: "FK_portfolios_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "app",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instruments",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ticker = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    quote_unit = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    instrument_type_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.id);
                    table.ForeignKey(
                        name: "FK_instruments_instrument_types_instrument_type_id",
                        column: x => x.instrument_type_id,
                        principalSchema: "app",
                        principalTable: "instrument_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "holdings",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    valuation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    instrument_id = table.Column<int>(type: "integer", nullable: false),
                    platform_id = table.Column<int>(type: "integer", nullable: false),
                    portfolio_id = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_holdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_holdings_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalSchema: "app",
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_holdings_platforms_platform_id",
                        column: x => x.platform_id,
                        principalSchema: "app",
                        principalTable: "platforms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_holdings_portfolios_portfolio_id",
                        column: x => x.portfolio_id,
                        principalSchema: "app",
                        principalTable: "portfolios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instrument_prices",
                schema: "app",
                columns: table => new
                {
                    instrument_id = table.Column<int>(type: "integer", nullable: false),
                    valuation_date = table.Column<DateOnly>(type: "date", nullable: false),
                    ticker = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrument_prices", x => new { x.instrument_id, x.valuation_date });
                    table.ForeignKey(
                        name: "FK_instrument_prices_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalSchema: "app",
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_user_name",
                schema: "app",
                table: "accounts",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_holdings_instrument_id",
                schema: "app",
                table: "holdings",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "IX_holdings_platform_id",
                schema: "app",
                table: "holdings",
                column: "platform_id");

            migrationBuilder.CreateIndex(
                name: "ix_holdings_portfolio_id",
                schema: "app",
                table: "holdings",
                column: "portfolio_id");

            migrationBuilder.CreateIndex(
                name: "ix_holdings_portfolio_instrument_date",
                schema: "app",
                table: "holdings",
                columns: new[] { "portfolio_id", "instrument_id", "valuation_date" });

            migrationBuilder.CreateIndex(
                name: "ix_holdings_valuation_date",
                schema: "app",
                table: "holdings",
                column: "valuation_date");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_instrument_id",
                schema: "app",
                table: "instrument_prices",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_ticker",
                schema: "app",
                table: "instrument_prices",
                column: "ticker");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_prices_valuation_date",
                schema: "app",
                table: "instrument_prices",
                column: "valuation_date");

            migrationBuilder.CreateIndex(
                name: "ix_instrument_types_name",
                schema: "app",
                table: "instrument_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_currency_code",
                schema: "app",
                table: "instruments",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_instruments_instrument_type_id",
                schema: "app",
                table: "instruments",
                column: "instrument_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_name",
                schema: "app",
                table: "instruments",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_quote_unit",
                schema: "app",
                table: "instruments",
                column: "quote_unit");

            migrationBuilder.CreateIndex(
                name: "ix_instruments_ticker",
                schema: "app",
                table: "instruments",
                column: "ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platforms_name",
                schema: "app",
                table: "platforms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_portfolios_account_id_name",
                schema: "app",
                table: "portfolios",
                columns: new[] { "account_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holdings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "instrument_prices",
                schema: "app");

            migrationBuilder.DropTable(
                name: "platforms",
                schema: "app");

            migrationBuilder.DropTable(
                name: "portfolios",
                schema: "app");

            migrationBuilder.DropTable(
                name: "instruments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "instrument_types",
                schema: "app");
        }
    }
}
