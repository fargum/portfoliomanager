using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteUnitToInstrument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "quote_unit",
                table: "instruments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_quote_unit",
                table: "instruments",
                column: "quote_unit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_instruments_quote_unit",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "quote_unit",
                table: "instruments");
        }
    }
}
