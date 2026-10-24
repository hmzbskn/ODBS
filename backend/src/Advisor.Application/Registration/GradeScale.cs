namespace Advisor.Application.Registration;

/// <summary>Harf notu → 4'lük katsayı. OBİS seed'indeki GANO değerleriyle birebir uyumludur
/// (kredi ağırlıklı, tekrar edilen derste son deneme geçerli).</summary>
public static class GradeScale
{
    private static readonly Dictionary<string, double> Points = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AA"] = 4.0, ["BA"] = 3.5, ["BB"] = 3.0, ["CB"] = 2.5, ["CC"] = 2.0,
        ["DC"] = 1.5, ["DD"] = 1.0, ["FD"] = 0.5, ["FF"] = 0.0,
    };

    public static double PointsOf(string letter) => Points.GetValueOrDefault(letter, 0.0);

    public static bool IsAtLeast(string letter, string minimum) => PointsOf(letter) >= PointsOf(minimum);
}
