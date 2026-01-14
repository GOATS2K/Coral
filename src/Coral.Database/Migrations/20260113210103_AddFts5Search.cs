using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFts5Search : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Create FTS5 virtual tables
            migrationBuilder.Sql("CREATE VIRTUAL TABLE TrackSearch USING fts5(id UNINDEXED, search_text);");
            migrationBuilder.Sql("CREATE VIRTUAL TABLE AlbumSearch USING fts5(id UNINDEXED, search_text);");
            migrationBuilder.Sql("CREATE VIRTUAL TABLE ArtistSearch USING fts5(id UNINDEXED, search_text);");

            // Populate SearchText for existing Artists
            migrationBuilder.Sql("UPDATE Artists SET SearchText = LOWER(Name);");

            // Populate SearchText for existing Albums (name + artists + year + label + catalog)
            migrationBuilder.Sql(@"
                UPDATE Albums SET SearchText = LOWER(
                    Name || ' ' ||
                    COALESCE((SELECT GROUP_CONCAT(a.Name, ' ')
                              FROM Artists a
                              INNER JOIN ArtistsWithRoles awr ON a.Id = awr.ArtistId
                              INNER JOIN AlbumArtistWithRole aawr ON awr.Id = aawr.ArtistsId
                              WHERE aawr.AlbumsId = Albums.Id), '') || ' ' ||
                    COALESCE(ReleaseYear, '') || ' ' ||
                    COALESCE((SELECT Name FROM RecordLabels WHERE Id = Albums.LabelId), '') || ' ' ||
                    COALESCE(CatalogNumber, '')
                );");

            // Populate SearchText for existing Tracks (title + artists + album + year + genre + label + catalog + isrc)
            migrationBuilder.Sql(@"
                UPDATE Tracks SET SearchText = LOWER(
                    Title || ' ' ||
                    COALESCE((SELECT GROUP_CONCAT(a.Name, ' ')
                              FROM Artists a
                              INNER JOIN ArtistsWithRoles awr ON a.Id = awr.ArtistId
                              INNER JOIN ArtistWithRoleTrack awrt ON awr.Id = awrt.ArtistsId
                              WHERE awrt.TracksId = Tracks.Id), '') || ' ' ||
                    COALESCE((SELECT Name FROM Albums WHERE Id = Tracks.AlbumId), '') || ' ' ||
                    COALESCE((SELECT ReleaseYear FROM Albums WHERE Id = Tracks.AlbumId), '') || ' ' ||
                    COALESCE((SELECT Name FROM Genres WHERE Id = Tracks.GenreId), '') || ' ' ||
                    COALESCE((SELECT rl.Name FROM RecordLabels rl
                              INNER JOIN Albums alb ON rl.Id = alb.LabelId
                              WHERE alb.Id = Tracks.AlbumId), '') || ' ' ||
                    COALESCE((SELECT CatalogNumber FROM Albums WHERE Id = Tracks.AlbumId), '') || ' ' ||
                    COALESCE(Isrc, '')
                );");

            // Populate FTS tables from existing data
            migrationBuilder.Sql("INSERT INTO ArtistSearch(id, search_text) SELECT Id, SearchText FROM Artists;");
            migrationBuilder.Sql("INSERT INTO AlbumSearch(id, search_text) SELECT Id, SearchText FROM Albums;");
            migrationBuilder.Sql("INSERT INTO TrackSearch(id, search_text) SELECT Id, SearchText FROM Tracks;");

            // Create triggers AFTER initial population to avoid duplicates
            migrationBuilder.Sql(@"
                CREATE TRIGGER Tracks_fts_ai AFTER INSERT ON Tracks BEGIN
                    INSERT INTO TrackSearch(id, search_text) VALUES (new.Id, new.SearchText);
                END;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER Tracks_fts_ad AFTER DELETE ON Tracks BEGIN
                    DELETE FROM TrackSearch WHERE id = old.Id;
                END;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER Tracks_fts_au AFTER UPDATE OF SearchText ON Tracks BEGIN
                    DELETE FROM TrackSearch WHERE id = old.Id;
                    INSERT INTO TrackSearch(id, search_text) VALUES (new.Id, new.SearchText);
                END;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER Albums_fts_ai AFTER INSERT ON Albums BEGIN
                    INSERT INTO AlbumSearch(id, search_text) VALUES (new.Id, new.SearchText);
                END;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER Albums_fts_ad AFTER DELETE ON Albums BEGIN
                    DELETE FROM AlbumSearch WHERE id = old.Id;
                END;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER Albums_fts_au AFTER UPDATE OF SearchText ON Albums BEGIN
                    DELETE FROM AlbumSearch WHERE id = old.Id;
                    INSERT INTO AlbumSearch(id, search_text) VALUES (new.Id, new.SearchText);
                END;");

            migrationBuilder.Sql(@"
                CREATE TRIGGER Artists_fts_ai AFTER INSERT ON Artists BEGIN
                    INSERT INTO ArtistSearch(id, search_text) VALUES (new.Id, new.SearchText);
                END;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER Artists_fts_ad AFTER DELETE ON Artists BEGIN
                    DELETE FROM ArtistSearch WHERE id = old.Id;
                END;");
            migrationBuilder.Sql(@"
                CREATE TRIGGER Artists_fts_au AFTER UPDATE OF SearchText ON Artists BEGIN
                    DELETE FROM ArtistSearch WHERE id = old.Id;
                    INSERT INTO ArtistSearch(id, search_text) VALUES (new.Id, new.SearchText);
                END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Tracks_fts_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Tracks_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Tracks_fts_au;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Albums_fts_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Albums_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Albums_fts_au;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Artists_fts_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Artists_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Artists_fts_au;");

            // Drop FTS virtual tables
            migrationBuilder.Sql("DROP TABLE IF EXISTS TrackSearch;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS AlbumSearch;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ArtistSearch;");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Albums");
        }
    }
}
