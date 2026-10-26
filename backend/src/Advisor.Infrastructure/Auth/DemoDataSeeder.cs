using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Auth;

/// <summary>Demo hesaplarını oluşturur (yalnızca kullanıcı tablosu boşsa). Giriş bilgileri
/// README'de. Öğrenci hesapları, seed'deki uç durum öğrencilerine bağlıdır.</summary>
public static class DemoDataSeeder
{
    public const string DemoPassword = "Demo123!";

    private const string DemoDepartment = "Bilgisayar Mühendisliği";

    private static readonly (string Username, string Name, UserRole Role, string? AdvisorId, string? StudentNo)[] Accounts =
    {
        ("admin", "Sistem Yöneticisi", UserRole.Admin, null, null),
        ("danisman1", "Dr. Öğr. Üyesi Elif Yıldız", UserRole.Danisman, "ADV1", null),
        ("danisman2", "Doç. Dr. Mehmet Arslan", UserRole.Danisman, "ADV2", null),
        ("danisman3", "Dr. Öğr. Üyesi Zeynep Kara", UserRole.Danisman, "ADV3", null),
        ("danisman4", "Prof. Dr. Ali Çelik", UserRole.Danisman, "ADV4", null),
        ("bolumbaskani1", "Prof. Dr. Ahmet Demir", UserRole.BolumBaskani, null, null),
        ("dekan1", "Prof. Dr. Ayşe Korkmaz", UserRole.Dekan, null, null),
        ("ogrenci1", "Can Şahin", UserRole.Ogrenci, null, "20261001"),
        ("ogrenci2", "Buse Ateş", UserRole.Ogrenci, null, "20241011"),
        ("ogrenci3", "Selin Kılıç", UserRole.Ogrenci, null, "20251006"),
        ("ogrenci4", "Caner Polat", UserRole.Ogrenci, null, "20231016"),
    };

    public static async Task SeedAsync(AdvisorDbContext db, CancellationToken ct = default)
    {
        // Hesap hesap eklenir: eski veritabanı sonradan eklenen demo rollerini de alır.
        var existing = (await db.Users.Select(u => u.Username).ToListAsync(ct)).ToHashSet();
        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        foreach (var a in Accounts.Where(a => !existing.Contains(a.Username)))
        {
            db.Users.Add(new AppUser
            {
                Id = Guid.NewGuid(),
                Username = a.Username,
                PasswordHash = hash,
                DisplayName = a.Name,
                Role = a.Role,
                LinkedAdvisorId = a.AdvisorId,
                LinkedStudentNo = a.StudentNo,
                Department = a.Role == UserRole.BolumBaskani ? DemoDepartment : null,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
