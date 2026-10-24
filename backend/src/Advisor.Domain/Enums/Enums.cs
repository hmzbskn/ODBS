namespace Advisor.Domain.Enums;

public enum UserRole
{
    Admin,
    Danisman,
    Ogrenci,
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
