namespace Obis.MockApi.SeedData;

/// <summary>Kurgusal öğrenci/danışman adları için isim havuzları. Gerçek kişilerle ilgisi yoktur.</summary>
public static class NamePools
{
    public static readonly string[] FirstNames =
    {
        "Ahmet", "Mehmet", "Ayşe", "Fatma", "Mustafa", "Elif", "Emre", "Zeynep", "Can", "Burak",
        "Ece", "Deniz", "Selin", "Onur", "Gizem", "Barış", "Merve", "Kerem", "Buse", "Yusuf",
        "Pınar", "Oğuz", "Nazlı", "Serkan", "Aslı", "Tolga", "Ceren", "Umut", "İrem", "Volkan",
        "Sude", "Caner", "Melis", "Hakan", "Esra", "Berk", "Sena", "Kaan", "Duygu", "Çağan",
    };

    public static readonly string[] LastNames =
    {
        "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Arslan", "Doğan", "Kılıç", "Aydın",
        "Özdemir", "Korkmaz", "Çetin", "Kurt", "Koç", "Aksoy", "Erdoğan", "Güneş", "Polat", "Avcı",
        "Yıldırım", "Karaca", "Bulut", "Şen", "Tekin", "Ateş", "Uysal", "Acar", "Keskin", "Toprak",
    };

    public static readonly (string FirstName, string LastName, string Title)[] Advisors =
    {
        ("Elif", "Yıldız", "Dr. Öğr. Üyesi"),
        ("Mehmet", "Arslan", "Doç. Dr."),
        ("Zeynep", "Kara", "Dr. Öğr. Üyesi"),
        ("Ali", "Çelik", "Prof. Dr."),
    };

    /// <summary>Ders veren öğretim elemanları (kurgusal). İlk dördü danışmanlarla aynı kişilerdir.</summary>
    public static readonly string[] Instructors =
    {
        "Dr. Öğr. Üyesi Elif Yıldız", "Doç. Dr. Mehmet Arslan", "Dr. Öğr. Üyesi Zeynep Kara", "Prof. Dr. Ali Çelik",
        "Prof. Dr. Hasan Aydın", "Doç. Dr. Selin Korkmaz", "Dr. Öğr. Üyesi Murat Demir", "Dr. Öğr. Üyesi Ayşe Polat",
        "Doç. Dr. Emre Kaya", "Dr. Öğr. Üyesi Burak Şahin", "Arş. Gör. Dr. Merve Öztürk", "Öğr. Gör. Dr. Okan Yalçın",
    };
}
