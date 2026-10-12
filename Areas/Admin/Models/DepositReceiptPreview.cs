using GoldShopWeb.Models;

namespace GoldShopWeb.Areas.Admin.Models;

public class DepositReceiptPreview
{
    public Customer Customer { get; init; } = new();
    public ShopSettings Shop { get; init; } = new();
    public DateTime PreviewTime { get; init; }
    public string DocumentNumber => $"D{PreviewTime:MMdd}0000";
    public string MemberCodeExample => $"M{PreviewTime:MMdd}0000";
}
