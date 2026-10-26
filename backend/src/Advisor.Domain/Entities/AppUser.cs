using Advisor.Domain.Enums;

namespace Advisor.Domain.Entities;

/// <summary>Ana uygulamanın kimlik doğrulama hesabı. OBİS'teki öğrenci/danışman
/// kimliğine <see cref="LinkedStudentNo"/> / <see cref="LinkedAdvisorId"/> ile bağlanır
/// (OBİS'in kendisi kimlik doğrulama yapmaz, sadece veri kaynağıdır).</summary>
public class AppUser
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }

    /// <summary>Role == Danisman ise OBİS AdvisorId ("ADV1" gibi).</summary>
    public string? LinkedAdvisorId { get; set; }

    /// <summary>Role == Ogrenci ise OBİS StudentNo.</summary>
    public string? LinkedStudentNo { get; set; }

    /// <summary>Role == BolumBaskani ise yetki alanı olan bölüm (OBİS'teki Department metni).
    /// Boşsa kullanıcı bölümle sınırlı değildir (yönetici, dekan).</summary>
    public string? Department { get; set; }

    public DateTime CreatedAt { get; set; }
}
