using Obis.MockApi.Models;

namespace Obis.MockApi.SeedData;

/// <summary>Selçuk Üniversitesi Teknoloji Fakültesi Bilgisayar Mühendisliği 2026-2027 güz yarıyılı GERÇEK haftalık ders
/// programı (bölümün 31.08.2026 tarihli PDF'i). Ders saatleri 45 dakikalık bloklardır; ardışık saatler tek blok olarak
/// yazılmıştır (ör. 08:30-12:00 = 1-4. saatler). NÖ saatleri 1-9. saat (08:30-16:55), İÖ saatleri 10-15. saat (17:00-21:55).
/// İkinci öğretim yalnızca 4. sınıfta var: 4. sınıf dersleri ayrı "NÖ" ve "İÖ" şubeleriyle farklı saatlerdedir. 1-3. sınıf dersleri
/// "(NÖ/İÖ)" olarak yalnızca gündüz açılır; bu dersi alttan alan 4. sınıf İÖ öğrencisi de gündüz saatine girer.
/// <list type="bullet">
/// <item>Programda sabit saati olmayan dersler (Bilgisayar Mühendisliği Uygulamaları, Girişimcilik, İşletmede Mesleki
/// Eğitim: "tüm öğretim elemanları") tabloda yoktur; bu derslerin saat çakışması olmaz.</item>
/// <item>Bahar dönemi dersleri (2-4-6-8) bu programda yoktur; onlar için seed kurgusal saat üretir.</item>
/// </list></summary>
public static class FallSchedule
{
    // ders kodu | şube | gün | başlangıç | bitiş | derslik
    private const string Raw = """
3301151|NÖ/İÖ|Mon|08:30|12:00|212
3301154|NÖ/İÖ|Mon|13:25|15:05|212
3301152|NÖ/İÖ|Tue|13:25|16:55|212
3301156|NÖ/İÖ|Wed|09:25|12:00|212
3301157|NÖ/İÖ|Wed|13:25|15:05|212
3301158|NÖ/İÖ|Thu|09:25|12:00|212
3301153|NÖ/İÖ|Fri|08:30|10:10|212
3301155|NÖ/İÖ|Fri|10:20|12:00|212
3301353|NÖ/İÖ|Mon|13:25|16:55|209
3301356|NÖ/İÖ|Tue|09:25|12:00|209
3301352|NÖ/İÖ|Tue|13:25|16:55|209
3301357|NÖ/İÖ|Wed|09:25|12:00|209
3301354|NÖ/İÖ|Wed|13:25|16:55|209
3301351|NÖ/İÖ|Thu|13:25|16:55|211
3301355|NÖ/İÖ|Fri|08:30|12:00|209
3301552|NÖ/İÖ|Mon|13:25|16:55|210
3301551|NÖ/İÖ|Tue|08:30|12:00|210
3301553|NÖ/İÖ|Wed|13:25|16:55|210
3301568|NÖ/İÖ|Thu|09:25|12:00|210
3301573|NÖ/İÖ|Thu|09:25|12:00|LAB3
3301564|NÖ/İÖ|Thu|13:25|16:00|210
3301562|NÖ/İÖ|Thu|13:25|16:00|LAB3
3301570|NÖ/İÖ|Fri|09:25|12:00|210
3301571|NÖ/İÖ|Fri|09:25|12:00|211
3301554|NÖ/İÖ|Fri|14:20|16:55|210
3301788|NÖ|Mon|10:20|12:00|210
3301786|NÖ|Mon|10:20|12:00|209
3301770|NÖ|Mon|13:25|16:00|211
3301761|NÖ|Mon|13:25|16:00|LAB2
3301788|İÖ|Mon|17:00|18:35|210
3301786|İÖ|Mon|17:00|18:35|209
3301770|İÖ|Mon|18:40|21:05|211
3301761|İÖ|Mon|18:40|21:05|LAB2
3301767|NÖ|Tue|13:25|16:00|210
3301766|NÖ|Tue|13:25|16:00|211
3301767|İÖ|Tue|17:00|19:25|210
3301766|İÖ|Tue|17:00|19:25|211
3301753|NÖ|Wed|13:25|16:00|211
3301753|İÖ|Wed|17:00|19:25|211
3301752|NÖ|Thu|08:30|12:00|211
3301752|İÖ|Thu|17:00|20:15|211
3301778|NÖ|Fri|14:20|16:55|211
3301773|NÖ|Fri|14:20|16:55|LAB3
3301778|İÖ|Fri|17:00|19:25|211
3301773|İÖ|Fri|17:00|19:25|LAB3
""";

    private static readonly Dictionary<string, DayOfWeek> DayCodes = new()
    {
        ["Mon"] = DayOfWeek.Monday, ["Tue"] = DayOfWeek.Tuesday, ["Wed"] = DayOfWeek.Wednesday,
        ["Thu"] = DayOfWeek.Thursday, ["Fri"] = DayOfWeek.Friday,
    };

    /// <summary>Katalogdan kurulmuş program (seçmeli seçimi gibi statik yardımcılar için).</summary>
    public static readonly Lazy<List<ScheduleSlot>> Slots =
        new(() => Build(CourseCatalog.Build().ToDictionary(c => c.CourseCode)));

    public static List<ScheduleSlot> Build(IReadOnlyDictionary<string, CourseInfo> courses) => Raw
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line =>
        {
            var f = line.Split('|');
            var c = courses[f[0]];
            return new ScheduleSlot(c.Term, c.CourseCode, c.CourseName, f[1], DayCodes[f[2]],
                TimeOnly.Parse(f[3]), TimeOnly.Parse(f[4]), f[5]);
        })
        .ToList();
}
