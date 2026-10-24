using Obis.MockApi.Models;

namespace Obis.MockApi.SeedData;

/// <summary>
/// Deterministik (sabit seed) mock OBİS veri üreticisi. Sadece <see cref="Generate"/>
/// çağrıldığında çalışır ve JSON dosyalarına yazılır (bkz. SeedWriter); normal API
/// çalışma zamanı bu dosyaları okur, yeniden üretmez.
///
/// Bilinçli olarak hazırlanmış uç durum öğrencileri (ApplyEdgeCases içinde,
/// StudentNo'ya göre aranabilir, docs/PROGRESS.md'de de listelenir):
///   EDGE-01 saat çakışması, EDGE-02 önkoşul ihlali, EDGE-03 alttan ders önceliği,
///   EDGE-04 kredi/AKTS limiti aşımı, EDGE-05 sınav çakışması, EDGE-06 tekrar alma,
///   EDGE-07 GANO düşüşü, EDGE-08 azami süreye yaklaşma, EDGE-09 mezuniyete az kalan,
///   EDGE-10 devamsızlık sınırına yaklaşma.
/// </summary>
public static class SeedGenerator
{
    private const string Department = "Bilgisayar Mühendisliği";
    private const int Seed = 1337;

    private static readonly string[] PassGradePool = { "AA", "AA", "BA", "BA", "BB", "BB", "BB", "CB", "CC" };
    private static readonly string[] FailGradePool = { "FF", "FD" };

    public static readonly Dictionary<string, double> GradePoints = new()
    {
        ["AA"] = 4.0, ["BA"] = 3.5, ["BB"] = 3.0, ["CB"] = 2.5, ["CC"] = 2.0,
        ["DC"] = 1.5, ["DD"] = 1.0, ["FD"] = 0.5, ["FF"] = 0.0,
    };

