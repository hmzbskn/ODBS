using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Advisor.Api.Controllers;
using Advisor.Application.Auth;
using Advisor.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Advisor.Tests;

/// <summary>Öğrenciler sekmesi: kapsamlı liste, okul/iletişim/akademik özet ve ders kaydı listesindeki çakışma sayısı.</summary>
public class StudentsApiTests : IClassFixture<SeedApiFactory>
{
    private readonly SeedApiFactory _factory;

    public StudentsApiTests(SeedApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    [Fact]
    public async Task List_IsForbiddenForStudents()
    {
        var ogr = await LoginAs("ogrenci1");
        Assert.Equal(HttpStatusCode.Forbidden, (await ogr.GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task List_AdvisorSeesOnlyOwnStudents_AdminSeesAll()
    {
        var all = await new SeedObisClient().GetStudentsAsync();

        var adv = await LoginAs("danisman1");
        var own = (await adv.GetFromJsonAsync<List<StudentListRow>>("/api/students", TestJson.Options))!;
        Assert.Equal(all.Count(s => s.AdvisorId == "ADV1"), own.Count);

        var admin = await LoginAs("admin");
        var everyone = (await admin.GetFromJsonAsync<List<StudentListRow>>("/api/students", TestJson.Options))!;
        Assert.Equal(all.Count, everyone.Count);
        Assert.All(everyone, s => Assert.False(string.IsNullOrWhiteSpace(s.Program)));
    }

    [Fact]
    public async Task Detail_ReturnsSchoolContactAndAcademicInfo()
    {
        var adv = await LoginAs("danisman1");

        var d = (await adv.GetFromJsonAsync<StudentProfileDetail>("/api/students/20241011", TestJson.Options))!;

        Assert.Equal("20241011", d.School.StudentNo);
        Assert.Contains(d.School.Program, new[] { "Birinci Öğretim", "İkinci Öğretim" });
        Assert.EndsWith("@ogr.selcuk.edu.tr", d.Contact.Email);
        Assert.False(string.IsNullOrWhiteSpace(d.Contact.Phone));
        Assert.False(string.IsNullOrWhiteSpace(d.Contact.Residence));
        Assert.Equal(Math.Max(d.Academic.RequiredEcts - d.Academic.EarnedEcts, 0), d.Academic.RemainingEcts);
        Assert.True(d.Academic.CurrentTermCourseCount > 0);
        Assert.NotNull(d.Advisor);
    }

    [Fact]
    public async Task Detail_OfAnotherAdvisorsStudent_IsForbidden()
    {
        var all = await new SeedObisClient().GetStudentsAsync();
        var foreign = all.First(s => s.AdvisorId == "ADV2").StudentNo;

        var adv = await LoginAs("danisman1");

        Assert.Equal(HttpStatusCode.Forbidden, (await adv.GetAsync($"/api/students/{foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await LoginAs("admin")).GetAsync($"/api/students/{foreign}")).StatusCode);
    }

    [Fact]
    public async Task RegistrationList_CountsTimeConflicts()
    {
        var adv = await LoginAs("danisman1");

        var list = (await adv.GetFromJsonAsync<AdvisorStudentList>("/api/registrations/students", TestJson.Options))!;

        // EDGE-01: aynı gün/saat çakışan iki ders; çakışmasız bir öğrenci 0 döner.
        Assert.True(list.Students.Single(s => s.StudentNo == "20241011").ConflictCount >= 1);
        Assert.Contains(list.Students, s => s.ConflictCount == 0);
    }
}
