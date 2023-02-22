using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class BaseTableMigration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_Albums_AlbumsId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_Artists_ArtistsId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTrack_Keywords_KeywordsId",
                table: "KeywordTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTrack_Tracks_TracksId",
                table: "KeywordTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Albums_AlbumId",
                table: "Tracks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Genres_GenreId",
                table: "Tracks");

            migrationBuilder.DropTable(
                name: "Artists");

            migrationBuilder.DropTable(
                name: "Artworks");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Keywords");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tracks",
                table: "Tracks");

            migrationBuilder.RenameTable(
                name: "Tracks",
                newName: "BaseTable");

            migrationBuilder.RenameIndex(
                name: "IX_Tracks_GenreId",
                table: "BaseTable",
                newName: "IX_BaseTable_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_Tracks_ArtistId",
                table: "BaseTable",
                newName: "IX_BaseTable_ArtistId");

            migrationBuilder.RenameIndex(
                name: "IX_Tracks_AlbumId",
                table: "BaseTable",
                newName: "IX_BaseTable_AlbumId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "BaseTable",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "BaseTable",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "DurationInSeconds",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "BaseTable",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 15, 27, 36, 814, DateTimeKind.Utc).AddTicks(3806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "BaseTable",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 15, 27, 36, 814, DateTimeKind.Utc).AddTicks(4275),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "ArtistId",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "AlbumId",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Album_Name",
                table: "BaseTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverFilePath",
                table: "BaseTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscTotal",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "BaseTable",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Genre_Name",
                table: "BaseTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "BaseTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "BaseTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseYear",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Size",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackTotal",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Track_AlbumId",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "BaseTable",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "BaseTable",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BaseTable",
                table: "BaseTable",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_BaseTable_Track_AlbumId",
                table: "BaseTable",
                column: "Track_AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseTable_Value",
                table: "BaseTable",
                column: "Value");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_BaseTable_AlbumsId",
                table: "AlbumArtist",
                column: "AlbumsId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_BaseTable_ArtistsId",
                table: "AlbumArtist",
                column: "ArtistsId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BaseTable_BaseTable_AlbumId",
                table: "BaseTable",
                column: "AlbumId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BaseTable_BaseTable_ArtistId",
                table: "BaseTable",
                column: "ArtistId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BaseTable_BaseTable_GenreId",
                table: "BaseTable",
                column: "GenreId",
                principalTable: "BaseTable",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BaseTable_BaseTable_Track_AlbumId",
                table: "BaseTable",
                column: "Track_AlbumId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTrack_BaseTable_KeywordsId",
                table: "KeywordTrack",
                column: "KeywordsId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTrack_BaseTable_TracksId",
                table: "KeywordTrack",
                column: "TracksId",
                principalTable: "BaseTable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_BaseTable_AlbumsId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtist_BaseTable_ArtistsId",
                table: "AlbumArtist");

            migrationBuilder.DropForeignKey(
                name: "FK_BaseTable_BaseTable_AlbumId",
                table: "BaseTable");

            migrationBuilder.DropForeignKey(
                name: "FK_BaseTable_BaseTable_ArtistId",
                table: "BaseTable");

            migrationBuilder.DropForeignKey(
                name: "FK_BaseTable_BaseTable_GenreId",
                table: "BaseTable");

            migrationBuilder.DropForeignKey(
                name: "FK_BaseTable_BaseTable_Track_AlbumId",
                table: "BaseTable");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTrack_BaseTable_KeywordsId",
                table: "KeywordTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTrack_BaseTable_TracksId",
                table: "KeywordTrack");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BaseTable",
                table: "BaseTable");

            migrationBuilder.DropIndex(
                name: "IX_BaseTable_Track_AlbumId",
                table: "BaseTable");

            migrationBuilder.DropIndex(
                name: "IX_BaseTable_Value",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Album_Name",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "CoverFilePath",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "DiscTotal",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Genre_Name",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "TrackTotal",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Track_AlbumId",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "BaseTable");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "BaseTable");

            migrationBuilder.RenameTable(
                name: "BaseTable",
                newName: "Tracks");

            migrationBuilder.RenameIndex(
                name: "IX_BaseTable_GenreId",
                table: "Tracks",
                newName: "IX_Tracks_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_BaseTable_ArtistId",
                table: "Tracks",
                newName: "IX_Tracks_ArtistId");

            migrationBuilder.RenameIndex(
                name: "IX_BaseTable_AlbumId",
                table: "Tracks",
                newName: "IX_Tracks_AlbumId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DurationInSeconds",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 15, 27, 36, 814, DateTimeKind.Utc).AddTicks(3806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 15, 27, 36, 814, DateTimeKind.Utc).AddTicks(4275));

            migrationBuilder.AlterColumn<int>(
                name: "ArtistId",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AlbumId",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tracks",
                table: "Tracks",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CoverFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DiscTotal = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    TrackTotal = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Artworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artworks_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artworks_AlbumId",
                table: "Artworks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_Value",
                table: "Keywords",
                column: "Value");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_Albums_AlbumsId",
                table: "AlbumArtist",
                column: "AlbumsId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtist_Artists_ArtistsId",
                table: "AlbumArtist",
                column: "ArtistsId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTrack_Keywords_KeywordsId",
                table: "KeywordTrack",
                column: "KeywordsId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTrack_Tracks_TracksId",
                table: "KeywordTrack",
                column: "TracksId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Albums_AlbumId",
                table: "Tracks",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Genres_GenreId",
                table: "Tracks",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id");
        }
    }
}
