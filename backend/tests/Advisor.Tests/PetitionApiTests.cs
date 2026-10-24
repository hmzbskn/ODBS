using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Communication;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Advisor.Tests;

public class AttachmentValidatorTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nbody");
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };

    private static (string Name, string Type, string Ext) Validate(string name, byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return AttachmentValidator.Validate(name, ms, bytes.Length);
    }

    [Theory]
    [InlineData("rapor.pdf", "application/pdf")]
    [InlineData("foto.png", "image/png")]
    [InlineData("foto.jpg", "image/jpeg")]
    [InlineData("foto.JPEG", "image/jpeg")]
    [InlineData("belge.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void ValidFiles_AreAccepted(string name, string expectedType)
    {
        var bytes = name.EndsWith("pdf") ? Pdf : name.EndsWith("png") ? Png : name.EndsWith("docx") ? Zip : Jpg;

        var (_, type, _) = Validate(name, bytes);

        Assert.Equal(expectedType, type);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("script.js")]
    [InlineData("sayfa.html")]
    [InlineData("arsiv.zip")]
    [InlineData("uzantisiz")]
    [InlineData("")]
    public void DisallowedExtensions_AreRejected(string name)
    {
        var ex = Assert.Throws<AppException>(() => Validate(name, Pdf));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void ContentMismatch_IsRejected_EvenWithAllowedExtension()
    {
        // "PDF" adıyla yüklenmiş bir çalıştırılabilir (MZ imzası).
        var ex = Assert.Throws<AppException>(() => Validate("belge.pdf", new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 }));

        Assert.Contains("uyuşmuyor", ex.Message);
        Assert.Throws<AppException>(() => Validate("foto.png", Jpg));
        Assert.Throws<AppException>(() => Validate("belge.docx", Pdf));
    }

    [Fact]
    public void EmptyAndOversizedFiles_AreRejected()
    {
        Assert.Throws<AppException>(() => Validate("bos.pdf", Array.Empty<byte>()));
        using var ms = new MemoryStream(Pdf);
        Assert.Throws<AppException>(() => AttachmentValidator.Validate("buyuk.pdf", ms, AttachmentValidator.MaxBytes + 1));
        using var ok = new MemoryStream(Pdf);
        AttachmentValidator.Validate("sinir.pdf", ok, AttachmentValidator.MaxBytes); // tam sınır serbest
    }

    [Fact]
    public void FileName_IsSanitized()
    {
        Assert.Equal("passwd.pdf", Validate("../../etc/passwd.pdf", Pdf).Name);
        Assert.Equal("a.pdf", Validate("C:\\Users\\x\\a.pdf", Pdf).Name);
        Assert.Equal("rapor.pdf", Validate("ra\"po<r>.pdf", Pdf).Name); // tırnak ve <> karakterleri atılır
        var long100 = new string('a', 200) + ".pdf";
        var safe = Validate(long100, Pdf).Name;
        Assert.True(safe.Length <= 100);
        Assert.EndsWith(".pdf", safe);
    }

    [Fact]
    public void Validation_RewindsTheStream()
    {
        using var ms = new MemoryStream(Pdf);
        AttachmentValidator.Validate("a.pdf", ms, Pdf.Length);

        Assert.Equal(0, ms.Position);
    }
}

public class PetitionApiTests : IDisposable
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n%test dosyası\n");
    private const string Description = "Sağlık sebebiyle bu dönem kaydımı dondurmak istiyorum.";

    private readonly CommApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> LoginAs(string username)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DemoDataSeeder.DemoPassword));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static MultipartFormDataContent Form(string type, string? description, byte[]? file = null, string fileName = "ek.pdf")
    {
        var form = new MultipartFormDataContent { { new StringContent(type), "type" } };
        if (description != null) form.Add(new StringContent(description), "description");
        if (file != null)
        {
            var content = new ByteArrayContent(file);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, "file", fileName);
        }

        return form;
    }

    private static async Task<PetitionDto> Create(HttpClient student, string type = "KayitDondurma", string? description = Description, byte[]? file = null, string fileName = "ek.pdf")
    {
        var res = await student.PostAsync("/api/petitions", Form(type, description, file, fileName));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<PetitionDto>(TestJson.Options))!;
    }

    private static Task<HttpResponseMessage> SetStatus(HttpClient staff, Guid id, string status, string? note = null) =>
        staff.PostAsJsonAsync($"/api/petitions/{id}/status", new { status, note });

    private async Task<string> ErrorOf(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["message"];

    // ------------------------------------------------------------ açma ve ek

    [Fact]
    public async Task Student_CreatesPetition_WithoutAttachment()
    {
        var ogr = await LoginAs("ogrenci2");

        var p = await Create(ogr);

        Assert.Equal(PetitionStatus.Beklemede, p.Status);
        Assert.Equal(PetitionType.KayitDondurma, p.Type);
        Assert.False(p.HasAttachment);
        Assert.Equal("Buse Ateş", p.StudentName);
        Assert.Equal("20241011", p.StudentNo);
    }

    [Fact]
    public async Task Student_CreatesPetition_WithPdfAttachment_AndDownloadsIt()
    {
        var ogr = await LoginAs("ogrenci2");

        var p = await Create(ogr, "Muafiyet", file: PdfBytes, fileName: "transkript.pdf");

        Assert.True(p.HasAttachment);
        Assert.Equal("transkript.pdf", p.AttachmentFileName);
        Assert.Single(_factory.Files.Stored);

        var res = await ogr.GetAsync($"/api/petitions/{p.Id}/attachment");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", res.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(PdfBytes, await res.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task StoredName_DoesNotLeakOriginalFileName()
    {
        var ogr = await LoginAs("ogrenci2");

        await Create(ogr, file: PdfBytes, fileName: "../../gizli-ad.pdf");

        var stored = Assert.Single(_factory.Files.Stored.Keys);
        Assert.DoesNotContain("gizli", stored);
        Assert.DoesNotContain("..", stored);
        Assert.EndsWith(".pdf", stored);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("sayfa.html")]
    public async Task DisallowedAttachment_IsRejected_AndNotStored(string fileName)
    {
        var ogr = await LoginAs("ogrenci2");

        var res = await ogr.PostAsync("/api/petitions", Form("Diger", Description, PdfBytes, fileName));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("kabul edilmiyor", await ErrorOf(res));
        Assert.Empty(_factory.Files.Stored);
        Assert.Empty((await ogr.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!);
    }

    [Fact]
    public async Task DisguisedExecutable_IsRejected()
    {
        var ogr = await LoginAs("ogrenci2");

        var res = await ogr.PostAsync("/api/petitions", Form("Diger", Description, new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "belge.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Empty(_factory.Files.Stored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kısa")]
    public async Task TooShortDescription_IsRejected(string description)
    {
        var ogr = await LoginAs("ogrenci2");

        var res = await ogr.PostAsync("/api/petitions", Form("Diger", description));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task InvalidType_IsRejected()
    {
        var ogr = await LoginAs("ogrenci2");

        var res = await ogr.PostAsync("/api/petitions", Form("Bilinmeyen", Description));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task OnlyStudents_CanCreate()
    {
        var adv = await LoginAs("danisman1");

        Assert.Equal(HttpStatusCode.Forbidden, (await adv.PostAsync("/api/petitions", Form("Diger", Description))).StatusCode);
    }

    [Fact]
    public async Task MaxFiveOpenPetitions()
    {
        var ogr = await LoginAs("ogrenci2");
        for (var i = 0; i < PetitionService_Max; i++) await Create(ogr);

        var sixth = await ogr.PostAsync("/api/petitions", Form("Diger", Description));

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Contains("En fazla 5", await ErrorOf(sixth));
    }

    private const int PetitionService_Max = Advisor.Infrastructure.Communication.PetitionService.MaxOpenPerStudent;

    [Fact]
    public async Task ClosedPetitions_DoNotCountTowardsTheLimit()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        for (var i = 0; i < PetitionService_Max; i++) await Create(ogr);
        var first = (await ogr.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!.First();
        await SetStatus(adv, first.Id, "Onaylandi");

        Assert.Equal(HttpStatusCode.OK, (await ogr.PostAsync("/api/petitions", Form("Diger", Description))).StatusCode);
    }

    // ------------------------------------------------------------ erişim

    [Fact]
    public async Task Attachment_IsReachableOnlyByOwnerOwnAdvisorAndAdmin()
    {
        var ogr2 = await LoginAs("ogrenci2");
        var ogr3 = await LoginAs("ogrenci3");
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");
        var p = await Create(ogr2, file: PdfBytes);
        var url = $"/api/petitions/{p.Id}/attachment";

        Assert.Equal(HttpStatusCode.OK, (await ogr2.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adv1.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ogr3.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adv2.GetAsync(url)).StatusCode);
        var anon = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Attachment_OfPetitionWithoutFile_Returns404()
    {
        var ogr = await LoginAs("ogrenci2");
        var p = await Create(ogr);

        Assert.Equal(HttpStatusCode.NotFound, (await ogr.GetAsync($"/api/petitions/{p.Id}/attachment")).StatusCode);
    }

    [Fact]
    public async Task List_IsScopedByRole()
    {
        var ogr2 = await LoginAs("ogrenci2");
        var ogr3 = await LoginAs("ogrenci3");
        var adv1 = await LoginAs("danisman1");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");
        await Create(ogr2);
        await Create(ogr3, "Muafiyet");

        Assert.Equal(new[] { "20241011" }, (await ogr2.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!.Select(p => p.StudentNo));
        Assert.Equal(2, (await adv1.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!.Count);
        Assert.Empty((await adv2.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!);
        Assert.Equal(2, (await admin.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!.Count);
    }

    [Fact]
    public async Task List_FiltersByStatus_AndShowsOpenFirst()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        var a = await Create(ogr, "DersEkleBirak");
        await Create(ogr, "Muafiyet");
        await SetStatus(adv, a.Id, "Onaylandi");

        var all = (await adv.GetFromJsonAsync<List<PetitionDto>>("/api/petitions", TestJson.Options))!;
        var approved = (await adv.GetFromJsonAsync<List<PetitionDto>>("/api/petitions?status=Onaylandi", TestJson.Options))!;

        Assert.Equal(PetitionStatus.Beklemede, all[0].Status);
        Assert.Equal(PetitionStatus.Onaylandi, all[^1].Status);
        Assert.Equal(a.Id, Assert.Single(approved).Id);
    }

    // ------------------------------------------------------------ durum akışı

    [Fact]
    public async Task StatusFlow_ReviewThenApprove_NotifiesStudentEachTime()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        var p = await Create(ogr);

        var review = await SetStatus(adv, p.Id, "Inceleniyor");
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var approve = await SetStatus(adv, p.Id, "Onaylandi", "Dilekçeniz uygun bulundu.");
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var final = (await approve.Content.ReadFromJsonAsync<PetitionDto>(TestJson.Options))!;

        Assert.Equal(PetitionStatus.Onaylandi, final.Status);
        Assert.Equal("Dilekçeniz uygun bulundu.", final.AdvisorNote);
        Assert.NotNull(final.UpdatedAt);

        var inbox = (await ogr.GetFromJsonAsync<List<InboxItem>>("/api/inbox", TestJson.Options))!;
        Assert.Equal(2, inbox.Count(i => i.Kind == "Notification"));
        Assert.Contains(inbox, i => i.Body.Contains("İnceleniyor"));
        Assert.Contains(inbox, i => i.Body.Contains("Onaylandı") && i.Body.Contains("Dilekçeniz uygun bulundu."));
        Assert.Equal(2, (await ogr.GetFromJsonAsync<UnreadCount>("/api/inbox/unread-count", TestJson.Options))!.Count);
    }

    [Fact]
    public async Task Rejection_RequiresReason()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        var p = await Create(ogr);

        var noReason = await SetStatus(adv, p.Id, "Reddedildi", "  ");
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Contains("gerekçe", await ErrorOf(noReason));

        Assert.Equal(HttpStatusCode.OK, (await SetStatus(adv, p.Id, "Reddedildi", "Süre dolmuş.")).StatusCode);
    }

    [Fact]
    public async Task FinalStatus_CannotBeChanged()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        var p = await Create(ogr);
        await SetStatus(adv, p.Id, "Onaylandi");

        Assert.Equal(HttpStatusCode.Conflict, (await SetStatus(adv, p.Id, "Reddedildi", "Vazgeçtim")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SetStatus(adv, p.Id, "Inceleniyor")).StatusCode);
    }

    [Fact]
    public async Task InvalidTransitions_AreRejected()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        var p = await Create(ogr);

        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(adv, p.Id, "Beklemede")).StatusCode);      // zaten bu durumda
        await SetStatus(adv, p.Id, "Inceleniyor");
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(adv, p.Id, "Inceleniyor")).StatusCode);    // aynı durum
        Assert.Equal(HttpStatusCode.BadRequest, (await SetStatus(adv, p.Id, "Beklemede")).StatusCode);      // geri alınamaz
        Assert.Equal(HttpStatusCode.NotFound, (await SetStatus(adv, Guid.NewGuid(), "Inceleniyor")).StatusCode);
    }

    [Fact]
    public async Task StatusChange_IsStaffOnly_AndLimitedToOwnStudents()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv2 = await LoginAs("danisman2");
        var admin = await LoginAs("admin");
        var p = await Create(ogr);

        Assert.Equal(HttpStatusCode.Forbidden, (await SetStatus(ogr, p.Id, "Onaylandi")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetStatus(adv2, p.Id, "Onaylandi")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SetStatus(admin, p.Id, "Inceleniyor")).StatusCode);
    }

    [Fact]
    public async Task Petition_AppearsInStudentTimeline_WithLatestStatus()
    {
        var ogr = await LoginAs("ogrenci2");
        var adv = await LoginAs("danisman1");
        var p = await Create(ogr, "Muafiyet");
        await SetStatus(adv, p.Id, "Inceleniyor");

        var file = (await adv.GetFromJsonAsync<StudentFile>("/api/student-files/20241011", TestJson.Options))!;

        var item = Assert.Single(file.Timeline, t => t.Kind == "Petition");
        Assert.Equal("Talep: Muafiyet", item.Title);
        Assert.StartsWith("İnceleniyor", item.Detail);
    }
}
