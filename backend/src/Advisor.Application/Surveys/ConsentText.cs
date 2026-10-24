namespace Advisor.Application.Surveys;

/// <summary>Öğrenciye anketi doldurmadan önce gösterilen KVKK aydınlatma metni ve açık rıza ifadesi.
/// (Kurgusal bir üniversite için hazırlanmış örnek metindir; gerçek kullanımda hukuk birimi onaylamalıdır.)</summary>
public static class ConsentText
{
    public const string Text =
        "KİŞİSEL VERİLERİN KORUNMASI AYDINLATMA METNİ ve AÇIK RIZA\n\n" +
        "Veri sorumlusu: Üniversite Bilgisayar Mühendisliği Bölümü Danışmanlık Birimi.\n\n" +
        "İşlenen veriler: Bu ankette vereceğiniz sosyoekonomik durumunuza ilişkin yanıtlar (hane geliri aralığı, " +
        "kardeş sayısı, ikamet ve çalışma durumu gibi) ve öğrenci numaranız.\n\n" +
        "İşleme amacı: Yemek kartı, burs gibi desteklerden yararlanacak öğrencilerin belirlenmesinde " +
        "danışmanınıza yardımcı olmak üzere ihtiyaç puanı hesaplanması.\n\n" +
        "Aktarım: Yanıtlarınızı yalnızca anketi oluşturan danışmanınız ve sistem yöneticisi görebilir; üçüncü " +
        "kişilerle paylaşılmaz. Anonim istatistik özetleri kimlik içermez.\n\n" +
        "Saklama: Yanıtlar, destek sürecinin tamamlanmasından sonra mevzuattaki süre boyunca saklanır ve ardından silinir.\n\n" +
        "Haklarınız: 6698 sayılı Kanun'un 11. maddesi uyarınca verilerinizin işlenip işlenmediğini öğrenme, " +
        "düzeltilmesini ya da silinmesini isteme haklarına sahipsiniz. Başvurularınızı danışmanlık birimine iletebilirsiniz.\n\n" +
        "Anket yanıtlamak zorunlu değildir; rıza vermemeniz hâlinde yanıt kaydedilmez ve bu durum akademik " +
        "işlemlerinizi etkilemez.";

    public const string Statement =
        "Aydınlatma metnini okudum, anladım ve sosyoekonomik verilerimin belirtilen amaçla işlenmesine açık rıza veriyorum.";
}
