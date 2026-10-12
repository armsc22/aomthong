using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using GoldShopWeb.Data;

namespace GoldShopWeb.Areas.Admin.Controllers;

[Area("Admin")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class AccessController(IConfiguration configuration,MemberStore store) : Controller
{
    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public async Task<IActionResult> Login(string? password,string? username)
    {
        StaffInfo? staff=null;
        if(!string.IsNullOrWhiteSpace(username)&&!string.IsNullOrEmpty(password)&&password.Length<=128)
            staff=store.AuthenticateStaff(username,password);
        var expected = configuration["Admin:Password"];
        var bootstrap=!store.Staff().Any(x=>x.Active&&x.Role=="Owner") && string.IsNullOrWhiteSpace(username) && !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(password) &&
            CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        if(staff is null && !bootstrap)
        {
            HttpContext.Items["AuditOutcome"]="LoginFailed";
            ViewData["Error"] = "เข้าสู่ระบบไม่สำเร็จ กรุณาตรวจสอบรหัสผ่านและการตั้งค่าผู้ดูแล";
            return View();
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name,staff?.Username??"Administrator"),new Claim(ClaimTypes.NameIdentifier,staff?.Id.ToString()??"bootstrap"),new Claim(ClaimTypes.Role,staff?.Role??"Owner"),new Claim("staff-version",staff?.Version.ToString()??"0")], "AdminCookie");
        await HttpContext.SignInAsync("AdminCookie", new ClaimsPrincipal(identity));
        HttpContext.User=new ClaimsPrincipal(identity);
        HttpContext.Items["AuditOutcome"]="LoginSucceeded";
        return RedirectToAction("Index", "Workspace", new { area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminCookie");
        HttpContext.Items["AuditOutcome"]="Logout";
        return RedirectToAction(nameof(Login));
    }
}
