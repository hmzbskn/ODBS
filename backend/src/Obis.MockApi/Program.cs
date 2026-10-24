using System.Text.Json.Serialization;
using Obis.MockApi.Endpoints;
using Obis.MockApi.SeedData;

// `dotnet run -- --generate-seed` : deterministik seed verisini üretir ve
// SeedData/json/ altına yazar, sonra çıkar. Normal çalışmada bu dosyalar okunur,
// yeniden üretilmez (bkz. docs/DECISIONS.md).
if (args.Contains("--generate-seed"))
{
    var bundle = SeedGenerator.Generate();
    SeedIo.Write(bundle, FindProjectRoot());
    Console.WriteLine($"Seed üretildi: {bundle.Students.Count} öğrenci, {bundle.Courses.Count} ders, " +
                       $"{bundle.Transcripts.Count} transkript satırı, {bundle.Registrations.Count} kayıt, " +
                       $"{bundle.Schedule.Count} program satırı, {bundle.Exams.Count} sınav satırı, " +
                       $"{bundle.EdgeCases.Count} uç durum öğrencisi.");
    foreach (var edge in bundle.EdgeCases)
    {
        Console.WriteLine($"  {edge.Tag} -> {edge.StudentNo}");
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(_ => SeedIo.Load(builder.Environment.ContentRootPath));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "Obis.MockApi çalışıyor.");
app.MapObisEndpoints();

app.Run();

// Program.cs, çalışma dizini her zaman proje köküyle aynı olmayabileceği için
// (örn. `dotnet run` farklı bir cwd'den çağrılırsa) seed üretimi sırasında proje
// kökünü (.csproj'un bulunduğu dizin) yukarı doğru arayarak bulur.
static string FindProjectRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !dir.GetFiles("*.csproj").Any())
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

namespace Obis.MockApi
{
    /// <summary>Testlerde <c>WebApplicationFactory&lt;MockApiMarker&gt;</c> ile bu uygulamayı bellek içinde ayağa kaldırmak için.
    /// (Program sınıfı, Advisor.Api'dekiyle ad çakışması yaşamasın diye açılmadı.)</summary>
    public sealed class MockApiMarker;
}
