using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

/// <summary>
/// Receives LINE Platform webhooks. Every request must carry an
/// <c>X-Line-Signature</c> header whose value is the Base64 of
/// <c>HMAC-SHA256(ChannelSecret, rawRequestBody)</c>. We reject any
/// request whose signature does not match before doing any further work.
/// </summary>
[ApiController]
[Route("api/line")]
public class LineWebhookController : ControllerBase
{
    private readonly LineMessagingService _line;
    private readonly IConfiguration _config;
    private readonly ILogger<LineWebhookController> _log;

    public LineWebhookController(LineMessagingService line, IConfiguration config, ILogger<LineWebhookController> log)
    {
        _line = line;
        _config = config;
        _log = log;
    }

    [HttpPost("webhook")]
    [Consumes("application/json")]
    [RequestSizeLimit(1048576)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var channelSecret = _config["LineApi:ChannelSecret"];
        var signature = Request.Headers["X-Line-Signature"].ToString();

        // Allow buffering so we hash the exact bytes LINE sent.
        Request.EnableBuffering();
        Request.Body.Position = 0;
        byte[] body;
        using (var ms = new MemoryStream())
        {
            await Request.Body.CopyToAsync(ms, ct);
            body = ms.ToArray();
        }

        if (string.IsNullOrWhiteSpace(channelSecret) || channelSecret.StartsWith("YOUR_", StringComparison.Ordinal))
            return StatusCode(503, new { error = "webhook is not configured" });
        if (string.IsNullOrEmpty(signature) || !VerifySignature(channelSecret, body, signature))
            return BadRequest(new { error = "invalid signature" });
        JsonElement payload;
        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "LINE webhook body is not valid JSON");
            return BadRequest(new { error = "invalid json" });
        }

        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
        {
            foreach (var evt in events.EnumerateArray())
            {
                var type = evt.TryGetProperty("type", out var t) ? t.GetString() : null;
                var sourceUserId = evt.TryGetProperty("source", out var src) && src.TryGetProperty("userId", out var u)
                    ? u.GetString() : null;
                var replyToken = evt.TryGetProperty("replyToken", out var r) ? r.GetString() : null;

                _log.LogInformation("LINE event: type={Type} userId={UserId}", type, sourceUserId);

                if (type == "follow" && !string.IsNullOrEmpty(replyToken))
                {
                    await _line.ReplyWelcomeMessageAsync(replyToken);
                }
            }
        }

        return Ok();
    }

    public static bool VerifySignature(string channelSecret, byte[] body, string signatureBase64)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        var expected = hmac.ComputeHash(body);
        byte[] supplied;
        try
        {
            supplied = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
