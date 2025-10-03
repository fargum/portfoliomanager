using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FtoConsulting.PortfolioManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTickerToInstrument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ticker",
                table: "instruments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_instruments_ticker",
                table: "instruments",
                column: "ticker");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_instruments_ticker",
                table: "instruments");

            migrationBuilder.DropColumn(
                name: "ticker",
                table: "instruments");
        }
    }
}
