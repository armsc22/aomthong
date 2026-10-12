namespace GoldShopWeb.Models;

public class Banner
{
    public string Badge { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string CtaText { get; set; } = "ดูสินค้า";
    public string CtaUrl { get; set; } = "/Products";
    public string Image { get; set; } = string.Empty;
    public string Theme { get; set; } = "dark";
}