    private static readonly DayOfWeek[] Days =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    };

    private static readonly TimeOnly[] Slots =
    {
        new(9, 0), new(11, 0), new(13, 0), new(15, 0),
    };

    // Section B alan "yoğun" dersler (gerçekçilik: ilk yıllarda şube sayısı daha fazla).
    private static readonly HashSet<string> DualSectionCourses = new()
    {
        "BLM101", "BLM103", "MAT101", "MAT102", "BLM201", "BLM202", "BLM203", "BLM205",
    };

    public static SeedBundle Generate()
    {
        var rnd = new Random(Seed);
        var courses = CourseCatalog.Build();
        var coursesByTerm = courses.GroupBy(c => c.Term).ToDictionary(g => g.Key, g => g.ToList());
        var advisors = BuildAdvisors();
        var plans = BuildStudentPlans(advisors, rnd);

        var schedule = BuildSchedule(courses, rnd);
        var sectionsByCourse = schedule
            .GroupBy(s => s.CourseCode)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Section).Distinct().ToList());

        var transcripts = new List<TranscriptEntry>();
        var registrations = new List<RegistrationEntry>();
        var attendance = new List<AttendanceRecord>();

        foreach (var plan in plans)
        {
            GenerateBaselineHistory(plan, coursesByTerm, sectionsByCourse, transcripts, registrations, attendance, rnd);
        }

        ApplyEdgeCases(plans, coursesByTerm, sectionsByCourse, schedule, transcripts, registrations, attendance, rnd);

        var exams = BuildExams(schedule, rnd);
        ApplyExamConflictEdgeCase(exams, registrations);

        var students = plans
            .Select(p => new StudentInfo(
                p.StudentNo, p.FirstName, p.LastName, p.ClassYear, Department, p.AdvisorId,
                ComputeGpa(p.StudentNo, transcripts), p.Status))
            .ToList();

        var curriculum = BuildCurriculum(courses);
        var edgeCases = plans
            .Where(p => p.EdgeCaseTag is not null)
            .Select(p => new EdgeCaseNote(p.StudentNo, p.EdgeCaseTag!))
            .ToList();

        return new SeedBundle(advisors, students, courses, curriculum, schedule, exams, transcripts, registrations, attendance, edgeCases);
    }

    private static List<AdvisorInfo> BuildAdvisors() =>
        NamePools.Advisors
            .Select((a, i) => new AdvisorInfo($"ADV{i + 1}", a.FirstName, a.LastName, a.Title))
            .ToList();

    private sealed class StudentPlan
    {
        public required string StudentNo { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string AdvisorId { get; init; }
        public int CurrentTerm { get; set; }
        public int ClassYear => Math.Min(4, (CurrentTerm + 1) / 2);
        public string Status { get; set; } = "Aktif";
        public string? EdgeCaseTag { get; set; }
    }

    private static List<StudentPlan> BuildStudentPlans(List<AdvisorInfo> advisors, Random rnd)
    {
        var plans = new List<StudentPlan>();
        var usedNames = new HashSet<string>();
        var counter = 1;
        const int currentAcademicYear = 2026;

        int[] perClassYear = { 5, 5, 5, 6 }; // sınıf 1,2,3,4 -> advisor başına

        foreach (var advisor in advisors)
        {
            for (var classYear = 1; classYear <= 4; classYear++)
            {
                var count = perClassYear[classYear - 1];
                for (var i = 0; i < count; i++)
                {
                    string first, last, full;
                    do
                    {
                        first = NamePools.FirstNames[rnd.Next(NamePools.FirstNames.Length)];
                        last = NamePools.LastNames[rnd.Next(NamePools.LastNames.Length)];
                        full = $"{first} {last}";
                    } while (!usedNames.Add(full));

                    var entryYear = currentAcademicYear - classYear + 1;
                    var studentNo = $"{entryYear}1{counter:D3}";
                    counter++;

                    plans.Add(new StudentPlan
                    {
                        StudentNo = studentNo,
                        FirstName = first,
                        LastName = last,
                        AdvisorId = advisor.AdvisorId,
                        CurrentTerm = 2 * classYear - 1, // güz dönemi anlık kesit
                    });
                }
            }
        }

        return plans;
    }

    private static void GenerateBaselineHistory(
        StudentPlan plan,
        Dictionary<int, List<CourseInfo>> coursesByTerm,
        Dictionary<string, List<string>> sectionsByCourse,
        List<TranscriptEntry> transcripts,
        List<RegistrationEntry> registrations,
        List<AttendanceRecord> attendance,
        Random rnd)
    {
        var effectiveCurrentTerm = Math.Min(plan.CurrentTerm, 8);

        // Geçmiş dönemler: tamamlanmış ve notlandırılmış.
        for (var term = 1; term < effectiveCurrentTerm; term++)
        {
            foreach (var course in CoursesForTerm(coursesByTerm, term, plan, rnd))
            {
                var passed = rnd.NextDouble() > 0.08; // ~%92 geçme oranı (gerçekçi taban gürültü)
                var grade = passed
                    ? PassGradePool[rnd.Next(PassGradePool.Length)]
                    : FailGradePool[rnd.Next(FailGradePool.Length)];

                transcripts.Add(new TranscriptEntry(
                    plan.StudentNo, term, course.CourseCode, course.CourseName,
                    course.Credit, course.Ects, grade, passed));

                registrations.Add(new RegistrationEntry(
                    plan.StudentNo, term, course.CourseCode, PickSection(sectionsByCourse, course.CourseCode, rnd),
                    "Onaylandı"));
            }
        }

        // Güncel dönem: notlandırılmamış, henüz incelenen kayıt.
        if (plan.CurrentTerm <= 8)
        {
            foreach (var course in CoursesForTerm(coursesByTerm, effectiveCurrentTerm, plan, rnd))
            {
                var section = PickSection(sectionsByCourse, course.CourseCode, rnd);
                registrations.Add(new RegistrationEntry(plan.StudentNo, plan.CurrentTerm, course.CourseCode, section, "Beklemede"));

                var attended = 14 - rnd.Next(0, 3);
                attendance.Add(new AttendanceRecord(plan.StudentNo, plan.CurrentTerm, course.CourseCode, 14, attended));
            }
        }
    }

    private static List<CourseInfo> CoursesForTerm(
        Dictionary<int, List<CourseInfo>> coursesByTerm, int term, StudentPlan plan, Random rnd)
    {
        if (!coursesByTerm.TryGetValue(term, out var all))
        {
            return new List<CourseInfo>();
        }

        var mandatory = all.Where(c => c.IsMandatory).ToList();
        if (term < 5)
        {
            return mandatory;
        }

        // Dönem 5+: zorunlular + o dönemin seçmeli havuzundan öğrenciye özgü deterministik bir seçim.
        var electives = all.Where(c => !c.IsMandatory).OrderBy(c => c.CourseCode).ToList();
        if (electives.Count == 0)
        {
            return mandatory;
        }

        var hash = int.Parse(plan.StudentNo) ^ term; // string.GetHashCode() süreç başına rastgeledir, seed tekrarlanabilir olmaz
        var pickIndex = hash & 0x7FFFFFFF; // her zaman negatif olmayan deterministik indeks
        var chosen = electives[pickIndex % electives.Count];
        return mandatory.Append(chosen).ToList();
    }

    private static string PickSection(Dictionary<string, List<string>> sectionsByCourse, string courseCode, Random rnd)
    {
        var options = sectionsByCourse.TryGetValue(courseCode, out var list) && list.Count > 0
            ? list
            : new List<string> { "A" };
        return options[rnd.Next(options.Count)];
    }

    private static List<ScheduleSlot> BuildSchedule(List<CourseInfo> courses, Random rnd)
    {
        var slots = new List<ScheduleSlot>();
        var roomCounter = 101;
        var rooms = new List<string>();
        for (var i = 0; i < 24; i++)
        {
            rooms.Add($"D-{roomCounter++}");
        }

        foreach (var termGroup in courses.GroupBy(c => c.Term).OrderBy(g => g.Key))
        {
            var combo = 0; // (gün, saat) kombinasyonunu sırayla dolaş -> aynı dönem içi çakışma olmaz
            // A şubeleri 0..n-1 numaralı kombinasyonları kullanır; B şubeleri n'den başlayarak
            // ayrı kombinasyonlara yerleşir, böylece hiçbir şube dönem içinde başka bir dersle çakışmaz.
            var nextSecondary = termGroup.Count();
            foreach (var course in termGroup.OrderBy(c => c.CourseCode))
            {
                var day = Days[combo % Days.Length];
                var start = Slots[(combo / Days.Length) % Slots.Length];
                var room = rooms[combo % rooms.Count];
                var end = start.AddMinutes(course.Credit >= 4 ? 120 : 90);

                slots.Add(new ScheduleSlot(course.Term, course.CourseCode, course.CourseName, "A", day, start, end, room));

                if (DualSectionCourses.Contains(course.CourseCode))
                {
                    var b = nextSecondary++;
                    var day2 = Days[b % Days.Length];
                    var start2 = Slots[(b / Days.Length) % Slots.Length];
                    var room2 = rooms[(b + 7) % rooms.Count];
                    var end2 = start2.AddMinutes(course.Credit >= 4 ? 120 : 90);
                    slots.Add(new ScheduleSlot(course.Term, course.CourseCode, course.CourseName, "B", day2, start2, end2, room2));
                }

                combo++;
            }
        }

        return slots;
    }

    private static List<ExamSlot> BuildExams(List<ScheduleSlot> schedule, Random rnd)
    {
        var exams = new List<ExamSlot>();
        var midtermBase = new DateOnly(2026, 11, 16);
        var finalBase = new DateOnly(2027, 1, 12);

        // Dönem içi indeks sıfırlanır: aynı dönemdeki dersler asla aynı tarih+saate denk
        // gelmez (5 gün x 4 saat = 20 kombinasyon, bir dönemde en fazla 7 ders var).
        // Farklı dönemler arası kesişim kasıtlı — sadece aynı anda iki farklı dönemden
        // ders alan (retake yapan) öğrencilerde anlamlı hale gelir.
        foreach (var termGroup in schedule.GroupBy(s => s.Term).OrderBy(g => g.Key))
        {
            var index = 0;
            foreach (var slot in termGroup.OrderBy(s => s.CourseCode).ThenBy(s => s.Section))
            {
                var midtermDate = midtermBase.AddDays(index % 5);
                var finalDate = finalBase.AddDays(index % 5);
                var time = Slots[index % Slots.Length];

                exams.Add(new ExamSlot(slot.Term, slot.CourseCode, slot.CourseName, slot.Section, ExamType.Midterm,
                    midtermDate, time, time.AddMinutes(60), slot.Room));
                exams.Add(new ExamSlot(slot.Term, slot.CourseCode, slot.CourseName, slot.Section, ExamType.Final,
                    finalDate, time, time.AddMinutes(90), slot.Room));

                index++;
            }
        }

        return exams;
    }

    private static double ComputeGpa(string studentNo, List<TranscriptEntry> transcripts)
    {
        var entries = transcripts.Where(t => t.StudentNo == studentNo).ToList();
        if (entries.Count == 0)
        {
            return 0.0;
        }

        var totalCredit = entries.Sum(e => e.Credit);
        if (totalCredit == 0)
        {
            return 0.0;
        }

        var totalPoints = entries.Sum(e => GradePoints[e.LetterGrade] * e.Credit);
        return Math.Round(totalPoints / totalCredit, 2);
    }

    private static CurriculumInfo BuildCurriculum(List<CourseInfo> courses)
    {
        var mandatory = courses.Where(c => c.IsMandatory).Select(c => c.CourseCode).ToList();
        return new CurriculumInfo(Department, mandatory, ElectiveCreditRequirement: 6, TotalEctsRequirement: 240, InternshipDaysRequirement: 40);
    }

    private static void ApplyEdgeCases(
        List<StudentPlan> plans,
        Dictionary<int, List<CourseInfo>> coursesByTerm,
        Dictionary<string, List<string>> sectionsByCourse,
        List<ScheduleSlot> schedule,
        List<TranscriptEntry> transcripts,
        List<RegistrationEntry> registrations,
        List<AttendanceRecord> attendance,
        Random rnd)
    {
        // Her dönüm noktası bir sınıf-yılı grubundan en az bir "temiz" aday bulup
        // onu bilinçli olarak bozuyor. Sınıf3 (CurrentTerm=5) ve sınıf4 (CurrentTerm=7)
        // öğrencilerinden seçim yapılıyor çünkü çakışma/önkoşul senaryoları için
        // yeterli geçmiş dönem gerekiyor.

        var byClass = plans.GroupBy(p => p.ClassYear).ToDictionary(g => g.Key, g => g.ToList());

        // EDGE-01: Saat çakışması — sınıf3 öğrencisi, MAT101'i (dönem1, geçmemiş) tekrar
        // almak için kaydoluyor; özel MAT101 Section C, dönem5 BLM301/A ile aynı gün/saate denk geliyor.
        var s1 = byClass[3][0];
        s1.EdgeCaseTag = "EDGE-01 saat-çakışması";
        MarkAsFailed(transcripts, s1.StudentNo, 1, "MAT101");
        // Ortak şubeleri bozmamak için MAT101'e yalnızca bu öğrencinin seçtiği özel bir "C"
        // şubesi eklenir; saati BLM301/A ile aynıdır.
        AddSectionOverlapping(schedule, "MAT101", "C", "BLM301");
        registrations.Add(new RegistrationEntry(s1.StudentNo, s1.CurrentTerm, "MAT101", "C", "Beklemede"));

        // EDGE-02: Önkoşul ihlali — sınıf2 öğrencisi BLM103'ü (dönem2) hiç geçemedi (FF),
        // ama dönem3'te BLM103'e bağımlı BLM201'e kayıtlı.
        var s2 = byClass[2][0];
        s2.EdgeCaseTag = "EDGE-02 önkoşul-ihlali";
        MarkAsFailed(transcripts, s2.StudentNo, 2, "BLM103");
        EnsureRegistered(registrations, s2.StudentNo, s2.CurrentTerm, "BLM201", "A", "Beklemede");

        // EDGE-03: Alttan ders önceliği — sınıf3 öğrencisi dönem2 FIZ102'yi geçemedi
        // ama alttan tamamlamadan dönem5 derslerine devam ediyor (retake kaydı yok).
        var s3 = byClass[3][1];
        s3.EdgeCaseTag = "EDGE-03 alttan-ders";
        MarkAsFailed(transcripts, s3.StudentNo, 2, "FIZ102");

        // EDGE-04: Kredi/AKTS limiti aşımı — düşük GANO'lu sınıf2 öğrencisi normal
        // dönem yüküne ek olarak üç ekstra ders daha almış (toplam kredi limiti aşıyor).
        var s4 = byClass[2][1];
        s4.EdgeCaseTag = "EDGE-04 kredi-limiti";
        LowerGpaByFailingMany(transcripts, s4.StudentNo, s4.CurrentTerm);
        foreach (var extra in new[] { "BLM301", "BLM302", "BLM303" })
        {
            EnsureRegistered(registrations, s4.StudentNo, s4.CurrentTerm, extra, "A", "Beklemede");
        }

        // EDGE-05: Sınav çakışması — sınıf4 öğrencisi hem BLM401 hem de (geçmemiş) BLM203'ü
        // tekrar alıyor; final tarihleri aynı güne denk getirilecek (ApplyExamConflictEdgeCase).
        var s5 = byClass[4][0];
        s5.EdgeCaseTag = "EDGE-05 sınav-çakışması";
        MarkAsFailed(transcripts, s5.StudentNo, 3, "BLM203");
        // Yalnızca bu öğrencinin seçtiği özel "C" şubesi: ders saati öğrencinin dönem programında
        // boş bir yerde (saat çakışması olmasın, tek bulgu sınav çakışması olsun).
        AddSectionAtFreeSlot(schedule, "BLM203", "C", s5.CurrentTerm);
        EnsureRegistered(registrations, s5.StudentNo, s5.CurrentTerm, "BLM203", "C", "Beklemede");

        // EDGE-06: Tekrar alma (bilgi) — sınıf4 öğrencisi dönem3'te BLM201'i AA ile geçmişken
        // bu dönem tekrar BLM201'e kayıtlı (GANO yükseltme amaçlı — bilgi seviyesi uyarı).
        var s6 = byClass[4][1];
        s6.EdgeCaseTag = "EDGE-06 tekrar-alma";
        ForceGrade(transcripts, s6.StudentNo, 3, "BLM201", "AA");
        EnsureRegistered(registrations, s6.StudentNo, s6.CurrentTerm, "BLM201", "A", "Beklemede");

        // EDGE-07: GANO düşüşü — sınıf3 öğrencisinin son tamamlanan dönemi (dönem4) zayıf
        // notlarla dolu, önceki dönem (dönem3) yüksek notluydu -> görünür düşüş.
        var s7 = byClass[3][2];
        s7.EdgeCaseTag = "EDGE-07 gano-düşüşü";
        ForceTermGrades(transcripts, s7.StudentNo, 3, "AA");
        ForceTermGrades(transcripts, s7.StudentNo, 4, "DD");

        // EDGE-08: Azami süreye yaklaşma — sınıf4 öğrencisi normalde dönem7'de olması
        // gerekirken birden fazla dersi tekrarladığı için dönem9'da (gecikmeli).
        var s8 = byClass[4][2];
        s8.EdgeCaseTag = "EDGE-08 azami-süre";
        MarkAsFailed(transcripts, s8.StudentNo, 1, "BLM101");
        MarkAsFailed(transcripts, s8.StudentNo, 2, "BLM103");
        MarkAsFailed(transcripts, s8.StudentNo, 4, "BLM204");
        PromoteCurrentTermToHistory(s8, registrations, transcripts, coursesByTerm, rnd);
        s8.CurrentTerm = 9;
        // Dönem9'da kalan zorunluları ve tekrarları alıyor.
        foreach (var retake in new[] { "BLM101", "BLM103", "BLM204" })
        {
            EnsureRegistered(registrations, s8.StudentNo, 9, retake, "A", "Beklemede");
        }

        // EDGE-09: Mezuniyete az kalan — sınıf4 öğrencisi dönem8'de sadece 2 dersi eksik,
        // GANO sağlıklı, staj tamamlanmış kabul ediliyor (müfredat kontrolünde ayrı test edilir).
        var s9 = byClass[4][3];
        s9.EdgeCaseTag = "EDGE-09 mezuniyete-az-kalan";
        // Baseline, bu öğrenciyi CurrentTerm=7 kabul edip dönem7'yi "Beklemede" kaydetmişti;
        // önce onu geçmişe (notlandırılmış) terfi ettir, sonra dönem8'e geç ki tutarsız
        // (hem bekleyen hem notsuz) bir dönem7 kalmasın.
        PromoteCurrentTermToHistory(s9, registrations, transcripts, coursesByTerm, rnd);
        s9.CurrentTerm = 8;
        AppendCurrentTermRegistrationsOnly(s9, coursesByTerm, sectionsByCourse, registrations, attendance, rnd);
        // "Az kaldı" senaryosunu netleştir: dönem8'in 5 dersinden 3'ünü daha önceden
        // tamamlanmış kabul et (transkripte geçir), sadece 2'si bu dönem gerçekten eksik/kayıtlı kalsın.
        var term8Regs = registrations.Where(r => r.StudentNo == s9.StudentNo && r.Term == 8).Select(r => r.CourseCode).OrderBy(c => c).ToList();
        foreach (var courseCode in term8Regs.Take(3))
        {
            var course = coursesByTerm[8].First(c => c.CourseCode == courseCode);
            transcripts.Add(new TranscriptEntry(s9.StudentNo, 8, course.CourseCode, course.CourseName, course.Credit, course.Ects, "BA", true));
            registrations.RemoveAll(r => r.StudentNo == s9.StudentNo && r.Term == 8 && r.CourseCode == courseCode);
            attendance.RemoveAll(a => a.StudentNo == s9.StudentNo && a.Term == 8 && a.CourseCode == courseCode);
        }

        // EDGE-10: Devamsızlık sınırına yaklaşma — sınıf2 öğrencisinin bu dönem bir dersten
        // devamsızlığı %28'e yaklaşmış (14 oturumdan 10'una girmiş).
        var s10 = byClass[2][2];
        s10.EdgeCaseTag = "EDGE-10 devamsızlık";
        var targetCourse = coursesByTerm[s10.CurrentTerm].First(c => c.IsMandatory).CourseCode;
        attendance.RemoveAll(a => a.StudentNo == s10.StudentNo && a.CourseCode == targetCourse);
        attendance.Add(new AttendanceRecord(s10.StudentNo, s10.CurrentTerm, targetCourse, 14, 10));
    }

    private static void MarkAsFailed(List<TranscriptEntry> transcripts, string studentNo, int term, string courseCode)
    {
        var idx = transcripts.FindIndex(t => t.StudentNo == studentNo && t.Term == term && t.CourseCode == courseCode);
        if (idx >= 0)
        {
            transcripts[idx] = transcripts[idx] with { LetterGrade = "FF", Passed = false };
        }
    }

    private static void ForceGrade(List<TranscriptEntry> transcripts, string studentNo, int term, string courseCode, string grade)
    {
        var idx = transcripts.FindIndex(t => t.StudentNo == studentNo && t.Term == term && t.CourseCode == courseCode);
        if (idx >= 0)
        {
            transcripts[idx] = transcripts[idx] with { LetterGrade = grade, Passed = true };
        }
    }

    private static void ForceTermGrades(List<TranscriptEntry> transcripts, string studentNo, int term, string grade)
    {
        for (var i = 0; i < transcripts.Count; i++)
        {
            if (transcripts[i].StudentNo == studentNo && transcripts[i].Term == term)
            {
                var passed = GradePoints[grade] > 0.0;
                transcripts[i] = transcripts[i] with { LetterGrade = grade, Passed = passed };
            }
        }
    }

    private static void LowerGpaByFailingMany(List<TranscriptEntry> transcripts, string studentNo, int currentTerm)
    {
        var count = 0;
        for (var i = 0; i < transcripts.Count && count < 3; i++)
        {
            if (transcripts[i].StudentNo == studentNo && transcripts[i].Term < currentTerm)
            {
                transcripts[i] = transcripts[i] with { LetterGrade = "FF", Passed = false };
                count++;
            }
        }
    }

    private static void EnsureRegistered(
        List<RegistrationEntry> registrations, string studentNo, int term, string courseCode, string section, string status)
    {
        if (!registrations.Any(r => r.StudentNo == studentNo && r.Term == term && r.CourseCode == courseCode))
        {
            registrations.Add(new RegistrationEntry(studentNo, term, courseCode, section, status));
        }
    }

    /// <summary><paramref name="courseCode"/> dersine, <paramref name="overlapWithCourse"/> dersinin A
    /// şubesiyle aynı gün/saatte yeni bir özel şube ekler. Mevcut şubelere dokunulmaz; yalnızca bu
    /// şubeyi seçen öğrenci çakışma görür.</summary>
    private static void AddSectionOverlapping(List<ScheduleSlot> schedule, string courseCode, string section, string overlapWithCourse)
    {
        var template = schedule.FirstOrDefault(s => s.CourseCode == courseCode && s.Section == "A");
        var target = schedule.FirstOrDefault(s => s.CourseCode == overlapWithCourse && s.Section == "A");
        if (template is null || target is null)
        {
            return;
        }

        var duration = template.EndTime - template.StartTime;
        schedule.Add(template with
        {
            Section = section, Day = target.Day, StartTime = target.StartTime,
            EndTime = target.StartTime.Add(duration), Room = target.Room,
        });
    }

    /// <summary><paramref name="courseCode"/> dersine, <paramref name="term"/> dönemi dersleriyle çakışmayan
    /// (boş bir gün/saatte) yeni bir özel şube ekler.</summary>
    private static void AddSectionAtFreeSlot(List<ScheduleSlot> schedule, string courseCode, string section, int term)
    {
        var template = schedule.FirstOrDefault(s => s.CourseCode == courseCode && s.Section == "A");
        if (template is null)
        {
            return;
        }

        var used = schedule.Where(s => s.Term == term).Select(s => (s.Day, s.StartTime)).ToHashSet();
        foreach (var start in Slots)
        {
            foreach (var day in Days)
            {
                if (used.Contains((day, start)))
                {
                    continue;
                }

                var duration = template.EndTime - template.StartTime;
                schedule.Add(template with { Section = section, Day = day, StartTime = start, EndTime = start.Add(duration) });
                return;
            }
        }
    }

    /// <summary>Baseline bir öğrenciyi CurrentTerm=t ile "bekleyen kayıt" olarak işlemişken,
    /// edge-case sonradan o öğrenciyi daha ileri bir döneme taşıyorsa, eski dönemin
    /// kayıtları önce notlandırılmış geçmişe dönüştürülmeli — aksi halde geçmişte
    /// notsuz ve sonuçsuz bir dönem kalır.</summary>
    private static void PromoteCurrentTermToHistory(
        StudentPlan plan,
        List<RegistrationEntry> registrations,
        List<TranscriptEntry> transcripts,
        Dictionary<int, List<CourseInfo>> coursesByTerm,
        Random rnd)
    {
        var term = plan.CurrentTerm;
        var pending = registrations
            .Select((r, i) => (r, i))
            .Where(x => x.r.StudentNo == plan.StudentNo && x.r.Term == term && x.r.ApprovalStatus == "Beklemede")
            .ToList();

        foreach (var (reg, i) in pending)
        {
            var course = coursesByTerm[term].FirstOrDefault(c => c.CourseCode == reg.CourseCode);
            if (course is null)
            {
                continue;
            }

            var passed = rnd.NextDouble() > 0.08;
            var grade = passed ? PassGradePool[rnd.Next(PassGradePool.Length)] : FailGradePool[rnd.Next(FailGradePool.Length)];
            transcripts.Add(new TranscriptEntry(plan.StudentNo, term, course.CourseCode, course.CourseName, course.Credit, course.Ects, grade, passed));
            registrations[i] = reg with { ApprovalStatus = "Onaylandı" };
        }
    }

    private static void AppendCurrentTermRegistrationsOnly(
        StudentPlan plan,
        Dictionary<int, List<CourseInfo>> coursesByTerm,
        Dictionary<string, List<string>> sectionsByCourse,
        List<RegistrationEntry> registrations,
        List<AttendanceRecord> attendance,
        Random rnd)
    {
        foreach (var course in CoursesForTerm(coursesByTerm, plan.CurrentTerm, plan, rnd))
        {
            EnsureRegistered(registrations, plan.StudentNo, plan.CurrentTerm, course.CourseCode,
                PickSection(sectionsByCourse, course.CourseCode, rnd), "Beklemede");
            if (!attendance.Any(a => a.StudentNo == plan.StudentNo && a.Term == plan.CurrentTerm && a.CourseCode == course.CourseCode))
            {
                attendance.Add(new AttendanceRecord(plan.StudentNo, plan.CurrentTerm, course.CourseCode, 14, 14 - rnd.Next(0, 3)));
            }
        }
    }

    private static void ApplyExamConflictEdgeCase(List<ExamSlot> exams, List<RegistrationEntry> registrations)
    {
        // EDGE-05 öğrencisinin BLM203 final'ini, aynı dönemdeki BLM401 final'iyle aynı
        // tarih/saate taşı (ikisi de bu öğrencinin kayıtlı olduğu dersler).
        var blm401Final = exams.FindIndex(e => e.CourseCode == "BLM401" && e.Section == "A" && e.Type == ExamType.Final);
        var blm203FinalIdx = exams.FindIndex(e => e.CourseCode == "BLM203" && e.Section == "C" && e.Type == ExamType.Final);
        if (blm401Final >= 0 && blm203FinalIdx >= 0)
        {
            var target = exams[blm401Final];
            exams[blm203FinalIdx] = exams[blm203FinalIdx] with { Date = target.Date, StartTime = target.StartTime, EndTime = target.EndTime };
        }
    }
}
