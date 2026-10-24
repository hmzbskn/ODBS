using Obis.MockApi.Models;

namespace Obis.MockApi.SeedData;

/// <summary>Bilgisayar Mühendisliği bölümü için kurgusal ama gerçekçi 8 dönemlik müfredat.
/// Önkoşul zinciri bilinçli olarak kuruldu: BLM101 → BLM103 → BLM201 → BLM204 → BLM304 → BLM401,
/// BLM202 → BLM303 → BLM403 → BLM404, BLM203 → BLM205 → {BLM301, BLM302, BLM305, BLM306}.</summary>
public static class CourseCatalog
{
    public static List<CourseInfo> Build() => new()
    {
        // Dönem 1
        new("MAT101", "Matematik I", 4, 6, 1, true, new()),
        new("FIZ101", "Fizik I", 4, 5, 1, true, new()),
        new("BLM101", "Programlamaya Giriş", 4, 6, 1, true, new()),
        new("TUR101", "Türk Dili I", 2, 2, 1, true, new()),
        new("ING101", "İngilizce I", 2, 2, 1, true, new()),
        new("BLM102", "Bilgisayar Mühendisliğine Giriş", 2, 3, 1, true, new()),

        // Dönem 2
        new("MAT102", "Matematik II", 4, 6, 2, true, new() { "MAT101" }),
        new("FIZ102", "Fizik II", 4, 5, 2, true, new() { "FIZ101" }),
        new("BLM103", "Programlama I", 4, 6, 2, true, new() { "BLM101" }),
        new("TUR102", "Türk Dili II", 2, 2, 2, true, new() { "TUR101" }),
        new("ING102", "İngilizce II", 2, 2, 2, true, new() { "ING101" }),
        new("KIM101", "Kimya", 3, 4, 2, true, new()),

        // Dönem 3
        new("BLM201", "Veri Yapıları", 4, 6, 3, true, new() { "BLM103" }),
        new("BLM202", "Nesne Yönelimli Programlama", 4, 6, 3, true, new() { "BLM103" }),
        new("MAT201", "Diferansiyel Denklemler", 4, 6, 3, true, new() { "MAT102" }),
        new("BLM203", "Sayısal Mantık Devreleri", 3, 5, 3, true, new() { "FIZ102" }),
        new("AIT101", "Atatürk İlkeleri ve İnkılap Tarihi I", 2, 2, 3, true, new()),
        new("ISG101", "İş Sağlığı ve Güvenliği", 1, 1, 3, true, new()),

        // Dönem 4
        new("BLM204", "Algoritma Analizi", 4, 6, 4, true, new() { "BLM201" }),
        new("BLM205", "Bilgisayar Organizasyonu", 4, 6, 4, true, new() { "BLM203" }),
        new("MAT202", "Olasılık ve İstatistik", 3, 5, 4, true, new() { "MAT102" }),
        new("BLM206", "Veritabanı Yönetim Sistemleri", 4, 6, 4, true, new() { "BLM201" }),
        new("AIT102", "Atatürk İlkeleri ve İnkılap Tarihi II", 2, 2, 4, true, new() { "AIT101" }),
        new("STJ101", "Staj I Hazırlık", 1, 2, 4, true, new()),

        // Dönem 5 (seçmeli havuzu başlar)
        new("BLM301", "İşletim Sistemleri", 4, 6, 5, true, new() { "BLM205" }),
        new("BLM302", "Bilgisayar Ağları", 4, 6, 5, true, new() { "BLM205" }),
        new("BLM303", "Yazılım Mühendisliği", 4, 6, 5, true, new() { "BLM202" }),
        new("BLM304", "Otomata Teorisi", 3, 5, 5, true, new() { "BLM204" }),
        new("ENG201", "Teknik İngilizce", 2, 3, 5, true, new() { "ING102" }),
        new("BLM351", "Mobil Uygulama Geliştirme", 3, 5, 5, false, new() { "BLM202" }),
        new("BLM352", "Web Programlama", 3, 5, 5, false, new() { "BLM202" }),

        // Dönem 6
        new("BLM305", "Mikroişlemciler", 4, 6, 6, true, new() { "BLM205" }),
        new("BLM306", "Yapay Zekaya Giriş", 3, 5, 6, true, new() { "BLM204" }),
        new("BLM307", "İnsan-Bilgisayar Etkileşimi", 3, 4, 6, true, new()),
        new("BLM308", "Çizge Teorisi", 3, 5, 6, true, new() { "BLM304" }),
        new("ISL201", "İşletme Bilimine Giriş", 2, 3, 6, true, new()),
        new("BLM353", "Bulut Bilişime Giriş", 3, 5, 6, false, new() { "BLM302" }),
        new("BLM354", "Oyun Programlama", 3, 5, 6, false, new() { "BLM202" }),

        // Dönem 7
        new("BLM401", "Derleyici Tasarımı", 4, 6, 7, true, new() { "BLM304" }),
        new("BLM402", "Bilgisayar Grafikleri", 3, 5, 7, true, new() { "BLM202" }),
        new("BLM403", "Bitirme Projesi I", 2, 6, 7, true, new() { "BLM303" }),
        new("EKO101", "Ekonomi", 2, 3, 7, true, new()),
        new("BLM451", "Makine Öğrenmesine Giriş", 3, 5, 7, false, new() { "BLM306" }),
        new("BLM452", "Siber Güvenliğe Giriş", 3, 5, 7, false, new() { "BLM302" }),
        new("BLM453", "Dağıtık Sistemler", 3, 5, 7, false, new() { "BLM301" }),

        // Dönem 8
        new("BLM404", "Bitirme Projesi II", 4, 8, 8, true, new() { "BLM403" }),
        new("BLM405", "Yazılım Kalite Güvencesi", 3, 5, 8, true, new() { "BLM303" }),
        new("BLM406", "Paralel Hesaplama", 3, 5, 8, true, new() { "BLM305" }),
        new("GIR101", "Girişimcilik", 2, 3, 8, true, new()),
        new("BLM454", "Doğal Dil İşleme", 3, 5, 8, false, new() { "BLM306" }),
        new("BLM455", "Gömülü Sistemler", 3, 5, 8, false, new() { "BLM305" }),
        new("BLM456", "Blok Zinciri Teknolojileri", 3, 5, 8, false, new()),
    };
}
