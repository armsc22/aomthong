using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

public class CartController : Controller
{
    private readonly CartService _cart;
    private readonly ShopSettings _settings;

    public CartController(CartService cart, ShopSettings settings)
    {
        _cart = cart;
        _settings = settings;
    }

    public IActionResult Index()
    {
        var cart = _cart.Get();
        ViewData["FreeShippingThreshold"] = _settings.FreeShippingThreshold;
        ViewData["FreeShippingRemaining"] = Math.Max(0m, _settings.FreeShippingThreshold - cart.SubTotal);
        ViewData["Shipping"] = cart.SubTotal >= _settings.FreeShippingThreshold || cart.IsEmpty ? 0m : 150m;
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(int productId, int quantity = 1, string? returnUrl = null)
    {
        _cart.Add(productId, quantity);
        TempData["CartMessage"] = "เพิ่มสินค้าลงตะกร้าแล้ว";

        return RedirectToLocal(returnUrl ?? Url.Action("Index")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int productId, int quantity)
    {
        _cart.UpdateQuantity(productId, quantity);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        _cart.Remove(productId);
        TempData["CartMessage"] = "ลบสินค้าออกจากตะกร้าแล้ว";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        _cart.Clear();
        TempData["CartMessage"] = "ล้างตะกร้าสินค้าแล้ว";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Returns the item count so the header badge can be refreshed without a full reload.</summary>
    [HttpGet]
    public IActionResult Count() => Json(new { count = _cart.Count() });

    private IActionResult RedirectToLocal(string url) =>
        Url.IsLocalUrl(url) ? Redirect(url) : RedirectToAction(nameof(Index));
}
