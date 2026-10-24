namespace Advisor.Application.Registration;

/// <summary>Ders kaydı doğrulama kuralı. Yeni kural eklemek = bu arayüzü uygulayan yeni bir
/// sınıf yazmak; <see cref="RegistrationServiceCollectionExtensions.AddRegistrationRules"/>
/// derlemedeki tüm kuralları otomatik kaydeder.</summary>
public interface IRegistrationRule
{
    /// <summary>Kararlı kural kimliği (arayüz ve testlerde kullanılır), ör. "TIME_CONFLICT".</summary>
    string Code { get; }

    IEnumerable<RegistrationIssue> Evaluate(RegistrationContext context);
}
