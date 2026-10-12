using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GoldShopWeb.Data;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoldShopWeb.Tests;

/// <summary>
/// Boots the real GoldShopWeb pipeline against an ephemeral SQLite store and a
/// fake LINE channel secret, then drives /api/line/webhook with raw HTTP so we
/// can assert signature verification is enforced end-to-end.
/// </summary>
public class LineWebhookEndpointTests : IClassFixture<LineWebhookEndpointTests.Factory>
{
    private readonly Factory _factory;

    public LineWebhookEndpointTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Webhook_Returns200_WhenSignatureMatches()
    {
        var client = _factory.CreateClient();
        var body = """{"events":[]}""";
        var sig = Sign(_factory.ChannelSecret, body);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/line/webhook") { Content = content };
        msg.Headers.Add("X-Line-Signature", sig);

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Returns400_WhenSignatureMismatches()
    {
        var client = _factory.CreateClient();
        var body = """{"events":[]}""";
        var sig = Sign("wrong-secret", body);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/line/webhook") { Content = content };
        msg.Headers.Add("X-Line-Signature", sig);

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Returns400_WhenSignatureMissing()
    {
        var client = _factory.CreateClient();
        var body = """{"events":[]}""";

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/line/webhook") { Content = content };
        // intentionally no X-Line-Signature

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_Returns400_WhenBodyTamperedAfterSigning()
    {
        var client = _factory.CreateClient();
        var signedBody = """{"events":[]}""";
        var sig = Sign(_factory.ChannelSecret, signedBody);
        var tamperedBody = """{"events":[{"type":"unfollow"}]}""";

        using var content = new StringContent(tamperedBody, Encoding.UTF8, "application/json");
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/line/webhook") { Content = content };
        msg.Headers.Add("X-Line-Signature", sig);

        var response = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string Sign(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(body));
    }

    private static string Sign(string secret, string body) => Sign(secret, Encoding.UTF8.GetBytes(body));

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public string ChannelSecret { get; } = "test-channel-secret-" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), "goldshop-line-tests-" + Guid.NewGuid().ToString("N") + ".db");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Path"] = dbPath,
                    ["Database:SeedDemoData"] = "false",
                    ["LineApi:ChannelSecret"] = ChannelSecret,
                    ["LineApi:ChannelAccessToken"] = "",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Admin:Password"] = "test-password-not-real",
                });
            });
        }
    }
}