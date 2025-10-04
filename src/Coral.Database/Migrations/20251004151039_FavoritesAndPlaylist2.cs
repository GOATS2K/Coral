using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class FavoritesAndPlaylist2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FavoriteTracks_TrackId",
                table: "FavoriteTracks");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteArtists_ArtistId",
                table: "FavoriteArtists");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteAlbums_AlbumId",
                table: "FavoriteAlbums");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTracks_TrackId",
                table: "FavoriteTracks",
                column: "TrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteArtists_ArtistId",
                table: "FavoriteArtists",
                column: "ArtistId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteAlbums_AlbumId",
                table: "FavoriteAlbums",
                column: "AlbumId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FavoriteTracks_TrackId",
                table: "FavoriteTracks");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteArtists_ArtistId",
                table: "FavoriteArtists");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteAlbums_AlbumId",
                table: "FavoriteAlbums");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTracks_TrackId",
                table: "FavoriteTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteArtists_ArtistId",
                table: "FavoriteArtists",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteAlbums_AlbumId",
                table: "FavoriteAlbums",
                column: "AlbumId");
        }
    }
}
