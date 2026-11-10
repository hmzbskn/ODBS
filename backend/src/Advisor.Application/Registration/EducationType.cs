namespace Advisor.Application.Registration;

/// <summary>Öğretim türü (normal / ikinci öğretim) ile şube uyumu. Bölüm programında şube = öğretim türü: "NÖ" (gündüz), "İÖ"
/// (akşam, ayrı saatler) ya da her iki öğretimin öğrencisine açık tek gündüz şubesi "NÖ/İÖ" (İÖ'nün olmadığı 1-3. sınıf dersleri).
/// Öğretim türü bilinmiyorsa ya da şube bu adlandırmayı kullanmıyorsa kısıt yoktur.</summary>
public static class EducationType
{
    public const string Normal = "NÖ";
    public const string Evening = "İÖ";
    public const string Both = "NÖ/İÖ";

    public static bool IsEvening(string? program) =>
        program is not null && program.Contains("İkinci", StringComparison.OrdinalIgnoreCase);

    public static string? OwnSection(string? program) =>
        program is null ? null : IsEvening(program) ? Evening : Normal;

    public static string Describe(string? program) => IsEvening(program) ? "ikinci öğretim" : "normal öğretim";

    public static bool SectionAllowed(string? program, string section)
    {
        if (program is null || section == Both) return true;
        if (section != Normal && section != Evening) return true;
        return section == OwnSection(program);
    }
}
