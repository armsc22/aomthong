using System.ComponentModel.DataAnnotations;
using GoldShopWeb.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace GoldShopWeb.Areas.Admin.Models;

public class CashSaleViewModel
{
    public int CustomerId { get; set; }
    public List<CashSaleSelection> Items { get; set; } = [];
    [ValidateNever]
    public Customer Customer { get; set; } = new();
    [ValidateNever]
    public IReadOnlyList<Product> Products { get; set; } = [];
}

public class CashSaleSelection
{
    public int ProductId { get; set; }
    [Range(1, 1000, ErrorMessage = "จำนวนสินค้าต้องอยู่ระหว่าง 1 ถึง 1,000 ชิ้น")]
    public int Quantity { get; set; } = 1;
}
