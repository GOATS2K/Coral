using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class KeywordMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Genres_Albums_AlbumId",
                table: "Genres");

            migrationBuilder.DropIndex(
                name: "IX_Genres_AlbumId",
                table: "Genres");

            migrationBuilder.DropColumn(
                name: "AlbumId",
                table: "Genres");

            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeywordTrack",
                columns: table => new
                {
                    KeywordsId = table.Column<int>(type: "INTEGER", nullable: false),
                    TracksId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordTrack", x => new { x.KeywordsId, x.TracksId });
                    table.ForeignKey(
                        name: "FK_KeywordTrack_Keywords_KeywordsId",
                        column: x => x.KeywordsId,
                        principalTable: "Keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KeywordTrack_Tracks_TracksId",
                        column: x => x.TracksId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Value",
                table: "Keywords",
                column: "Value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTrack_TracksId",
                table: "KeywordTrack",
                column: "TracksId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeywordTrack");

            migrationBuilder.DropTable(
                name: "Keywords");

            migrationBuilder.AddColumn<int>(
                name: "AlbumId",
                table: "Genres",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_AlbumId",
                table: "Genres",
                column: "AlbumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_Albums_AlbumId",
                table: "Genres",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id");
        }
    }
}
