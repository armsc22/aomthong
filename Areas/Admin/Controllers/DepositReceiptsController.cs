using GoldShopWeb.Areas.Admin.Models;
using GoldShopWeb.Data;
using GoldShopWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookie")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DepositReceiptsController(MemberStore store, ShopSettings shop) : Controller
{
    // Preview only: does not post deposits, allocate numbers or send LINE messages.
    [HttpGet]
    public IActionResult Preview(int customerId)
    {
        var customer = store.FindCustomer(customerId);
        if (customer is null) return NotFound();
        return View(new DepositReceiptPreview
        {
            Customer = customer, Shop = shop,
            PreviewTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Bangkok").DateTime
        });
    }
}
