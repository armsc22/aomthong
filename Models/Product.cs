namespace GoldShopWeb.Models;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public string Weight { get; set; } = string.Empty;
    public string Purity { get; set; } = "96.5%";

    /// <summary>ค่ากำเหน็จ (ทองรูปพรรณ) หรือค่าบล็อก (ทองคำแท่ง).</summary>
    public decimal CraftingFee { get; set; }
    public string Image { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public bool IsBestSeller { get; set; }
    public int Stock { get; set; } = 10;

    public decimal DiscountPercent =>
        OldPrice is > 0 ? Math.Round((OldPrice.Value - Price) / OldPrice.Value * 100m) : 0m;

    public bool InStock => Stock > 0;
}
