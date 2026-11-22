using Microsoft.AspNetCore.Mvc;
using PrzewodnikRodzica.Api.Data;
using PrzewodnikRodzica.Core.Models;
using PrzewodnikRodzica.Core.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace PrzewodnikRodzica.Api.Controllers;

public record SaveCalculationRequest(string Name, CalculationRequest Request);

[ApiController]
[Route("api/[controller]")]
public class CalculatorController : ControllerBase
{
    private readonly BenefitEngine _engine;
    private readonly AppSqliteStore _store;

    public CalculatorController(BenefitEngine engine, AppSqliteStore store)
    {
        _engine = engine;
        _store = store;
    }

    [HttpPost("calculate")]
    public ActionResult<CalculationResult> Calculate([FromBody] CalculationRequest request)
    {
        var parameters = _store.GetParameters();
        var result = _engine.Calculate(request, parameters);
        return Ok(result);
    }

    [HttpGet("charities")]
    public ActionResult<List<CharityOrganization>> GetCharities() =>
        Ok(CharitySupportCatalog.GetAll());

    [HttpGet("links")]
    public ActionResult<List<ExternalLinkItem>> GetOfficialLinks() =>
        Ok(CharitySupportCatalog.GetOfficialLinks());

    [HttpGet("ai-disclosure")]
    public ActionResult GetAiDisclosure() => Ok(new
    {
        UsesAi = true,
        Scope = new[]
        {
            new
            {
                Module = "Analiza wgrywanych wniosków i zaœwiadczeñ PDF",
                Technology = "Azure AI Document Intelligence (model prebuilt-layout — OCR)",
                Role = "Wy³¹cznie ekstrakcja strukturalna tekstu, tabel i kratek wyboru ze skanów PDF do ujednoliconego formatu Markdown. Mo¿e zawieraæ b³êdy odczytu tekstu, zw³aszcza przy niewyraŸnych skanach."
            }
        }
    });

    [Authorize]
    [HttpGet("saved")]
    public ActionResult<List<SavedCalculationRecord>> GetSavedCalculations()
    {
        string? personaId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(personaId))
            return Unauthorized();

        return Ok(_store.GetSavedCalculations(personaId));
    }

    [Authorize]
    [HttpPost("saved")]
    public ActionResult<SavedCalculationRecord> SaveCalculation([FromBody] SaveCalculationRequest payload)
    {
        string? personaId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(personaId))
            return Unauthorized();

        string scenarioName = string.IsNullOrWhiteSpace(payload.Name)
            ? $"Symulacja {DateTime.Now:dd.MM HH:mm}"
            : payload.Name.Trim();

        var parameters = _store.GetParameters();
        var calc = _engine.Calculate(payload.Request, parameters);

        var record = new SavedCalculationRecord(
            $"SYM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            personaId,
            scenarioName,
            payload.Request,
            calc.TotalCombinedLeaveDays,
            calc.TotalEstimatedBenefitsGross,
            calc.PrimaryParentBase.BenefitBase,
            calc.EligibilityHeadline,
            DateTime.UtcNow);

        _store.SaveCalculation(record);
        return Ok(record);
    }

    [Authorize]
    [HttpDelete("saved/{id}")]
    public ActionResult DeleteSavedCalculation(string id)
    {
        string? personaId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(personaId))
            return Unauthorized();

        bool deleted = _store.DeleteSavedCalculation(personaId, id);
        return deleted ? NoContent() : NotFound(new { error = "Nie znaleziono zapisanej symulacji." });
    }
}