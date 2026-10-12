using System.Net;
using System.Text;
using GoldShopWeb.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoldShopWeb.Tests;
public class LineIdentityTests
{
    private sealed class Handler(string json, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://api.line.me/oauth2/v2.1/verify", request.RequestUri!.ToString());
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
    [Theory]
    [InlineData("correct", 600, true)]
    [InlineData("wrong", 600, false)]
    [InlineData("correct", -600, false)]
    public async Task ValidatesChannelAndExpiry(string audience, int seconds, bool valid)
    {
        var handler = new Handler(System.Text.Json.JsonSerializer.Serialize(new {iss="https://access.line.me",sub="Uverified",aud=audience,exp=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+seconds,name="Verified"}));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["LineApi:LoginChannelId"]="correct" }).Build();
        var result = await new LineIdentityService(new HttpClient(handler),config).VerifyAsync("test-token");
        Assert.Equal(valid, result is not null);
        Assert.Contains("client_id=correct", handler.Body);
        if(valid) Assert.Equal("Uverified", result!.UserId);
    }
    [Theory]
    [InlineData("{}", 200)]
    [InlineData("not-json", 200)]
    [InlineData("{}", 400)]
    public async Task InvalidResponsesFailClosed(string json, int status)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["LineApi:LoginChannelId"]="correct" }).Build();
        Assert.Null(await new LineIdentityService(new HttpClient(new Handler(json,(HttpStatusCode)status)),config).VerifyAsync("fake"));
    }
    [Fact]
    public async Task MissingConfigurationDoesNotCallLine()
    {
        var handler = new Handler("{}");
        Assert.Null(await new LineIdentityService(new HttpClient(handler),new ConfigurationBuilder().Build()).VerifyAsync("fake"));
        Assert.Null(handler.Body);
    }
}
