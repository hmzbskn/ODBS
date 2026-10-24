using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Auth;
using Advisor.Application.Communication;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Advisor.Tests;

public class AnnouncementApiTests : IDisposable
{
    private readonly CommApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static object Filter(int? classYear = null, double? minGpa = null, double? maxGpa = null, string? risk = null, bool? internship = null) =>
        new { classYear, minGpa, maxGpa, riskLevel = risk, internshipComplete = internship };

    private static async Task<AudiencePreview> Preview(HttpClient client, object filter)
    {
        var res = await client.PostAsJsonAsync("/api/announcements/preview", filter);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AudiencePreview>(TestJson.Options))!;
    }

    private static async Task<AnnouncementSummary> Send(HttpClient client, object filter, string title = "Bilgilendirme", string body = "Önemli bir duyuru metni.")
    {
        var res = await client.PostAsJsonAsync("/api/announcements", new { title, body, filter });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AnnouncementSummary>(TestJson.Options))!;
    }

    private async Task<string> ErrorOf(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"];

    // ------------------------------------------------------------ hedef kitle süzgeçleri

    [Fact]
    public async Task Preview_WithoutFilter_ReturnsAllOwnStudents()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var adv = await LoginAs("danisman1");

        var p = await Preview(adv, Filter());

        Assert.Equal(all.Count(s => s.AdvisorId == "ADV1"), p.Count);
        Assert.Equal(p.Count, p.Students.Count);
    }

    [Fact]
    public async Task Preview_ByClassYear()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var adv = await LoginAs("danisman1");

        var p = await Preview(adv, Filter(classYear: 3));

        Assert.Equal(all.Count(s => s.AdvisorId == "ADV1" && s.ClassYear == 3), p.Count);
        Assert.All(p.Students, s => Assert.Equal(3, s.ClassYear));
    }

    [Fact]
    public async Task Preview_ByGpaRange_ExcludesStudentsWithoutGpa()
    {
        var adv = await LoginAs("danisman1");

        var high = await Preview(adv, Filter(minGpa: 3.0));
        var low = await Preview(adv, Filter(maxGpa: 2.5));
        var band = await Preview(adv, Filter(minGpa: 2.5, maxGpa: 3.0));

        Assert.NotEmpty(high.Students);
        Assert.All(high.Students, s => Assert.True(s.Gpa >= 3.0));
        Assert.All(low.Students, s => Assert.True(s.Gpa is > 0 and <= 2.5)); // GANO'su olmayan birinci sınıflar dahil değil
        Assert.All(band.Students, s => Assert.InRange(s.Gpa!.Value, 2.5, 3.0));
        Assert.DoesNotContain(low.Students, s => s.ClassYear == 1);
    }

    [Fact]
    public async Task Preview_ByRiskLevel_FindsTheHighRiskStudent()
    {
        var adv = await LoginAs("danisman1");

        var high = await Preview(adv, Filter(risk: "Yuksek"));
        var medium = await Preview(adv, Filter(risk: "Orta"));

        Assert.Equal("20251007", Assert.Single(high.Students).StudentNo);
        Assert.All(medium.Students, s => Assert.Equal(RiskLevel.Orta, s.Risk));
    }

    [Fact]
    public async Task Preview_ByInternship_PartitionsAudience()
    {
        var adv = await LoginAs("danisman1");

        var all = await Preview(adv, Filter());
        var done = await Preview(adv, Filter(internship: true));
        var notDone = await Preview(adv, Filter(internship: false));

        Assert.Equal(all.Count, done.Count + notDone.Count);
        Assert.NotEmpty(done.Students);
        Assert.All(done.Students, s => Assert.True(s.InternshipDays >= 40));
        Assert.All(notDone.Students, s => Assert.True(s.InternshipDays < 40));
    }

    [Fact]
    public async Task Preview_CombinedFilters_AreIntersected()
    {
        var adv = await LoginAs("danisman1");

        var p = await Preview(adv, Filter(classYear: 4, internship: true, minGpa: 2.0));

        Assert.All(p.Students, s =>
        {
            Assert.Equal(4, s.ClassYear);
            Assert.True(s.InternshipDays >= 40);
            Assert.True(s.Gpa >= 2.0);
        });
    }

    [Theory]
    [InlineData(7, null, null)]
    [InlineData(null, 5.0, null)]
    [InlineData(null, null, -1.0)]
    [InlineData(null, 3.5, 2.0)]
    public async Task Preview_InvalidFilter_Returns400(int? classYear, double? min, double? max)
    {
        var adv = await LoginAs("danisman1");

        var res = await adv.PostAsJsonAsync("/api/announcements/preview", Filter(classYear: classYear, minGpa: min, maxGpa: max));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Preview_AdvisorCannotSeeOtherAdvisorsStudents()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var adv2 = await LoginAs("danisman2");

        var p = await Preview(adv2, Filter());

        Assert.Equal(all.Count(s => s.AdvisorId == "ADV2"), p.Count);
        var adv1Numbers = all.Where(s => s.AdvisorId == "ADV1").Select(s => s.StudentNo).ToHashSet();
        Assert.DoesNotContain(p.Students, s => adv1Numbers.Contains(s.StudentNo));
    }

    // ------------------------------------------------------------ gönderim ve okundu durumu

    [Fact]
    public async Task Send_DeliversToExactlyTheFilteredAudience()
    {
        var adv = await LoginAs("danisman1");
        var ogr3 = await LoginAs("ogrenci3"); // 20251006, 2. sınıf
        var ogr2 = await LoginAs("ogrenci2"); // 20241011, 3. sınıf

        var sent = await Send(adv, Filter(classYear: 2), "İkinci sınıflara duyuru");

        var preview = await Preview(adv, Filter(classYear: 2));
        Assert.Equal(preview.Count, sent.RecipientCount);
        Assert.Equal(0, sent.ReadCount);
        var inbox3 = (await ogr3.GetFromJsonAsync<List<InboxItem>>("/api/inbox", TestJson.Options))!;
        Assert.Contains(inbox3, i => i.Id == sent.Id && i.Kind == "Announcement" && i.Title == "İkinci sınıflara duyuru" && !i.Read);
        var inbox2 = (await ogr2.GetFromJsonAsync<List<InboxItem>>("/api/inbox", TestJson.Options))!;
        Assert.DoesNotContain(inbox2, i => i.Id == sent.Id);
    }

    [Fact]
    public async Task ReadStatus_IsTrackedPerStudent_AndVisibleToAdvisor()
    {
        var adv = await LoginAs("danisman1");
        var ogr3 = await LoginAs("ogrenci3");
        var sent = await Send(adv, Filter(classYear: 2));

        Assert.Equal(1, (await ogr3.GetFromJsonAsync<UnreadCount>("/api/inbox/unread-count", TestJson.Options))!.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await ogr3.PostAsync($"/api/inbox/announcement/{sent.Id}/read", null)).StatusCode);
        Assert.Equal(0, (await ogr3.GetFromJsonAsync<UnreadCount>("/api/inbox/unread-count", TestJson.Options))!.Count);

        var detail = (await adv.GetFromJsonAsync<AnnouncementDetail>($"/api/announcements/{sent.Id}", TestJson.Options))!;
        Assert.Equal(1, detail.Summary.ReadCount);
        Assert.Equal(sent.RecipientCount, detail.Recipients.Count);
        var read = Assert.Single(detail.Recipients, r => r.ReadAt != null);
        Assert.Equal("20251006", read.StudentNo);
        Assert.Equal("Selin Kılıç", read.FullName);
    }

    [Fact]
    public async Task MarkRead_IsIdempotent_AndKeepsFirstReadTime()
    {
        var adv = await LoginAs("danisman1");
        var ogr3 = await LoginAs("ogrenci3");
        var sent = await Send(adv, Filter(classYear: 2));

        await ogr3.PostAsync($"/api/inbox/announcement/{sent.Id}/read", null);
        var first = (await adv.GetFromJsonAsync<AnnouncementDetail>($"/api/announcements/{sent.Id}", TestJson.Options))!.Recipients.Single(r => r.ReadAt != null).ReadAt;
        await ogr3.PostAsync($"/api/inbox/announcement/{sent.Id}/read", null);
        var second = (await adv.GetFromJsonAsync<AnnouncementDetail>($"/api/announcements/{sent.Id}", TestJson.Options))!.Recipients.Single(r => r.ReadAt != null).ReadAt;

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task MarkRead_OfSomeoneElsesAnnouncement_Returns404()
    {
        var adv = await LoginAs("danisman1");
        var ogr2 = await LoginAs("ogrenci2"); // 3. sınıf: hedefte değil
        var sent = await Send(adv, Filter(classYear: 2));

        Assert.Equal(HttpStatusCode.NotFound, (await ogr2.PostAsync($"/api/inbox/announcement/{sent.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ogr2.PostAsync($"/api/inbox/gizli/{sent.Id}/read", null)).StatusCode);
    }

    [Fact]
    public async Task MarkAllRead_ClearsAnnouncementsAndNotifications()
    {
        var adv = await LoginAs("danisman1");
        var ogr3 = await LoginAs("ogrenci3");
        await Send(adv, Filter(classYear: 2), "Bir");
        await Send(adv, Filter(classYear: 2), "İki");
        Assert.Equal(2, (await ogr3.GetFromJsonAsync<UnreadCount>("/api/inbox/unread-count", TestJson.Options))!.Count);

        Assert.Equal(HttpStatusCode.NoContent, (await ogr3.PostAsync("/api/inbox/read-all", null)).StatusCode);

        Assert.Equal(0, (await ogr3.GetFromJsonAsync<UnreadCount>("/api/inbox/unread-count", TestJson.Options))!.Count);
        var inbox = (await ogr3.GetFromJsonAsync<List<InboxItem>>("/api/inbox", TestJson.Options))!;
        Assert.All(inbox, i => Assert.True(i.Read));
    }

    [Fact]
    public async Task Inbox_ListsNewestFirst_AndShowsSenderName()
    {
        var adv = await LoginAs("danisman1");
        var ogr3 = await LoginAs("ogrenci3");
        var old = await Send(adv, Filter(classYear: 2), "Eski");
        await Send(adv, Filter(classYear: 2), "Yeni");
        // Saat sabit olduğundan, "eski" duyurunun tarihini bir gün geriye al.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Advisor.Infrastructure.Persistence.AdvisorDbContext>();
            var row = await db.Announcements.FindAsync(old.Id);
            row!.CreatedAt = row.CreatedAt.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var inbox = (await ogr3.GetFromJsonAsync<List<InboxItem>>("/api/inbox", TestJson.Options))!;

        Assert.Equal(new[] { "Yeni", "Eski" }, inbox.Select(i => i.Title));
        Assert.Contains("Elif", inbox[0].Sender);
    }

    [Fact]
    public async Task Send_ToEmptyAudience_IsRejected_AndNothingIsStored()
    {
        var adv = await LoginAs("danisman1");

        // 4. sınıf + GANO >= 3.99 pratikte kimse yok
        var res = await adv.PostAsJsonAsync("/api/announcements", new { title = "Başlık", body = "Metin içeriği", filter = Filter(classYear: 4, minGpa: 3.99) });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("eşleşen öğrenci yok", await ErrorOf(res));
        Assert.Empty((await adv.GetFromJsonAsync<List<AnnouncementSummary>>("/api/announcements", TestJson.Options))!);
    }

    [Theory]
    [InlineData("", "Yeterince uzun metin")]
    [InlineData("ab", "Yeterince uzun metin")]
    [InlineData("Geçerli başlık", "")]
    [InlineData("Geçerli başlık", "kısa")]
    public async Task Send_InvalidTitleOrBody_Returns400(string title, string body)
    {
        var adv = await LoginAs("danisman1");

        var res = await adv.PostAsJsonAsync("/api/announcements", new { title, body, filter = Filter() });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ------------------------------------------------------------ yetki

    [Fact]
    public async Task Advisors_SeeOnlyTheirOwnAnnouncements_AdminSeesAll()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");
        var a1 = await Send(adv1, Filter(classYear: 1));
        var a2 = await Send(adv2, Filter());

        var list1 = (await adv1.GetFromJsonAsync<List<AnnouncementSummary>>("/api/announcements", TestJson.Options))!;
        var listAdmin = (await admin.GetFromJsonAsync<List<AnnouncementSummary>>("/api/announcements", TestJson.Options))!;

        Assert.Equal(new[] { a1.Id }, list1.Select(a => a.Id));
        Assert.Contains(listAdmin, a => a.Id == a1.Id);
        Assert.Contains(listAdmin, a => a.Id == a2.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/announcements/{a1.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/announcements/{a1.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_CanSendToAllStudents()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var admin = await LoginAs("admin");

        var sent = await Send(admin, Filter());

        Assert.Equal(all.Count, sent.RecipientCount);
        Assert.Equal("ADMIN", sent.AdvisorId);
    }

    [Fact]
    public async Task RolesAreSeparated()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/announcements")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.PostAsJsonAsync("/api/announcements/preview", Filter())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv.GetAsync("/api/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv.GetAsync("/api/inbox/unread-count")).StatusCode);

        var anon = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/inbox")).StatusCode);
    }

    [Fact]
    public async Task Announcement_AppearsInStudentTimeline()
    {
        var adv = await LoginAs("danisman1");
        await Send(adv, Filter(classYear: 3), "Staj bilgilendirmesi");

        var file = (await adv.GetFromJsonAsync<StudentFile>("/api/student-files/20241011", TestJson.Options))!;

        var item = Assert.Single(file.Timeline, t => t.Kind == "Announcement");
        Assert.Equal("Duyuru: Staj bilgilendirmesi", item.Title);
        Assert.Equal("Okunmadı", item.Detail);
    }
}
