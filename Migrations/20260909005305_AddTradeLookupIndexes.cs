using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Trades_ExecutedAt",
                table: "Trades",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_OrderId",
                table: "Trades",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Symbol",
                table: "Trades",
                column: "Symbol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trades_ExecutedAt",
                table: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_Trades_OrderId",
                table: "Trades");

            migrationBuilder.DropIndex(
                name: "IX_Trades_Symbol",
                table: "Trades");
        }
    }
}
