namespace GoldShopWeb.Models;

public class ProductListViewModel
{
    public IReadOnlyList<Product> Products { get; set; } = [];
    public IReadOnlyList<Category> Categories { get; set; } = [];
    public string? Category { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "popular";
    public int TotalCount { get; set; }
    public Category? ActiveCategory =>
        Category is null ? null : Categories.FirstOrDefault(c => c.Slug == Category);
}

public class ProductDetailsViewModel
{
    public Product Product { get; set; } = new();
    public Category Category { get; set; } = new();
    public IReadOnlyList<Product> Related { get; set; } = [];
}

public class HomeViewModel
{
    public IReadOnlyList<Banner> Banners { get; set; } = [];
    public IReadOnlyList<Category> Categories { get; set; } = [];
    public IReadOnlyList<Product> Featured { get; set; } = [];
    public IReadOnlyList<Product> NewArrivals { get; set; } = [];
    public IReadOnlyList<Product> BestSellers { get; set; } = [];
    public IReadOnlyList<GoldPrice> GoldPrices { get; set; } = [];
}
