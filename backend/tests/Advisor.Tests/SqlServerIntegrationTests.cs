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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Advisor.Tests;

// Bu dosyadaki testler GERÇEK SQL Server'a karşı çalışır (diğer testler EF InMemory kullanır; InMemory indeks,
// kısıt ve tür eşlemesini sınamaz). Her çalıştırma kendi geçici veritabanını kurar ve sonunda siler; canlı
// "AdvisorDb"ye dokunulmaz. SQL Server yoksa testler hata vermek yerine atlanır.
// Farklı bir sunucu için: ODBS_TEST_SQLSERVER="Server=...;User Id=...;Password=...;TrustServerCertificate=True"

public static class SqlServerTestEnv
{
    public const string DbPrefix = "AdvisorDb_Test_";

    public static string ServerConnection { get; } =
        (Environment.GetEnvironmentVariable("ODBS_TEST_SQLSERVER") ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True")
        .TrimEnd(';');

    /// <summary>Sunucuya ulaşılamıyorsa nedeni, ulaşılıyorsa null.</summary>
    public static string? UnavailableReason { get; } = Probe();

    private static string? Probe()
    {
        try
        {
            using var c = new SqlConnection(ServerConnection + ";Connect Timeout=3");
            c.Open();
            return null;
        }
        catch (Exception e)
        {
            return "SQL Server'a bağlanılamadı (testler atlandı): " + e.Message;
        }
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (SqlServerTestEnv.UnavailableReason is { } reason) Skip = reason;
    }
}

/// <summary>Ana uygulama + gerçek SQL Server (geçici veritabanı, migration'larla kurulur).</summary>
public class SqlServerFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = SqlServerTestEnv.DbPrefix + Guid.NewGuid().ToString("N")[..12];

    private string ConnectionString => $"{SqlServerTestEnv.ServerConnection};Database={DbName}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AdvisorDbContext>>();
            services.RemoveAll<AdvisorDbContext>();
            services.AddDbContext<AdvisorDbContext>(o => o.UseSqlServer(ConnectionString));

            services.RemoveAll<IObisClient>();
            services.AddSingleton<IObisClient, SeedObisClient>();

            services.RemoveAll<IFileStore>();
            services.AddSingleton<IFileStore, MemoryFileStore>();

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || SqlServerTestEnv.UnavailableReason is not null) return;

        // Yalnızca bu fabrikanın kendi geçici veritabanını sil; ad öneki canlı veritabanını koruyan emniyet.
        if (!DbName.StartsWith(SqlServerTestEnv.DbPrefix, StringComparison.Ordinal)) return;
        try
        {
            using var db = new AdvisorDbContext(new DbContextOptionsBuilder<AdvisorDbContext>().UseSqlServer(ConnectionString).Options);
            db.Database.EnsureDeleted();
        }
        catch
        {
            // Temizlik başarısız olursa test sonucunu bozma; ad "AdvisorDb_Test_" önekli olduğundan elle silinebilir.
        }
    }
}

public class SqlServerIntegrationTests : IClassFixture<SqlServerFactory>
{
    private static readonly string[] ExpectedTables =
    {
        "__EFMigrationsHistory", "AdvisorAssignments", "AnnouncementRecipients", "Announcements", "Appointments",
        "AppointmentSlots", "MeetingNotes", "NeedScoreResults", "Notifications", "PetitionRequests",
        "RegistrationReviews", "SurveyAnswers", "SurveyQuestionOptions", "SurveyQuestions", "SurveyResponses",
        "Surveys", "Users",
    };

    private readonly SqlServerFactory _factory;

    public SqlServerIntegrationTests(SqlServerFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<T> WithDb<T>(Func<AdvisorDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AdvisorDbContext>());
    }

