using GoldShopWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoldShopWeb.Areas.Admin.Models;

namespace GoldShopWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookie")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CustomersController(MemberStore store) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(store.GetCustomerBalances());

    [HttpGet]
    public IActionResult Pin(int customerId)
    {
        var customer = store.FindCustomer(customerId);
        if (customer is null) return NotFound();
        ViewData["CustomerName"] = customer.FullName;
        return View(new MemberPinViewModel { CustomerId = customerId, Phone = customer.Phone });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Pin(MemberPinViewModel model)
    {
        var customer = store.FindCustomer(model.CustomerId);
        if (customer is null) return NotFound();
        ViewData["CustomerName"] = customer.FullName;
        if (!model.IdentityVerified) ModelState.AddModelError("", "กรุณายืนยันว่าได้ตรวจสอบตัวตนลูกค้าแล้ว");
        if (ModelState.IsValid && store.SetMemberPin(model.CustomerId, model.Phone, model.Pin))
        {
            TempData["PinMessage"] = "ตั้ง PIN เรียบร้อย ลูกค้าสามารถเข้าสู่ระบบด้วยเบอร์โทรและ PIN ใหม่ได้";
            return RedirectToAction(nameof(Index));
        }
        if (ModelState.IsValid) ModelState.AddModelError("", "เบอร์โทรไม่ถูกต้องหรือถูกใช้กับสมาชิกอื่นแล้ว");
        ModelState.Remove(nameof(model.Pin)); ModelState.Remove(nameof(model.ConfirmPin));
        model.Pin = model.ConfirmPin = "";
        return View(model);
    }
}
