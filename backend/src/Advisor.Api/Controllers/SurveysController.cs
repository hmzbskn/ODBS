using System.Text;
using Advisor.Application.Surveys;
using Advisor.Domain.Enums;
using Advisor.Infrastructure.Surveys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advisor.Api.Controllers;

/// <summary>Anket ve ihtiyaç puanlama. Danışman/yönetici uçları yalnızca anketin sahibine (yönetici hepsine)
/// açıktır; öğrenci uçları yalnızca hedef kitledeki öğrenciye. Öğrenciye puanlar hiçbir zaman gönderilmez.</summary>
[ApiController]
[Route("api/surveys")]
public class SurveysController : ControllerBase
{
    private const string Staff = nameof(UserRole.Danisman) + "," + nameof(UserRole.Admin);

    private readonly SurveyService _surveys;

    public SurveysController(SurveyService surveys) => _surveys = surveys;

    // ---- danışman / yönetici ----

    [HttpPost]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<SurveyDefinition>> Create(CreateSurveyRequest request, CancellationToken ct) =>
        Ok(await _surveys.CreateAsync(request, ct));

    [HttpGet]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<List<SurveySummary>>> List(CancellationToken ct) => Ok(await _surveys.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<SurveyDefinition>> Get(Guid id, CancellationToken ct) => Ok(await _surveys.GetAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _surveys.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/scoring")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<SurveyDefinition>> UpdateScoring(Guid id, ScoringUpdateRequest request, CancellationToken ct) =>
        Ok(await _surveys.UpdateScoringAsync(id, request, ct));

    [HttpGet("{id:guid}/results")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<ResultsResponse>> Results(Guid id, CancellationToken ct) => Ok(await _surveys.GetResultsAsync(id, ct));

    [HttpPost("{id:guid}/support")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> AssignSupport(Guid id, AssignSupportRequest request, CancellationToken ct) =>
        Ok(new { updated = await _surveys.AssignSupportAsync(id, request, ct) });

    [HttpGet("{id:guid}/export")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) => CsvFile(await _surveys.ExportCsvAsync(id, ct), "ihtiyac-puanlari.csv");

    [HttpGet("{id:guid}/stats")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<StatsResponse>> Stats(Guid id, CancellationToken ct) => Ok(await _surveys.GetStatsAsync(id, ct));

    [HttpGet("{id:guid}/import-template")]
    [Authorize(Roles = Staff)]
    public async Task<IActionResult> ImportTemplate(Guid id, CancellationToken ct) =>
        CsvFile(await _surveys.ImportTemplateAsync(id, ct), "anket-iceaktarma-sablonu.csv");

    [HttpPost("{id:guid}/import")]
    [Authorize(Roles = Staff)]
    public async Task<ActionResult<ImportResult>> Import(Guid id, ImportRequest request, CancellationToken ct) =>
        Ok(await _surveys.ImportAsync(id, request, ct));

    // ---- öğrenci ----

    [HttpGet("mine")]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    public async Task<ActionResult<List<StudentSurveyItem>>> Mine(CancellationToken ct) => Ok(await _surveys.ListMineAsync(ct));

    [HttpGet("{id:guid}/form")]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    public async Task<ActionResult<SurveyForm>> Form(Guid id, CancellationToken ct) => Ok(await _surveys.GetFormAsync(id, ct));

    [HttpGet("consent-statement")]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    public IActionResult ConsentStatement() => Ok(new { statement = ConsentText.Statement });

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = nameof(UserRole.Ogrenci))]
    public async Task<IActionResult> Submit(Guid id, SubmitRequest request, CancellationToken ct)
    {
        await _surveys.SubmitAsync(id, request, ct);
        return NoContent();
    }

    // UTF-8 BOM: Türkçe Excel dosyayı doğru kodlamayla açsın.
    private FileContentResult CsvFile(string content, string fileName) =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray(), "text/csv; charset=utf-8", fileName);
}