    /// <summary>Eklemeyi dener; SQL Server kısıt ihlali (benzersiz indeks/anahtar: 2601, 2627) beklenir.</summary>
    private async Task AssertUniqueViolation(Func<AdvisorDbContext, Task> insert)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => WithDb(async db => { await insert(db); return 0; }));
        var sql = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains(sql.Number, new[] { 2601, 2627 });
    }

    // ------------------------------------------------------------ şema

    [SqlServerFact]
    public async Task Migrations_BuildTheWholeSchema_OnAnEmptyDatabase()
    {
        // Fabrika ilk istekte boş veritabanı üzerinde Migrate() çalıştırır.
        using var _ = _factory.CreateClient();

        var tables = await WithDb(db => db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sys.tables")
            .ToListAsync());
        Assert.Equal(ExpectedTables.OrderBy(x => x), tables.OrderBy(x => x));

        var pending = await WithDb(db => Task.FromResult(db.Database.GetPendingMigrations().ToList()));
        Assert.Empty(pending);
        var applied = await WithDb(db => Task.FromResult(db.Database.GetAppliedMigrations().ToList()));
        Assert.Contains(applied, m => m.EndsWith("_InitialSqlServer"));
    }

    [SqlServerFact]
    public async Task Model_HasNoChangesMissingFromTheMigrations()
    {
        using var _ = _factory.CreateClient();

        // Entity'de değişiklik yapılıp migration eklenmediyse canlı veritabanı koddan geri kalır.
        var drift = await WithDb(db => Task.FromResult(db.Database.HasPendingModelChanges()));

        Assert.False(drift, "Model, son migration'dan farklı: yeni bir migration eklenmeli.");
    }

    [SqlServerFact]
    public async Task AppointmentSlotIndex_IsFilteredToScheduledOnly()
    {
        using var _ = _factory.CreateClient();

        var filter = await WithDb(db => db.Database
            .SqlQueryRaw<string>("SELECT filter_definition AS Value FROM sys.indexes WHERE name = 'IX_Appointments_SlotId_Date'")
            .SingleAsync());

        Assert.Contains("Status", filter);
    }

    // ------------------------------------------------------------ uçtan uca

    [SqlServerFact]
    public async Task DemoAccounts_AreSeeded_AndLoginReturnsTheRightIdentity()
    {
        var count = await WithDb(async db => { using var __ = _factory.CreateClient(); return await db.Users.CountAsync(); });
        Assert.Equal(11, count);

        var adv = await LoginAs("danisman1");
        var me = await adv.GetFromJsonAsync<UserInfo>("/api/auth/me", TestJson.Options);
        Assert.Equal(UserRole.Danisman, me!.Role);
        Assert.Equal("ADV1", me.LinkedAdvisorId);

        Assert.Equal(HttpStatusCode.Forbidden, (await adv.GetAsync("/api/management/overview")).StatusCode);
        var admin = await LoginAs("bolumbaskani1");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/management/overview")).StatusCode);
    }

    [SqlServerFact]
    public async Task Appointment_RoundTripsTimeAndDate_AndDoubleBookingIsRefused()
    {
        var adv = await LoginAs("danisman1");
        var ogr2 = await LoginAs("ogrenci2");
        var ogr3 = await LoginAs("ogrenci3");

        var slotRes = await adv.PostAsJsonAsync("/api/appointments/slots", new { day = "Friday", startTime = "15:00", endTime = "15:30" });
        Assert.Equal(HttpStatusCode.OK, slotRes.StatusCode);
        var slot = (await slotRes.Content.ReadFromJsonAsync<SlotDto>(TestJson.Options))!;
        Assert.Equal("15:00", slot.StartTime);
        Assert.Equal("15:30", slot.EndTime);

        var date = new DateOnly(2026, 10, 30);
        object body = new { slotId = slot.Id, date = date.ToString("yyyy-MM-dd") };

        var first = await ogr2.PostAsJsonAsync("/api/appointments/book", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var appt = (await first.Content.ReadFromJsonAsync<AppointmentDto>(TestJson.Options))!;
        Assert.Equal(date, appt.Date);

        Assert.Equal(HttpStatusCode.Conflict, (await ogr3.PostAsJsonAsync("/api/appointments/book", body)).StatusCode);

        // İptal edilen randevunun yeri yeniden alınabilir (indeks yalnızca "planlı" kayıtlar içindir).
        Assert.Equal(HttpStatusCode.NoContent, (await ogr2.PostAsJsonAsync($"/api/appointments/{appt.Id}/cancel", new { reason = "test" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ogr3.PostAsJsonAsync("/api/appointments/book", body)).StatusCode);
    }

    // ------------------------------------------------------------ kısıtlar (veritabanı katmanında)
    // Bu bölümdeki testler sınıfın ortak veritabanını paylaşır; gerçek öğrenci numaraları (20241011 vb.) API testlerinde
    // kullanıldığı için burada uydurma numaralar (90000001...) kullanılır, böylece test sırası sonucu etkilemez.

    [SqlServerFact]
    public async Task Database_RefusesTwoScheduledAppointments_ForTheSameSlotAndDate_ButAllowsCancelledOnes()
    {
        using var _ = _factory.CreateClient();
        var slotId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 6);
        await WithDb(async db =>
        {
            db.AppointmentSlots.Add(new AppointmentSlot { Id = slotId, AdvisorId = "ADV1", DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30) });
            db.Appointments.Add(new Appointment { Id = Guid.NewGuid(), SlotId = slotId, StudentNo = "90000001", Date = date, Status = AppointmentStatus.Scheduled, BookedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });

        await AssertUniqueViolation(async db =>
        {
            db.Appointments.Add(new Appointment { Id = Guid.NewGuid(), SlotId = slotId, StudentNo = "90000002", Date = date, Status = AppointmentStatus.Scheduled, BookedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        // İptal edilmiş kayıtlar aynı saate sınırsız eklenebilir.
        var cancelled = await WithDb(async db =>
        {
            db.Appointments.Add(new Appointment { Id = Guid.NewGuid(), SlotId = slotId, StudentNo = "90000002", Date = date, Status = AppointmentStatus.CancelledByStudent, BookedAt = DateTime.UtcNow });
            db.Appointments.Add(new Appointment { Id = Guid.NewGuid(), SlotId = slotId, StudentNo = "90000003", Date = date, Status = AppointmentStatus.CancelledByAdvisor, BookedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(2, cancelled);
    }

    [SqlServerFact]
    public async Task Database_EnforcesTheOtherUniqueRules()
    {
        using var _ = _factory.CreateClient();

        // Kullanıcı adı tekil.
        await AssertUniqueViolation(async db =>
        {
            db.Users.Add(new AppUser { Id = Guid.NewGuid(), Username = "danisman1", PasswordHash = "x", DisplayName = "Kopya", Role = UserRole.Danisman, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        // Öğrenci başına en fazla bir danışman ataması.
        var assigner = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.AdvisorAssignments.Add(new AdvisorAssignment { Id = Guid.NewGuid(), StudentNo = "90000001", AdvisorId = "ADV2", AssignedByUserId = assigner, AssignedByName = "Test", AssignedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        await AssertUniqueViolation(async db =>
        {
            db.AdvisorAssignments.Add(new AdvisorAssignment { Id = Guid.NewGuid(), StudentNo = "90000001", AdvisorId = "ADV3", AssignedByUserId = assigner, AssignedByName = "Test", AssignedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        // Bir öğrenci bir ankete yalnızca bir kez yanıt verir.
        var surveyId = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.Surveys.Add(new Survey { Id = surveyId, Title = "t", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), CreatedByAdvisorId = "ADV1" });
            db.SurveyResponses.Add(new SurveyResponse { Id = Guid.NewGuid(), SurveyId = surveyId, StudentNo = "90000001", ConsentAccepted = true });
            return await db.SaveChangesAsync();
        });
        await AssertUniqueViolation(async db =>
        {
            db.SurveyResponses.Add(new SurveyResponse { Id = Guid.NewGuid(), SurveyId = surveyId, StudentNo = "90000001", ConsentAccepted = true });
            await db.SaveChangesAsync();
        });
    }

    [SqlServerFact]
    public async Task Database_StoresTurkishTextAndLongContent_WithoutLoss()
    {
        using var _ = _factory.CreateClient();
        var id = Guid.NewGuid();
        // Türkçe karakterler + emoji + 10 bin karakter: sütun nvarchar(max) olmalı, kesme/bozulma olmamalı.
        var content = "Öğrenci şğüıİç ✓ 😀 " + new string('ğ', 10_000);

        await WithDb(async db =>
        {
            db.MeetingNotes.Add(new MeetingNote { Id = id, AdvisorId = "ADV1", StudentNo = "90000001", Content = content, CreatedAt = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        var read = await WithDb(db => db.MeetingNotes.AsNoTracking().Where(n => n.Id == id).Select(n => n.Content).SingleAsync());

        Assert.Equal(content, read);
    }
}
