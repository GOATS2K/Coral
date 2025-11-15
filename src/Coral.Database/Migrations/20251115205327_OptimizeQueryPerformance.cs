using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeQueryPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tracks_AlbumId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Artworks_AlbumId",
                table: "Artworks");

            migrationBuilder.DropIndex(
                name: "IX_ArtistsWithRoles_ArtistId",
                table: "ArtistsWithRoles");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_AlbumId_DiscNumber_TrackNumber",
                table: "Tracks",
                columns: new[] { "AlbumId", "DiscNumber", "TrackNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Artworks_AlbumId_Size",
                table: "Artworks",
                columns: new[] { "AlbumId", "Size" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtistsWithRoles_ArtistId_Role",
                table: "ArtistsWithRoles",
                columns: new[] { "ArtistId", "Role" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tracks_AlbumId_DiscNumber_TrackNumber",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Artworks_AlbumId_Size",
                table: "Artworks");

            migrationBuilder.DropIndex(
                name: "IX_ArtistsWithRoles_ArtistId_Role",
                table: "ArtistsWithRoles");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_AlbumId",
                table: "Tracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Artworks_AlbumId",
                table: "Artworks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistsWithRoles_ArtistId",
                table: "ArtistsWithRoles",
                column: "ArtistId");
        }
    }
}
