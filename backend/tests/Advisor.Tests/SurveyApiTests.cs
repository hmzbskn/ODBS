using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Advisor.Application.Auth;
using Advisor.Application.Obis;
using Advisor.Application.Surveys;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Tests;

internal class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>Seed verisi + sabit saat (24 Ekim 2026) ile çalışan API fabrikası.</summary>
public class SurveyApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AdvisorDbContext>>();
            services.RemoveAll<AdvisorDbContext>();
            services.AddDbContext<AdvisorDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IObisClient>();
            services.AddSingleton<IObisClient, SeedObisClient>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero)));
        });
    }
}

public class SurveyApiTests : IClassFixture<SurveyApiFactory>
{
    private readonly SurveyApiFactory _factory;

    public SurveyApiTests(SurveyApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    // Gelir (tek seçim, ağırlık 1), kardeş sayısı (sayı, ağırlık 2), çalışma durumu (çoklu seçim), not (metin).
    private static object SurveyBody(int? targetClassYear = null, string start = "2026-10-01", string end = "2026-11-30", string title = "Sosyoekonomik Anket") => new
    {
        title,
        description = "Yemek kartı için ihtiyaç anketi",
        startDate = start,
        endDate = end,
        targetClassYear,
        questions = new object[]
        {
            new
            {
                text = "Hane geliri", type = "SingleChoice", weight = 1.0, required = true,
                options = new object[]
                {
                    new { text = "0-20 bin TL", score = 10.0 },
                    new { text = "20-40 bin TL", score = 5.0 },
                    new { text = "40 bin TL üstü", score = 0.0 },
                },
            },
            new { text = "Kardeş sayısı", type = "Number", weight = 2.0, required = true, options = (object[]?)null },
            new
            {
                text = "Durumunuz", type = "MultiChoice", weight = 1.0, required = false,
                options = new object[]
                {
                    new { text = "Çalışıyorum", score = 3.0 },
                    new { text = "Yurtta kalıyorum", score = 4.0 },
                },
            },
            new { text = "Eklemek istedikleriniz", type = "Text", weight = 1.0, required = false, options = (object[]?)null },
        },
    };

    private static async Task<SurveyDefinition> Create(HttpClient advisor, object? body = null)
    {
        var res = await advisor.PostAsJsonAsync("/api/surveys", body ?? SurveyBody());
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<SurveyDefinition>(TestJson.Options))!;
    }

    private static object Answers(SurveyDefinition s, string income, double siblings, string[]? status = null, string? note = null, bool consent = true)
    {
        Guid Q(string text) => s.Questions.Single(q => q.Text == text).Id;
        Guid Opt(string question, string option) => s.Questions.Single(q => q.Text == question).Options.Single(o => o.Text == option).Id;

        return new
        {
            consent,
            answers = new object[]
            {
                new { questionId = Q("Hane geliri"), optionIds = new[] { Opt("Hane geliri", income) } },
                new { questionId = Q("Kardeş sayısı"), number = siblings },
                new { questionId = Q("Durumunuz"), optionIds = (status ?? Array.Empty<string>()).Select(o => Opt("Durumunuz", o)).ToArray() },
                new { questionId = Q("Eklemek istedikleriniz"), text = note },
            },
        };
    }

    private static async Task<HttpResponseMessage> Submit(HttpClient student, SurveyDefinition s, object body) =>
        await student.PostAsJsonAsync($"/api/surveys/{s.Id}/submit", body);

    // ------------------------------------------------------------ oluşturma ve doğrulama

    [Fact]
    public async Task Create_ReturnsDefinitionWithScores_AndTargetCount()
    {
        var adv = await LoginAs("danisman1");

        var s = await Create(adv);

        Assert.Equal("Open", s.Status);
        Assert.Equal(4, s.Questions.Count);
        Assert.Equal(10, s.Questions[0].Options[0].Score);
        Assert.Equal(2, s.Questions[1].Weight);
        var all = await new SeedObisClient().GetStudentsAsync();
        Assert.Equal(all.Count(x => x.AdvisorId == "ADV1"), s.TargetCount);
        Assert.Equal("ADV1", s.CreatedByAdvisorId);
    }

