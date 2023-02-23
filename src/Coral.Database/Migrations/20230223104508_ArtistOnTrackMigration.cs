using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class ArtistOnTrackMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_ArtistId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ArtistId",
                table: "Tracks");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415));

            migrationBuilder.CreateTable(
                name: "ArtistOnTrack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719)),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306)),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    ArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                name: "IX_ArtistOnTrack_ArtistId",
                table: "ArtistOnTrack",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtistOnTrack_TrackId",
                table: "ArtistOnTrack",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtistOnTrack");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AddColumn<Guid>(
                name: "ArtistId",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6088),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 22, 16, 45, 7, 356, DateTimeKind.Utc).AddTicks(6415),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 23, 10, 45, 7, 870, DateTimeKind.Utc).AddTicks(9719));

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_ArtistId",
                table: "Tracks",
                column: "ArtistId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
