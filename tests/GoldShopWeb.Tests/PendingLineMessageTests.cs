using System.Net;
using System.Text.Json;
using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GoldShopWeb.Tests;
public class PendingLineMessageTests
{
    [Fact]
    public void ApprovedFlexHasReceiptButtonAndFallbackProductCode()
    {
        var order = new CashSale { DocumentNumber="order",MemberCode="M001",CustomerName="Test",Lines=[new(){ProductId=99999,Name="Gold",UnitPrice=100m,Quantity=1}] };
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(OrderPaymentFlex.Build(order,"C09180001","https://shop.example/receipt")));
        var bubble=json.RootElement.GetProperty("contents");
        var action=bubble.GetProperty("footer").GetProperty("contents")[0].GetProperty("action");
        Assert.Equal("uri",action.GetProperty("type").GetString());
        Assert.Equal("https://shop.example/receipt",action.GetProperty("uri").GetString());
        Assert.Contains("PRODUCT-99999",json.RootElement.GetRawText());
    }
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Assert.Equal("https://api.line.me/v2/bot/message/push",request.RequestUri!.ToString());
            Body = await request.Content!.ReadAsStringAsync(token);
            return new(status);
        }
    }
    private sealed class Factory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    [Theory]
    [InlineData(200,true)]
    [InlineData(401,false)]
    public async Task PushIncludesPendingStatusAndReportsApiFailure(int status,bool success)
    {
        var handler = new Handler((HttpStatusCode)status);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["LineApi:ChannelAccessToken"]="test-token" }).Build();
        var service = new LineMessagingService(new Factory(new HttpClient(handler)),config,NullLogger<LineMessagingService>.Instance);
        var order = new CashSale { DocumentNumber="order-test", MemberCode="M09180001", CustomerName="Test Owner", Lines=[new(){Sku="GP-01",Name="Gold",Quantity=1,UnitPrice=1000m}] };
        Assert.Equal(success,await service.SendOrderPaymentPendingAsync("Uverified",order));
        using var payload=JsonDocument.Parse(handler.Body!);
        Assert.Equal("Uverified",payload.RootElement.GetProperty("to").GetString());
        var message=payload.RootElement.GetProperty("messages")[0];
        Assert.Equal("flex",message.GetProperty("type").GetString());
        Assert.Equal("bubble",message.GetProperty("contents").GetProperty("type").GetString());
        var text=System.Text.RegularExpressions.Regex.Unescape(message.GetRawText());
        Assert.Contains("order-test",text); Assert.Contains("GP-01",text); Assert.Contains("ตรวจสอบ : รอผล",text);
    }
}
