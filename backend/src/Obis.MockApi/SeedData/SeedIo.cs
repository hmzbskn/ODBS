using System.Text.Json;
using System.Text.Json.Serialization;

namespace Obis.MockApi.SeedData;

/// <summary>Seed paketinin JSON dosyalarına yazılması ve okunması. Dosyalar
/// <c>SeedData/json/</c> altında tek tek koleksiyon başına tutulur (students.json,
/// courses.json, ...) — tek bir dev dosya yerine ayrı dosyalar, insan gözüyle
/// incelemeyi ve ileride gerçek OBİS'e geçişte referans almayı kolaylaştırır.</summary>
public static class SeedIo
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string JsonDirectory(string contentRootPath) => Path.Combine(contentRootPath, "SeedData", "json");

    public static void Write(SeedBundle bundle, string contentRootPath)
    {
        var dir = JsonDirectory(contentRootPath);
        Directory.CreateDirectory(dir);

        WriteFile(dir, "advisors.json", bundle.Advisors);
        WriteFile(dir, "students.json", bundle.Students);
        WriteFile(dir, "courses.json", bundle.Courses);
        WriteFile(dir, "curriculum.json", bundle.Curriculum);
        WriteFile(dir, "schedule.json", bundle.Schedule);
        WriteFile(dir, "exams.json", bundle.Exams);
        WriteFile(dir, "transcripts.json", bundle.Transcripts);
        WriteFile(dir, "registrations.json", bundle.Registrations);
        WriteFile(dir, "attendance.json", bundle.Attendance);
        WriteFile(dir, "internships.json", bundle.Internships);
        WriteFile(dir, "edge-cases.json", bundle.EdgeCases);
    }

    public static SeedBundle Load(string contentRootPath)
    {
        var dir = JsonDirectory(contentRootPath);

        return new SeedBundle(
            ReadFile<List<Models.AdvisorInfo>>(dir, "advisors.json"),
            ReadFile<List<Models.StudentInfo>>(dir, "students.json"),
            ReadFile<List<Models.CourseInfo>>(dir, "courses.json"),
            ReadFile<Models.CurriculumInfo>(dir, "curriculum.json"),
            ReadFile<List<Models.ScheduleSlot>>(dir, "schedule.json"),
            ReadFile<List<Models.ExamSlot>>(dir, "exams.json"),
            ReadFile<List<Models.TranscriptEntry>>(dir, "transcripts.json"),
            ReadFile<List<Models.RegistrationEntry>>(dir, "registrations.json"),
            ReadFile<List<Models.AttendanceRecord>>(dir, "attendance.json"),
            ReadFile<List<Models.InternshipRecord>>(dir, "internships.json"),
            ReadFile<List<EdgeCaseNote>>(dir, "edge-cases.json"));
    }

    private static void WriteFile<T>(string dir, string fileName, T data)
    {
        var json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(Path.Combine(dir, fileName), json);
    }

    private static T ReadFile<T>(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException($"Seed dosyası boş/geçersiz: {path}");
    }
}
