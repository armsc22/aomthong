using System.Net;
using System.Security.Cryptography;
using System.Text;
using GoldShopWeb.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoldShopWeb.Tests;

/// <summary>
/// End-to-end check that the webhook accepts a request signed with the real
/// ChannelSecret pulled at runtime from the project's user-secrets store.
/// This test does NOT inline any secret literal — the secret value is read
/// from the host's IConfiguration in the test factory's services.
/// Requires the dotnet user-secrets for project GoldShopWeb to be set with a
/// real LineApi:ChannelSecret; otherwise the test is skipped via Assert.Skip.
/// </summary>
public class LineWebhookRealSecretTests : IClassFixture<LineWebhookRealSecretTests.Factory>
{
    private readonly Factory _factory;

    public LineWebhookRealSecretTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Webhook_Accepts_RequestSignedWithRealChannelSecret()
    {
        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var realSecret = cfg["LineApi:ChannelSecret"];
        // This test is only meaningful when a real secret is loaded via user-secrets.
        Assert.False(
            string.IsNullOrEmpty(realSecret) || realSecret == "YOUR_LINE_CHANNEL_SECRET",
            "LineApi:ChannelSecret is not configured via user-secrets; cannot verify real-secret path.");

        var client = _factory.CreateClient();
        var body = """{"events":[]}""";
        var sig = Sign(realSecret!, Encoding.UTF8.GetBytes(body));

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/line/webhook") { Content = content };
        msg.Headers.Add("X-Line-Signature", sig);

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Sign(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(body));
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dbPath = Path.Combine(
                Path.GetTempPath(),
                "goldshop-realsecret-tests-" + Guid.NewGuid().ToString("N") + ".db");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                // Deliberately omit LineApi:ChannelSecret / AccessToken / AdminUserId
                // so the user-secrets layer (auto-loaded for Development env by the
                // host) provides the real values to IConfiguration.
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Path"] = dbPath,
                    ["Database:SeedDemoData"] = "false",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Admin:Password"] = "test-password-not-real",
                });
            });
        }
    }
}
