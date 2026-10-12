using GoldShopWeb.Data;
using GoldShopWeb.Models;
using GoldShopWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options=>options.Filters.Add<ActivityFilter>());
builder.Services.AddScoped<ActivityFilter>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddAuthentication("AdminCookie").AddCookie("AdminCookie", options =>
{
    options.LoginPath = "/Admin/Access/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.Events.OnValidatePrincipal = context =>
    {
        var store=context.HttpContext.RequestServices.GetRequiredService<MemberStore>();
        var id=context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var valid=id=="bootstrap" ? !store.Staff().Any(x=>x.Active&&x.Role=="Owner")
            : int.TryParse(id,out var staffId) && store.Staff().Any(x=>x.Id==staffId&&x.Active&&x.Version.ToString()==context.Principal?.FindFirst("staff-version")?.Value);
        if(!valid)context.RejectPrincipal();
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context => {context.Response.StatusCode=403;return Task.CompletedTask;};
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("MemberLogin", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddSingleton<MemberStore>();
builder.Services.AddSingleton<PaymentSlipStore>();
builder.Services.AddHttpClient("line");
builder.Services.AddHttpClient<LineIdentityService>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<LineMessagingService>();

// Brand/contact details live in appsettings.json so they can be changed without a rebuild.
var shopSettings = builder.Configuration.GetSection("ShopSettings").Get<ShopSettings>() ?? new ShopSettings();
builder.Services.AddSingleton(shopSettings);

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();
// Validate and initialize durable storage before accepting requests.
_ = app.Services.GetRequiredService<MemberStore>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.Use(async (context,next) =>
{
    try
    {
        await next();
        if(context.Response.StatusCode is 401 or 403 or 429)
        {
            var actor=context.User.Identity?.IsAuthenticated==true?"staff:"+context.User.Identity.Name:"anonymous";
            context.RequestServices.GetRequiredService<MemberStore>().RecordActivity(actor,"AccessDenied",context.GetRouteValue("controller")?.ToString()??"",context.Response.StatusCode.ToString(),context.TraceIdentifier);
        }
    }
    catch(Exception ex)
    {
        try{context.RequestServices.GetRequiredService<MemberStore>().RecordActivity("system","RequestError",context.GetRouteValue("controller")?.ToString()??"",ex.GetType().Name,context.TraceIdentifier);}
        catch{ /* The original exception remains the primary failure. */ }
        throw;
    }
});
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Deposit}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "productDetails",
    pattern: "Products/Details/{slug}",
    defaults: new { controller = "Products", action = "Details" });

app.MapControllerRoute(
    name: "productCategory",
    pattern: "Products/{category}",
    defaults: new { controller = "Products", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
