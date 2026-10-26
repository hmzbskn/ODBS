using Xunit;

namespace Advisor.Tests;

/// <summary>İş kuralı: her danışman yalnızca bir sınıfa bakar, her sınıfın tek danışmanı vardır (seed verisi).</summary>
public class AdvisorPerClassYearTests
{
    [Fact]
    public void EveryClassYear_HasExactlyOneAdvisor()
    {
        var students = new SeedObisClient().GetSeedStudents();

        foreach (var year in students.Select(s => s.ClassYear).Distinct())
            Assert.Single(students.Where(s => s.ClassYear == year).Select(s => s.AdvisorId).Distinct());
    }

    [Fact]
    public void EveryAdvisor_OnlyAdvisesOneClassYear()
    {
        var students = new SeedObisClient().GetSeedStudents();

        foreach (var advisor in students.Select(s => s.AdvisorId).Distinct())
            Assert.Single(students.Where(s => s.AdvisorId == advisor).Select(s => s.ClassYear).Distinct());
    }

    [Fact]
    public async Task FourAdvisors_ForFourClassYears()
    {
        var seed = new SeedObisClient();

        Assert.Equal(4, (await seed.GetAdvisorsAsync()).Count);
        Assert.Equal(4, seed.GetSeedStudents().Select(s => s.AdvisorId).Distinct().Count());
    }
}
