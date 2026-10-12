using GoldShopWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminCookie")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WorkspaceController(MemberStore store) : Controller
{
    public IActionResult Index() => View(store.GetCustomerBalances());
    public IActionResult Accounts() => View(store.GetCustomerBalances());
    public IActionResult Inventory() => View(ShopCatalog.Products);
}