    [Fact]
    public async Task Create_StudentIsForbidden()
    {
        var ogr = await LoginAs("ogrenci1");
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.PostAsJsonAsync("/api/surveys", SurveyBody())).StatusCode);
    }

    [Theory]
    [InlineData("noQuestions")]
    [InlineData("oneOption")]
    [InlineData("endBeforeStart")]
    [InlineData("emptyTitle")]
    [InlineData("badClassYear")]
    [InlineData("duplicateQuestion")]
    [InlineData("optionsOnNumber")]
    [InlineData("pipeInOption")]
    public async Task Create_InvalidInput_Returns400WithTurkishMessage(string kind)
    {
        var adv = await LoginAs("danisman1");
        object body = kind switch
        {
            "noQuestions" => new { title = "x", startDate = "2026-10-01", endDate = "2026-11-01", questions = Array.Empty<object>() },
            "oneOption" => new
            {
                title = "x", startDate = "2026-10-01", endDate = "2026-11-01",
                questions = new object[] { new { text = "q", type = "SingleChoice", weight = 1.0, required = true, options = new object[] { new { text = "a", score = 1.0 } } } },
            },
            "endBeforeStart" => SurveyBody(start: "2026-11-01", end: "2026-10-01"),
            "emptyTitle" => SurveyBody(title: "  "),
            "badClassYear" => SurveyBody(targetClassYear: 7),
            "duplicateQuestion" => new
            {
                title = "x", startDate = "2026-10-01", endDate = "2026-11-01",
                questions = new object[]
                {
                    new { text = "Aynı", type = "Number", weight = 1.0, required = true },
                    new { text = "aynı", type = "Number", weight = 1.0, required = true },
                },
            },
            "optionsOnNumber" => new
            {
                title = "x", startDate = "2026-10-01", endDate = "2026-11-01",
                questions = new object[] { new { text = "q", type = "Number", weight = 1.0, required = true, options = new object[] { new { text = "a", score = 1.0 } } } },
            },
            _ => new
            {
                title = "x", startDate = "2026-10-01", endDate = "2026-11-01",
                questions = new object[]
                {
                    new { text = "q", type = "SingleChoice", weight = 1.0, required = true, options = new object[] { new { text = "a|b", score = 1.0 }, new { text = "c", score = 0.0 } } },
                },
            },
        };

        var res = await adv.PostAsJsonAsync("/api/surveys", body);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var message = (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"];
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    // ------------------------------------------------------------ gizlilik ve yetki

    [Fact]
    public async Task OtherAdvisor_CannotSeeOrChangeSurvey()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var s = await Create(adv1);

        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/surveys/{s.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/surveys/{s.Id}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/surveys/{s.Id}/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/surveys/{s.Id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.DeleteAsync($"/api/surveys/{s.Id}")).StatusCode);

        var list = await adv2.GetFromJsonAsync<List<SurveySummary>>("/api/surveys", TestJson.Options);
        Assert.DoesNotContain(list!, x => x.Id == s.Id);
    }

    [Fact]
    public async Task Admin_CanSeeAnySurvey()
    {
        var adv1 = await LoginAs("danisman1");
        var admin = await LoginAs("admin");
        var s = await Create(adv1);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/surveys/{s.Id}")).StatusCode);
        var list = await admin.GetFromJsonAsync<List<SurveySummary>>("/api/surveys", TestJson.Options);
        Assert.Contains(list!, x => x.Id == s.Id);
    }

    [Fact]
    public async Task UnknownSurvey_Returns404()
    {
        var adv = await LoginAs("danisman1");
        Assert.Equal(HttpStatusCode.NotFound, (await adv.GetAsync($"/api/surveys/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Student_CannotCallStaffEndpoints()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync($"/api/surveys/{s.Id}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync($"/api/surveys/{s.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/surveys")).StatusCode);
    }

    [Fact]
    public async Task Advisor_CannotCallStudentEndpoints()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        Assert.Equal(HttpStatusCode.Forbidden, (await adv.GetAsync($"/api/surveys/{s.Id}/form")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv.GetAsync("/api/surveys/mine")).StatusCode);
    }

    [Fact]
    public async Task StudentForm_NeverContainsScoresOrWeights()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);

        var raw = await (await ogr.GetAsync($"/api/surveys/{s.Id}/form")).Content.ReadAsStringAsync();

        Assert.Contains("Hane geliri", raw);
        Assert.Contains("KİŞİSEL VERİLERİN KORUNMASI", raw);
        Assert.DoesNotContain("score", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StudentOutsideTarget_GetsNotFound_AndDoesNotSeeItInList()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2"); // 3. sınıf
        var s = await Create(adv, SurveyBody(targetClassYear: 1));

        Assert.Equal(HttpStatusCode.NotFound, (await ogr.GetAsync($"/api/surveys/{s.Id}/form")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(ogr, s, Answers(s, "0-20 bin TL", 1))).StatusCode);
        var mine = await ogr.GetFromJsonAsync<List<StudentSurveyItem>>("/api/surveys/mine", TestJson.Options);
        Assert.DoesNotContain(mine!, x => x.Id == s.Id);

        var first = await LoginAs("ogrenci1"); // 1. sınıf, hedefte
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync($"/api/surveys/{s.Id}/form")).StatusCode);
    }

    [Fact]
    public async Task ClosedAndPlannedSurveys_CannotBeAnswered()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var closed = await Create(adv, SurveyBody(start: "2026-08-01", end: "2026-09-01"));
        var planned = await Create(adv, SurveyBody(start: "2026-12-01", end: "2026-12-31"));

        Assert.Equal("Closed", closed.Status);
        Assert.Equal("Planned", planned.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await ogr.GetAsync($"/api/surveys/{closed.Id}/form")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(ogr, planned, Answers(planned, "0-20 bin TL", 1))).StatusCode);
        var mine = await ogr.GetFromJsonAsync<List<StudentSurveyItem>>("/api/surveys/mine", TestJson.Options);
        Assert.DoesNotContain(mine!, x => x.Id == closed.Id || x.Id == planned.Id);
    }

    // ------------------------------------------------------------ KVKK ve gönderim

    [Fact]
    public async Task Submit_WithoutConsent_IsRejected_AndNothingIsStored()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);

        var res = await Submit(ogr, s, Answers(s, "0-20 bin TL", 2, consent: false));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("KVKK", (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"]);
        var results = await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options);
        Assert.Empty(results!.Rows);
    }

    [Fact]
    public async Task Submit_Twice_Returns409()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);

        Assert.Equal(HttpStatusCode.NoContent, (await Submit(ogr, s, Answers(s, "0-20 bin TL", 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(ogr, s, Answers(s, "40 bin TL üstü", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ogr.GetAsync($"/api/surveys/{s.Id}/form")).StatusCode);

        var mine = await ogr.GetFromJsonAsync<List<StudentSurveyItem>>("/api/surveys/mine", TestJson.Options);
        Assert.True(mine!.Single(x => x.Id == s.Id).Submitted);
    }

    [Fact]
    public async Task Submit_MissingRequiredAnswer_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);
        var onlyIncome = new
        {
            consent = true,
            answers = new object[]
            {
                new { questionId = s.Questions[0].Id, optionIds = new[] { s.Questions[0].Options[0].Id } },
            },
        };

        var res = await Submit(ogr, s, onlyIncome);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Kardeş sayısı", (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"]);
    }

    [Fact]
    public async Task Submit_OptionFromAnotherQuestion_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);
        var foreign = s.Questions[2].Options[0].Id; // "Durumunuz" seçeneği, gelir sorusuna gönderiliyor
        var body = new
        {
            consent = true,
            answers = new object[]
            {
                new { questionId = s.Questions[0].Id, optionIds = new[] { foreign } },
                new { questionId = s.Questions[1].Id, number = 1.0 },
            },
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(ogr, s, body)).StatusCode);
    }

    [Fact]
    public async Task Submit_TwoOptionsOnSingleChoice_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);
        var body = new
        {
            consent = true,
            answers = new object[]
            {
                new { questionId = s.Questions[0].Id, optionIds = new[] { s.Questions[0].Options[0].Id, s.Questions[0].Options[1].Id } },
                new { questionId = s.Questions[1].Id, number = 1.0 },
            },
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(ogr, s, body)).StatusCode);
    }

    [Fact]
    public async Task Submit_AnswerToUnknownQuestion_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var s = await Create(adv);
        var body = new
        {
            consent = true,
            answers = new object[]
            {
                new { questionId = s.Questions[0].Id, optionIds = new[] { s.Questions[0].Options[0].Id } },
                new { questionId = s.Questions[1].Id, number = 1.0 },
                new { questionId = Guid.NewGuid(), text = "x" },
            },
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(ogr, s, body)).StatusCode);
    }

    // ------------------------------------------------------------ puanlama ve sıralama

    [Fact]
    public async Task Results_AreRankedAndExplainEveryPoint()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        var ogr2 = await LoginAs("ogrenci2"); // 20241011
        var ogr1 = await LoginAs("ogrenci1"); // 20261001
        var ogr3 = await LoginAs("ogrenci3"); // 20251006

        // ogrenci2: 10*1 + 3*2 + (3+4)*1 = 23;  ogrenci1: 5 + 0 + 0 = 5;  ogrenci3: 10 + 2 = 12
        await Submit(ogr2, s, Answers(s, "0-20 bin TL", 3, new[] { "Çalışıyorum", "Yurtta kalıyorum" }, "Burs lazım"));
        await Submit(ogr1, s, Answers(s, "20-40 bin TL", 0));
        await Submit(ogr3, s, Answers(s, "0-20 bin TL", 1));

        var results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;

        Assert.Equal(3, results.ResponseCount);
        Assert.Equal(new[] { "20241011", "20251006", "20261001" }, results.Rows.Select(r => r.StudentNo));
        Assert.Equal(new[] { 23.0, 12.0, 5.0 }, results.Rows.Select(r => r.TotalScore));
        Assert.Equal(new[] { 1, 2, 3 }, results.Rows.Select(r => r.Rank));

        var top = results.Rows[0];
        Assert.Equal("Buse Ateş", top.FullName);
        // Toplam, kırılımın toplamına eşit olmalı (şeffaflık).
        Assert.Equal(top.TotalScore, top.Breakdown.Sum(b => b.Points));
        Assert.Contains(top.Breakdown, b => b.QuestionText == "Hane geliri" && b.Points == 10);
        Assert.Contains(top.Breakdown, b => b.QuestionText == "Kardeş sayısı" && b.Points == 6);
        Assert.Contains(top.Breakdown, b => b.QuestionText == "Durumunuz" && b.Points == 7 && b.AnswerText.Contains("Yurtta"));
        Assert.Contains(top.Breakdown, b => b.QuestionText == "Eklemek istedikleriniz" && b.Points == 0);
    }

    [Fact]
    public async Task Results_TiedScoresShareRank()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), s, Answers(s, "0-20 bin TL", 1));
        await Submit(await LoginAs("ogrenci3"), s, Answers(s, "0-20 bin TL", 1));
        await Submit(await LoginAs("ogrenci1"), s, Answers(s, "40 bin TL üstü", 0));

        var results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;

        Assert.Equal(new[] { 1, 1, 3 }, results.Rows.Select(r => r.Rank));
    }

    [Fact]
    public async Task UpdateScoring_ChangesExistingResultsImmediately()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), s, Answers(s, "0-20 bin TL", 3));

        var income = s.Questions[0];
        var siblings = s.Questions[1];
        var update = new
        {
            questions = new object[]
            {
                new { questionId = income.Id, weight = 1.0, options = new object[] { new { optionId = income.Options[0].Id, score = 50.0 } } },
                new { questionId = siblings.Id, weight = 10.0 },
            },
        };
        var res = await adv.PutAsJsonAsync($"/api/surveys/{s.Id}/scoring", update);
        res.EnsureSuccessStatusCode();

        var results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;
        Assert.Equal(80, results.Rows[0].TotalScore); // 50 + 3*10
    }

    [Fact]
    public async Task UpdateScoring_UnknownQuestion_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        var res = await adv.PutAsJsonAsync($"/api/surveys/{s.Id}/scoring", new { questions = new object[] { new { questionId = Guid.NewGuid(), weight = 1.0 } } });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ------------------------------------------------------------ destek ataması

    [Fact]
    public async Task AssignSupport_ShowsInResults_AndCanBeCleared()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), s, Answers(s, "0-20 bin TL", 3));
        await Submit(await LoginAs("ogrenci1"), s, Answers(s, "40 bin TL üstü", 0));

        var assign = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/support", new { studentNos = new[] { "20241011" }, supportType = "Yemek kartı" });
        assign.EnsureSuccessStatusCode();

        var results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;
        Assert.Equal("Yemek kartı", results.Rows.Single(r => r.StudentNo == "20241011").Support);
        Assert.Null(results.Rows.Single(r => r.StudentNo == "20261001").Support);

        await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/support", new { studentNos = new[] { "20241011" }, supportType = "" });
        results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;
        Assert.Null(results.Rows.Single(r => r.StudentNo == "20241011").Support);
    }

    [Fact]
    public async Task AssignSupport_ToStudentWhoDidNotRespond_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        var res = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/support", new { studentNos = new[] { "20241011" }, supportType = "Burs" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AssignSupport_EmptySelection_IsRejected()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        var res = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/support", new { studentNos = Array.Empty<string>(), supportType = "Burs" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ------------------------------------------------------------ dışa aktarma ve istatistik

    [Fact]
    public async Task Export_IsExcelFriendlyCsv_WithRankScoreAndSupport()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), s, Answers(s, "0-20 bin TL", 3));
        await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/support", new { studentNos = new[] { "20241011" }, supportType = "Yemek kartı" });

        var res = await adv.GetAsync($"/api/surveys/{s.Id}/export");
        var bytes = await res.Content.ReadAsByteArrayAsync();

        Assert.Equal("text/csv", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray()); // UTF-8 BOM
        var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("Sıra;Öğrenci No;Ad Soyad;Sınıf;İhtiyaç Puanı;Destek", lines[0]);
        Assert.Equal("1;20241011;Buse Ateş;3;16;Yemek kartı", lines[1]);
    }

    [Fact]
    public async Task Stats_AreAnonymous_AndCountAnswers()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), s, Answers(s, "0-20 bin TL", 4, note: "Gizli not içeriği"));
        await Submit(await LoginAs("ogrenci3"), s, Answers(s, "0-20 bin TL", 2));
        await Submit(await LoginAs("ogrenci1"), s, Answers(s, "40 bin TL üstü", 0));

        var raw = await (await adv.GetAsync($"/api/surveys/{s.Id}/stats")).Content.ReadAsStringAsync();
        var stats = System.Text.Json.JsonSerializer.Deserialize<StatsResponse>(raw, TestJson.Options)!;

        Assert.Equal(3, stats.ResponseCount);
        var income = stats.Questions.Single(q => q.Text == "Hane geliri");
        Assert.Equal(2, income.Options!.Single(o => o.Text == "0-20 bin TL").Count);
        Assert.Equal(1, income.Options!.Single(o => o.Text == "40 bin TL üstü").Count);
        var siblings = stats.Questions.Single(q => q.Text == "Kardeş sayısı").Number!;
        Assert.Equal(new[] { 0.0, 4.0, 2.0 }, new[] { siblings.Min, siblings.Max, siblings.Average });
        // Kimlik ve serbest metin özette yer almaz.
        Assert.DoesNotContain("2024", raw);
        Assert.DoesNotContain("Buse", raw);
        Assert.DoesNotContain("Gizli not içeriği", raw);
        Assert.Equal(1, stats.Questions.Single(q => q.Type == Advisor.Domain.Enums.QuestionType.Text).AnsweredCount);
    }

    // ------------------------------------------------------------ CSV içe aktarma

    private static async Task<string> TemplateOf(HttpClient adv, SurveyDefinition s) =>
        (await adv.GetStringAsync($"/api/surveys/{s.Id}/import-template")).TrimStart('﻿').TrimEnd();

    [Fact]
    public async Task ImportTemplate_HasStudentNoAndQuestionColumns()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        var header = await TemplateOf(adv, s);

        Assert.Equal("Öğrenci No;Hane geliri;Kardeş sayısı;Durumunuz;Eklemek istedikleriniz", header);
    }

    [Fact]
    public async Task Import_ImportsValidRows_AndReportsEachBadRow()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        var all = await new SeedObisClient().GetStudentsAsync();
        var adv1 = all.Where(x => x.AdvisorId == "ADV1").Select(x => x.StudentNo).ToList();
        var foreignStudent = all.First(x => x.AdvisorId == "ADV2").StudentNo;
        var header = await TemplateOf(adv, s);

        var csv = string.Join("\n", new[]
        {
            header,
            $"{adv1[0]};0-20 bin TL;3;Çalışıyorum|Yurtta kalıyorum;Not",
            $"{adv1[1]};20-40 bin TL;1,5;;",
            $"{foreignStudent};0-20 bin TL;1;;",          // başka danışmanın öğrencisi
            $"{adv1[2]};Bilinmeyen;1;;",                  // geçersiz seçenek
            $"{adv1[3]};0-20 bin TL;abc;;",               // geçersiz sayı
            $"{adv1[0]};0-20 bin TL;1;;",                 // dosyada tekrar
            $";0-20 bin TL;1;;",                          // numara boş
            $"{adv1[4]};;2;;",                            // zorunlu soru boş
        });

        var res = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/import", new { csvText = csv, consentConfirmed = true });
        res.EnsureSuccessStatusCode();
        var result = (await res.Content.ReadFromJsonAsync<ImportResult>(TestJson.Options))!;

        Assert.Equal(2, result.Imported);
        Assert.Equal(new[] { 4, 5, 6, 7, 8, 9 }, result.Errors.Select(e => e.Row).OrderBy(r => r));
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("hedef kitlesinde değil"));
        Assert.Contains(result.Errors, e => e.Row == 5 && e.Message.Contains("geçerli bir seçenek değil"));
        Assert.Contains(result.Errors, e => e.Row == 6 && e.Message.Contains("geçerli bir sayı değil"));
        Assert.Contains(result.Errors, e => e.Row == 7 && e.Message.Contains("zaten yanıt var"));

        var results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;
        Assert.Equal(2, results.ResponseCount);
        Assert.All(results.Rows, r => Assert.True(r.ImportedExternally));
        // 10 + 3*2 + 7 = 23 ; 5 + 1.5*2 = 8
        Assert.Equal(new[] { 23.0, 8.0 }, results.Rows.Select(r => r.TotalScore));
    }

    [Fact]
    public async Task Import_RequiresConsentConfirmation()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        var csv = await TemplateOf(adv, s) + "\n20241011;0-20 bin TL;1;;";

        var res = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/import", new { csvText = csv, consentConfirmed = false });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var results = (await adv.GetFromJsonAsync<ResultsResponse>($"/api/surveys/{s.Id}/results", TestJson.Options))!;
        Assert.Empty(results.Rows);
    }

    [Fact]
    public async Task Import_MissingRequiredColumn_Rejects()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        var res = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/import",
            new { csvText = "Öğrenci No;Hane geliri\n20241011;0-20 bin TL", consentConfirmed = true });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Kardeş sayısı", (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"]);
    }

    [Fact]
    public async Task Import_WrongFirstColumn_Rejects()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);

        var res = await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/import",
            new { csvText = "Ad;Hane geliri;Kardeş sayısı\nx;0-20 bin TL;1", consentConfirmed = true });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Import_CannotImportIntoOtherAdvisorsSurvey()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var s = await Create(adv1);

        var res = await adv2.PostAsJsonAsync($"/api/surveys/{s.Id}/import",
            new { csvText = "Öğrenci No;Hane geliri;Kardeş sayısı\n1;a;1", consentConfirmed = true });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Import_StudentAlreadyAnsweredThroughForm_IsReportedAsDuplicate()
    {
        var adv = await LoginAs("danisman1");
        var s = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), s, Answers(s, "0-20 bin TL", 1));
        var csv = await TemplateOf(adv, s) + "\n20241011;40 bin TL üstü;0;;";

        var result = (await (await adv.PostAsJsonAsync($"/api/surveys/{s.Id}/import", new { csvText = csv, consentConfirmed = true }))
            .Content.ReadFromJsonAsync<ImportResult>(TestJson.Options))!;

        Assert.Equal(0, result.Imported);
        Assert.Contains(result.Errors, e => e.Message.Contains("zaten yanıt var"));
    }

    // ------------------------------------------------------------ silme

    [Fact]
    public async Task Delete_SurveyWithResponses_IsRefused_ButEmptyOneIsDeleted()
    {
        var adv = await LoginAs("danisman1");
        var answered = await Create(adv);
        var empty = await Create(adv);
        await Submit(await LoginAs("ogrenci2"), answered, Answers(answered, "0-20 bin TL", 1));

        Assert.Equal(HttpStatusCode.Conflict, (await adv.DeleteAsync($"/api/surveys/{answered.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adv.DeleteAsync($"/api/surveys/{empty.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adv.GetAsync($"/api/surveys/{empty.Id}")).StatusCode);
    }
}
