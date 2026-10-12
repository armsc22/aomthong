namespace GoldShopWeb.Models;

public class GoldPrice
{
    public string Label { get; set; } = string.Empty;
    public decimal Buy { get; set; }
    public decimal Sell { get; set; }
    public decimal Change { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
}
