using System.Globalization;
using System.Text;
using Advisor.Application.Auth;
using Advisor.Application.Common;
using Advisor.Application.Management;
using Advisor.Application.Obis;
using Advisor.Application.Surveys;
using Advisor.Domain.Entities;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advisor.Infrastructure.Surveys;

public class SurveyService
{
    public const string AdminOwner = "ADMIN";
    private const int MaxImportRows = 5000;
    private const int MaxCsvLength = 1_000_000;
    private const int MaxTextLength = 1000;

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly AdvisorDbContext _db;
    private readonly IObisClient _obis;
    private readonly IStudentDirectory _students;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _time;

    public SurveyService(AdvisorDbContext db, IObisClient obis, IStudentDirectory students, ICurrentUser user, TimeProvider time)
    {
        _db = db;
        _obis = obis;
        _students = students;
        _user = user;
        _time = time;
    }

    // Türkiye saati (UTC+3, yaz saati uygulaması yok).
    private DateOnly Today => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime.AddHours(3));

    public static string StatusOf(Survey s, DateOnly today)
    {
        var start = DateOnly.FromDateTime(s.StartDate);
        var end = DateOnly.FromDateTime(s.EndDate);
        return today < start ? "Planned" : today > end ? "Closed" : "Open";
    }

    // ------------------------------------------------------------------ danışman: tanım

    public async Task<SurveyDefinition> CreateAsync(CreateSurveyRequest request, CancellationToken ct)
    {
        var owner = CurrentOwnerId();
        Validate(request);

        var survey = new Survey
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            StartDate = request.StartDate.ToDateTime(TimeOnly.MinValue),
            EndDate = request.EndDate.ToDateTime(TimeOnly.MinValue),
            CreatedByAdvisorId = owner,
            TargetClassYear = request.TargetClassYear,
        };

        for (var i = 0; i < request.Questions.Count; i++)
        {
            var q = request.Questions[i];
            var isChoice = q.Type is QuestionType.SingleChoice or QuestionType.MultiChoice;
            survey.Questions.Add(new SurveyQuestion
            {
                Id = Guid.NewGuid(),
                Text = q.Text.Trim(),
                Type = q.Type,
                Order = i,
                Weight = q.Weight,
                Required = q.Required,
                Options = isChoice
                    ? q.Options!.Select(o => new SurveyQuestionOption { Id = Guid.NewGuid(), Text = o.Text.Trim(), Score = o.Score }).ToList()
                    : new List<SurveyQuestionOption>(),
            });
        }

        _db.Surveys.Add(survey);
        await _db.SaveChangesAsync(ct);
        return await ToDefinitionAsync(survey, ct);
    }

    public async Task<List<SurveySummary>> ListAsync(CancellationToken ct)
    {
        var owner = CurrentOwnerFilter();
        var surveys = await _db.Surveys
            .Include(s => s.Questions)
            .Where(s => owner == null || s.CreatedByAdvisorId == owner)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        var counts = await _db.SurveyResponses
            .GroupBy(r => r.SurveyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var students = await _students.GetStudentsAsync(ct);

        return surveys
            .Select(s => new SurveySummary(
                s.Id, s.Title, DateOnly.FromDateTime(s.StartDate), DateOnly.FromDateTime(s.EndDate),
                StatusOf(s, Today), s.Questions.Count, TargetStudents(s, students).Count, counts.GetValueOrDefault(s.Id)))
            .ToList();
    }

    public async Task<SurveyDefinition> GetAsync(Guid id, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct);
        return await ToDefinitionAsync(survey, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct);
        if (await _db.SurveyResponses.AnyAsync(r => r.SurveyId == id, ct))
        {
            throw AppException.Conflict("Yanıt almış bir anket silinemez; kişisel verilerin korunması için yanıtlar saklanır.");
        }

        _db.Surveys.Remove(survey);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Soru ağırlıklarını ve seçenek puanlarını günceller. Puan her zaman cevaplardan canlı
    /// hesaplandığından yeni değerler mevcut yanıtlara da hemen yansır.</summary>
    public async Task<SurveyDefinition> UpdateScoringAsync(Guid id, ScoringUpdateRequest request, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct);
        foreach (var input in request.Questions)
        {
            var q = survey.Questions.FirstOrDefault(x => x.Id == input.QuestionId)
                ?? throw AppException.BadRequest("Puanlanmak istenen soru bu ankette yok.");
            if (!double.IsFinite(input.Weight) || Math.Abs(input.Weight) > 1000)
                throw AppException.BadRequest("Soru ağırlığı -1000 ile 1000 arasında geçerli bir sayı olmalıdır.");
            q.Weight = input.Weight;

            foreach (var opt in input.Options ?? new List<OptionScoreInput>())
            {
                var option = q.Options.FirstOrDefault(o => o.Id == opt.OptionId)
                    ?? throw AppException.BadRequest("Puanlanmak istenen seçenek bu soruda yok.");
                if (!double.IsFinite(opt.Score) || Math.Abs(opt.Score) > 1000)
                    throw AppException.BadRequest("Seçenek puanı -1000 ile 1000 arasında geçerli bir sayı olmalıdır.");
                option.Score = opt.Score;
            }
        }

        await _db.SaveChangesAsync(ct);
        return await ToDefinitionAsync(survey, ct);
    }

    // ------------------------------------------------------------------ danışman: sonuçlar

    public async Task<ResultsResponse> GetResultsAsync(Guid id, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct, includeResponses: true);
        var students = await _students.GetStudentsAsync(ct);
        var byNo = students.ToDictionary(s => s.StudentNo);
        var support = await _db.NeedScoreResults.Where(r => r.SurveyId == id).ToDictionaryAsync(r => r.StudentNo, r => r.SupportAssigned, ct);

        var scored = survey.Responses
            .Select(r => (Response: r, Score: SurveyScoring.Score(survey.Questions, r.Answers)))
            .OrderByDescending(x => x.Score.Total)
            .ThenBy(x => x.Response.StudentNo, StringComparer.Ordinal)
            .ToList();
        var ranks = SurveyScoring.Rank(scored.Select(x => x.Score.Total).ToList());

        var rows = scored
            .Select((x, i) =>
            {
                byNo.TryGetValue(x.Response.StudentNo, out var s);
                return new ResultRow(
                    ranks[i], x.Response.StudentNo, s is null ? x.Response.StudentNo : $"{s.FirstName} {s.LastName}",
                    s?.ClassYear ?? 0, x.Score.Total, x.Response.ImportedExternally,
                    support.GetValueOrDefault(x.Response.StudentNo), x.Score.Items);
            })
            .ToList();

        return new ResultsResponse(id, survey.Title, TargetStudents(survey, students).Count, rows.Count, rows);
    }

    /// <summary>Seçilen öğrencilere destek türü atar (boş tür = atamayı kaldırır). Yalnızca ankete
    /// yanıt vermiş öğrenciler seçilebilir.</summary>
    public async Task<int> AssignSupportAsync(Guid id, AssignSupportRequest request, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct, includeResponses: true);
        var support = string.IsNullOrWhiteSpace(request.SupportType) ? null : request.SupportType.Trim();
        if (support is { Length: > 100 }) throw AppException.BadRequest("Destek türü en fazla 100 karakter olabilir.");
        if (request.StudentNos is null || request.StudentNos.Count == 0)
            throw AppException.BadRequest("En az bir öğrenci seçmelisiniz.");

        var studentNos = request.StudentNos.Select(n => n.Trim()).Distinct().ToList();
        var responses = survey.Responses.ToDictionary(r => r.StudentNo);
        var unknown = studentNos.Where(n => !responses.ContainsKey(n)).ToList();
        if (unknown.Count > 0)
            throw AppException.BadRequest($"Şu öğrenciler bu ankete yanıt vermemiş: {string.Join(", ", unknown)}.");

        var existing = await _db.NeedScoreResults.Where(r => r.SurveyId == id && studentNos.Contains(r.StudentNo)).ToListAsync(ct);
        foreach (var no in studentNos)
        {
            var row = existing.FirstOrDefault(r => r.StudentNo == no);
            if (support is null)
            {
                if (row is not null) _db.NeedScoreResults.Remove(row);
                continue;
            }

            var total = SurveyScoring.Score(survey.Questions, responses[no].Answers).Total;
            if (row is null)
            {
                _db.NeedScoreResults.Add(new NeedScoreResult
                {
                    Id = Guid.NewGuid(), SurveyId = id, StudentNo = no, TotalScore = total,
                    ComputedAt = _time.GetUtcNow().UtcDateTime, SupportAssigned = support,
                });
            }
            else
            {
                row.TotalScore = total;
                row.ComputedAt = _time.GetUtcNow().UtcDateTime;
                row.SupportAssigned = support;
            }
        }

        await _db.SaveChangesAsync(ct);
        return studentNos.Count;
    }

    public async Task<string> ExportCsvAsync(Guid id, CancellationToken ct)
    {
        var results = await GetResultsAsync(id, ct);
        var sb = new StringBuilder();
        sb.AppendLine(Csv.Line(new[] { "Sıra", "Öğrenci No", "Ad Soyad", "Sınıf", "İhtiyaç Puanı", "Destek" }));
        foreach (var r in results.Rows)
        {
            sb.AppendLine(Csv.Line(new[]
            {
                r.Rank.ToString(CultureInfo.InvariantCulture), r.StudentNo, r.FullName,
                r.ClassYear.ToString(CultureInfo.InvariantCulture),
                r.TotalScore.ToString("0.##", CultureInfo.InvariantCulture), r.Support,
            }));
        }

        return sb.ToString();
    }

    public async Task<StatsResponse> GetStatsAsync(Guid id, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct, includeResponses: true);
        var students = await _students.GetStudentsAsync(ct);
        var answers = survey.Responses.SelectMany(r => r.Answers).ToLookup(a => a.QuestionId);

        var stats = new List<QuestionStat>();
        foreach (var q in survey.Questions.OrderBy(q => q.Order))
        {
            var given = answers[q.Id].ToList();
            switch (q.Type)
            {
                case QuestionType.SingleChoice:
                case QuestionType.MultiChoice:
                {
                    var picks = given.SelectMany(a => SurveyScoring.ParseOptionIds(a.SelectedOptionIds)).ToList();
                    var options = q.Options.Select(o => new OptionStat(o.Text, picks.Count(p => p == o.Id))).ToList();
                    stats.Add(new QuestionStat(q.Id, q.Text, q.Type, options, null, given.Count));
                    break;
                }
                case QuestionType.Number:
                {
                    var values = given.Where(a => a.NumberValue.HasValue).Select(a => a.NumberValue!.Value).ToList();
                    NumberStat? n = values.Count == 0 ? null : new NumberStat(values.Count, values.Min(), values.Max(), Math.Round(values.Average(), 2));
                    stats.Add(new QuestionStat(q.Id, q.Text, q.Type, null, n, values.Count));
                    break;
                }
                default:
                    // Serbest metin cevapları kimliksiz özette bile gösterilmez (kişi tanımlayabilir); yalnızca sayı.
                    stats.Add(new QuestionStat(q.Id, q.Text, q.Type, null, null, given.Count(a => !string.IsNullOrWhiteSpace(a.TextValue))));
                    break;
            }
        }

        return new StatsResponse(TargetStudents(survey, students).Count, survey.Responses.Count, stats);
    }

    // ------------------------------------------------------------------ danışman: CSV içe aktarma

    public async Task<string> ImportTemplateAsync(Guid id, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct);
        var header = new[] { "Öğrenci No" }.Concat(survey.Questions.OrderBy(q => q.Order).Select(q => q.Text));
        return Csv.Line(header) + Environment.NewLine;
    }

    public async Task<ImportResult> ImportAsync(Guid id, ImportRequest request, CancellationToken ct)
    {
        var survey = await LoadOwnedAsync(id, ct, includeResponses: true);
        if (!request.ConsentConfirmed)
            throw AppException.BadRequest("Verilerin öğrencilerin açık rızasıyla toplandığını onaylamadan içe aktarma yapılamaz.");
        if (string.IsNullOrWhiteSpace(request.CsvText)) throw AppException.BadRequest("Dosya boş.");
        if (request.CsvText.Length > MaxCsvLength) throw AppException.BadRequest("Dosya çok büyük (en fazla 1 MB).");

        var rows = Csv.Parse(request.CsvText, out _);
        if (rows.Count < 2) throw AppException.BadRequest("Dosyada başlık satırından sonra en az bir veri satırı olmalıdır.");
        if (rows.Count - 1 > MaxImportRows) throw AppException.BadRequest($"En fazla {MaxImportRows} satır içe aktarılabilir.");

        // Sütun eşleme: ilk sütun öğrenci no, diğerleri soru metniyle.
        var header = rows[0].Select(Norm).ToList();
        if (!StudentNoHeaders.Contains(header[0]))
            throw AppException.BadRequest("İlk sütunun başlığı 'Öğrenci No' olmalıdır. Şablonu indirip kullanabilirsiniz.");

        var questions = survey.Questions.OrderBy(q => q.Order).ToList();
        var columnOf = new Dictionary<Guid, int>();
        foreach (var q in questions)
        {
            var index = header.IndexOf(Norm(q.Text));
            if (index > 0) columnOf[q.Id] = index;
        }

        var missing = questions.Where(q => q.Required && !columnOf.ContainsKey(q.Id)).Select(q => $"'{q.Text}'").ToList();
        if (missing.Count > 0) throw AppException.BadRequest($"Zorunlu soru sütunları eksik: {string.Join(", ", missing)}.");

        var targets = TargetStudents(survey, await _students.GetStudentsAsync(ct)).Select(s => s.StudentNo).ToHashSet();
        var already = survey.Responses.Select(r => r.StudentNo).ToHashSet();
        var errors = new List<ImportError>();
        var imported = 0;
        var now = _time.GetUtcNow().UtcDateTime;

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNo = i + 1;
            var cells = rows[i];
            var studentNo = cells.Length > 0 ? cells[0].Trim() : string.Empty;
            var rowErrors = new List<string>();

            if (studentNo.Length == 0) rowErrors.Add("Öğrenci numarası boş.");
            else if (!targets.Contains(studentNo)) rowErrors.Add("Bu öğrenci anketin hedef kitlesinde değil ya da size bağlı değil.");
            else if (already.Contains(studentNo)) rowErrors.Add("Bu öğrenci için zaten yanıt var.");

            var answers = new List<SurveyAnswer>();
            foreach (var q in questions)
            {
                var raw = columnOf.TryGetValue(q.Id, out var col) && col < cells.Length ? cells[col].Trim() : string.Empty;
                if (raw.Length == 0)
                {
                    if (q.Required) rowErrors.Add($"'{q.Text}' sorusu zorunlu.");
                    continue;
                }

                var parsed = ParseCell(q, raw, out var cellError);
                if (cellError is not null) rowErrors.Add(cellError);
                else if (parsed is not null) answers.Add(parsed);
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors.Select(m => new ImportError(rowNo, studentNo.Length == 0 ? null : studentNo, m)));
                continue;
            }

            var response = new SurveyResponse
            {
                Id = Guid.NewGuid(), SurveyId = id, StudentNo = studentNo, ConsentAccepted = true,
                ConsentAcceptedAt = now, SubmittedAt = now, ImportedExternally = true,
            };
            foreach (var a in answers)
            {
                a.Id = Guid.NewGuid();
                response.Answers.Add(a);
            }

            _db.SurveyResponses.Add(response);
            already.Add(studentNo);
            imported++;
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(imported, errors);
    }

    private static readonly HashSet<string> StudentNoHeaders = new()
    {
        "öğrenci no", "ogrenci no", "öğrenci numarası", "ogrenci numarasi", "studentno", "student no", "no",
    };

    private static string Norm(string s) => Tr.TextInfo.ToLower(s.Trim());

    private static SurveyAnswer? ParseCell(SurveyQuestion q, string raw, out string? error)
    {
        error = null;
        switch (q.Type)
        {
            case QuestionType.SingleChoice:
            case QuestionType.MultiChoice:
            {
                var parts = q.Type == QuestionType.MultiChoice
                    ? raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : new[] { raw };
                var ids = new List<Guid>();
                foreach (var part in parts)
                {
                    var option = q.Options.FirstOrDefault(o => Norm(o.Text) == Norm(part));
                    if (option is null)
                    {
                        error = $"'{q.Text}': \"{part}\" geçerli bir seçenek değil. Seçenekler: {string.Join(" | ", q.Options.Select(o => o.Text))}.";
                        return null;
                    }

                    if (!ids.Contains(option.Id)) ids.Add(option.Id);
                }

                return new SurveyAnswer { QuestionId = q.Id, SelectedOptionIds = SurveyScoring.JoinOptionIds(ids) };
            }
            case QuestionType.Number:
            {
                if (!TryParseNumber(raw, out var value))
                {
                    error = $"'{q.Text}': \"{raw}\" geçerli bir sayı değil.";
                    return null;
                }

                return new SurveyAnswer { QuestionId = q.Id, NumberValue = value };
            }
            default:
                if (raw.Length > MaxTextLength)
                {
                    error = $"'{q.Text}': metin en fazla {MaxTextLength} karakter olabilir.";
                    return null;
                }

                return new SurveyAnswer { QuestionId = q.Id, TextValue = raw };
        }
    }

    private static bool TryParseNumber(string raw, out double value) =>
        (double.TryParse(raw, NumberStyles.Float, Tr, out value) || double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        && double.IsFinite(value) && Math.Abs(value) <= 1_000_000_000;

    // ------------------------------------------------------------------ öğrenci

    public async Task<List<StudentSurveyItem>> ListMineAsync(CancellationToken ct)
    {
        var student = await CurrentStudentAsync(ct);
        var submitted = await _db.SurveyResponses.Where(r => r.StudentNo == student.StudentNo).Select(r => r.SurveyId).ToListAsync(ct);
        var surveys = await _db.Surveys.OrderByDescending(s => s.StartDate).ToListAsync(ct);

        return surveys
            .Where(s => IsTargeted(s, student))
            .Select(s => (Survey: s, Status: StatusOf(s, Today), Submitted: submitted.Contains(s.Id)))
            .Where(x => x.Status == "Open" || x.Submitted)
            .Select(x => new StudentSurveyItem(
                x.Survey.Id, x.Survey.Title, x.Survey.Description,
                DateOnly.FromDateTime(x.Survey.StartDate), DateOnly.FromDateTime(x.Survey.EndDate), x.Status, x.Submitted))
            .ToList();
    }

    public async Task<SurveyForm> GetFormAsync(Guid id, CancellationToken ct)
    {
        var (survey, student) = await LoadForStudentAsync(id, ct);
        await EnsureNotSubmittedAsync(id, student.StudentNo, ct);

        // Puanlar öğrenciye ASLA gönderilmez: yalnızca soru ve seçenek metinleri.
        var questions = survey.Questions
            .OrderBy(q => q.Order)
            .Select(q => new FormQuestionDto(q.Id, q.Text, q.Type, q.Required, q.Options.Select(o => new FormOptionDto(o.Id, o.Text)).ToList()))
            .ToList();
        return new SurveyForm(survey.Id, survey.Title, survey.Description, DateOnly.FromDateTime(survey.EndDate), ConsentText.Text, ConsentText.Statement, questions);
    }

    public async Task SubmitAsync(Guid id, SubmitRequest request, CancellationToken ct)
    {
        var (survey, student) = await LoadForStudentAsync(id, ct);
        await EnsureNotSubmittedAsync(id, student.StudentNo, ct);

        if (!request.Consent)
            throw AppException.BadRequest("KVKK açık rıza onayı verilmeden anket gönderilemez.");

        var given = (request.Answers ?? new List<AnswerInput>()).GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.First());
        var unknown = given.Keys.FirstOrDefault(k => survey.Questions.All(q => q.Id != k));
        if (unknown != Guid.Empty) throw AppException.BadRequest("Gönderilen cevaplardan biri bu ankete ait olmayan bir soruya ait.");

        var answers = new List<SurveyAnswer>();
        foreach (var q in survey.Questions.OrderBy(q => q.Order))
        {
            given.TryGetValue(q.Id, out var input);
            var answer = BuildAnswer(q, input);
            if (answer is not null) answers.Add(answer);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var response = new SurveyResponse
        {
            Id = Guid.NewGuid(), SurveyId = id, StudentNo = student.StudentNo,
            ConsentAccepted = true, ConsentAcceptedAt = now, SubmittedAt = now,
        };
        foreach (var a in answers)
        {
            a.Id = Guid.NewGuid();
            response.Answers.Add(a);
        }

        _db.SurveyResponses.Add(response);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Eşzamanlı çift gönderim: (SurveyId, StudentNo) tekil indeksi ikinci kaydı reddeder.
            throw AppException.Conflict("Bu anketi zaten yanıtladınız.");
        }
    }

    private static SurveyAnswer? BuildAnswer(SurveyQuestion q, AnswerInput? input)
    {
        var label = $"'{q.Text}'";
        switch (q.Type)
        {
            case QuestionType.SingleChoice:
            case QuestionType.MultiChoice:
            {
                var ids = (input?.OptionIds ?? new List<Guid>()).Distinct().ToList();
                if (ids.Count == 0)
                {
                    if (q.Required) throw AppException.BadRequest($"{label} sorusunu yanıtlamalısınız.");
                    return null;
                }

                if (q.Type == QuestionType.SingleChoice && ids.Count != 1)
                    throw AppException.BadRequest($"{label} sorusunda yalnızca bir seçenek işaretlenebilir.");
                if (ids.Any(i => q.Options.All(o => o.Id != i)))
                    throw AppException.BadRequest($"{label} sorusu için geçersiz bir seçenek gönderildi.");
                return new SurveyAnswer { QuestionId = q.Id, SelectedOptionIds = SurveyScoring.JoinOptionIds(ids) };
            }
            case QuestionType.Number:
            {
                if (input?.Number is null)
                {
                    if (q.Required) throw AppException.BadRequest($"{label} sorusunu yanıtlamalısınız.");
                    return null;
                }

                if (!double.IsFinite(input.Number.Value) || Math.Abs(input.Number.Value) > 1_000_000_000)
                    throw AppException.BadRequest($"{label} sorusu için geçerli bir sayı girin.");
                return new SurveyAnswer { QuestionId = q.Id, NumberValue = input.Number };
            }
            default:
            {
                var text = input?.Text?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    if (q.Required) throw AppException.BadRequest($"{label} sorusunu yanıtlamalısınız.");
                    return null;
                }

                if (text.Length > MaxTextLength)
                    throw AppException.BadRequest($"{label} sorusunun cevabı en fazla {MaxTextLength} karakter olabilir.");
                return new SurveyAnswer { QuestionId = q.Id, TextValue = text };
            }
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    private string CurrentOwnerId() => _user.Role switch
    {
        UserRole.Admin => AdminOwner,
        UserRole.Danisman when _user.LinkedAdvisorId is not null => _user.LinkedAdvisorId,
        _ => throw AppException.Forbidden(),
    };

    /// <summary>Listeleme süzgeci: yönetici hepsini (null), danışman yalnızca kendi anketlerini görür.</summary>
    private string? CurrentOwnerFilter() => _user.Role == UserRole.Admin ? null : CurrentOwnerId();

    private async Task<Survey> LoadOwnedAsync(Guid id, CancellationToken ct, bool includeResponses = false)
    {
        IQueryable<Survey> query = _db.Surveys.AsSplitQuery().Include(s => s.Questions).ThenInclude(q => q.Options);
        if (includeResponses) query = query.Include(s => s.Responses).ThenInclude(r => r.Answers);

        var survey = await query.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("Anket bulunamadı.");

        if (_user.Role != UserRole.Admin && survey.CreatedByAdvisorId != _user.LinkedAdvisorId)
            throw AppException.Forbidden("Bu anket size ait değil.");
        return survey;
    }

    private async Task<SurveyDefinition> ToDefinitionAsync(Survey s, CancellationToken ct)
    {
        var students = await _students.GetStudentsAsync(ct);
        var responseCount = await _db.SurveyResponses.CountAsync(r => r.SurveyId == s.Id, ct);
        var questions = s.Questions
            .OrderBy(q => q.Order)
            .Select(q => new QuestionDto(
                q.Id, q.Text, q.Type, q.Order, q.Weight, q.Required,
                q.Options.Select(o => new OptionDto(o.Id, o.Text, o.Score)).ToList()))
            .ToList();
        return new SurveyDefinition(
            s.Id, s.Title, s.Description, DateOnly.FromDateTime(s.StartDate), DateOnly.FromDateTime(s.EndDate),
            StatusOf(s, Today), s.TargetClassYear, s.CreatedByAdvisorId, TargetStudents(s, students).Count, responseCount, questions);
    }

    private static List<ObisStudent> TargetStudents(Survey s, IEnumerable<ObisStudent> students) =>
        students.Where(st => IsTargeted(s, st)).ToList();

    private static bool IsTargeted(Survey s, ObisStudent st) =>
        (s.CreatedByAdvisorId == AdminOwner || s.CreatedByAdvisorId == st.AdvisorId)
        && (s.TargetClassYear is null || s.TargetClassYear == st.ClassYear);

    private async Task<ObisStudent> CurrentStudentAsync(CancellationToken ct)
    {
        if (_user.Role != UserRole.Ogrenci || _user.LinkedStudentNo is null) throw AppException.Forbidden();
        return await _students.GetStudentAsync(_user.LinkedStudentNo, ct)
            ?? throw AppException.NotFound("Öğrenci kaydınız OBİS'te bulunamadı.");
    }

    private async Task<(Survey Survey, ObisStudent Student)> LoadForStudentAsync(Guid id, CancellationToken ct)
    {
        var student = await CurrentStudentAsync(ct);
        var survey = await _db.Surveys.AsSplitQuery().Include(s => s.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw AppException.NotFound("Anket bulunamadı.");

        // Hedef kitlede olmayan öğrenciye anketin varlığı bile bildirilmez.
        if (!IsTargeted(survey, student)) throw AppException.NotFound("Anket bulunamadı.");
        if (StatusOf(survey, Today) != "Open") throw AppException.BadRequest("Anket şu anda yanıtlamaya açık değil.");
        return (survey, student);
    }

    private async Task EnsureNotSubmittedAsync(Guid surveyId, string studentNo, CancellationToken ct)
    {
        if (await _db.SurveyResponses.AnyAsync(r => r.SurveyId == surveyId && r.StudentNo == studentNo, ct))
            throw AppException.Conflict("Bu anketi zaten yanıtladınız.");
    }

    private static void Validate(CreateSurveyRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200)
            throw AppException.BadRequest("Anket başlığı zorunludur ve en fazla 200 karakter olabilir.");
        if (r.Description is { Length: > 2000 })
            throw AppException.BadRequest("Açıklama en fazla 2000 karakter olabilir.");
        if (r.EndDate < r.StartDate)
            throw AppException.BadRequest("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        if (r.TargetClassYear is < 1 or > 4)
            throw AppException.BadRequest("Hedef sınıf 1 ile 4 arasında olmalıdır.");
        if (r.Questions is null || r.Questions.Count == 0 || r.Questions.Count > 30)
            throw AppException.BadRequest("Ankette 1 ile 30 arasında soru olmalıdır.");

        var seen = new HashSet<string>();
        for (var i = 0; i < r.Questions.Count; i++)
        {
            var q = r.Questions[i];
            var no = i + 1;
            if (string.IsNullOrWhiteSpace(q.Text) || q.Text.Trim().Length > 300)
                throw AppException.BadRequest($"{no}. soru metni zorunludur ve en fazla 300 karakter olabilir.");
            if (!seen.Add(Norm(q.Text)))
                throw AppException.BadRequest($"{no}. soru metni başka bir soruyla aynı; sorular ayırt edilebilmelidir.");
            if (!double.IsFinite(q.Weight) || Math.Abs(q.Weight) > 1000)
                throw AppException.BadRequest($"{no}. sorunun ağırlığı -1000 ile 1000 arasında olmalıdır.");

            var isChoice = q.Type is QuestionType.SingleChoice or QuestionType.MultiChoice;
            if (!isChoice)
            {
                if (q.Options is { Count: > 0 })
                    throw AppException.BadRequest($"{no}. soru seçim sorusu değil; seçenek tanımlanamaz.");
                continue;
            }

            if (q.Options is null || q.Options.Count < 2 || q.Options.Count > 20)
                throw AppException.BadRequest($"{no}. soru için 2 ile 20 arasında seçenek tanımlayın.");
            var optionTexts = new HashSet<string>();
            foreach (var o in q.Options)
            {
                if (string.IsNullOrWhiteSpace(o.Text) || o.Text.Trim().Length > 200)
                    throw AppException.BadRequest($"{no}. sorudaki seçenek metinleri zorunludur ve en fazla 200 karakter olabilir.");
                if (o.Text.Contains('|'))
                    throw AppException.BadRequest($"{no}. sorudaki seçenek metni '|' karakterini içeremez (CSV içe aktarmada ayırıcıdır).");
                if (!double.IsFinite(o.Score) || Math.Abs(o.Score) > 1000)
                    throw AppException.BadRequest($"{no}. sorudaki seçenek puanı -1000 ile 1000 arasında olmalıdır.");
                if (!optionTexts.Add(Norm(o.Text)))
                    throw AppException.BadRequest($"{no}. soruda aynı metinli iki seçenek var.");
            }
        }
    }
}
