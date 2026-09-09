using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddClientOrderIdToTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientOrderId",
                table: "Trades",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_ClientOrderId",
                table: "Trades",
                column: "ClientOrderId",
                unique: true,
                filter: "\"ClientOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trades_ClientOrderId",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "ClientOrderId",
                table: "Trades");
        }
    }
}
