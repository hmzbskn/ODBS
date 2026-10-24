using Advisor.Domain.Entities;
using Advisor.Domain.Enums;

namespace Advisor.Application.Surveys;

public record ScoreBreakdown(double Total, List<ScoreItem> Items);

/// <summary>İhtiyaç puanı hesabı (saf; veritabanına gitmez).
/// Seçim soruları: seçilen seçeneklerin puanları toplamı x soru ağırlığı.
/// Sayı soruları: girilen değer x soru ağırlığı. Metin soruları puanlanmaz.
/// Her satır, toplam puanın hangi cevaptan geldiğini gösterir (şeffaflık).</summary>
public static class SurveyScoring
{
    public static ScoreBreakdown Score(IEnumerable<SurveyQuestion> questions, IEnumerable<SurveyAnswer> answers)
    {
        var byQuestion = answers.ToDictionary(a => a.QuestionId);
        var items = new List<ScoreItem>();

        foreach (var q in questions.OrderBy(q => q.Order))
        {
            if (!byQuestion.TryGetValue(q.Id, out var answer))
            {
                continue;
            }

            switch (q.Type)
            {
                case QuestionType.SingleChoice:
                case QuestionType.MultiChoice:
                {
                    var selected = ParseOptionIds(answer.SelectedOptionIds);
                    var chosen = q.Options.Where(o => selected.Contains(o.Id)).ToList();
                    if (chosen.Count == 0)
                    {
                        break;
                    }

                    var points = chosen.Sum(o => o.Score) * q.Weight;
                    items.Add(new ScoreItem(q.Id, q.Text, string.Join(", ", chosen.Select(o => o.Text)), points));
                    break;
                }
                case QuestionType.Number:
                    if (answer.NumberValue is { } value)
                    {
                        items.Add(new ScoreItem(q.Id, q.Text, FormatNumber(value), value * q.Weight));
                    }

                    break;
                case QuestionType.Text:
                    if (!string.IsNullOrWhiteSpace(answer.TextValue))
                    {
                        items.Add(new ScoreItem(q.Id, q.Text, answer.TextValue, 0));
                    }

                    break;
            }
        }

        return new ScoreBreakdown(Math.Round(items.Sum(i => i.Points), 2), items);
    }

    public static HashSet<Guid> ParseOptionIds(string? csv) =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

    public static string JoinOptionIds(IEnumerable<Guid> ids) => string.Join(",", ids);

    /// <summary>Eşit puanlılar aynı sırayı alır (1, 2, 2, 4 ...).</summary>
    public static List<int> Rank(IReadOnlyList<double> scoresDescending)
    {
        var ranks = new List<int>(scoresDescending.Count);
        for (var i = 0; i < scoresDescending.Count; i++)
        {
            ranks.Add(i > 0 && scoresDescending[i] == scoresDescending[i - 1] ? ranks[i - 1] : i + 1);
        }

        return ranks;
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
}
