using Obis.MockApi.SeedData;

namespace Obis.MockApi.Endpoints;

/// <summary>Mock OBİS'in dışa verdiği tüm Minimal API uç noktaları. Gerçek bir
/// öğrenci bilgi sisteminin davranışını taklit eder: sadece okuma, kimlik doğrulama
/// yok (gerçek OBİS entegrasyonunda ayrı bir kimlik/anahtar katmanı eklenecektir —
/// bkz. docs/DECISIONS.md).</summary>
public static class ObisEndpoints
{
    public static void MapObisEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/students", (SeedBundle db) => db.Students);

        group.MapGet("/students/{no}", (string no, SeedBundle db) =>
        {
            var student = db.Students.FirstOrDefault(s => s.StudentNo == no);
            return student is null ? Results.NotFound() : Results.Ok(student);
        });

        group.MapGet("/students/{no}/transcript", (string no, SeedBundle db) =>
        {
            if (!StudentExists(db, no))
            {
                return Results.NotFound();
            }

            var rows = db.Transcripts
                .Where(t => t.StudentNo == no)
                .OrderBy(t => t.Term)
                .ThenBy(t => t.CourseCode)
                .ToList();
            return Results.Ok(rows);
        });

        group.MapGet("/students/{no}/registrations", (string no, int? term, SeedBundle db) =>
        {
            if (!StudentExists(db, no))
            {
                return Results.NotFound();
            }

            var rows = db.Registrations.Where(r => r.StudentNo == no);
            if (term.HasValue)
            {
                rows = rows.Where(r => r.Term == term.Value);
            }

            return Results.Ok(rows.OrderBy(r => r.Term).ThenBy(r => r.CourseCode).ToList());
        });

        group.MapGet("/students/{no}/attendance", (string no, int? term, SeedBundle db) =>
        {
            if (!StudentExists(db, no))
            {
                return Results.NotFound();
            }

            var rows = db.Attendance.Where(a => a.StudentNo == no);
            if (term.HasValue)
            {
                rows = rows.Where(a => a.Term == term.Value);
            }

            return Results.Ok(rows.OrderBy(a => a.Term).ThenBy(a => a.CourseCode).ToList());
        });

        group.MapGet("/courses", (SeedBundle db) => db.Courses.OrderBy(c => c.Term).ThenBy(c => c.CourseCode));

        group.MapGet("/schedule", (int? term, SeedBundle db) =>
        {
            var rows = db.Schedule.AsEnumerable();
            if (term.HasValue)
            {
                rows = rows.Where(s => s.Term == term.Value);
            }

            return Results.Ok(rows.OrderBy(s => s.CourseCode).ThenBy(s => s.Section).ToList());
        });

        group.MapGet("/exams", (int? term, SeedBundle db) =>
        {
            var rows = db.Exams.AsEnumerable();
            if (term.HasValue)
            {
                rows = rows.Where(e => e.Term == term.Value);
            }

            return Results.Ok(rows.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ToList());
        });

        group.MapGet("/curriculum", (SeedBundle db) => db.Curriculum);
    }

    private static bool StudentExists(SeedBundle db, string studentNo) =>
        db.Students.Any(s => s.StudentNo == studentNo);
}
