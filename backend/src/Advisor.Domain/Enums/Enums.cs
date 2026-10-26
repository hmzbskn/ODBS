namespace Advisor.Domain.Enums;

public enum UserRole
{
    Admin,
    Danisman,
    Ogrenci,
    // Sonradan eklenenler listenin sonunda: rol veritabanında sayı olarak saklanır.
    BolumBaskani,
    Dekan,
}

public static class UserRoleExtensions
{
    /// <summary>Danışmanların üstündeki yönetim katmanı: öğrenci atar, tüm danışmanların analizlerini görür.</summary>
    public static bool IsManagement(this UserRole role) =>
        role is UserRole.Admin or UserRole.BolumBaskani or UserRole.Dekan;
}

public enum RegistrationDecision
{
    Pending,
    Approved,
    RevisionRequested,
}

public enum QuestionType
{
    SingleChoice,
    MultiChoice,
    Number,
    Text,
}

public enum AppointmentStatus
{
    Scheduled,
    CancelledByStudent,
    CancelledByAdvisor,
    Completed,
}

public enum PetitionType
{
    DersEkleBirak,
    KayitDondurma,
    Muafiyet,
    Diger,
}

public enum PetitionStatus
{
    Beklemede,
    Inceleniyor,
    Onaylandi,
    Reddedildi,
}

public enum RiskLevel
{
    Dusuk,
    Orta,
    Yuksek,
}
