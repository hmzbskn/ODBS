using System.Globalization;
using Obis.MockApi.Models;

namespace Obis.MockApi.SeedData;

/// <summary>Selçuk Üniversitesi Teknoloji Fakültesi Bilgisayar Mühendisliği (Lisans) 8 dönemlik GERÇEK ders listesi:
/// kod, ad, AKTS, zorunlu/seçmeli (Bologna bilgi paketi). Bologna sayfası yalnızca bunları verir; aşağıdakiler
/// kural motorunun çalışması için SEED'E ÖZGÜ varsayımlardır ve gerçek müfredatla birebir değildir:
/// <list type="bullet">
/// <item>Önkoşullar (sayfada yok): yalnızca zorunlu ders zincirleri tanımlıdır, seçmeliler önkoşulsuzdur.</item>
/// <item>Kredi (sayfada yok): AKTS'den türetilir (6+ → 4, 4-5 → 3, ≤3 → 2; mesleki eğitim 6).</item>
/// </list>
/// Önkoşul zincirleri: Matematik-1 → Matematik-2 → Mühendislik Matematiği; Programlama Dili-1 → NYPD → PD-2 → PD-3;
/// Programlama Dili-1 → Veri Yapıları → {Veritabanı, Yapay Zeka}; Mantık Devreleri-1 → -2 → Mikroişlemciler.</summary>
public static class CourseCatalog
{
    // Kolay referans: kenar durum senaryoları ve testler bu kodları kullanır.
    public const string Math1 = "3301151";
    public const string Physics2 = "3301252";
    public const string Programming1 = "3301256";
    public const string ObjectOriented = "3301351";
    public const string LogicCircuits1 = "3301353";
    public const string DataStructures = "3301354";
    public const string Programming2 = "3301452";
    public const string Microprocessors = "3301551";
    public const string Programming3 = "3301552";
    public const string DataCommunication = "3301553";
    public const string ComputerArchitecture = "3301752";
    public const string Entrepreneurship = "3301754";

