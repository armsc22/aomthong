using GoldShopWeb.Data;
using GoldShopWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

public class ProductsController : Controller
{
    public IActionResult Index(string? category, string? search, string sort = "popular")
    {
        if (category is not null && ShopCatalog.FindCategoryBySlug(category) is null)
        {
            return NotFound();
        }

        var model = new ProductListViewModel
        {
            Products = ShopCatalog.Query(category, search, sort),
            Categories = ShopCatalog.Categories,
            Category = category,
            Search = search,
            Sort = sort,
            TotalCount = ShopCatalog.Products.Count,
        };

        return View(model);
    }

    public IActionResult Details(string? slug)
    {
        var product = slug is null ? null : ShopCatalog.FindBySlug(slug);
        if (product is null)
        {
            return NotFound();
        }

        var model = new ProductDetailsViewModel
        {
            Product = product,
            Category = ShopCatalog.FindCategory(product.CategoryId) ?? new Category(),
            Related = ShopCatalog.Related(product),
        };

        return View(model);
    }
}
