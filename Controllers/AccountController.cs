using System.Security.Cryptography;
using System.Text;
using GoldShopWeb.Data;
using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class AccountController : Controller
{
    public const string SessionNonceKey = "line:link:nonce";
    public const string SessionBoundUserIdKey = "line:link:userId";

    private readonly AccountService _account;
    private readonly MemberStore _store;
    private readonly CartService _cart;
    private readonly ShopSettings _settings;
    private readonly LineMessagingService _line;
    private readonly IConfiguration _config;
    private readonly LineIdentityService _identity;

    public AccountController(AccountService account, MemberStore store, CartService cart, ShopSettings settings, LineMessagingService line, IConfiguration config, LineIdentityService identity)
    {
        _account = account;
        _store = store;
        _cart = cart;
        _settings = settings;
        _line = line;
        _config = config;
        _identity = identity;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_account.IsSignedIn)
        {
            if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
            return RedirectToAction(nameof(Index));
        }

        return View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public IActionResult Login(LoginViewModel model)
    {
        if (ModelState.IsValid && _account.TrySignIn(model.Phone, model.Pin))
        {
            HttpContext.Items["AuditOutcome"]="LoginSucceeded";
            if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
            return RedirectToAction(nameof(Index));
        }

        model.Error = "เบอร์โทรหรือ PIN ไม่ถูกต้อง หรือบัญชีถูกพักการเข้าสู่ระบบ กรุณาลองใหม่ภายหลัง";
        HttpContext.Items["AuditOutcome"]="LoginFailed";
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (_account.IsSignedIn)
        {
            return RedirectToAction(nameof(Index));
        }

        // CSRF/session binding is separate from proof of LINE ownership via ID token.
        HttpContext.Session.SetString(SessionNonceKey, GenerateNonce());
        HttpContext.Session.Remove(SessionBoundUserIdKey);
        HttpContext.Session.Remove("line:verified:expires");

        ViewData["LiffId"] = _config["LineApi:LiffId"] ?? string.Empty;
        return View(new RegisterViewModel { LinkNonce = HttpContext.Session.GetString(SessionNonceKey) ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(17 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 17 * 1024 * 1024)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
        {
            ModelState.AddModelError(string.Empty, "กรุณากรอกชื่อและนามสกุล");
        }

        if (string.IsNullOrWhiteSpace(model.CitizenId) || !System.Text.RegularExpressions.Regex.IsMatch(model.CitizenId.Trim(), @"^[0-9]{13}$"))
        {
            ModelState.AddModelError(nameof(model.CitizenId), "เลขบัตรประชาชนต้องเป็น 13 หลัก");
        }

        if (MemberCredentials.NormalizePhone(model.Phone).Length == 0) ModelState.AddModelError(nameof(model.Phone), "กรุณาระบุเบอร์โทรศัพท์ไทยให้ถูกต้อง");

        if (string.IsNullOrWhiteSpace(model.LineUserId))
        {
            ModelState.AddModelError(nameof(model.LineUserId), "กรุณาเชื่อมบัญชี LINE เพื่อใช้รับข่าวสารและแจ้งรายการ");
        }

        // Owner verification: the submitted LineUserId must equal the value bound
        // to this session via /Account/BindLineUserId and the nonce must match.
        var sessionNonce = HttpContext.Session.GetString(SessionNonceKey);
        var boundUserId = HttpContext.Session.GetString(SessionBoundUserIdKey);
        var expires = long.TryParse(HttpContext.Session.GetString("line:verified:expires"), out var expiry) ? expiry : 0;
        if (string.IsNullOrEmpty(sessionNonce) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(sessionNonce),
                Encoding.UTF8.GetBytes(model.LinkNonce ?? string.Empty)))
        {
            ModelState.AddModelError(nameof(model.LineUserId), "เซสชันยืนยัน LINE หมดอายุ กรุณาเชื่อมบัญชี LINE ใหม่");
        }
        else if (expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || string.IsNullOrEmpty(boundUserId) || boundUserId != model.LineUserId)
        {
            ModelState.AddModelError(nameof(model.LineUserId), "ไม่พบการยืนยันเจ้าของบัญชี LINE กรุณาเชื่อมบัญชี LINE ผ่าน LIFF อีกครั้ง");
        }

        if (!ModelState.IsValid)
        {
            ViewData["LiffId"] = _config["LineApi:LiffId"] ?? string.Empty;
            model.LinkNonce = sessionNonce ?? string.Empty;
            return View(model);
        }

        var uploads = new Dictionary<string, RegistrationImage>();
        foreach(var (key,file) in new[]{(nameof(model.CitizenCardImage),model.CitizenCardImage),(nameof(model.PortraitImage),model.PortraitImage),(nameof(model.BankBookImage),model.BankBookImage)})
        {
            if(file is null || file.Length<=0 || file.Length>5*1024*1024){ModelState.AddModelError(key,"กรุณาแนบรูป ขนาดไม่เกิน 5 MB ต่อรูป");continue;}
            using var buffer = new MemoryStream(); await file.CopyToAsync(buffer,HttpContext.RequestAborted);
            var bytes = buffer.ToArray(); var mime=PaymentSlipStore.DetectImage(bytes);
            if(mime is null) ModelState.AddModelError(key,"รองรับเฉพาะรูป JPG หรือ PNG เท่านั้น");
            else uploads[key]=new RegistrationImage(bytes,mime);
        }
        if(!ModelState.IsValid)
        {
            ViewData["LiffId"]=_config["LineApi:LiffId"]??"";
            model.LinkNonce=sessionNonce??"";
            return View(model);
        }
        var customer = _store.RegisterCustomer(
            model.FirstName, model.LastName, model.CitizenId, model.Phone,
            model.Email, model.Address, boundUserId!, _settings.SavingsAnnualDividendPercent, model.Pin,
            new RegistrationDocuments(model.BankName,model.BankAccountNumber,uploads[nameof(model.CitizenCardImage)],uploads[nameof(model.PortraitImage)],uploads[nameof(model.BankBookImage)]));

        if (customer is null)
        {
            ModelState.AddModelError(string.Empty, "ไม่สามารถสมัครได้ กรุณาตรวจสอบเบอร์โทรหรือเลขบัตรประชาชนที่อาจลงทะเบียนแล้ว");
            ViewData["LiffId"] = _config["LineApi:LiffId"] ?? string.Empty;
            model.LinkNonce = sessionNonce ?? string.Empty;
            return View(model);
        }

        // Single-use nonce: clear it so the same nonce cannot bind another account.
        HttpContext.Session.Remove(SessionNonceKey);
        HttpContext.Session.Remove(SessionBoundUserIdKey);

        _account.SignInAs(customer.Id);

        await _line.SendRegistrationSuccessAsync(
            customer.LineUserId, customer.MemberCode, customer.Phone, customer.CitizenId, customer.FullName);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public async Task<IActionResult> BindLineUserId([FromBody] BindLineRequest request)
    {
        var nonce = HttpContext.Session.GetString(SessionNonceKey);
        if (request is null || string.IsNullOrEmpty(nonce) || !string.Equals(nonce, request.Nonce, StringComparison.Ordinal))
            return BadRequest(new { error = "เซสชันหมดอายุ กรุณาโหลดหน้าใหม่" });
        HttpContext.Session.Remove(SessionBoundUserIdKey);
        HttpContext.Session.Remove("line:verified:expires");
        var identity = await _identity.VerifyAsync(request.IdToken, HttpContext.RequestAborted);
        if (identity is null) return BadRequest(new { error = "ตรวจสอบ LINE ไม่สำเร็จ กรุณาเข้าสู่ระบบ LINE ใหม่ หรือติดต่อร้าน" });
        var customer = _account.CurrentCustomer();
        if (customer is not null && !_store.BindVerifiedLine(customer.Id, identity.UserId))
            return BadRequest(new { error = "บัญชี LINE นี้เชื่อมกับสมาชิกอื่นแล้ว" });
        HttpContext.Session.SetString(SessionBoundUserIdKey, identity.UserId);
        HttpContext.Session.SetString("line:verified:expires", Math.Min(identity.ExpiresAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600).ToString());
        return Ok(new { bound = true, userId = identity.UserId, displayName = identity.Name });
    }
    private static string GenerateNonce()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public record BindLineRequest(string? IdToken, string? Nonce);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _account.SignOut();
        HttpContext.Items["AuditOutcome"]="Logout";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Index()
    {
        var customer = _account.CurrentCustomer();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var summary = _store.GetSummary(customer.Id);
        var model = new DashboardViewModel
        {
            Customer = customer,
            Account = _store.FindAccountFor(customer.Id),
            Summary = summary,
            AnnualDividendPercent = _settings.SavingsAnnualDividendPercent,
            DailyDividend = SavingsRules.DailyDividend(summary.TotalBalance, _settings.SavingsAnnualDividendPercent),
            Cart = _cart.Get(),
            RecentEntries = summary.Entries.OrderByDescending(e => e.Seq).Take(5).ToList(),
        };
        return View(model);
    }

    [HttpGet]
    public IActionResult Profile()
    {
        var customer = _account.CurrentCustomer();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        HttpContext.Session.SetString(SessionNonceKey, GenerateNonce());
        ViewData["LiffId"] = _config["LineApi:LiffId"] ?? "";
        ViewData["LinkNonce"] = HttpContext.Session.GetString(SessionNonceKey);
        return View(new ProfileViewModel
        {
            MemberCode = customer.MemberCode,
            CitizenId = customer.CitizenId,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            LineUserId = customer.LineUserId,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Profile(ProfileViewModel model)
    {
        var customer = _account.CurrentCustomer();
        if (customer is null)
        {
            return RedirectToAction(nameof(Login));
        }

        model.Phone = customer.Phone;
        model.LineUserId = customer.LineUserId;
        ViewData["LiffId"] = _config["LineApi:LiffId"] ?? "";
        ViewData["LinkNonce"] = HttpContext.Session.GetString(SessionNonceKey);
        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
        {
            ModelState.AddModelError(string.Empty, "กรุณากรอกชื่อและนามสกุล");
        }

        if (!ModelState.IsValid)
        {
            model.MemberCode = customer.MemberCode;
            model.CitizenId = customer.CitizenId;
            return View(model);
        }

        model.Phone = customer.Phone;
        model.LineUserId = customer.LineUserId;
        _store.UpdateCustomer(customer.Id, model.FirstName, model.LastName, customer.Phone, model.Email, model.Address, customer.LineUserId);
        model.MemberCode = customer.MemberCode;
        model.CitizenId = customer.CitizenId;
        model.Saved = true;
        return View(model);
    }
}



