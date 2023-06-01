using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coral.Database.Migrations
{
    /// <inheritdoc />
    public partial class MusicLibrarySeparation2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Track");

            migrationBuilder.AddColumn<int>(
                name: "AudioFileId",
                table: "Track",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AudioMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bitrate = table.Column<int>(type: "INTEGER", nullable: false),
                    BitDepth = table.Column<int>(type: "INTEGER", nullable: true),
                    SampleRate = table.Column<double>(type: "REAL", nullable: false),
                    Channels = table.Column<int>(type: "INTEGER", nullable: false),
                    Codec = table.Column<string>(type: "TEXT", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MusicLibrary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LibraryPath = table.Column<string>(type: "TEXT", nullable: false),
                    LastScan = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicLibrary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileSizeInBytes = table.Column<decimal>(type: "TEXT", nullable: false),
                    AudioMetadataId = table.Column<int>(type: "INTEGER", nullable: false),
                    LibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateIndexed = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioFile_AudioMetadata_AudioMetadataId",
                        column: x => x.AudioMetadataId,
                        principalTable: "AudioMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AudioFile_MusicLibrary_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "MusicLibrary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Track_AudioFileId",
                table: "Track",
                column: "AudioFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioFile_AudioMetadataId",
                table: "AudioFile",
                column: "AudioMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioFile_LibraryId",
                table: "AudioFile",
                column: "LibraryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Track_AudioFile_AudioFileId",
                table: "Track",
                column: "AudioFileId",
                principalTable: "AudioFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Track_AudioFile_AudioFileId",
                table: "Track");

            migrationBuilder.DropTable(
                name: "AudioFile");

            migrationBuilder.DropTable(
                name: "AudioMetadata");

            migrationBuilder.DropTable(
                name: "MusicLibrary");

            migrationBuilder.DropIndex(
                name: "IX_Track_AudioFileId",
                table: "Track");

            migrationBuilder.DropColumn(
                name: "AudioFileId",
                table: "Track");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Track",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
