using System.Text.Json;
using DineMaster.Api.Extensions;
using DineMaster.Application.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    // Provayderlar javob maydonlarini aynan o'z nomlari bilan kutadi (snake_case, null qiymatlar ham)
    private static readonly JsonSerializerOptions RawJson = new();

    private readonly PaymentService _payments;
    private readonly PaymentOptions _options;

    public PaymentsController(PaymentService payments, IOptions<PaymentOptions> options)
    {
        _payments = payments;
        _options = options.Value;
    }

    /// <summary>Qaysi to'lov usullari yoqilgan (frontend tugmalari uchun).</summary>
    [HttpGet("providers")]
    public IActionResult Providers() => Ok(new { providers = _options.EnabledProviders });

    /// <summary>Bron depoziti uchun to'lov sahifasi havolasini yaratadi.</summary>
    [HttpPost("deposit")]
    [Authorize]
    public async Task<IActionResult> Deposit([FromBody] DepositCheckoutRequest req, CancellationToken ct)
    {
        var result = await _payments.CreateDepositCheckoutAsync(User.GetUserIdOrThrow(), req.ReservationId, req.Provider, ct);
        return Ok(new { provider = result.Provider, redirectUrl = result.RedirectUrl, paid = result.Paid });
    }

    /// <summary>Payme Merchant API (JSON-RPC). Kassa sozlamalarida endpoint: /api/payments/payme</summary>
    [HttpPost("payme")]
    [AllowAnonymous]
    public async Task<IActionResult> Payme(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var response = await _payments.HandlePaymeAsync(Request.Headers.Authorization.ToString(), body, ct);
        return Content(JsonSerializer.Serialize(response, RawJson), "application/json");
    }

    /// <summary>Click SHOP API — Prepare (action=0).</summary>
    [HttpPost("click/prepare")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ClickPrepare(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var response = await _payments.HandleClickPrepareAsync(form.ToDictionary(k => k.Key, v => v.Value.ToString()), ct);
        return Content(JsonSerializer.Serialize(response, RawJson), "application/json");
    }

    /// <summary>Click SHOP API — Complete (action=1).</summary>
    [HttpPost("click/complete")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ClickComplete(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var response = await _payments.HandleClickCompleteAsync(form.ToDictionary(k => k.Key, v => v.Value.ToString()), ct);
        return Content(JsonSerializer.Serialize(response, RawJson), "application/json");
    }
}

public record DepositCheckoutRequest(Guid ReservationId, string Provider);
