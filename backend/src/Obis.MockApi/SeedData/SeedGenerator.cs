using Obis.MockApi.Models;

namespace Obis.MockApi.SeedData;

/// <summary>
/// Deterministik (sabit seed) mock OBİS veri üreticisi. Sadece <see cref="Generate"/>
/// çağrıldığında çalışır ve JSON dosyalarına yazılır (bkz. SeedWriter); normal API
/// çalışma zamanı bu dosyaları okur, yeniden üretmez.
///
/// Bilinçli olarak hazırlanmış uç durum öğrencileri (ApplyEdgeCases içinde,
/// StudentNo'ya göre aranabilir, docs/PROGRESS.md'de de listelenir):
///   EDGE-01 saat çakışması, EDGE-02 devam şartı olmayan çakışma, EDGE-03 alttan ders önceliği,
///   EDGE-04 kredi/AKTS limiti aşımı, EDGE-05 alternatif şubeli çakışma, EDGE-06 tekrar alma,
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
        new(8, 0), new(10, 0), new(12, 0), new(14, 0), new(16, 0), new(18, 0),
    };

    // Section B alan "yoğun" dersler (gerçekçilik: ilk yıllarda şube sayısı daha fazla).
    private static readonly HashSet<string> DualSectionCourses = new()
    {
        "3301151", "3301251", "3301256", "3301351", "3301353", "3301354", "3301452", "3301453",
    };

    public static SeedBundle Generate()
    {
        var rnd = new Random(Seed);
        var courses = CourseCatalog.Build();
        var coursesByTerm = courses.GroupBy(c => c.Term).ToDictionary(g => g.Key, g => g.ToList());
        var advisors = BuildAdvisors();
        var plans = BuildStudentPlans(advisors, rnd);

        // Güz dersleri (1-3-5-7) bölümün gerçek 2026-2027 güz programından; bahar dersleri için kurgusal saatler.
        var schedule = FallSchedule.Build(courses.ToDictionary(c => c.CourseCode))
            .Concat(BuildSchedule(courses.Where(c => c.Term % 2 == 0).ToList(), rnd))
            .ToList();
        schedule = AssignInstructors(schedule);
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

        var students = plans
            .Select(p => new StudentInfo(
                p.StudentNo, p.FirstName, p.LastName, p.ClassYear, Department, p.AdvisorId,
                ComputeGpa(p.StudentNo, transcripts), p.Status,
                StudentEmail(p), StudentPhone(p.StudentNo), StudentResidence(p.StudentNo), StudentProgram(p)))
            .ToList();

        var curriculum = BuildCurriculum(courses);
        var edgeCases = plans
            .Where(p => p.EdgeCaseTag is not null)
            .Select(p => new EdgeCaseNote(p.StudentNo, p.EdgeCaseTag!))
            .ToList();

        // Staj verisi, rastgele akışın EN SONUNDA üretilir: önceki hiçbir çıktıyı (transkript, kayıt...) değiştirmez.
        var internships = BuildInternships(plans, rnd);

        return new SeedBundle(advisors, students, courses, curriculum, schedule, exams, transcripts, registrations, attendance, internships, edgeCases);
    }

    // İletişim ve öğretim türü rastgele akıştan DEĞİL, öğrenci sıra numarasından türetilir: üretim deterministik
    // kalır ve diğer seed çıktıları (transkript, kayıt...) değişmez. Telefon numaraları kurgusaldır.
    private static int Ordinal(string studentNo) => int.Parse(studentNo[^3..]);

    private static string StudentEmail(StudentPlan p)
    {
        static string Ascii(string v) => new string(v.ToLowerInvariant()
            .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c')
            .Where(char.IsAsciiLetterOrDigit).ToArray());
        return $"{Ascii(p.FirstName)}{Ascii(p.LastName)}@ogr.selcuk.edu.tr";
    }

    private static string StudentPhone(string studentNo)
    {
        var n = Ordinal(studentNo);
        return $"05{30 + n % 20:D2} {(n * 7919) % 900 + 100} {(n * 31) % 90 + 10} {(n * 17) % 90 + 10}";
    }

    private static readonly string[] Residences =
    {
        "Selçuklu / Bosna Hersek Mah.", "Selçuklu / Yazır Mah.", "Meram / Havzan Mah.", "Karatay / Fevziçakmak Mah.",
        "Selçuklu / Akademi Mah.", "Meram / Yaka Mah.", "Selçuklu / Nişantaş Mah.", "Karatay / Aziziye Mah.",
    };

    private static string StudentResidence(string studentNo) => Residences[Ordinal(studentNo) % Residences.Length];

    /// <summary>Bölümde ikinci öğretim yalnızca 4. sınıfta sürüyor (güz programında 1-3. sınıfın İÖ saati yok; Hamza onayı).</summary>
    private static string StudentProgram(StudentPlan p) =>
        p.ClassYear == 4 && Ordinal(p.StudentNo) % 4 == 0 ? "İkinci Öğretim" : "Birinci Öğretim";

    private static List<AdvisorInfo> BuildAdvisors() =>
        NamePools.Advisors
            .Select((a, i) => new AdvisorInfo($"ADV{i + 1}", a.FirstName, a.LastName, a.Title))
            .ToList();

    private sealed class StudentPlan
    {
        public required string StudentNo { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string AdvisorId { get; set; }
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

        // Öğrenci kümesi (ve rastgele akışı) ilk üç danışmanla üretilir; danışman ataması sonradan sınıfa göre yapılır.
        foreach (var advisor in advisors.Take(3))
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

        // İş kuralı: her danışman yalnızca bir sınıfa bakar, her sınıfın tek danışmanı vardır.
        // 3. sınıf ADV1'de kalır (demo: danisman1 <-> ogrenci2), diğerleri farklı hocalara dağılır.
        string[] advisorByClassYear = { advisors[1].AdvisorId, advisors[2].AdvisorId, advisors[0].AdvisorId, advisors[3].AdvisorId };
        foreach (var plan in plans) plan.AdvisorId = advisorByClassYear[plan.ClassYear - 1];

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
                var passed = rnd.NextDouble() > 0.06; // ~%94 geçme oranı: gerçek müfredatta ~35 ders var, taban gürültü ders sayısıyla birlikte büyümesin
                var grade = passed
                    ? PassGradePool[rnd.Next(PassGradePool.Length)]
                    : FailGradePool[rnd.Next(FailGradePool.Length)];

                transcripts.Add(new TranscriptEntry(
                    plan.StudentNo, term, course.CourseCode, course.CourseName,
                    course.Credit, course.Ects, grade, passed));

                registrations.Add(new RegistrationEntry(
                    plan.StudentNo, term, course.CourseCode, SectionFor(plan, term, registrations, sectionsByCourse, course.CourseCode, rnd),
                    "Onaylandı"));
            }
        }

        // Güncel dönem: notlandırılmamış, henüz incelenen kayıt.
        if (plan.CurrentTerm <= 8)
        {
            foreach (var course in CoursesForTerm(coursesByTerm, effectiveCurrentTerm, plan, rnd))
            {
                var section = SectionFor(plan, plan.CurrentTerm, registrations, sectionsByCourse, course.CourseCode, rnd);
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
        if (!ElectivesPerTerm.TryGetValue(term, out var electiveCount))
        {
            return mandatory;
        }

        var electives = all.Where(c => !c.IsMandatory).ToList();
        var hash = int.Parse(plan.StudentNo) ^ term; // string.GetHashCode() süreç başına rastgeledir, seed tekrarlanabilir olmaz
        var start = hash & 0x7FFFFFFF; // her zaman negatif olmayan deterministik indeks

        // Güz dönemi: yalnızca gerçek programda açılan seçmeliler. Aynı saatteki seçmeliler bir grup oluşturur ve her
        // gruptan bir ders seçilir; böylece öğrencinin seçmelileri kendi aralarında çakışmaz.
        var opened = FallSchedule.Slots.Value.Where(s => electives.Any(e => e.CourseCode == s.CourseCode)).ToList();
        if (opened.Count > 0)
        {
            var groups = opened
                .GroupBy(s => s.CourseCode)
                .Select(g => g.OrderBy(s => s.Section, StringComparer.Ordinal).First())
                .GroupBy(s => (s.Day, s.StartTime))
                .OrderBy(g => g.Key.Day).ThenBy(g => g.Key.StartTime)
                .Select(g => g.Select(s => s.CourseCode).OrderBy(c => c, StringComparer.Ordinal).ToList())
                .ToList();
            // 2 AKTS'lik sosyal seçmeliler yerine 4 AKTS'lik gruplar seçilir; gruplar yetmezse açık olan önceki güz dönemi
            // seçmelilerinden öğrencinin almadığı, programına uyan biri eklenir (7. dönem: 15 zorunlu + 4x4 = 31 AKTS, toplam 240).
            var ects = all.ToDictionary(c => c.CourseCode, c => c.Ects);
            var picked = groups.Where(g => g.All(c => ects[c] >= 4)).Take(electiveCount)
                .Select((g, i) => g[(start + i) % g.Count]).ToList();
            var result = mandatory.Concat(electives.Where(e => picked.Contains(e.CourseCode))).ToList();
            if (picked.Count < electiveCount && coursesByTerm.ContainsKey(term - 2))
            {
                var takenBefore = CoursesForTerm(coursesByTerm, term - 2, plan, rnd).Select(c => c.CourseCode).ToHashSet();
                var busy = result.SelectMany(c => OwnFallSlots(plan, c.CourseCode)).ToList();
                foreach (var extra in coursesByTerm[term - 2].Where(c => !c.IsMandatory && !takenBefore.Contains(c.CourseCode)).OrderBy(c => c.CourseCode))
                {
                    if (picked.Count >= electiveCount) break;
                    var slots = OwnFallSlots(plan, extra.CourseCode).ToList();
                    if (slots.Count == 0 || slots.Any(x => busy.Any(b => b.Day == x.Day && b.StartTime < x.EndTime && x.StartTime < b.EndTime))) continue;
                    picked.Add(extra.CourseCode);
                    busy.AddRange(slots);
                    result.Add(extra);
                }
            }

            return result;
        }

        // Bahar dönemi: zorunlular + seçmeli havuzundan öğrenciye özgü deterministik seçim. Havuz, dönemin en sık AKTS'li
        // seçmelileridir (dönem yükü ~30 AKTS kalsın); kurgusal programda aynı dönemin dersleri ayrı saat dilimindedir.
        var modalEcts = electives.GroupBy(c => c.Ects).OrderByDescending(g => g.Count()).First().Key;
        var pool = electives.Where(c => c.Ects == modalEcts).OrderBy(c => c.CourseCode).ToList();
        var chosen = Enumerable.Range(0, Math.Min(electiveCount, pool.Count)).Select(i => pool[(start + i) % pool.Count]);
        return mandatory.Concat(chosen).ToList();
    }

    /// <summary>Dönem başına öğrencinin aldığı seçmeli sayısı (gerçek müfredatta seçmeliler 4-7. dönemlerde).</summary>
    private static readonly Dictionary<int, int> ElectivesPerTerm = new() { [4] = 1, [5] = 3, [6] = 3, [7] = 4 };

    /// <summary>Öğrencinin öğretim türüne uygun şube: iki öğretim birlikteyse "NÖ/İÖ", ayrıysa ikinci öğretim öğrencisi
    /// "İÖ", normal öğretim öğrencisi "NÖ" (öğrenci başka öğretimin şubesinden ders alamaz). Rastgele akış her derste yine bir
    /// kez ilerler; diğer seed çıktıları kararlı kalır.</summary>
    private static string SectionFor(
        StudentPlan plan, int term, List<RegistrationEntry> registrations,
        Dictionary<string, List<string>> sectionsByCourse, string courseCode, Random rnd)
    {
        var random = PickSection(sectionsByCourse, courseCode, rnd);
        if (!sectionsByCourse.TryGetValue(courseCode, out var list)) return "NÖ/İÖ"; // sabit saati olmayan ders (uygulama, staj...)
        if (list.Contains("NÖ/İÖ")) return "NÖ/İÖ";
        var own = OwnSection(plan);
        return list.Contains(own) ? own : random;
    }

    private static string OwnSection(StudentPlan plan) => StudentProgram(plan) == "İkinci Öğretim" ? "İÖ" : "NÖ";

    /// <summary>Dersin, öğrencinin öğretim türünün alabileceği şubelerindeki güz programı saatleri.</summary>
    private static IEnumerable<ScheduleSlot> OwnFallSlots(StudentPlan plan, string courseCode) =>
        FallSchedule.Slots.Value.Where(s => s.CourseCode == courseCode && (s.Section == "NÖ/İÖ" || s.Section == OwnSection(plan)));

    private static string PickSection(Dictionary<string, List<string>> sectionsByCourse, string courseCode, Random rnd)
    {
        var options = sectionsByCourse.TryGetValue(courseCode, out var list) && list.Count > 0
            ? list
            : new List<string> { "A" };
        return options[rnd.Next(options.Count)];
    }

    /// <summary>Her derse (tüm şubeleriyle) bir öğretim elemanı atar; aynı hocanın iki dersi aynı saate düşmesin diye
    /// çakışmayan ilk hocayı seçer. Rastgelelik kullanmaz, seed'in geri kalanını etkilemez.</summary>
    private static List<ScheduleSlot> AssignInstructors(List<ScheduleSlot> schedule)
    {
        var pool = NamePools.Instructors;
        var busy = pool.ToDictionary(n => n, _ => new List<ScheduleSlot>());
        var byCourse = new Dictionary<string, string>();
        var start = 0;
        foreach (var group in schedule.GroupBy(s => s.CourseCode).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var slots = group.ToList();
            var chosen = Enumerable.Range(0, pool.Length)
                .Select(i => pool[(start + i) % pool.Length])
                .FirstOrDefault(n => !slots.Any(s => busy[n].Any(o => o.Day == s.Day && o.StartTime < s.EndTime && s.StartTime < o.EndTime)))
                ?? pool[start % pool.Length];
            busy[chosen].AddRange(slots);
            byCourse[group.Key] = chosen;
            start++;
        }

        return schedule.Select(s => s with { Instructor = byCourse[s.CourseCode] }).ToList();
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
        // gelmez (5 gün x 6 saat = 30 kombinasyon, bir dönemde en fazla 29 ders var).
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

    /// <summary>Tamamlanan staj günleri: 1-2. sınıf henüz staj yapmadı; 3. sınıfta yarısı tamamlamış;
    /// 4. sınıfın çoğu tamamlamış. Uç durumlar: EDGE-09 (mezuniyete az kalan) tamamlamış, EDGE-08 (azami süre) yarım.</summary>
    private static List<InternshipRecord> BuildInternships(List<StudentPlan> plans, Random rnd)
    {
        var records = new List<InternshipRecord>();
        foreach (var p in plans.OrderBy(p => p.StudentNo, StringComparer.Ordinal))
        {
            int days;
            if (p.EdgeCaseTag is { } tag && tag.StartsWith("EDGE-09", StringComparison.Ordinal))
            {
                days = 40;
            }
            else if (p.EdgeCaseTag is { } tag8 && tag8.StartsWith("EDGE-08", StringComparison.Ordinal))
            {
                days = 20;
            }
            else
            {
                days = p.ClassYear switch
                {
                    <= 2 => 0,
                    3 => new[] { 0, 20, 40, 40 }[rnd.Next(4)],
                    _ => new[] { 20, 40, 40, 40, 40 }[rnd.Next(5)],
                };
            }

            records.Add(new InternshipRecord(p.StudentNo, days));
        }

        return records;
    }

    private static CurriculumInfo BuildCurriculum(List<CourseInfo> courses)
    {
        var mandatory = courses.Where(c => c.IsMandatory).Select(c => c.CourseCode).ToList();
        return new CurriculumInfo(Department, mandatory, ElectiveCreditRequirement: 30, TotalEctsRequirement: 240, InternshipDaysRequirement: 40);
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

        // EDGE-01: Saat çakışması (alternatifsiz) — sınıf3 öğrencisi dönem3'te Mantık Devreleri-1'den kaldı ve alttan alıyor.
        // Gerçek programda Mantık Devreleri-1 ile Programlama Dili-3 ikisi de Pazartesi 13:25-16:55; iki dersin de devam şartı
        // var ve Programlama Dili-3'ün başka şubesi yok: sistem çakışmayı gösterir, alternatif bulamaz.
        var s1 = byClass[3][0];
        s1.EdgeCaseTag = "EDGE-01 saat-çakışması";
        MarkAsFailed(transcripts, s1.StudentNo, 3, CourseCatalog.LogicCircuits1);
        EnsureRegistered(registrations, s1.StudentNo, s1.CurrentTerm, CourseCatalog.LogicCircuits1, "NÖ/İÖ", "Beklemede");
        DropOneElective(registrations, attendance, coursesByTerm, s1); // tekrar dersi AKTS limitini aşırmasın

        // EDGE-02: Devam şartı yok — sınıf2 öğrencisi Atatürk İlkeleri-1'den (dönem1) kaldı ve tekrar alıyor; önceki
        // denemede devam şartını sağladığı için OBİS "devam aranmaz" döner. Ders Pazartesi 13:25-15:05'te Mantık
        // Devreleri-1 ile çakışır ama birinde devam şartı olmadığından çakışma SAYILMAZ (programda turuncu).
        var s2 = byClass[2][0];
        s2.EdgeCaseTag = "EDGE-02 devam-şartı-yok";
        MarkAsFailed(transcripts, s2.StudentNo, 1, CourseCatalog.Ataturk1);
        registrations.Add(new RegistrationEntry(s2.StudentNo, s2.CurrentTerm, CourseCatalog.Ataturk1, "NÖ/İÖ", "Beklemede", AttendanceRequired: false));

        // EDGE-03: Alttan ders önceliği — sınıf3 öğrencisi dönem1 Fizik-1'i geçemedi; ders bu güz açık (Salı 13:25,
        // öğrencinin programında boş) ama alttan almadan dönem5 derslerine devam ediyor (tekrar kaydı yok).
        var s3 = byClass[3][1];
        s3.EdgeCaseTag = "EDGE-03 alttan-ders";
        MarkAsFailed(transcripts, s3.StudentNo, 1, CourseCatalog.Physics1);

        // EDGE-04: Kredi/AKTS limiti aşımı — düşük GANO'lu sınıf2 öğrencisi normal
        // dönem yüküne ek olarak üç ekstra ders daha almış (toplam kredi limiti aşıyor).
        var s4 = byClass[2][1];
        s4.EdgeCaseTag = "EDGE-04 kredi-limiti";
        LowerGpaByFailingMany(transcripts, s4.StudentNo, s4.CurrentTerm);
        // Aynı öğrenci bu dönem bir dersten devamsızlık sınırını da aşmış (14 oturumdan 9'una girmiş, %36).
        var s4Course = coursesByTerm[s4.CurrentTerm].First(c => c.IsMandatory).CourseCode;
        attendance.RemoveAll(a => a.StudentNo == s4.StudentNo && a.CourseCode == s4Course);
        attendance.Add(new AttendanceRecord(s4.StudentNo, s4.CurrentTerm, s4Course, 14, 9));
        // Ek dersler 1. dönemden kalınmış derslerin tekrarı, öğrencinin programındaki boş saatlerde (Pazartesi ve Perşembe sabahı):
        // tek bulgu AKTS limiti olsun.
        foreach (var extra in new[] { CourseCatalog.Math1, CourseCatalog.LinearAlgebra })
        {
            MarkAsFailed(transcripts, s4.StudentNo, 1, extra);
            EnsureRegistered(registrations, s4.StudentNo, s4.CurrentTerm, extra, "NÖ/İÖ", "Beklemede");
        }

        // EDGE-05: Yanlış öğretim şubesi + alternatifli çakışma — ikinci öğretim (İÖ) öğrencisi Veri Haberleşmesi ve Ağlar'dan
        // (dönem5) kaldı ve alttan alıyor (NÖ/İÖ, Çarşamba 13:25-16:55). Veri Bilimi'ni kendi öğretimi yerine NÖ şubesinden
        // seçmiş: hem öğretim türü hatası hem saat çakışması var; sistem kendi öğretiminin İÖ şubesini (Çarşamba 17:00-19:25) önerir.
        var s5 = byClass[4][0];
        s5.EdgeCaseTag = "EDGE-05 alternatifli-çakışma";
        MarkAsFailed(transcripts, s5.StudentNo, 5, CourseCatalog.DataCommunication);
        EnsureRegistered(registrations, s5.StudentNo, s5.CurrentTerm, CourseCatalog.DataCommunication, "NÖ/İÖ", "Beklemede");
        var dataScience = registrations.FindIndex(x => x.StudentNo == s5.StudentNo && x.Term == s5.CurrentTerm && x.CourseCode == CourseCatalog.DataScience);
        if (dataScience >= 0) registrations[dataScience] = registrations[dataScience] with { Section = "NÖ" };
        DropOneElective(registrations, attendance, coursesByTerm, s5); // tekrar dersi (5 AKTS) AKTS limitini aşırmasın
        DropOneElective(registrations, attendance, coursesByTerm, s5);

        // EDGE-06: Tekrar alma (hata) — sınıf4 öğrencisi dönem5'te Mikroişlemciler'i AA ile geçmişken bu dönem yeniden
        // seçmiş. Yalnızca CB ve altı notla geçilen ders tekrar alınabilir. Ders Salı 08:30-12:00'de, programında boş.
        var s6 = byClass[4][1];
        s6.EdgeCaseTag = "EDGE-06 tekrar-alma";
        ForceGrade(transcripts, s6.StudentNo, 5, CourseCatalog.Microprocessors, "AA");
        EnsureRegistered(registrations, s6.StudentNo, s6.CurrentTerm, CourseCatalog.Microprocessors, "NÖ/İÖ", "Beklemede");
        DropOneElective(registrations, attendance, coursesByTerm, s6);
        DropOneElective(registrations, attendance, coursesByTerm, s6); // 5 AKTS tekrar dersi: tek hata tekrar alma olsun

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
        MarkAsFailed(transcripts, s8.StudentNo, 1, CourseCatalog.Math1);
        MarkAsFailed(transcripts, s8.StudentNo, 1, CourseCatalog.Physics1);
        MarkAsFailed(transcripts, s8.StudentNo, 3, CourseCatalog.DataStructures);
        PromoteCurrentTermToHistory(s8, registrations, transcripts, coursesByTerm, rnd);
        s8.CurrentTerm = 9;
        // Dönem9 (güz) kalan güz derslerini alttan alıyor; gerçek programda üçü de farklı saatlerde.
        foreach (var retake in new[] { CourseCatalog.Math1, CourseCatalog.Physics1, CourseCatalog.DataStructures })
        {
            EnsureRegistered(registrations, s8.StudentNo, 9, retake, "NÖ/İÖ", "Beklemede");
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
        // "Az kaldı" senaryosunu netleştir: dönem8'in 3 dersinden 1'ini daha önceden
        // tamamlanmış kabul et (transkripte geçir), sadece 2'si bu dönem gerçekten eksik/kayıtlı kalsın.
        var term8Regs = registrations.Where(r => r.StudentNo == s9.StudentNo && r.Term == 8).Select(r => r.CourseCode).OrderBy(c => c).ToList();
        foreach (var courseCode in term8Regs.Take(1))
        {
            var course = coursesByTerm[8].First(c => c.CourseCode == courseCode);
            transcripts.Add(new TranscriptEntry(s9.StudentNo, 8, course.CourseCode, course.CourseName, course.Credit, course.Ects, "BA", true));
            registrations.RemoveAll(r => r.StudentNo == s9.StudentNo && r.Term == 8 && r.CourseCode == courseCode);
            attendance.RemoveAll(a => a.StudentNo == s9.StudentNo && a.Term == 8 && a.CourseCode == courseCode);
        }

        // Geçmişte kalınmış dersler de geçilmiş sayılır: gerçekten yalnızca bu dönemin dersleri kalsın.
        PassAllFailed(transcripts, s9.StudentNo);

        // EDGE-10: Devamsızlık sınırına yaklaşma — sınıf2 öğrencisinin bu dönem bir dersten
        // devamsızlığı %28'e yaklaşmış (14 oturumdan 10'una girmiş).
        var s10 = byClass[2][2];
        s10.EdgeCaseTag = "EDGE-10 devamsızlık";
        var targetCourse = coursesByTerm[s10.CurrentTerm].First(c => c.IsMandatory).CourseCode;
        attendance.RemoveAll(a => a.StudentNo == s10.StudentNo && a.CourseCode == targetCourse);
        attendance.Add(new AttendanceRecord(s10.StudentNo, s10.CurrentTerm, targetCourse, 14, 10));
    }

    /// <summary>Güncel dönemde öğrencinin seçmeli derslerinden birini kayıttan çıkarır (eklenen tekrar dersinin AKTS'ini dengeler).</summary>
    private static void DropOneElective(
        List<RegistrationEntry> registrations, List<AttendanceRecord> attendance,
        Dictionary<int, List<CourseInfo>> coursesByTerm, StudentPlan plan)
    {
        var electiveCodes = coursesByTerm[plan.CurrentTerm].Where(c => !c.IsMandatory).Select(c => c.CourseCode).ToHashSet();
        var drop = registrations
            .Where(x => x.StudentNo == plan.StudentNo && x.Term == plan.CurrentTerm && electiveCodes.Contains(x.CourseCode))
            .OrderBy(x => x.CourseCode)
            .FirstOrDefault();
        if (drop is null) return;
        registrations.Remove(drop);
        attendance.RemoveAll(a => a.StudentNo == plan.StudentNo && a.Term == plan.CurrentTerm && a.CourseCode == drop.CourseCode);
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

    /// <summary>İlk iki dönemin ilk dörder dersini FF yapar: GANO 2.00'ın altına iner ve art arda iki dönem
    /// ders kalınmış olur (erken uyarı: düşük GANO + üst üste kalma).</summary>
    private static void LowerGpaByFailingMany(List<TranscriptEntry> transcripts, string studentNo, int currentTerm)
    {
        foreach (var term in new[] { 1, 2 }.Where(t => t < currentTerm))
        {
            var count = 0;
            for (var i = 0; i < transcripts.Count && count < 4; i++)
            {
                if (transcripts[i].StudentNo == studentNo && transcripts[i].Term == term)
                {
                    transcripts[i] = transcripts[i] with { LetterGrade = "FF", Passed = false };
                    count++;
                }
            }
        }
    }

    /// <summary>Öğrencinin kalınmış tüm derslerini CC ile geçilmiş yapar (EDGE-09: geçmişi temiz, gerçekten "az kalan").</summary>
    private static void PassAllFailed(List<TranscriptEntry> transcripts, string studentNo)
    {
        for (var i = 0; i < transcripts.Count; i++)
        {
            if (transcripts[i].StudentNo == studentNo && !transcripts[i].Passed)
            {
                transcripts[i] = transcripts[i] with { LetterGrade = "CC", Passed = true };
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
            // Ders başka dönemin seçmelisi olabilir (7. dönemde alınan 5. dönem seçmelisi): tüm katalogda aranır.
            var course = coursesByTerm.Values.SelectMany(c => c).FirstOrDefault(c => c.CourseCode == reg.CourseCode);
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
                SectionFor(plan, plan.CurrentTerm, registrations, sectionsByCourse, course.CourseCode, rnd), "Beklemede");
            if (!attendance.Any(a => a.StudentNo == plan.StudentNo && a.Term == plan.CurrentTerm && a.CourseCode == course.CourseCode))
            {
                attendance.Add(new AttendanceRecord(plan.StudentNo, plan.CurrentTerm, course.CourseCode, 14, 14 - rnd.Next(0, 3)));
            }
        }
    }
}
