using Advisor.Domain.Enums;

namespace Advisor.Application.Surveys;

// ---- danışman: tanım ----

public record OptionInput(string Text, double Score);

public record QuestionInput(string Text, QuestionType Type, double Weight, bool Required, List<OptionInput>? Options);

public record CreateSurveyRequest(
    string Title, string? Description, DateOnly StartDate, DateOnly EndDate,
    int? TargetClassYear, List<QuestionInput> Questions);

public record OptionScoreInput(Guid OptionId, double Score);

public record QuestionScoringInput(Guid QuestionId, double Weight, List<OptionScoreInput>? Options);

public record ScoringUpdateRequest(List<QuestionScoringInput> Questions);

public record OptionDto(Guid Id, string Text, double Score);

public record QuestionDto(Guid Id, string Text, QuestionType Type, int Order, double Weight, bool Required, List<OptionDto> Options);

public record SurveySummary(
    Guid Id, string Title, DateOnly StartDate, DateOnly EndDate, string Status,
    int QuestionCount, int TargetCount, int ResponseCount);

public record SurveyDefinition(
    Guid Id, string Title, string? Description, DateOnly StartDate, DateOnly EndDate, string Status,
    int? TargetClassYear, string CreatedByAdvisorId, int TargetCount, int ResponseCount,
    List<QuestionDto> Questions);

// ---- öğrenci: form ve gönderim (puanlar ASLA gönderilmez) ----

public record StudentSurveyItem(Guid Id, string Title, string? Description, DateOnly StartDate, DateOnly EndDate, string Status, bool Submitted);

public record FormOptionDto(Guid Id, string Text);

public record FormQuestionDto(Guid Id, string Text, QuestionType Type, bool Required, List<FormOptionDto> Options);

public record SurveyForm(
    Guid Id, string Title, string? Description, DateOnly EndDate, string ConsentText, string ConsentStatement,
    List<FormQuestionDto> Questions);

public record AnswerInput(Guid QuestionId, List<Guid>? OptionIds, double? Number, string? Text);

public record SubmitRequest(bool Consent, List<AnswerInput> Answers);

// ---- sonuçlar ----

public record ScoreItem(Guid QuestionId, string QuestionText, string AnswerText, double Points);

public record ResultRow(
    int Rank, string StudentNo, string FullName, int ClassYear, double TotalScore,
    bool ImportedExternally, string? Support, List<ScoreItem> Breakdown);

public record ResultsResponse(Guid SurveyId, string Title, int TargetCount, int ResponseCount, List<ResultRow> Rows);

public record AssignSupportRequest(List<string> StudentNos, string? SupportType);

// ---- içe aktarma ----

public record ImportRequest(string CsvText, bool ConsentConfirmed);

public record ImportError(int Row, string? StudentNo, string Message);

public record ImportResult(int Imported, List<ImportError> Errors);

// ---- anonim istatistik ----

public record OptionStat(string Text, int Count);

public record NumberStat(int Count, double Min, double Max, double Average);

public record QuestionStat(Guid QuestionId, string Text, QuestionType Type, List<OptionStat>? Options, NumberStat? Number, int AnsweredCount);

public record StatsResponse(int TargetCount, int ResponseCount, List<QuestionStat> Questions);
