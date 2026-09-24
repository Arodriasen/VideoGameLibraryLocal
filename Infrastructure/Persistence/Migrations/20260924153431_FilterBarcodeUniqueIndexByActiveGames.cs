using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoGameLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilterBarcodeUniqueIndexByActiveGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_Barcode",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Barcode",
                table: "Games",
                column: "Barcode",
                unique: true,
                filter: "\"DeletedDate\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_Barcode",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Barcode",
                table: "Games",
                column: "Barcode",
                unique: true);
        }
    }
}
