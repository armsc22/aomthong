using System.Net;
using System.Text.RegularExpressions;
using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoldShopWeb.Tests;
public class TellerWorkspaceTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(log => log.ClearProviders());
            builder.ConfigureAppConfiguration((_,cfg)=>cfg.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Database:Path"]=Path.Combine(Path.GetTempPath(),"goldshop-tests",Guid.NewGuid().ToString("N"),"goldshop.db"),
                ["Database:SeedDemoData"]="false", ["Admin:Password"]="test-only-password", ["LineApi:ChannelAccessToken"]=""
            }));
        }
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    [Fact]
    public async Task AdminCanReviewButAnonymousCannotReadReceiptOrChangeStatus()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Admin/Workspace")).StatusCode);
        var store = factory.Services.GetRequiredService<PaymentSlipStore>();
        store.Save("test-slip",new CheckoutViewModel { Customer=new(){Id=123,MemberCode="M0001",FirstName="Test"}, Cart=new(){Items=[new(){Name="Gold",Price=100m,Quantity=1}]} },"image/png",[1,2,3]);
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Checkout/Slip?id=test-slip")).StatusCode);
        var login=await client.GetStringAsync("/Admin/Access/Login");
        var response=await client.PostAsync("/Admin/Access/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["password"]="test-only-password",["__RequestVerificationToken"]=Token(login)}));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        foreach(var url in new[]{"/Admin/Workspace","/Admin/Workspace/Accounts","/Admin/Workspace/Inventory","/Checkout/Slips"})
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Checkout/ReviewSlip",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]="test-slip",["decision"]="approve",["verified"]="true"}))).StatusCode);
        var html=await client.GetStringAsync("/Checkout/Slips");
        response=await client.PostAsync("/Checkout/ReviewSlip",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]="test-slip",["decision"]="approve",["verified"]="true",["__RequestVerificationToken"]=Token(html)}));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        Assert.NotNull(store.Receipt("test-slip"));
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/Checkout/Receipt?id=test-slip")).StatusCode);
        using var stranger=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.GetAsync("/Checkout/Receipt?id=test-slip")).StatusCode);
    }
}
