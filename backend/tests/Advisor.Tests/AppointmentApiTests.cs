using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Application.Auth;
using Advisor.Application.Communication;
using Advisor.Application.Obis;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Advisor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Tests;

/// <summary>Seed verisi + sabit saat: Pazartesi 26 Ekim 2026, 12:00 (Türkiye saati) = 09:00 UTC.</summary>
/// <summary>Testlerde diske yazmayan dosya deposu.</summary>
public class MemoryFileStore : Advisor.Application.Communication.IFileStore
{
    public Dictionary<string, byte[]> Stored { get; } = new();

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var name = $"{Guid.NewGuid():N}{extension}";
        Stored[name] = ms.ToArray();
        return name;
    }

    public Task<Stream?> OpenAsync(string storedName, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Stored.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);
}

public class CommApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public MemoryFileStore Files { get; } = new();

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

            services.RemoveAll<Advisor.Application.Communication.IFileStore>();
            services.AddSingleton<Advisor.Application.Communication.IFileStore>(Files);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero)));
        });
    }
}

/// <summary>Her test kendi fabrikasını (ve boş veritabanını) kullanır: randevu sınırı gibi durumlar testler arasında sızmaz.</summary>
public class AppointmentApiTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 26); // Pazartesi
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

    private static async Task<SlotDto> CreateSlot(HttpClient advisor, string day, string start, string end)
    {
        var res = await advisor.PostAsJsonAsync("/api/appointments/slots", new { day, startTime = start, endTime = end });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<SlotDto>(TestJson.Options))!;
    }

    // Her test kendi gününü kullanır (aynı danışman için çakışma olmasın diye) ya da saat aralığını ayırır.
    private static Task<HttpResponseMessage> Book(HttpClient student, Guid slotId, DateOnly date) =>
        student.PostAsJsonAsync("/api/appointments/book", new { slotId, date = date.ToString("yyyy-MM-dd") });

    private async Task<string> ErrorOf(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"];

    private async Task<T> WithDb<T>(Func<AdvisorDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
    }

    // ------------------------------------------------------------ görüşme saatleri

    [Fact]
    public async Task Advisor_CreatesAndListsSlots()
    {
        var adv = await LoginAs("danisman1");

        var slot = await CreateSlot(adv, "Wednesday", "09:00", "09:30");

        var list = (await adv.GetFromJsonAsync<List<SlotDto>>("/api/appointments/slots", TestJson.Options))!;
        Assert.Contains(list, s => s.Id == slot.Id && s.StartTime == "09:00" && s.EndTime == "09:30" && s.AdvisorId == "ADV1");
    }

    [Fact]
    public async Task Slots_AreRoleRestricted()
    {
        var ogr = await LoginAs("ogrenci2");
        var admin = await LoginAs("admin");

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/appointments/slots")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.PostAsJsonAsync("/api/appointments/slots", new { day = "Monday", startTime = "10:00", endTime = "10:30" })).StatusCode);
        // Yönetici görüşme saati tanımlayamaz (danışmanı yoktur).
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/appointments/slots", new { day = "Monday", startTime = "10:00", endTime = "10:30" })).StatusCode);
    }

    [Theory]
    [InlineData("Saturday", "10:00", "10:30", "hafta içi")]
    [InlineData("Monday", "10:30", "10:00", "Bitiş saati")]
    [InlineData("Monday", "10:00", "10:00", "Bitiş saati")]
    [InlineData("Monday", "07:00", "07:30", "arasında olmalıdır")]
    [InlineData("Monday", "19:45", "20:15", "arasında olmalıdır")]
    [InlineData("Monday", "10:00", "10:05", "dakika arasında")]
    [InlineData("Monday", "10:00", "13:00", "dakika arasında")]
    [InlineData("Monday", "25:00", "26:00", "SS:dd")]
    [InlineData("Monday", "ten", "eleven", "SS:dd")]
    public async Task CreateSlot_InvalidInput_Returns400(string day, string start, string end, string messagePart)
    {
        var adv = await LoginAs("danisman1");

        var res = await adv.PostAsJsonAsync("/api/appointments/slots", new { day, startTime = start, endTime = end });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(messagePart, await ErrorOf(res));
    }

    [Fact]
    public async Task CreateSlot_OverlappingSlot_Returns409_ButAdjacentIsFine()
    {
        var adv = await LoginAs("danisman2");
        await CreateSlot(adv, "Thursday", "10:00", "10:30");

        var overlap = await adv.PostAsJsonAsync("/api/appointments/slots", new { day = "Thursday", startTime = "10:15", endTime = "10:45" });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);

        var adjacent = await adv.PostAsJsonAsync("/api/appointments/slots", new { day = "Thursday", startTime = "10:30", endTime = "11:00" });
        Assert.Equal(HttpStatusCode.OK, adjacent.StatusCode);
    }

    [Fact]
    public async Task DeleteSlot_WithFutureBooking_IsRefused_ThenAllowedAfterCancel()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci1");
        var slot = await CreateSlot(adv, "Friday", "15:00", "15:30");
        var booked = await (await Book(ogr, slot.Id, new DateOnly(2026, 10, 30))).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, (await adv.DeleteAsync($"/api/appointments/slots/{slot.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ogr.PostAsJsonAsync($"/api/appointments/{booked!.Id}/cancel", new { reason = "Vazgeçtim" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adv.DeleteAsync($"/api/appointments/slots/{slot.Id}")).StatusCode);

        var list = (await adv.GetFromJsonAsync<List<SlotDto>>("/api/appointments/slots", TestJson.Options))!;
        Assert.DoesNotContain(list, s => s.Id == slot.Id);
    }

    [Fact]
    public async Task DeleteSlot_OfAnotherAdvisor_IsForbidden()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var slot = await CreateSlot(adv1, "Tuesday", "16:00", "16:30");

        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.DeleteAsync($"/api/appointments/slots/{slot.Id}")).StatusCode);
    }

    // ------------------------------------------------------------ randevu alma

    [Fact]
    public async Task Availability_ListsOnlyOwnAdvisorsFutureSlots_WithinFourWeeks()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var ogr = await LoginAs("ogrenci2"); // ADV1
        var mine = await CreateSlot(adv1, "Wednesday", "11:00", "11:30");
        var theirs = await CreateSlot(adv2, "Wednesday", "11:00", "11:30");

        var av = (await ogr.GetFromJsonAsync<AvailabilityResponse>("/api/appointments/availability", TestJson.Options))!;

        Assert.Equal("ADV1", av.AdvisorId);
        Assert.Contains("Elif", av.AdvisorName);
        var wednesdays = av.Slots.Where(s => s.SlotId == mine.Id).ToList();
        Assert.Equal(4, wednesdays.Count); // 28.10, 04.11, 11.11, 18.11 (25.11 ufkun dışında)
        Assert.All(wednesdays, s => Assert.True(s.Available));
        Assert.DoesNotContain(av.Slots, s => s.SlotId == theirs.Id);
        Assert.All(av.Slots, s => Assert.True(s.Date <= Today.AddDays(28)));
    }

    [Fact]
    public async Task Book_Succeeds_AppearsForBothSides_AndBlocksOthers()
    {
        var adv = await LoginAs("danisman1");
        var ogr2 = await LoginAs("ogrenci2");
        var ogr3 = await LoginAs("ogrenci3");
        var slot = await CreateSlot(adv, "Monday", "17:00", "17:30");
        var date = new DateOnly(2026, 11, 2);

        var res = await Book(ogr2, slot.Id, date);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var appt = (await res.Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;
        Assert.Equal("Buse Ateş", appt.StudentName);
        Assert.Equal(AppointmentStatus.Scheduled, appt.Status);

        var mineList = (await ogr2.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", TestJson.Options))!;
        Assert.Contains(mineList, a => a.Id == appt.Id);
        var advList = (await adv.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", TestJson.Options))!;
        Assert.Contains(advList, a => a.Id == appt.Id && a.StudentNo == "20241011");
        var otherStudentList = (await ogr3.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", TestJson.Options))!;
        Assert.DoesNotContain(otherStudentList, a => a.Id == appt.Id);

        var av = (await ogr3.GetFromJsonAsync<AvailabilityResponse>("/api/appointments/availability", TestJson.Options))!;
        var taken = av.Slots.Single(s => s.SlotId == slot.Id && s.Date == date);
        Assert.False(taken.Available);
        Assert.False(taken.BookedByMe);
        var avMine = (await ogr2.GetFromJsonAsync<AvailabilityResponse>("/api/appointments/availability", TestJson.Options))!;
        Assert.True(avMine.Slots.Single(s => s.SlotId == slot.Id && s.Date == date).BookedByMe);

        var second = await Book(ogr3, slot.Id, date);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("dolu", await ErrorOf(second));
    }

    [Fact]
    public async Task Book_AnotherAdvisorsSlot_IsForbidden()
    {
        var adv2 = await LoginAs("danisman2");
        var ogr = await LoginAs("ogrenci2"); // ADV1
        var slot = await CreateSlot(adv2, "Friday", "09:00", "09:30");

        var res = await Book(ogr, slot.Id, new DateOnly(2026, 10, 30));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Book_Validations()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var soon = await CreateSlot(adv, "Monday", "12:30", "13:00");   // bugün 12:00 -> 30 dk sonra
        var later = await CreateSlot(adv, "Monday", "18:00", "18:30");

        // Hafta günü uyuşmuyor (Salı)
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(ogr, later.Id, new DateOnly(2026, 10, 27))).StatusCode);
        // Geçmiş tarih
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(ogr, later.Id, new DateOnly(2026, 10, 19))).StatusCode);
        // 28 günden uzak (Pazartesi 30 Kasım = 35 gün)
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(ogr, later.Id, new DateOnly(2026, 11, 30))).StatusCode);
        // Bir saatten az kala
        var tooSoon = await Book(ogr, soon.Id, Today);
        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);
        Assert.Contains("en az 60 dakika", await ErrorOf(tooSoon));
        // Aynı gün ama yeterince ileride: olur
        Assert.Equal(HttpStatusCode.OK, (await Book(ogr, later.Id, Today)).StatusCode);
        // Bilinmeyen saat
        Assert.Equal(HttpStatusCode.NotFound, (await Book(ogr, Guid.NewGuid(), Today)).StatusCode);
    }

    [Fact]
    public async Task Book_MoreThanTwoActive_IsRefused()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci4");
        var a = await CreateSlot(adv, "Tuesday", "08:00", "08:30");
        var b = await CreateSlot(adv, "Tuesday", "08:30", "09:00");
        var c = await CreateSlot(adv, "Tuesday", "09:00", "09:30");
        var date = new DateOnly(2026, 11, 3);

        Assert.Equal(HttpStatusCode.OK, (await Book(ogr, a.Id, date)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Book(ogr, b.Id, date)).StatusCode);
        var third = await Book(ogr, c.Id, date);

        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Contains("En fazla 2", await ErrorOf(third));
    }

    // ------------------------------------------------------------ iptal ve tamamlama

    [Fact]
    public async Task StudentCancel_FreesTheSlotForOthers()
    {
        var adv = await LoginAs("danisman1");
        var ogr2 = await LoginAs("ogrenci2");
        var ogr3 = await LoginAs("ogrenci3");
        var slot = await CreateSlot(adv, "Wednesday", "14:00", "14:30");
        var date = new DateOnly(2026, 11, 4);
        var appt = (await (await Book(ogr2, slot.Id, date)).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.NoContent, (await ogr2.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { reason = "Sınavım var" })).StatusCode);

        var list = (await adv.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", TestJson.Options))!;
        var cancelled = list.Single(a => a.Id == appt.Id);
        Assert.Equal(AppointmentStatus.CancelledByStudent, cancelled.Status);
        Assert.Equal("Sınavım var", cancelled.CancellationReason);
        Assert.Equal(HttpStatusCode.OK, (await Book(ogr3, slot.Id, date)).StatusCode);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Returns400()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var slot = await CreateSlot(adv, "Thursday", "13:00", "13:30");
        var appt = (await (await Book(ogr, slot.Id, new DateOnly(2026, 10, 29))).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;

        await ogr.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { });
        var again = await ogr.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task AdvisorCancel_NotifiesTheStudent_WithReason()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci3");
        var slot = await CreateSlot(adv, "Friday", "10:00", "10:30");
        var appt = (await (await Book(ogr, slot.Id, new DateOnly(2026, 11, 6))).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;

        var res = await adv.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { reason = "Toplantım çıktı" });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var notes = await WithDb(db => db.Notifications.Where(n => n.StudentNo == "20251006").ToListAsync());
        var n = Assert.Single(notes);
        Assert.Contains("06.11.2026", n.Message);
        Assert.Contains("10:00", n.Message);
        Assert.Contains("Toplantım çıktı", n.Message);
        var list = (await ogr.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", TestJson.Options))!;
        Assert.Equal(AppointmentStatus.CancelledByAdvisor, list.Single(a => a.Id == appt.Id).Status);
    }

    [Fact]
    public async Task Cancel_ByUnrelatedParties_IsRejected()
    {
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var ogr2 = await LoginAs("ogrenci2");
        var ogr3 = await LoginAs("ogrenci3");
        var slot = await CreateSlot(adv1, "Monday", "19:00", "19:30");
        var appt = (await (await Book(ogr2, slot.Id, new DateOnly(2026, 11, 9))).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.NotFound, (await ogr3.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await adv2.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { })).StatusCode);
    }

    [Fact]
    public async Task Cancel_PastAppointment_IsRefused_AndCompleteWorksOnlyAfterStart()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var slot = await CreateSlot(adv, "Tuesday", "11:00", "11:30");
        var future = (await (await Book(ogr, slot.Id, new DateOnly(2026, 11, 10))).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;

        // Geçmişte kalmış planlı bir randevuyu doğrudan veritabanına ekle (24 Ekim Cumartesi değil: 20 Ekim Salı).
        var past = await WithDb(async db =>
        {
            var a = new Appointment
            {
                Id = Guid.NewGuid(), SlotId = slot.Id, StudentNo = "20241011", Date = new DateOnly(2026, 10, 20),
                Status = AppointmentStatus.Scheduled, BookedAt = DateTime.UtcNow,
            };
            db.Appointments.Add(a);
            await db.SaveChangesAsync();
            return a.Id;
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await adv.PostAsJsonAsync($"/api/appointments/{past}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adv.PostAsync($"/api/appointments/{future.Id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adv.PostAsync($"/api/appointments/{past}/complete", null)).StatusCode);
        // Tamamlanan tekrar tamamlanamaz / iptal edilemez.
        Assert.Equal(HttpStatusCode.BadRequest, (await adv.PostAsync($"/api/appointments/{past}/complete", null)).StatusCode);

        var list = (await adv.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments", TestJson.Options))!;
        var done = list.Single(a => a.Id == past);
        Assert.Equal(AppointmentStatus.Completed, done.Status);
        Assert.True(done.IsPast);
    }

    [Fact]
    public async Task Complete_IsStaffOnly()
    {
        var adv = await LoginAs("danisman1");
        var ogr = await LoginAs("ogrenci2");
        var slot = await CreateSlot(adv, "Wednesday", "16:00", "16:30");
        var appt = (await (await Book(ogr, slot.Id, new DateOnly(2026, 11, 11))).Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;

        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.PostAsync($"/api/appointments/{appt.Id}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/appointments/availability")).StatusCode);
    }
}
