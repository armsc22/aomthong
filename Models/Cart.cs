namespace GoldShopWeb.Models;

public class CartItem
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Url { get; set; } = "/Products";
    public decimal Price { get; set; }
    public int Quantity { get; set; } = 1;

    public decimal LineTotal => Price * Quantity;
}

public class Cart
{
    public List<CartItem> Items { get; set; } = [];

    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public decimal SubTotal => Items.Sum(i => i.LineTotal);
    public bool IsEmpty => Items.Count == 0;
}
