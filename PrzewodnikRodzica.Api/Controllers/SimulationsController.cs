using Microsoft.AspNetCore.Mvc;
using PrzewodnikRodzica.Api.Data;
using PrzewodnikRodzica.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PrzewodnikRodzica.Api.Controllers;

public record MObywatelLoginRequest(string PersonaId);
public record PueQuestionRequest(string Question);
public record ApplicationSubmitRequest(
    string Category,      // "URLOP_PRACODAWCA" | "ZUS_ZAM" | "ZUS_ZAS53" | "USC_AKT"
    string ApplicantName,
    string ChildNameOrEventDate,
    string Recipient,
    string Details);

[ApiController]
[Route("api/[controller]")]
public class SimulationsController : ControllerBase
{
    private readonly GovSimulationService _gov;
    private readonly AppSqliteStore _store;

    public SimulationsController(GovSimulationService gov, AppSqliteStore store)
    {
        _gov = gov;
        _store = store;
    }

    [HttpGet("mobywatel/personas")]
    public ActionResult GetPersonas() => Ok(_gov.GetMObywatelPersonas().Select(p => new
    {
        p.PersonaId,
        p.FullName,
        p.InsuranceTitleCode,
        p.InsuranceTitleDescription,
        p.SocialSecurityDebtPln
    }));

    [HttpPost("mobywatel/login")]
    public async Task<ActionResult<SimulatedZusAccount>> LoginMObywatel([FromBody] MObywatelLoginRequest request)
    {
        var account = _gov.GetPersonaById(request.PersonaId);
        if (account == null)
            return NotFound(new { error = "Nieznana persona mObywatel." });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.PersonaId),
            new(ClaimTypes.Name, account.FullName)
        };

        if (User.IsInRole("Admin"))
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Ok(account);
    }

    [HttpPost("mobywatel/logout")]
    public async Task<IActionResult> LogoutMObywatel()
    {
        if (User.IsInRole("Admin"))
        {
            var adminOnlyClaims = new List<Claim>
            {
                new(ClaimTypes.Name, "Administrator ZUS"),
                new(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(adminOnlyClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
        }
        else
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return NoContent();
    }

    [HttpGet("mobywatel/me")]
    public ActionResult GetCurrentSession()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Ok(new { authenticated = false, account = (SimulatedZusAccount?)null });

        string? personaId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var account = string.IsNullOrWhiteSpace(personaId) ? null : _gov.GetPersonaById(personaId);

        return Ok(new { authenticated = account != null, account });
    }

    [HttpPost("pue/question")]
    public ActionResult AskPue([FromBody] PueQuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Wpisz treść pytania do PUE/eZUS." });

        string answer = _gov.AnswerPueQuestionDeterministic(request.Question);
        var record = new SubmittedApplicationRecord(
            $"PUE-{DateTime.UtcNow:yyyyMMddHHmmss}",
            "PUE_PYTANIE",
            "Zapytanie przez symulację PUE/eZUS",
            "Zakład Ubezpieczeń Społecznych (PUE/eZUS)",
            request.Question.Trim(),
            "Odpowiedziano automatycznie (symulacja)",
            DateTime.UtcNow);

        _store.SaveApplication(record);

        return Ok(new
        {
            ticketId = record.Id,
            answer,
            contactChannels = new { hotline = "22 560 16 00", email = "mama@zus.pl", eVisit = "https://www.zus.pl/e-wizyta" }
        });
    }

    [HttpPost("applications/submit")]
    public ActionResult<SubmittedApplicationRecord> SubmitApplication([FromBody] ApplicationSubmitRequest request)
    {
        string title = request.Category switch
        {
            "URLOP_PRACODAWCA" => "Wniosek pracowniczy o urlop związany z rodzicielstwem (KP)",
            "ZUS_ZAM" => "Zintegrowany wniosek o zasiłek macierzyński (druk ZAM)",
            "ZUS_ZAS53" => "Wniosek o zasiłek chorobowy w ciąży (druk ZAS-53)",
            "USC_AKT" => "Wniosek o odpis skrócony/zupełny aktu urodzenia (USC)",
            _ => "Wniosek o świadczenie rodzinne"
        };

        string prefix = request.Category == "USC_AKT" ? "USC" : "WNI";
        var record = new SubmittedApplicationRecord(
            $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Random.Shared.Next(100, 999)}",
            request.Category,
            title,
            string.IsNullOrWhiteSpace(request.Recipient) ? "ZUS (eZUS)" : request.Recipient,
            $"Wnioskodawca: {request.ApplicantName} | Zdarzenie/Dziecko: {request.ChildNameOrEventDate} | {request.Details}",
            "Złożono elektronicznie (symulacja UPO wygenerowana)",
            DateTime.UtcNow);

        _store.SaveApplication(record);
        return Ok(record);
    }

    [HttpGet("applications")]
    public ActionResult<List<SubmittedApplicationRecord>> GetApplications() =>
        Ok(_store.GetSubmittedApplications());
}