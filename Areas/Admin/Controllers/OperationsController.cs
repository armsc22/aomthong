using GoldShopWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GoldShopWeb.Areas.Admin.Controllers;
[Area("Admin"),Authorize(AuthenticationSchemes="AdminCookie",Roles="Owner")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class OperationsController(MemberStore store) : Controller
{
    public IActionResult Staff() => View(store.Staff());
    public IActionResult Timeline(string? actor,string? activity)
    {
        ViewData["Actor"]=actor;ViewData["Activity"]=activity;
        return View(store.Activities(actor,activity));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult CreateStaff(string? username,string? password,string? role)
    {
        var ok=store.CreateStaff(username??"",password??"",role??"");
        HttpContext.Items["AuditOutcome"]=ok?"StaffCreated":"StaffCreationRejected";
        TempData["StaffMessage"]=ok?"สร้างบัญชีแล้ว หากเป็นเจ้าของคนแรก ให้เข้าสู่ระบบด้วยบัญชีใหม่":"สร้างไม่ได้: ชื่อซ้ำ รูปแบบไม่ถูกต้อง หรือบัญชีแรกต้องเป็นเจ้าของระบบ";
        return RedirectToAction(nameof(Staff));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public IActionResult SetActive(int id,bool active)
    {
        var ok=store.SetStaffActive(id,active);
        HttpContext.Items["AuditOutcome"]=ok?(active?"StaffEnabled":"StaffDisabled"):"StaffChangeRejected";
        TempData["StaffMessage"]=ok?"ปรับสถานะแล้ว":"ปิดเจ้าของระบบคนสุดท้ายไม่ได้";
        return RedirectToAction(nameof(Staff));
    }
}
