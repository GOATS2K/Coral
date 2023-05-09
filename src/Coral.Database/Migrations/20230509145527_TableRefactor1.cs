using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class TableRefactor1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtistWithRole_Albums_AlbumsId",
                table: "AlbumArtistWithRole");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtistWithRole_ArtistsWithRoles_ArtistsId",
                table: "AlbumArtistWithRole");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistsWithRoles_Artists_ArtistId",
                table: "ArtistsWithRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistWithRoleTrack_ArtistsWithRoles_ArtistsId",
                table: "ArtistWithRoleTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistWithRoleTrack_Tracks_TracksId",
                table: "ArtistWithRoleTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Artworks_Albums_AlbumId",
                table: "Artworks");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTrack_Tracks_TracksId",
                table: "KeywordTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Albums_AlbumId",
                table: "Tracks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Genres_GenreId",
                table: "Tracks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tracks",
                table: "Tracks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Genres",
                table: "Genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Artworks",
                table: "Artworks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArtistsWithRoles",
                table: "ArtistsWithRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Artists",
                table: "Artists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Albums",
                table: "Albums");

            migrationBuilder.RenameTable(
                name: "Tracks",
                newName: "Track");

            migrationBuilder.RenameTable(
                name: "Genres",
                newName: "Genre");

            migrationBuilder.RenameTable(
                name: "Artworks",
                newName: "Artwork");

            migrationBuilder.RenameTable(
                name: "ArtistsWithRoles",
                newName: "ArtistWithRole");

            migrationBuilder.RenameTable(
                name: "Artists",
                newName: "Artist");

            migrationBuilder.RenameTable(
                name: "Albums",
                newName: "Album");

            migrationBuilder.RenameIndex(
                name: "IX_Tracks_GenreId",
                table: "Track",
                newName: "IX_Track_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_Tracks_AlbumId",
                table: "Track",
                newName: "IX_Track_AlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_Artworks_AlbumId",
                table: "Artwork",
                newName: "IX_Artwork_AlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_ArtistsWithRoles_ArtistId",
                table: "ArtistWithRole",
                newName: "IX_ArtistWithRole_ArtistId");

            migrationBuilder.RenameIndex(
                name: "IX_Albums_Type",
                table: "Album",
                newName: "IX_Album_Type");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Track",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Track",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genre",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genre",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artwork",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artwork",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "ArtistWithRole",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "ArtistWithRole",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artist",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artist",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Album",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Album",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Track",
                table: "Track",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Genre",
                table: "Genre",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Artwork",
                table: "Artwork",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArtistWithRole",
                table: "ArtistWithRole",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Artist",
                table: "Artist",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Album",
                table: "Album",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtistWithRole_Album_AlbumsId",
                table: "AlbumArtistWithRole",
                column: "AlbumsId",
                principalTable: "Album",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtistWithRole_ArtistWithRole_ArtistsId",
                table: "AlbumArtistWithRole",
                column: "ArtistsId",
                principalTable: "ArtistWithRole",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistWithRole_Artist_ArtistId",
                table: "ArtistWithRole",
                column: "ArtistId",
                principalTable: "Artist",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistWithRoleTrack_ArtistWithRole_ArtistsId",
                table: "ArtistWithRoleTrack",
                column: "ArtistsId",
                principalTable: "ArtistWithRole",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistWithRoleTrack_Track_TracksId",
                table: "ArtistWithRoleTrack",
                column: "TracksId",
                principalTable: "Track",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Artwork_Album_AlbumId",
                table: "Artwork",
                column: "AlbumId",
                principalTable: "Album",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KeywordTrack_Track_TracksId",
                table: "KeywordTrack",
                column: "TracksId",
                principalTable: "Track",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Track_Album_AlbumId",
                table: "Track",
                column: "AlbumId",
                principalTable: "Album",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Track_Genre_GenreId",
                table: "Track",
                column: "GenreId",
                principalTable: "Genre",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtistWithRole_Album_AlbumsId",
                table: "AlbumArtistWithRole");

            migrationBuilder.DropForeignKey(
                name: "FK_AlbumArtistWithRole_ArtistWithRole_ArtistsId",
                table: "AlbumArtistWithRole");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistWithRole_Artist_ArtistId",
                table: "ArtistWithRole");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistWithRoleTrack_ArtistWithRole_ArtistsId",
                table: "ArtistWithRoleTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_ArtistWithRoleTrack_Track_TracksId",
                table: "ArtistWithRoleTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Artwork_Album_AlbumId",
                table: "Artwork");

            migrationBuilder.DropForeignKey(
                name: "FK_KeywordTrack_Track_TracksId",
                table: "KeywordTrack");

            migrationBuilder.DropForeignKey(
                name: "FK_Track_Album_AlbumId",
                table: "Track");

            migrationBuilder.DropForeignKey(
                name: "FK_Track_Genre_GenreId",
                table: "Track");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Track",
                table: "Track");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Genre",
                table: "Genre");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Artwork",
                table: "Artwork");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArtistWithRole",
                table: "ArtistWithRole");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Artist",
                table: "Artist");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Album",
                table: "Album");

            migrationBuilder.RenameTable(
                name: "Track",
                newName: "Tracks");

            migrationBuilder.RenameTable(
                name: "Genre",
                newName: "Genres");

            migrationBuilder.RenameTable(
                name: "Artwork",
                newName: "Artworks");

            migrationBuilder.RenameTable(
                name: "ArtistWithRole",
                newName: "ArtistsWithRoles");

            migrationBuilder.RenameTable(
                name: "Artist",
                newName: "Artists");

            migrationBuilder.RenameTable(
                name: "Album",
                newName: "Albums");

            migrationBuilder.RenameIndex(
                name: "IX_Track_GenreId",
                table: "Tracks",
                newName: "IX_Tracks_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_Track_AlbumId",
                table: "Tracks",
                newName: "IX_Tracks_AlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_Artwork_AlbumId",
                table: "Artworks",
                newName: "IX_Artworks_AlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_ArtistWithRole_ArtistId",
                table: "ArtistsWithRoles",
                newName: "IX_ArtistsWithRoles_ArtistId");

            migrationBuilder.RenameIndex(
                name: "IX_Album_Type",
                table: "Albums",
                newName: "IX_Albums_Type");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "ArtistsWithRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "ArtistsWithRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4000),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 16, 33, 43, 99, DateTimeKind.Utc).AddTicks(4269),
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tracks",
                table: "Tracks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Genres",
                table: "Genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Artworks",
                table: "Artworks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArtistsWithRoles",
                table: "ArtistsWithRoles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Artists",
                table: "Artists",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Albums",
                table: "Albums",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtistWithRole_Albums_AlbumsId",
                table: "AlbumArtistWithRole",
                column: "AlbumsId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumArtistWithRole_ArtistsWithRoles_ArtistsId",
                table: "AlbumArtistWithRole",
                column: "ArtistsId",
                principalTable: "ArtistsWithRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistsWithRoles_Artists_ArtistId",
                table: "ArtistsWithRoles",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistWithRoleTrack_ArtistsWithRoles_ArtistsId",
                table: "ArtistWithRoleTrack",
                column: "ArtistsId",
                principalTable: "ArtistsWithRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtistWithRoleTrack_Tracks_TracksId",
                table: "ArtistWithRoleTrack",
                column: "TracksId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Artworks_Albums_AlbumId",
                table: "Artworks",
                column: "AlbumId",
                principalTable: "Albums",
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
                name: "FK_Tracks_Genres_GenreId",
                table: "Tracks",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id");
        }
    }
}