    // dönem | kod | ad (Bologna'daki büyük harfli yazım) | AKTS | Z=zorunlu S=seçmeli | önkoşul kodları
    private const string Raw = """
1|3301151|MATEMATİK-1|6|Z|
1|3301152|FİZİK-1|6|Z|
1|3301153|TÜRK DİLİ I|2|Z|
1|3301154|ATATÜRK İLKELERİ VE İNKILAP TARİHİ-1|2|Z|
1|3301155|YABANCI DİL-1|2|Z|
1|3301156|BİLGİSAYAR MÜHENDİSLİĞİNE GİRİŞ|4|Z|
1|3301157|İŞ SAĞLIĞI VE GÜVENLİĞİ-1|2|Z|
1|3301158|LİNEER CEBİR|6|Z|
2|3301251|MATEMATİK-2|6|Z|3301151
2|3301252|FİZİK-2|6|Z|3301152
2|3301253|TÜRK DİLİ II|2|Z|3301153
2|3301254|ATATÜRK İLKELERİ VE İNKILAP TARİHİ-2|2|Z|3301154
2|3301255|YABANCI DİL-2|2|Z|3301155
2|3301256|PROGRAMLAMA DİLİ-1|4|Z|
2|3301257|İŞ SAĞLIĞI VE GÜVENLİĞİ-2|2|Z|3301157
2|3301258|AYRIK MATEMATİK|6|Z|
3|3301351|NESNEYE YÖNELİK PROGRAMLAMA DİLİ|4|Z|3301256
3|3301352|İŞLETİM SİSTEMLERİ|4|Z|3301256
3|3301353|MANTIK DEVRELERİ-1|4|Z|
3|3301354|VERİ YAPILARI VE UYGULAMALARI|4|Z|3301256
3|3301355|ELEKTRONİK DEVRE ELEMANLARI VE ÖLÇME|4|Z|3301152
3|3301356|SAYISAL YÖNTEMLER|5|Z|3301158
3|3301357|İSTATİSTİK VE OLASILIK|5|Z|3301151
4|3301451|MÜHENDİSLİK MATEMATİĞİ|7|Z|3301251
4|3301452|PROGRAMLAMA DİLİ-2|4|Z|3301351
4|3301453|MANTIK DEVRELERİ-2|4|Z|3301353
4|3301454|VERİTABANI YÖNETİM SİSTEMLERİ|5|Z|3301354
4|3301455|YAZILIM MÜHENDİSLİĞİ|4|Z|3301351
4|3301456|MOBİL PROGRAMLAMA|4|Z|3301351
4|3301461|TEKNOLOJİ TARİHİ|2|S|
4|3301462|İŞ HUKUKU VE ETİK|2|S|
4|3301463|İLETİŞİM BECERİLERİ VE AKADEMİK RAPORLAMA|2|S|
4|3301464|SOSYAL SORUMLULUK VE DEĞERLER EĞİTİMİ|2|S|
4|3301465|ETKİLİ İLETİŞİM VE BEDEN DİLİ|2|S|
4|3301466|MEDYA OKUR YAZARLIĞI|2|S|
4|3301467|YÖNETİM VE ORGANİZASYON|2|S|
4|3301468|ÇEVRE VE EKOLOJİ|2|S|
4|3301469|YAŞAM BOYU ÖĞRENME|2|S|
4|3301470|İLETİŞİM VE STRES YÖNETİMİ|2|S|
4|3301471|GÖNÜLLÜLÜK ÇALIŞMALARI|2|S|
4|3301472|DİJİTAL DÖNÜŞÜM|2|S|
4|3301473|SOSYAL İNGİLİZCE|2|S|
5|3301551|MİKROİŞLEMCİLER VE MİKRODENETLEYİCİLER|5|Z|3301453
5|3301552|PROGRAMLAMA DİLİ-3|5|Z|3301452
5|3301553|VERİ HABERLEŞMESİ VE AĞLAR|5|Z|
5|3301554|TEKNİK İNGİLİZCE|3|Z|3301255
5|3301561|SİNYAL İŞLEME|4|S|
5|3301562|TASARIM DESENLERİ|4|S|
5|3301563|İNSAN BİLGİSAYAR ETKİLEŞİMİ|4|S|
5|3301564|BİYOMEDİKAL SİSTEMLER|4|S|
5|3301565|SİMÜLASYON VE MODELLEME|4|S|
5|3301566|BİLGİSAYAR DONANIMI|4|S|
5|3301567|WEB TASARIMI|4|S|
5|3301568|VERİ MADENCİLİĞİ|4|S|
5|3301569|ADLİ BİLİŞİM|4|S|
5|3301570|VERİ TABANI PROGRAMLAMA|4|S|
5|3301571|MAKİNE ÖĞRENMESİ|4|S|
5|3301572|ALGORİTMA ANALİZİ|4|S|
5|3301573|DOĞAL DİL İŞLEME TEMELLERİ|4|S|
5|3301574|NESNELERİN İNTERNETİ|4|S|
5|3301575|İLERİ DİJİTAL SİSTEMLER|4|S|
5|3301576|BİLGİSAYAR MÜHENDİSLİĞİNDE ÖZEL KONULAR|4|S|
5|3301577|SİBER SİSTEMLER VE BİLGİ GÜVENLİĞİ|4|S|
5|3301578|SÜRDÜRÜLEBİLİR YAZILIM MÜHENDİSLİĞİ|4|S|
6|3301651|YAPAY ZEKA|4|Z|3301354
6|3301652|OTOMATA TEORİSİ VE BİÇİMSEL DİLLER|4|Z|3301258
6|3301653|GÖRÜNTÜ İŞLEME|4|Z|3301356
6|3301654|MÜHENDİSLİK TASARIMI|6|Z|3301455
6|3301661|SİSTEM PROGRAMLAMA|4|S|
6|3301662|BİLGİSAYAR VE AĞ GÜVENLİĞİ|4|S|
6|3301663|GÖMÜLÜ SİSTEMLER|4|S|
6|3301664|İŞ ZEKASI|4|S|
6|3301665|ROBOTİK|4|S|
6|3301666|İŞLETİM SİSTEMLERİ YÖNETİMİ|4|S|
6|3301667|WEB SERVİSLERİ|4|S|
6|3301668|MİKROBİLGİSAYARLI SİSTEM TASARIMI|4|S|
6|3301669|MESLEKİ İNGİLİZCE|4|S|
6|3301670|KRİPTOLOJİ|4|S|
6|3301671|BİLGİSAYAR BİLİMLERİNDE İNOVASYON VE ARAŞTIRMA TEKNİKLERİ|4|S|
6|3301672|DİSİPLİNLERARASI ÇALIŞMA|4|S|
6|3301673|GERÇEKÇİ MODELLEME VE SİMÜLASYON TASARIMI|4|S|
6|3301674|WEB PROGRAMLAMA|4|S|
6|3301675|SÜRDÜRÜLEBİLİR BİLİŞİM TEKNOLOJİLERİ|4|S|
6|3301676|STEGANOGRAFİ|4|S|
7|3301751|BİLGİSAYAR MÜHENDİSLİĞİ UYGULAMALARI|4|Z|
7|3301752|BİLGİSAYAR MİMARİSİ VE ORGANİZASYONU|4|Z|3301453
7|3301753|VERİ BİLİMİ|4|Z|3301357
7|3301754|GİRİŞİMCİLİK VE PROJE YÖNETİMİ|3|Z|
7|3301761|PROG. MANTIK KONTROLLÖR|4|S|
7|3301762|PARALEL İŞLEMCİLİ SİSTEMLER|4|S|
7|3301763|KONTROL SİSTEMLERİ|4|S|
7|3301764|BİLGİSAYAR DESTEKLİ TASARIM|4|S|
7|3301765|YÖNEYLEM ARAŞTIRMASI|4|S|
7|3301766|KABLOSUZ AĞLAR|4|S|
7|3301767|BULUT BİLİŞİM|4|S|
7|3301768|PAKET PROGRAMLAR|4|S|
7|3301769|BİLGİSAYAR GRAFİKLERİ|4|S|
7|3301770|VERİ İLETİŞİM TEKNİKLERİ|4|S|
7|3301771|OYUN PROGRAMLAMA|4|S|
7|3301772|E-TİCARET VE UYGULAMALARI|4|S|
7|3301773|PARALEL PROGRAMLAMA|4|S|
7|3301774|MVC TABANLI WEB MİMARİSİ|4|S|
7|3301775|YEŞİL BİLİŞİM|4|S|
7|3301776|ÇEVRE VE ENERJİ|6|S|
7|3301777|BLOKZİNCİR TEMELLERİ|4|S|
7|3301778|BÜYÜK VERİ ANALİTİĞİ|4|S|
7|3301779|BİLGİSAYARLA GÖRME|4|S|
7|3301782|KALİTE VE GÜVENCE SİSTEMLERİ|2|S|
7|3301783|MÜHENDİSLİK EKONOMİSİ|2|S|
7|3301784|İŞARET DİLİ|2|S|
7|3301785|DIŞ TİCARET|2|S|
7|3301786|TOPLUMA HİZMET UYGULAMALARI|2|S|
7|3301787|REKLAMCILIK VE STRATEJİLERİ|2|S|
7|3301788|ARAŞTIRMA VE RAPOR YAZMA TEKNİKLERİ|2|S|
7|3301789|MÜHENDİSLİK ETİĞİ|2|S|
8|3301851|İŞLETMEDE MESLEKİ EĞİTİM|20|Z|
8|3301852|STAJ-1|5|Z|
8|3301853|STAJ-2|5|Z|
""";

