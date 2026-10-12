using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using GoldShopWeb.Data;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.Extensions.Logging;

namespace GoldShopWeb.Tests;
public class VerifiedLineFlowTests
{
    private sealed class VerifyHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            if (!body.Contains("id_token=valid-test-proof")) return new(HttpStatusCode.BadRequest);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {iss="https://access.line.me",aud="test-channel",sub="Uverified-owner",name="Test",exp=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+600}) };
        }
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(log=>log.ClearProviders());
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Database:Path"]=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db"),["Database:SeedDemoData"]="false",
                ["LineApi:ChannelAccessToken"]="",["LineApi:ChannelSecret"]="",["LineApi:LoginChannelId"]="test-channel",["Logging:LogLevel:Default"]="Warning",["Admin:Password"]="test-only-admin"
            }));
            builder.ConfigureTestServices(services => services.AddTransient<LineIdentityService>(provider => new(new HttpClient(new VerifyHandler()),provider.GetRequiredService<IConfiguration>())));
        }
    }
    private static string Value(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static MultipartFormDataContent RegistrationForm(Dictionary<string,string> fields, bool validImage=true)
    {
        var content=new MultipartFormDataContent();
        foreach(var field in fields) content.Add(new StringContent(field.Value),field.Key);
        content.Add(new StringContent("ธนาคารทดสอบ"),"BankName");
        content.Add(new StringContent("0012345678"),"BankAccountNumber");
        foreach(var name in new[]{"CitizenCardImage","PortraitImage","BankBookImage"})
            content.Add(new ByteArrayContent(validImage ? new byte[]{137,80,78,71,13,10,26,10} : new byte[]{60,104,116,109,108,62}),name,"image.png");
        return content;
    }
    [Fact]
    public void NewMemberCodesUseBangkokMonthDayAndIncrement()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<MemberStore>();
        var prefix = "M" + DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("MMdd", System.Globalization.CultureInfo.InvariantCulture);
        var first = store.RegisterCustomer("Test", "One", "1111111111111", "0812345678", null, null, "Uone", 9.9m, "012345");
        var second = store.RegisterCustomer("Test", "Two", "2222222222222", "0812345679", null, null, "Utwo", 9.9m, "012345");
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(prefix + "0001", first.MemberCode);
        Assert.Equal(prefix + "0002", second.MemberCode);
        Assert.Equal(first.MemberCode, store.FindCustomer(first.Id)!.MemberCode);
    }

    [Fact]
    public async Task OnlyVerifiedIdentityCanRegisterAndProfileCannotOverwriteIt()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() {AllowAutoRedirect=false});
        var callback = await client.GetAsync("/?liff.state=%2FAccount%2FRegister&code=test-code&liffClientId=test-channel");
        var callbackHtml = await callback.Content.ReadAsStringAsync();
        Assert.Contains("line-return.js", callbackHtml);
        Assert.Contains("sdk.js", callbackHtml);
        Assert.DoesNotContain("test-code", callbackHtml);
        Assert.True(callback.Headers.CacheControl!.NoStore);
        var html = await client.GetStringAsync("/Account/Register");
        var csrf = Value(html,"__RequestVerificationToken"); var nonce = Value(html,"LinkNonce");
        client.DefaultRequestHeaders.Add("RequestVerificationToken",csrf);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/Account/BindLineUserId",new {userId="Uforged",nonce})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/Account/BindLineUserId",new {idToken="fake",nonce})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/Account/BindLineUserId",new {idToken="valid-test-proof",nonce})).StatusCode);
        var form = new Dictionary<string,string> {
            ["__RequestVerificationToken"]=csrf,["LinkNonce"]=nonce,["FirstName"]="Test",["LastName"]="Owner",
            ["CitizenId"]="3333333333333",["Phone"]="0812345678",["Pin"]="012345",["ConfirmPin"]="012345",["LineUserId"]="Uforged"
        };
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/Account/Register",new FormUrlEncodedContent(form))).StatusCode);
        var store = factory.Services.GetRequiredService<MemberStore>(); Assert.Empty(store.Search(null));
        form["LineUserId"]="Uverified-owner";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/Account/Register",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Empty(store.Search(null));
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/Account/Register",RegistrationForm(form,false))).StatusCode);
        Assert.Empty(store.Search(null));
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Register",RegistrationForm(form))).StatusCode);
        var member=Assert.Single(store.Search(null)); Assert.Equal("Uverified-owner",store.VerifiedLineUserId(member.Id));
        Assert.Equal("0012345678",store.BankDetails(member.Id)!.BankAccountNumber);
        var documentUrl=$"/MemberDocuments/Image?customerId={member.Id}&kind=citizen-card";
        var imageResponse=await client.GetAsync(documentUrl);
        Assert.Equal(HttpStatusCode.OK,imageResponse.StatusCode);
        Assert.True(imageResponse.Headers.CacheControl!.NoStore);
        Assert.Equal("image/png",imageResponse.Content.Headers.ContentType!.MediaType);
        using var anonymous=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(documentUrl)).StatusCode);
        using var admin=factory.CreateClient(new(){AllowAutoRedirect=false});
        var adminHtml=await admin.GetStringAsync("/Admin/Access/Login");
        await admin.PostAsync("/Admin/Access/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["password"]="test-only-admin",["__RequestVerificationToken"]=Value(adminHtml,"__RequestVerificationToken")}));
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync(documentUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/MemberDocuments/Image?customerId={member.Id+1}&kind=citizen-card")).StatusCode);
        html=await client.GetStringAsync("/Account/Profile"); csrf=Value(html,"__RequestVerificationToken");
        Assert.Contains("0012345678",html);
        Assert.DoesNotContain("name=\"BankAccountNumber\"",html);
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/Account/Profile",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=csrf,["FirstName"]="Test",["LastName"]="Owner",["LineUserId"]="Uforged",["Phone"]="0899999999",["BankAccountNumber"]="9999999999",["BankName"]="forged"
        }))).StatusCode);
        Assert.Equal("Uverified-owner",store.FindCustomer(member.Id)!.LineUserId);
        Assert.Equal("0812345678",store.FindCustomer(member.Id)!.Phone);
        Assert.Equal("0012345678",store.BankDetails(member.Id)!.BankAccountNumber);
        Assert.NotNull(store.AuthenticatePin("+66812345678","012345"));
        for(var i=0;i<5;i++) Assert.Null(store.AuthenticatePin("0812345678","999999"));
        Assert.Null(store.AuthenticatePin("0812345678","012345"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync("/api/line/webhook",new {events=Array.Empty<object>()})).StatusCode);
    }
}
