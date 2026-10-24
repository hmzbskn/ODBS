using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Faz8SurveyScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveyResponses_SurveyId",
                table: "SurveyResponses");

            migrationBuilder.AddColumn<bool>(
                name: "Required",
                table: "SurveyQuestions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Weight",
                table: "SurveyQuestions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<double>(
                name: "Score",
                table: "SurveyQuestionOptions",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<double>(
                name: "TotalScore",
                table: "NeedScoreResults",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_SurveyId_StudentNo",
                table: "SurveyResponses",
                columns: new[] { "SurveyId", "StudentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NeedScoreResults_SurveyId_StudentNo",
                table: "NeedScoreResults",
                columns: new[] { "SurveyId", "StudentNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveyResponses_SurveyId_StudentNo",
                table: "SurveyResponses");

            migrationBuilder.DropIndex(
                name: "IX_NeedScoreResults_SurveyId_StudentNo",
                table: "NeedScoreResults");

            migrationBuilder.DropColumn(
                name: "Required",
                table: "SurveyQuestions");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "SurveyQuestions");

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                table: "SurveyQuestionOptions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<int>(
                name: "TotalScore",
                table: "NeedScoreResults",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyResponses_SurveyId",
                table: "SurveyResponses",
                column: "SurveyId");
        }
    }
}
