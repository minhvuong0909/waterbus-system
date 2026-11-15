using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterbusSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioClips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "AudioPlaybackRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "AudioPlaybackRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AudioClips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioClips", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AudioClips_Subject_Language",
                table: "AudioClips",
                columns: new[] { "Kind", "SubjectId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioClips");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AudioPlaybackRequests");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "AudioPlaybackRequests");
        }
    }
}
