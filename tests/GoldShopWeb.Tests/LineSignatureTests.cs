using System.Security.Cryptography;
using System.Text;
using GoldShopWeb.Controllers;
using Xunit;

namespace GoldShopWeb.Tests;

public class LineSignatureTests
{
    private const string Secret = "test-channel-secret-do-not-use-in-prod";

    [Fact]
    public void VerifySignature_AcceptsValidBase64Hmac()
    {
        var body = Encoding.UTF8.GetBytes("""{"events":[]}""");
        var sig = Sign(Secret, body);

        Assert.True(LineWebhookController.VerifySignature(Secret, body, sig));
    }

    [Fact]
    public void VerifySignature_RejectsTamperedBody()
    {
        var body = Encoding.UTF8.GetBytes("""{"events":[]}""");
        var sig = Sign(Secret, body);
        var tampered = Encoding.UTF8.GetBytes("""{"events":[{"type":"unfollow"}]}""");

        Assert.False(LineWebhookController.VerifySignature(Secret, tampered, sig));
    }

    [Fact]
    public void VerifySignature_RejectsWrongSecret()
    {
        var body = Encoding.UTF8.GetBytes("""{"events":[]}""");
        var sig = Sign(Secret, body);

        Assert.False(LineWebhookController.VerifySignature("other-secret", body, sig));
    }

    [Fact]
    public void VerifySignature_RejectsNonBase64()
    {
        var body = Encoding.UTF8.GetBytes("""{"events":[]}""");
        Assert.False(LineWebhookController.VerifySignature(Secret, body, "not-base64!!!"));
    }

    private static string Sign(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(body));
    }
}