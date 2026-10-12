using System.Diagnostics;
using GoldShopWeb.Data;
using GoldShopWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // LINE first returns to the LIFF endpoint (/). Initialize the SDK here
        // before it redirects to the secondary registration/profile URL.
        if (Request.Query.ContainsKey("liff.state") || Request.Query.ContainsKey("liffClientId"))
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return View("LineReturn");
        }
        var model = new HomeViewModel
        {
            Banners = ShopCatalog.Banners,
            Categories = ShopCatalog.Categories,
            Featured = ShopCatalog.Products.Where(p => p.IsFeatured).Take(8).ToList(),
            NewArrivals = ShopCatalog.Products.Where(p => p.IsNew).Take(4).ToList(),
            BestSellers = ShopCatalog.Products.Where(p => p.IsBestSeller).Take(4).ToList(),
            GoldPrices = ShopCatalog.GoldPrices(),
        };

        return View(model);
    }

    public IActionResult Privacy() => View();

    public IActionResult Savings()
    {
        var model = new SavingsViewModel
        {
            Plans = ShopCatalog.SavingsPlans,
            Steps = ShopCatalog.SavingsSteps,
            Faqs = ShopCatalog.SavingsFaqs,
            GoldPrices = ShopCatalog.GoldPrices(),
        };

        return View(model);
    }

    public IActionResult Contact() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
