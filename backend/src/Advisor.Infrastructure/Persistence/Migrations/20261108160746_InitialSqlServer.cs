using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvisorAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AdvisorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PreviousAdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FilterClassYear = table.Column<int>(type: "int", nullable: true),
                    FilterMinGpa = table.Column<double>(type: "float", nullable: true),
                    FilterMaxGpa = table.Column<double>(type: "float", nullable: true),
                    FilterRiskLevel = table.Column<int>(type: "int", nullable: true),
                    FilterInternshipComplete = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NeedScoreResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TotalScore = table.Column<double>(type: "float", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupportAssigned = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeedScoreResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PetitionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdvisorNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HandledByAdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetitionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedByAdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Surveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByAdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetClassYear = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    LinkedAdvisorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedStudentNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BookedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_AppointmentSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "AppointmentSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyQuestions_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConsentAccepted = table.Column<bool>(type: "bit", nullable: false),
                    ConsentAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportedExternally = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyResponses_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyQuestionOptions_SurveyQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "SurveyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedOptionIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumberValue = table.Column<double>(type: "float", nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyAnswers_SurveyResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "SurveyResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementRecipients_AnnouncementId_StudentNo",
                table: "AnnouncementRecipients",
                columns: new[] { "AnnouncementId", "StudentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_SlotId_Date",
                table: "Appointments",
                columns: new[] { "SlotId", "Date" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NeedScoreResults_SurveyId_StudentNo",
                table: "NeedScoreResults",
                columns: new[] { "SurveyId", "StudentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAnswers_ResponseId",
                table: "SurveyAnswers",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestionOptions_QuestionId",
                table: "SurveyQuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestions_SurveyId",
                table: "SurveyQuestions",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_SurveyId_StudentNo",
                table: "SurveyResponses",
                columns: new[] { "SurveyId", "StudentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvisorAssignments");

            migrationBuilder.DropTable(
                name: "AnnouncementRecipients");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "MeetingNotes");

            migrationBuilder.DropTable(
                name: "NeedScoreResults");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PetitionRequests");

            migrationBuilder.DropTable(
                name: "RegistrationReviews");

            migrationBuilder.DropTable(
                name: "SurveyAnswers");

            migrationBuilder.DropTable(
                name: "SurveyQuestionOptions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "AppointmentSlots");

            migrationBuilder.DropTable(
                name: "SurveyResponses");

            migrationBuilder.DropTable(
                name: "SurveyQuestions");

            migrationBuilder.DropTable(
                name: "Surveys");
        }
    }
}
