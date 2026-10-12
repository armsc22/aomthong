namespace GoldShopWeb.Models;

public class CheckoutViewModel
{
    public Customer Customer { get; init; } = new();
    public Cart Cart { get; init; } = new();
    public decimal Shipping { get; init; }
    public decimal Total => Cart.SubTotal + Shipping;
}
