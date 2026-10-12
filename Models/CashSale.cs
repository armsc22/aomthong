namespace GoldShopWeb.Models;

public class CashSale
{
    public string DocumentNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string ShopAddress { get; set; } = string.Empty;
    public string ShopPhone { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public List<CashSaleLine> Lines { get; set; } = [];
    public decimal Total => Lines.Sum(line => line.Total);
}

public class CashSaleLine
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;
}
