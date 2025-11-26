using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class MigrateFavoriteTracksToPlaylist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add Type column to Playlists (0 = Normal, 1 = LikedSongs)
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // 2. Create the Liked Songs playlist
            // Note: Use uppercase GUID to match EF Core's format (SQLite is case-sensitive)
            var likedSongsId = Guid.NewGuid().ToString().ToUpperInvariant();
            migrationBuilder.Sql($@"
                INSERT INTO Playlists (Id, Name, Description, Type, CreatedAt, UpdatedAt)
                VALUES ('{likedSongsId}', 'Liked Songs', 'Your favorite tracks', 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ");

            // 3. Copy FavoriteTracks to PlaylistTracks, converting IDs to uppercase
            migrationBuilder.Sql($@"
                INSERT INTO PlaylistTracks (Id, PlaylistId, TrackId, Position, CreatedAt, UpdatedAt)
                SELECT
                    UPPER(Id),
                    '{likedSongsId}',
                    UPPER(TrackId),
                    (ROW_NUMBER() OVER (ORDER BY CreatedAt)) - 1,
                    CreatedAt,
                    UpdatedAt
                FROM FavoriteTracks
            ");

            // 4. Drop FavoriteTracks table
            migrationBuilder.DropTable(
                name: "FavoriteTracks");

            // 5. Add composite index for fast playlist membership lookups
            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTracks_PlaylistId_TrackId",
                table: "PlaylistTracks",
                columns: new[] { "PlaylistId", "TrackId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Playlists");

            migrationBuilder.CreateTable(
                name: "FavoriteTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTracks_TrackId",
                table: "FavoriteTracks",
                column: "TrackId",
                unique: true);
        }
    }
}
