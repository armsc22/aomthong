using System.Net;
using System.Net.Http.Json;
using System.Text;
using GoldShopWeb.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoldShopWeb.Tests;

/// <summary>
/// Verifies the Account/Register flow now refuses to bind a LINE user id that
/// was not first recorded in the session via /Account/BindLineUserId with a
/// matching nonce. This is the server-side part of owner verification.
/// </summary>
public class LineOwnerBindingTests : IClassFixture<LineOwnerBindingTests.Factory>
{
    private readonly Factory _factory;
    public LineOwnerBindingTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Register_Rejects_LineUserId_When_Nonce_NotBound()
    {
        // No session cookie, no nonce bound -> server must refuse to associate a LINE id.
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/Account/Register");
        var token = ExtractAntiforgeryToken(html);
        var nonceInHtml = ExtractNonce(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["FirstName"] = "Test", ["LastName"] = "User",
            ["CitizenId"] = "1234567890123", ["Phone"] = "0812345678",
            ["LineUserId"] = "Umalicious-attacker-id", ["LinkNonce"] = nonceInHtml,
            ["Pin"] = "123456", ["ConfirmPin"] = "123456",
        });
        var resp = await client.PostAsync("/Account/Register", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", body);
        // ASP.NET Core's HtmlEncoder emits non-ASCII text as &#xNNNN; numeric refs.
        // The owner-binding error starts with THAI MAI YAMOKKAN (U+0E44), i.e. &#xE44;.
        Assert.Contains("&#xE44;&#xE21;&#xE48;", body); // ไม่
    }

    [Fact]
    public async Task Register_Rejects_LineUserId_When_Nonce_Mismatches()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/Account/Register");
        var token = ExtractAntiforgeryToken(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["FirstName"] = "Test", ["LastName"] = "User",
            ["CitizenId"] = "1234567890124", ["Phone"] = "0812345679",
            ["LineUserId"] = "Uany-id",
            ["LinkNonce"] = "AAAAAAAA-not-the-real-nonce",
            ["Pin"] = "123456", ["ConfirmPin"] = "123456",
        });
        var resp = await client.PostAsync("/Account/Register", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", body);
        // The nonce-mismatch error starts with เซส (U+0E40, U+0E0B, U+0E2A).
        Assert.Contains("&#xE40;&#xE0B;&#xE2A;&#xE0A;&#xE31;&#xE19;", body); // เซสชัน
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success, "antiforgery token not found");
        return m.Groups[1].Value;
    }

    private static string ExtractNonce(string html)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, "id=\"LinkNonce\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success, "link nonce not found");
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), "goldshop-binding-tests-" + Guid.NewGuid().ToString("N") + ".db");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Path"] = dbPath,
                    ["Database:SeedDemoData"] = "false",
                    ["LineApi:ChannelSecret"] = "test-secret",
                    ["LineApi:ChannelAccessToken"] = "",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Admin:Password"] = "test-password-not-real",
                });
            });
        }
    }
}