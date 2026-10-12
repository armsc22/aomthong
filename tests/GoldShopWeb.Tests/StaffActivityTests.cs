using System.Net;
using System.Text.RegularExpressions;
using GoldShopWeb.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
namespace GoldShopWeb.Tests;
public class StaffActivityTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(x=>x.ClearProviders());
            builder.ConfigureAppConfiguration((_,cfg)=>cfg.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Database:Path"]=Path.Combine(Path.GetTempPath(),"staff-tests",Guid.NewGuid().ToString("N"),"goldshop.db"),["Database:SeedDemoData"]="false",["Admin:Password"]="bootstrap-test",["LineApi:ChannelAccessToken"]=""
            }));
        }
    }
    private static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<HttpResponseMessage> Login(HttpClient client,string username,string password)
    {
        var html=await client.GetStringAsync("/Admin/Access/Login");
        return await client.PostAsync("/Admin/Access/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["username"]=username,["password"]=password,["__RequestVerificationToken"]=Token(html)}));
    }
    [Fact]
    public async Task RolesRevocationAndAuditAreEnforced()
    {
        using var factory=new Factory();using var owner=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await Login(owner,"","bootstrap-test")).StatusCode);
        var store=factory.Services.GetRequiredService<MemberStore>();
        Assert.False(store.CreateStaff("viewer","long-test-password","Viewer"));
        Assert.True(store.CreateStaff("owner","long-owner-password","Owner"));
        Assert.Equal(HttpStatusCode.Redirect,(await owner.GetAsync("/Admin/Workspace")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Login(owner,"","bootstrap-test")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Login(owner,"owner","long-owner-password")).StatusCode);
        Assert.True(store.CreateStaff("viewer","long-viewer-password","Viewer"));
        Assert.True(store.CreateStaff("operator","long-operator-password","Operator"));
        Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync("/Admin/Operations/Timeline")).StatusCode);
        using var viewer=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await Login(viewer,"viewer","long-viewer-password")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await viewer.GetAsync("/Admin/Customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await viewer.GetAsync("/Admin/Operations/Timeline")).StatusCode);
        var html=await viewer.GetStringAsync("/Admin/Deposit");
        viewer.DefaultRequestHeaders.Add("RequestVerificationToken",Token(html));
        Assert.Equal(HttpStatusCode.Forbidden,(await viewer.PostAsync("/api/core/deposits/1/deposit",new StringContent("{\"amount\":100}",System.Text.Encoding.UTF8,"application/json"))).StatusCode);
        Assert.True(store.SetStaffActive(store.Staff().Single(x=>x.Username=="viewer").Id,false));
        Assert.Equal(HttpStatusCode.Redirect,(await viewer.GetAsync("/Admin/Customers")).StatusCode);
        Assert.False(store.SetStaffActive(store.Staff().Single(x=>x.Username=="owner").Id,false));
        var events=store.Activities(null,null);
        Assert.Contains(events,x=>x.Actor=="staff:owner"&&x.Outcome=="LoginSucceeded");
        Assert.Contains(events,x=>x.Outcome=="LoginFailed");
        Assert.Contains(events,x=>x.Actor=="staff:viewer"&&x.Outcome=="Forbidden:ReadOnlyStaff");
        Assert.DoesNotContain(events,x=>System.Text.Json.JsonSerializer.Serialize(x).Contains("long-owner-password"));
        for(var n=0;n<5;n++)Assert.Null(store.AuthenticateStaff("operator","wrong-password"));
        Assert.Null(store.AuthenticateStaff("operator","long-operator-password"));
    }
}
