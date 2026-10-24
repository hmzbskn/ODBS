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

    private static readonly (string Username, string Name, UserRole Role, string? AdvisorId, string? StudentNo)[] Accounts =
    {
        ("admin", "Sistem Yöneticisi", UserRole.Admin, null, null),
        ("danisman1", "Dr. Öğr. Üyesi Elif Yıldız", UserRole.Danisman, "ADV1", null),
        ("danisman2", "Doç. Dr. Mehmet Arslan", UserRole.Danisman, "ADV2", null),
        ("danisman3", "Dr. Öğr. Üyesi Zeynep Kara", UserRole.Danisman, "ADV3", null),
        ("ogrenci1", "Can Şahin", UserRole.Ogrenci, null, "20261001"),
        ("ogrenci2", "Buse Ateş", UserRole.Ogrenci, null, "20241011"),
        ("ogrenci3", "Selin Kılıç", UserRole.Ogrenci, null, "20251006"),
        ("ogrenci4", "Caner Polat", UserRole.Ogrenci, null, "20231016"),
    };

    public static async Task SeedAsync(AdvisorDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        foreach (var a in Accounts)
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
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
