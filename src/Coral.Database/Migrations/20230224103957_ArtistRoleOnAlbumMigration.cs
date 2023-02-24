using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class ArtistRoleOnAlbumMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumArtist");

            migrationBuilder.DropTable(
                name: "ArtistOnTrack");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.CreateTable(
                name: "ArtistsWithRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983)),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641)),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistsWithRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtistsWithRoles_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumArtistWithRole",
                columns: table => new
                {
                    AlbumsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArtistsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumArtistWithRole", x => new { x.AlbumsId, x.ArtistsId });
                    table.ForeignKey(
                        name: "FK_AlbumArtistWithRole_Albums_AlbumsId",
                        column: x => x.AlbumsId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumArtistWithRole_ArtistsWithRoles_ArtistsId",
                        column: x => x.ArtistsId,
                        principalTable: "ArtistsWithRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistWithRoleTrack",
                columns: table => new
                {
                    ArtistsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TracksId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistWithRoleTrack", x => new { x.ArtistsId, x.TracksId });
                    table.ForeignKey(
                        name: "FK_ArtistWithRoleTrack_ArtistsWithRoles_ArtistsId",
                        column: x => x.ArtistsId,
                        principalTable: "ArtistsWithRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistWithRoleTrack_Tracks_TracksId",
                        column: x => x.TracksId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumArtistWithRole_ArtistsId",
                table: "AlbumArtistWithRole",
                column: "ArtistsId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistsWithRoles_ArtistId",
                table: "ArtistsWithRoles",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistWithRoleTrack_TracksId",
                table: "ArtistWithRoleTrack",
                column: "TracksId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumArtistWithRole");

            migrationBuilder.DropTable(
                name: "ArtistWithRoleTrack");

            migrationBuilder.DropTable(
                name: "ArtistsWithRoles");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.CreateTable(
                name: "AlbumArtist",
                columns: table => new
                {
                    AlbumsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArtistsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumArtist", x => new { x.AlbumsId, x.ArtistsId });
                    table.ForeignKey(
                        name: "FK_AlbumArtist_Albums_AlbumsId",
                        column: x => x.AlbumsId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumArtist_Artists_ArtistsId",
                        column: x => x.ArtistsId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtistOnTrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719)),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306)),
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistOnTrack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtistOnTrack_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtistOnTrack_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumArtist_ArtistsId",
                table: "AlbumArtist",
                column: "ArtistsId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistOnTrack_ArtistId",
                table: "ArtistOnTrack",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistOnTrack_TrackId",
                table: "ArtistOnTrack",
                column: "TrackId");
        }
    }
}
