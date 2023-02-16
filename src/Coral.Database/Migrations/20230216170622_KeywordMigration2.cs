using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class KeywordMigration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keywords_Value",
                table: "Keywords");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Value",
                table: "Keywords",
                column: "Value");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keywords_Value",
                table: "Keywords");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Value",
                table: "Keywords",
                column: "Value",
                unique: true);
        }
    }
}
