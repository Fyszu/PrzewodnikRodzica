using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PrzewodnikRodzica.Api.Data;
using PrzewodnikRodzica.Api.Services;
using PrzewodnikRodzica.Core.Models;
using PrzewodnikRodzica.Core.Services;

namespace PrzewodnikRodzica.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentExtractionService _extractionService;
    private readonly GovSimulationService _simulationService;
    private readonly BenefitEngine _engine;
    private readonly AppSqliteStore _store;

    public DocumentsController(
        DocumentExtractionService extractionService,
        GovSimulationService simulationService,
        BenefitEngine engine,
        AppSqliteStore store)
    {
        _extractionService = extractionService;
        _simulationService = simulationService;
        _engine = engine;
        _store = store;
    }

    [HttpPost("analyze")]
    [EnableRateLimiting("pdf-upload")]
    [RequestSizeLimit(30_000_000)] // 30 MB
    public async Task<ActionResult<DocumentAnalysisResponse>> AnalyzePdf(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Nie przesłano pliku PDF." });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Obsługiwane są wyłącznie pliki w formacie PDF." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        try
        {
            var result = await _extractionService.AnalyzePdfAsync(ms.ToArray(), file.FileName, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("demo-cases")]
    public ActionResult<List<DemoTestCaseItem>> GetDemoCases() =>
        Ok(_simulationService.GetDemoTestCases());

    [HttpPost("demo-cases/{caseId}/run")]
    public ActionResult RunDemoCase(string caseId)
    {
        var demo = _simulationService.GetDemoTestCases()
            .FirstOrDefault(c => c.Id.Equals(caseId, StringComparison.OrdinalIgnoreCase));

        if (demo == null)
            return NotFound(new { error = "Nie znaleziono wskazanej sprawy testowej." });

        var parameters = _store.GetParameters();
        var calculation = _engine.Calculate(demo.RequestData, parameters);

        return Ok(new
        {
            demoCase = demo,
            calculation
        });
    }
}