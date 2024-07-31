using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class RecordLabel1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Isrc",
                table: "Track",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogNumber",
                table: "Album",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LabelId",
                table: "Album",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Upc",
                table: "Album",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecordLabel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordLabel", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Album_LabelId",
                table: "Album",
                column: "LabelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Album_RecordLabel_LabelId",
                table: "Album",
                column: "LabelId",
                principalTable: "RecordLabel",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Album_RecordLabel_LabelId",
                table: "Album");

            migrationBuilder.DropTable(
                name: "RecordLabel");

            migrationBuilder.DropIndex(
                name: "IX_Album_LabelId",
                table: "Album");

            migrationBuilder.DropColumn(
                name: "Isrc",
                table: "Track");

            migrationBuilder.DropColumn(
                name: "CatalogNumber",
                table: "Album");

            migrationBuilder.DropColumn(
                name: "LabelId",
                table: "Album");

            migrationBuilder.DropColumn(
                name: "Upc",
                table: "Album");
        }
    }
}
