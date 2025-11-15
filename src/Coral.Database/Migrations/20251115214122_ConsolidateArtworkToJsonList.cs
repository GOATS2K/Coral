using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateArtworkToJsonList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create new table with JSON structure
            migrationBuilder.Sql(@"
                CREATE TABLE Artworks_New (
                    Id TEXT NOT NULL PRIMARY KEY,
                    AlbumId TEXT NOT NULL,
                    Paths TEXT NULL,
                    Colors TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
            ");

            // Step 2: Migrate data - consolidate multiple artwork rows per album into single rows with JSON array
            migrationBuilder.Sql(@"
                INSERT INTO Artworks_New (Id, AlbumId, Paths, Colors, CreatedAt, UpdatedAt)
                SELECT
                    (SELECT Id FROM Artworks a WHERE a.AlbumId = album_artworks.AlbumId LIMIT 1) as Id,
                    album_artworks.AlbumId,
                    json_array(
                        json_object(
                            'Size', 0,
                            'Height', COALESCE((SELECT Height FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 0), 0),
                            'Width', COALESCE((SELECT Width FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 0), 0),
                            'Path', COALESCE((SELECT Path FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 0), '')
                        ),
                        json_object(
                            'Size', 1,
                            'Height', COALESCE((SELECT Height FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 1), 0),
                            'Width', COALESCE((SELECT Width FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 1), 0),
                            'Path', COALESCE((SELECT Path FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 1), '')
                        ),
                        json_object(
                            'Size', 2,
                            'Height', COALESCE((SELECT Height FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 2), 0),
                            'Width', COALESCE((SELECT Width FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 2), 0),
                            'Path', COALESCE((SELECT Path FROM Artworks WHERE AlbumId = album_artworks.AlbumId AND Size = 2), '')
                        )
                    ) as Paths,
                    (SELECT Colors FROM Artworks a WHERE a.AlbumId = album_artworks.AlbumId LIMIT 1) as Colors,
                    (SELECT CreatedAt FROM Artworks a WHERE a.AlbumId = album_artworks.AlbumId LIMIT 1) as CreatedAt,
                    (SELECT UpdatedAt FROM Artworks a WHERE a.AlbumId = album_artworks.AlbumId LIMIT 1) as UpdatedAt
                FROM (
                    SELECT DISTINCT AlbumId FROM Artworks
                ) as album_artworks;
            ");

            // Step 3: Drop old table and rename new one
            migrationBuilder.Sql("DROP TABLE Artworks;");
            migrationBuilder.Sql("ALTER TABLE Artworks_New RENAME TO Artworks;");

            // Step 4: Create index on AlbumId (unique since it's now one-to-one)
            migrationBuilder.CreateIndex(
                name: "IX_Artworks_AlbumId",
                table: "Artworks",
                column: "AlbumId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Artworks_AlbumId",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "Paths",
                table: "Artworks");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Artworks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Size",
                table: "Artworks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Artworks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Artworks_AlbumId_Size",
                table: "Artworks",
                columns: new[] { "AlbumId", "Size" });
        }
    }
}
