using System.Text.Json;
using GoldShopWeb.Data;
using GoldShopWeb.Models;

namespace GoldShopWeb.Services;

/// <summary>
/// Cart kept in the user's session cookie store, so no database is required.
/// </summary>
public class CartService
{
    private const string SessionKey = "shop-cart";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ISession Session =>
        _httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("CartService requires an active HTTP context.");

    public Cart Get()
    {
        var json = Session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Cart();
        }

        return JsonSerializer.Deserialize<Cart>(json, JsonOptions) ?? new Cart();
    }

    public int Count() => Get().TotalQuantity;

    public void Add(int productId, int quantity = 1)
    {
        var product = ShopCatalog.FindById(productId);
        if (product is null || product.Stock < 1 || quantity < 1)
        {
            return;
        }

        var cart = Get();
        var existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (existing is null)
        {
            var category = ShopCatalog.FindCategory(product.CategoryId);
            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                Name = product.Name,
                Weight = product.Weight,
                Image = product.Image,
                Url = $"/Products/Details/{product.Slug}",
                Price = product.Price,
                Quantity = Math.Min(quantity, product.Stock),
            });
        }
        else
        {
            existing.Quantity = (int)Math.Min((long)existing.Quantity + quantity, product.Stock);
        }

        Save(cart);
    }

    public void UpdateQuantity(int productId, int quantity)
    {
        var cart = Get();
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
        {
            return;
        }

        if (quantity < 1)
        {
            cart.Items.Remove(item);
        }
        else
        {
            var stock = ShopCatalog.FindById(productId)?.Stock ?? quantity;
            item.Quantity = Math.Min(quantity, stock);
        }

        Save(cart);
    }

    public void Remove(int productId)
    {
        var cart = Get();
        cart.Items.RemoveAll(i => i.ProductId == productId);
        Save(cart);
    }

    public void Clear() => Session.Remove(SessionKey);

    private void Save(Cart cart) => Session.SetString(SessionKey, JsonSerializer.Serialize(cart, JsonOptions));
}
