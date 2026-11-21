using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikRodzica.Api.Data;
using PrzewodnikRodzica.Api.Services;
using PrzewodnikRodzica.Core.Models;

namespace PrzewodnikRodzica.Api.Controllers;

public record AdminLoginRequest(string Pin);

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppSqliteStore _store;
    private readonly DocumentExtractionService _docService;
    private readonly IConfiguration _config;

    public AdminController(AppSqliteStore store, DocumentExtractionService docService, IConfiguration config)
    {
        _store = store;
        _docService = docService;
        _config = config;
    }

    [HttpGet("status")]
    public ActionResult GetAdminStatus() =>
        Ok(new { isAdmin = User.IsInRole("Admin") });

    [HttpPost("login")]
    public async Task<ActionResult> LoginAdmin([FromBody] AdminLoginRequest request)
    {
        string expectedPin = _config["AdminPanel:DemoPin"] ?? "ZUS2026";
        string providedPin = request.Pin ?? string.Empty;

        bool pinMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedPin),
            Encoding.UTF8.GetBytes(expectedPin));

        if (!pinMatches)
            return Unauthorized(new { error = "Nieprawidłowy kod PIN administratora." });

        // Zachowujemy istniejące claimy (np. aktywną sesję mObywatel) i dopisujemy rolę Admin
        var claims = User.Claims
            .Where(c => !(c.Type == ClaimTypes.Role && c.Value == "Admin"))
            .ToList();

        if (!claims.Any(c => c.Type == ClaimTypes.Name))
            claims.Add(new Claim(ClaimTypes.Name, "Administrator ZUS"));

        claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Ok(new { isAdmin = true });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAdmin()
    {
        // Jeśli użytkownik jest też zalogowany przez mObywatel, usuwamy tylko rolę Admin
        var remainingClaims = User.Claims
            .Where(c => !(c.Type == ClaimTypes.Role && c.Value == "Admin"))
            .ToList();

        bool hasMObywatelSession = remainingClaims.Any(c => c.Type == ClaimTypes.NameIdentifier);
        if (hasMObywatelSession)
        {
            var identity = new ClaimsIdentity(remainingClaims, CookieAuthenticationDefaults.AuthenticationScheme);
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

    [HttpGet("parameters")]
    public ActionResult<BenefitParameters> GetParameters() =>
        Ok(_store.GetParameters());

    [Authorize(Roles = "Admin")]
    [HttpPut("parameters")]
    public ActionResult<BenefitParameters> UpdateParameters([FromBody] BenefitParameters updated)
    {
        _store.SaveParameters(updated);
        return Ok(updated);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("parameters/reset")]
    public ActionResult<BenefitParameters> ResetParameters() =>
        Ok(_store.ResetParameters());

    [HttpGet("quota")]
    public ActionResult<QuotaStatus> GetQuota() =>
        Ok(_store.GetQuotaStatus(_docService.HardMonthlyPageBudget));
}