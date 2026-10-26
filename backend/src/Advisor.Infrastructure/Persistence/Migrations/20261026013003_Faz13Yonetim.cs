using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Faz13Yonetim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdvisorAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StudentNo = table.Column<string>(type: "TEXT", nullable: false),
                    AdvisorId = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousAdvisorId = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedByName = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorAssignments_AdvisorId",
                table: "AdvisorAssignments",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorAssignments_StudentNo",
                table: "AdvisorAssignments",
                column: "StudentNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvisorAssignments");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Users");
        }
    }
}
