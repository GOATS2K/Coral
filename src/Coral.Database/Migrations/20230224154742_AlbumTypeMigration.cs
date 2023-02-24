using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class AlbumTypeMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "ArtistsWithRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "ArtistsWithRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983));

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Albums",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Albums");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Keyword",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Genres",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artworks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "ArtistsWithRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "ArtistsWithRoles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Artists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModified",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6641),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateIndexed",
                table: "Albums",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(2023, 2, 24, 10, 39, 57, 94, DateTimeKind.Utc).AddTicks(6983),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValue: new DateTime(2023, 2, 24, 15, 47, 42, 212, DateTimeKind.Utc).AddTicks(6087));
        }
    }
}