    private static readonly CultureInfo Tr = new("tr-TR");

    public static List<CourseInfo> Build() => Raw
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line =>
        {
            var f = line.Split('|');
            var ects = int.Parse(f[3]);
            var prereq = f[5].Length == 0 ? new List<string>() : f[5].Split(',').ToList();
            return new CourseInfo(f[1], TitleCase(f[2]), CreditFor(ects), ects, int.Parse(f[0]), f[4] == "Z", prereq);
        })
        .ToList();

    /// <summary>Bologna'da kredi verilmez; AKTS'den türetilir (bkz. sınıf açıklaması).</summary>
    private static int CreditFor(int ects) => ects switch
    {
        >= 20 => 6,
        >= 6 => 4,
        >= 4 => 3,
        _ => 2,
    };

    private static readonly HashSet<string> LowerWords = new() { "ve", "ile" };
    private static readonly HashSet<string> KeepUpper = new() { "I", "II", "III", "MVC" };

    /// <summary>"BİLGİSAYAR MÜHENDİSLİĞİNE GİRİŞ" → "Bilgisayar Mühendisliğine Giriş" (Türkçe i/İ, ı/I kurallarıyla).</summary>
    private static string TitleCase(string upper) => string.Join(' ', upper.Split(' ').Select(word =>
    {
        if (KeepUpper.Contains(word)) return word;
        var lower = word.ToLower(Tr);
        if (LowerWords.Contains(lower)) return lower;
        return lower.Length == 0 ? lower : char.ToUpper(lower[0], Tr) + lower[1..];
    }));
}
