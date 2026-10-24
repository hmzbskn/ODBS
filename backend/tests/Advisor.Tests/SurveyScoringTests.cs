using Advisor.Application.Surveys;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;

namespace Advisor.Tests;

public class SurveyScoringTests
{
    private static SurveyQuestion Choice(string text, QuestionType type, double weight, params (string Text, double Score)[] options)
    {
        var q = new SurveyQuestion { Id = Guid.NewGuid(), Text = text, Type = type, Weight = weight, Order = 0 };
        q.Options = options.Select(o => new SurveyQuestionOption { Id = Guid.NewGuid(), QuestionId = q.Id, Text = o.Text, Score = o.Score }).ToList();
        return q;
    }

    private static SurveyQuestion Simple(string text, QuestionType type, double weight = 1) =>
        new() { Id = Guid.NewGuid(), Text = text, Type = type, Weight = weight };

    private static SurveyAnswer Pick(SurveyQuestion q, params string[] optionTexts) => new()
    {
        QuestionId = q.Id,
        SelectedOptionIds = SurveyScoring.JoinOptionIds(q.Options.Where(o => optionTexts.Contains(o.Text)).Select(o => o.Id)),
    };

    [Fact]
    public void SingleChoice_ScoreIsOptionScoreTimesWeight()
    {
        var q = Choice("Gelir", QuestionType.SingleChoice, 2, ("Düşük", 10), ("Yüksek", 0));

        var result = SurveyScoring.Score(new[] { q }, new[] { Pick(q, "Düşük") });

        Assert.Equal(20, result.Total);
        var item = Assert.Single(result.Items);
        Assert.Equal("Düşük", item.AnswerText);
        Assert.Equal(20, item.Points);
    }

    [Fact]
    public void MultiChoice_SumsSelectedOptions()
    {
        var q = Choice("Durum", QuestionType.MultiChoice, 1, ("Çalışıyor", 3), ("Yurtta kalıyor", 4), ("Burslu", -2));

        var result = SurveyScoring.Score(new[] { q }, new[] { Pick(q, "Çalışıyor", "Yurtta kalıyor", "Burslu") });

        Assert.Equal(5, result.Total);
    }

    [Fact]
    public void Number_ScoreIsValueTimesWeight_AndWeightMayBeNegative()
    {
        var siblings = Simple("Kardeş sayısı", QuestionType.Number, 3);
        var income = Simple("Aylık gelir (bin TL)", QuestionType.Number, -0.5);
        var answers = new[]
        {
            new SurveyAnswer { QuestionId = siblings.Id, NumberValue = 4 },
            new SurveyAnswer { QuestionId = income.Id, NumberValue = 10 },
        };

        var result = SurveyScoring.Score(new[] { siblings, income }, answers);

        Assert.Equal(7, result.Total); // 12 - 5
    }

    [Fact]
    public void Text_IsListedButNotScored()
    {
        var q = Simple("Eklemek istedikleriniz", QuestionType.Text);

        var result = SurveyScoring.Score(new[] { q }, new[] { new SurveyAnswer { QuestionId = q.Id, TextValue = "Burs lazım" } });

        Assert.Equal(0, result.Total);
        Assert.Equal(0, Assert.Single(result.Items).Points);
    }

    [Fact]
    public void UnansweredQuestions_AreSkipped()
    {
        var q1 = Choice("A", QuestionType.SingleChoice, 1, ("x", 5), ("y", 1));
        var q2 = Simple("B", QuestionType.Number, 1);

        var result = SurveyScoring.Score(new[] { q1, q2 }, new[] { Pick(q1, "x") });

        Assert.Single(result.Items);
        Assert.Equal(5, result.Total);
    }

    [Fact]
    public void ForeignOptionIds_AreIgnored()
    {
        var q = Choice("A", QuestionType.SingleChoice, 1, ("x", 5), ("y", 1));
        var answer = new SurveyAnswer { QuestionId = q.Id, SelectedOptionIds = Guid.NewGuid().ToString() };

        Assert.Equal(0, SurveyScoring.Score(new[] { q }, new[] { answer }).Total);
    }

    [Fact]
    public void Total_IsRoundedToTwoDecimals()
    {
        var q = Simple("Oran", QuestionType.Number, 1.0 / 3);

        var result = SurveyScoring.Score(new[] { q }, new[] { new SurveyAnswer { QuestionId = q.Id, NumberValue = 1 } });

        Assert.Equal(0.33, result.Total);
    }

    [Fact]
    public void Rank_TiesShareRank_ThenSkip()
    {
        var ranks = SurveyScoring.Rank(new[] { 30.0, 20.0, 20.0, 10.0 });

        Assert.Equal(new[] { 1, 2, 2, 4 }, ranks);
    }

    [Fact]
    public void ParseOptionIds_ToleratesGarbage()
    {
        var id = Guid.NewGuid();

        var parsed = SurveyScoring.ParseOptionIds($"{id}, not-a-guid,,");

        Assert.Equal(new[] { id }, parsed);
        Assert.Empty(SurveyScoring.ParseOptionIds(null));
    }
}

public class CsvTests
{
    [Fact]
    public void Parse_DetectsSemicolonDelimiter_AndStripsBom()
    {
        var rows = Csv.Parse("﻿No;Ad\n1;Ali\n2;Veli\n", out var delimiter);

        Assert.Equal(';', delimiter);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "No", "Ad" }, rows[0]);
        Assert.Equal(new[] { "2", "Veli" }, rows[2]);
    }

    [Fact]
    public void Parse_HandlesQuotesEscapedQuotesAndEmbeddedNewlines()
    {
        var rows = Csv.Parse("a,b\n\"x,1\",\"he said \"\"hi\"\"\"\n\"line1\nline2\",z", out var delimiter);

        Assert.Equal(',', delimiter);
        Assert.Equal("x,1", rows[1][0]);
        Assert.Equal("he said \"hi\"", rows[1][1]);
        Assert.Equal("line1\nline2", rows[2][0]);
        Assert.Equal("z", rows[2][1]);
    }

    [Fact]
    public void Parse_SkipsBlankLines_AndHandlesCrLf()
    {
        var rows = Csv.Parse("a;b\r\n\r\n1;2\r\n;\r\n", out _);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Parse_LastRowWithoutTrailingNewline_IsKept()
    {
        var rows = Csv.Parse("a;b\n1;2", out _);

        Assert.Equal(new[] { "1", "2" }, rows[1]);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")] // enjeksiyon etkisizleştirilir, ardından tırnaklanır
    public void Escape_NeutralizesFormulaInjection(string input, string expected) =>
        Assert.Equal(expected, Csv.Escape(input));

    [Theory]
    [InlineData("-12.5")]
    [InlineData("+90555")]
    [InlineData("-3")]
    public void Escape_KeepsNumbersIntact(string number) => Assert.Equal(number, Csv.Escape(number));

    [Fact]
    public void Escape_QuotesCellsContainingDelimiter()
    {
        Assert.Equal("\"a;b\"", Csv.Escape("a;b"));
        Assert.Equal("a,b", Csv.Escape("a,b")); // ';' ayırıcıyken virgül serbest
    }

    [Fact]
    public void RoundTrip_WriteThenParse()
    {
        var line = Csv.Line(new[] { "Ali; Veli", "he \"said\"", "x" });

        var rows = Csv.Parse("h1;h2;h3\n" + line, out _);

        Assert.Equal(new[] { "Ali; Veli", "he \"said\"", "x" }, rows[1]);
    }
}
