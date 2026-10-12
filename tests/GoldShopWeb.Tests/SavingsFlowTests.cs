using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using GoldShopWeb.Data;
using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoldShopWeb.Tests;
public class SavingsFlowTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(log=>log.ClearProviders());
            builder.ConfigureAppConfiguration((_,cfg)=>cfg.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Database:Path"]=Path.Combine(Path.GetTempPath(),"savings-tests",Guid.NewGuid().ToString("N"),"goldshop.db"),
                ["Database:SeedDemoData"]="false",["Admin:Password"]="test-admin",["LineApi:ChannelAccessToken"]=""
            }));
        }
    }
    private static byte[] Image(byte marker=1) => [137,80,78,71,13,10,26,10,marker];
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    [Fact]
    public void ApprovalCreditsOnceAndReceiptIsImmutableSnapshot()
    {
        using var factory=new Factory(); using var client=factory.CreateClient();
        var store=factory.Services.GetRequiredService<MemberStore>();
        var customer=store.RegisterCustomer("First","Owner","1111111111111","0812345678",null,"Original address","Utest",9.9m,"012345")!;
        var id=Guid.NewGuid().ToString("N");
        Assert.NotNull(store.SubmitSavings(id,customer.Id,1250.25m,Image(),"image/png"));
        Assert.Matches(@"^AT[0-9]{4}/[0-9]{2}/[0-9]{2}SE0000$",store.SavingsRequest(id)!.TransactionNumber);
        var slips=factory.Services.GetRequiredService<PaymentSlipStore>();
        var cart=new CheckoutViewModel{Customer=customer,Cart=new(){Items=[new(){ProductId=1,Name="Gold",Quantity=1,Price=100m}]}};
        slips.Save("cash-one",cart,"image/png",Image(7));
        Assert.EndsWith("SE0001",Assert.Single(slips.List()).TransactionNumber);
        slips.Save("cash-one",cart,"image/png",Image(7));
        slips.Save("cash-two",cart,"image/png",Image(8));
        Assert.EndsWith("SE0002",slips.List().Single(x=>x.Id=="cash-two").TransactionNumber);
        Assert.Equal(0m,store.GetSummary(customer.Id).TotalBalance);
        using(var pendingJson=JsonDocument.Parse(JsonSerializer.Serialize(SavingsFlex.Build(store.SavingsRequest(id)!))))
        {
            Assert.Equal("flex",pendingJson.RootElement.GetProperty("type").GetString());
            Assert.False(pendingJson.RootElement.GetProperty("contents").TryGetProperty("footer",out _));
        }
        Assert.NotNull(store.SubmitSavings(id,customer.Id,1250.25m,Image(),"image/png"));
        Assert.Null(store.SubmitSavings(Guid.NewGuid().ToString("N"),customer.Id,1250.25m,Image(),"image/png"));
        Assert.True(store.ClaimSavingsMessage(id,"Pending"));
        Assert.False(store.ClaimSavingsMessage(id,"Pending"));
        store.CompleteSavingsMessage(id,"Pending",false);
        Assert.True(store.ClaimSavingsMessage(id,"Pending"));
        store.CompleteSavingsMessage(id,"Pending",true);
        var results=new bool[2]; Parallel.For(0,2,i=>results[i]=store.ReviewSavings(id,true,"Admin","checked"));
        Assert.Single(results,x=>x);
        Assert.Equal(1250.25m,store.GetSummary(customer.Id).TotalBalance);
        Assert.Single(store.GetSummary(customer.Id).Entries);
        Assert.False(store.ReviewSavings(id,true,"Admin",""));
        Assert.False(store.ClaimSavingsMessage(id,"Pending"));
        var receipt=store.SavingsRequest(id)!;
        Assert.Equal("Approved",receipt.Status); Assert.Matches("^D[0-9]{8,}$",receipt.ReceiptNumber);
        store.UpdateCustomer(customer.Id,"Changed","Name",customer.Phone,null,"new address","Utest");
        Assert.Equal("First Owner",store.SavingsRequest(id)!.CustomerName);
        var rejected=Guid.NewGuid().ToString("N");
        Assert.NotNull(store.SubmitSavings(rejected,customer.Id,200m,Image(2),"image/png"));
        Assert.False(store.ReviewSavings(rejected,false,"Admin",""));
        Assert.True(store.ReviewSavings(rejected,false,"Admin","ยอดไม่ตรง"));
        Assert.Equal(1250.25m,store.GetSummary(customer.Id).TotalBalance);
        Assert.Equal("",store.SavingsRequest(rejected)!.ReceiptNumber);
        Assert.Null(store.SubmitSavings(Guid.NewGuid().ToString("N"),customer.Id,0.001m,Image(3),"image/png"));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(SavingsFlex.Build(receipt,"https://shop.example/receipt")));
        Assert.Equal("flex",doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("https://shop.example/receipt",doc.RootElement.GetProperty("contents").GetProperty("footer").GetProperty("contents")[0].GetProperty("action").GetProperty("uri").GetString());
    }
    [Fact]
    public async Task MemberUploadAndAdminApprovalRespectAuthorization()
    {
        using var factory=new Factory(); using var member=factory.CreateClient(new(){AllowAutoRedirect=false});
        var store=factory.Services.GetRequiredService<MemberStore>();
        var customer=store.RegisterCustomer("Test","Owner","1111111111111","0812345678",null,null,"Utest",9.9m,"012345")!;
        var login=await member.GetStringAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect,(await member.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["Phone"]="0812345678",["Pin"]="012345",["__RequestVerificationToken"]=Token(login)}))).StatusCode);
        var html=await member.GetStringAsync("/Savings");
        var id=Regex.Match(html,"name=\"id\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(id);
        using var upload=new MultipartFormDataContent();
        upload.Add(new StringContent(id),"id"); upload.Add(new StringContent("500.50"),"amount"); upload.Add(new StringContent(Token(html)),"__RequestVerificationToken"); upload.Add(new ByteArrayContent(Image()),"slip","slip.png");
        Assert.Equal(HttpStatusCode.Redirect,(await member.PostAsync("/Savings/Submit",upload)).StatusCode);
        Assert.Equal("Pending",store.SavingsRequest(id)!.Status);
        Assert.Equal(0m,store.GetSummary(customer.Id).TotalBalance);
        Assert.Equal(HttpStatusCode.Redirect,(await member.GetAsync("/Savings/Review")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await member.GetAsync("/Savings/Receipt?id="+id)).StatusCode);
        using var admin=factory.CreateClient(new(){AllowAutoRedirect=false});
        login=await admin.GetStringAsync("/Admin/Access/Login");
        await admin.PostAsync("/Admin/Access/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["password"]="test-admin",["__RequestVerificationToken"]=Token(login)}));
        html=await admin.GetStringAsync("/Savings/Review");
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsync("/Savings/Decide",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=id,["decision"]="approve",["verified"]="true"}))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await admin.PostAsync("/Savings/Decide",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=id,["decision"]="approve",["verified"]="true",["__RequestVerificationToken"]=Token(html)}))).StatusCode);
        Assert.Equal(500.50m,store.GetSummary(customer.Id).TotalBalance);
        Assert.Equal(HttpStatusCode.OK,(await member.GetAsync("/Savings/Receipt?id="+id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync("/Savings/Receipt?id="+id)).StatusCode);
        using var stranger=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.GetAsync("/Savings/Receipt?id="+id)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await stranger.GetAsync("/Savings/Slip?id="+id)).StatusCode);
    }
}
