using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Auth;
using Advisor.Application.Communication;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Advisor.Tests;

public class StudentFileApiTests : IDisposable
{
    private const string Student = "20241011"; // Buse Ateş, ADV1 (demo hesabı ogrenci2)

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

    private static async Task<NoteDto> AddNote(HttpClient advisor, string content, string studentNo = Student)
    {
        var res = await advisor.PostAsJsonAsync($"/api/student-files/{studentNo}/notes", new { content });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<NoteDto>(TestJson.Options))!;
    }

    private static async Task<StudentFile> GetFile(HttpClient client, string studentNo = Student) =>
        (await client.GetFromJsonAsync<StudentFile>($"/api/student-files/{studentNo}", TestJson.Options))!;

    [Fact]
    public async Task Advisor_AddsNote_AndSeesItInTimeline()
    {
        var adv = await LoginAs("danisman1");

        var note = await AddNote(adv, "Ders seçimi konusunda görüştük; MAT101 şubesini değiştirecek.");

        var file = await GetFile(adv);
        Assert.Equal("Buse Ateş", file.FullName);
        Assert.Equal(note.Id, Assert.Single(file.Notes).Id);
        var item = file.Timeline.Single(t => t.Kind == "Note");
        Assert.Contains("MAT101", item.Detail);
        Assert.Equal("ADV1", note.AdvisorId);
    }

    [Fact]
    public async Task Timeline_MergesNotesAppointmentsAndPetitions_NewestFirst()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var slot = await (await adv.PostAsJsonAsync("/api/appointments/slots", new { day = "Friday", startTime = "10:00", endTime = "10:30" }))
            .Content.ReadFromJsonAsync<SlotDto>(TestJson.Options);
        await ogr.PostAsJsonAsync("/api/appointments/book", new { slotId = slot!.Id, date = "2026-11-06" });
        await AddNote(adv, "İlk görüşme notu");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdvisorDbContext>();
            db.PetitionRequests.Add(new PetitionRequest
            {
                Id = Guid.NewGuid(), StudentNo = Student, Type = PetitionType.KayitDondurma,
                Description = "Sağlık sebebiyle kaydımı dondurmak istiyorum.", CreatedAt = new DateTime(2026, 10, 1),
            });
            await db.SaveChangesAsync();
        }

        var file = await GetFile(adv);

        Assert.Equal(new[] { "Appointment", "Note", "Petition" }.OrderBy(x => x), file.Timeline.Select(t => t.Kind).Distinct().OrderBy(x => x));
        Assert.Equal(file.Timeline.OrderByDescending(t => t.Date).Select(t => t.Date), file.Timeline.Select(t => t.Date));
        Assert.Contains(file.Timeline, t => t.Kind == "Petition" && t.Title == "Talep: Kayıt dondurma" && t.Detail!.StartsWith("Beklemede"));
        Assert.Contains(file.Timeline, t => t.Kind == "Appointment" && t.Title.Contains("06.11.2026") && t.Detail == "Planlandı");
    }

    [Fact]
    public async Task Notes_AreClosedToStudents()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        await AddNote(adv, "Gizli not içeriği");

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync($"/api/student-files/{Student}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.PostAsJsonAsync($"/api/student-files/{Student}/notes", new { content = "x" })).StatusCode);

        // Öğrencinin erişebildiği hiçbir uçta not metni geçmemeli.
        foreach (var url in new[] { "/api/appointments", "/api/appointments/availability", "/api/auth/me", "/api/registrations/" + Student })
        {
            var body = await (await ogr.GetAsync(url)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("Gizli not içeriği", body);
        }
    }

    [Fact]
    public async Task OtherAdvisor_CannotReadOrWriteNotes()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        await AddNote(adv1, "Sadece ADV1 görsün");

        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.GetAsync($"/api/student-files/{Student}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.PostAsJsonAsync($"/api/student-files/{Student}/notes", new { content = "x" })).StatusCode);
    }

    [Fact]
    public async Task Admin_CanRead_ButCannotAddNotes()
    {
        var adv = await LoginAs("danisman1");
        var admin = await LoginAs("admin");
        await AddNote(adv, "Yönetici de okuyabilir");

        Assert.Single((await GetFile(admin)).Notes);
        var res = await admin.PostAsJsonAsync($"/api/student-files/{Student}/notes", new { content = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNote_IsRejected(string content)
    {
        var adv = await LoginAs("danisman1");

        var res = await adv.PostAsJsonAsync($"/api/student-files/{Student}/notes", new { content });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TooLongNote_IsRejected_ButMaximumIsAccepted()
    {
        var adv = await LoginAs("danisman1");

        Assert.Equal(HttpStatusCode.OK, (await adv.PostAsJsonAsync($"/api/student-files/{Student}/notes", new { content = new string('a', 2000) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adv.PostAsJsonAsync($"/api/student-files/{Student}/notes", new { content = new string('a', 2001) })).StatusCode);
    }

    [Fact]
    public async Task UnknownStudent_AdminGets404()
    {
        var admin = await LoginAs("admin");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/student-files/99999999")).StatusCode);
    }

    [Fact]
    public async Task DeleteNote_AuthorAndAdminOnly()
    {
        var adv = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");
        var note1 = await AddNote(adv, "Silinecek not");
        var note2 = await AddNote(adv, "Yönetici silecek");

        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.DeleteAsync($"/api/student-files/notes/{note1.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adv.DeleteAsync($"/api/student-files/notes/{note1.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/student-files/notes/{note2.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adv.DeleteAsync($"/api/student-files/notes/{note1.Id}")).StatusCode);
        Assert.Empty((await GetFile(adv)).Notes);
    }

    [Fact]
    public async Task Student_CannotDeleteNotes()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var note = await AddNote(adv, "Silinemez");

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.DeleteAsync($"/api/student-files/notes/{note.Id}")).StatusCode);
    }
}
