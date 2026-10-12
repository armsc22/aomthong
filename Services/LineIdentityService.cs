using System.Text.Json;
namespace GoldShopWeb.Services;

public sealed record VerifiedLineIdentity(string UserId, string Name, long ExpiresAt);

public class LineIdentityService(HttpClient client, IConfiguration configuration)
{
    public async Task<VerifiedLineIdentity?> VerifyAsync(string? idToken, CancellationToken cancellationToken = default)
    {
        var channelId = configuration["LineApi:LoginChannelId"];
        if (string.IsNullOrWhiteSpace(channelId) || string.IsNullOrWhiteSpace(idToken) || idToken.Length > 10000) return null;
        try
        {
            using var body = new FormUrlEncodedContent(new Dictionary<string,string> { ["id_token"] = idToken, ["client_id"] = channelId });
            using var response = await client.PostAsync("https://api.line.me/oauth2/v2.1/verify", body, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            if (root.GetProperty("iss").GetString() != "https://access.line.me" || root.GetProperty("aud").GetString() != channelId) return null;
            var expires = root.GetProperty("exp").GetInt64();
            var userId = root.GetProperty("sub").GetString();
            if (expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || string.IsNullOrWhiteSpace(userId)) return null;
            return new(userId, root.TryGetProperty("name", out var name) ? name.GetString() ?? "LINE" : "LINE", expires);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return null; }
    }
}
