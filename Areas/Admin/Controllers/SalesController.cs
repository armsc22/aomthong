using GoldShopWeb.Areas.Admin.Models;
using GoldShopWeb.Data;
using GoldShopWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Areas.Admin.Controllers;

// Read-only scaffolding. Posting sales and LINE OA delivery will be added with the workflow.
[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookie")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SalesController(MemberStore store, ShopSettings shop) : Controller
{
    [HttpGet]
    public IActionResult Create(int customerId)
    {
        var customer = store.FindCustomer(customerId);
        if (customer is null) return NotFound();
        return View(new CashSaleViewModel
        {
            CustomerId = customerId, Customer = customer, Products = ShopCatalog.Products,
            Items = [new CashSaleSelection()]
        });
    }

    [HttpGet]
    public IActionResult Preview(int customerId)
    {
        var customer = store.FindCustomer(customerId);
        if (customer is null) return NotFound();
        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Bangkok").DateTime;
        return View("Receipt", new CashSale
        {
            CustomerId = customerId, DocumentNumber = $"C{now:MMdd}0000", MemberCode = $"M{now:MMdd}0000",
            CustomerName = customer.FullName, Phone = customer.Phone, Address = customer.Address,
            ShopName = shop.ShopName, ShopAddress = shop.Address, ShopPhone = shop.Phone, IssuedAt = now
        });
    }
}
