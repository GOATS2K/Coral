using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class KeywordIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Keyword_Value",
                table: "Keyword",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_Album_Type",
                table: "Album",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Keyword_Value",
                table: "Keyword");

            migrationBuilder.DropIndex(
                name: "IX_Album_Type",
                table: "Album");
        }
    }
}
