using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Faz10Communications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementReads");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "PetitionRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "PetitionRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FilterInternshipComplete",
                table: "Announcements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnnouncementRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StudentNo = table.Column<string>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementRecipients_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementRecipients_AnnouncementId_StudentNo",
                table: "AnnouncementRecipients",
                columns: new[] { "AnnouncementId", "StudentNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementRecipients");

            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "PetitionRequests");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "PetitionRequests");

            migrationBuilder.DropColumn(
                name: "FilterInternshipComplete",
                table: "Announcements");

            migrationBuilder.CreateTable(
                name: "AnnouncementReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StudentNo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementReads_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReads_AnnouncementId",
                table: "AnnouncementReads",
                column: "AnnouncementId");
        }
    }
}
