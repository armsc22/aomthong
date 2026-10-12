using System.Globalization;

namespace GoldShopWeb.Extensions;

public static class MoneyExtensions
{
    private static readonly CultureInfo Th = new("th-TH");

    /// <summary>1234.5 -> "1,235 บาท"</summary>
    public static string ToBaht(this decimal value) =>
        $"{Math.Round(value, 2).ToString("N2", Th)} บาท";

    /// <summary>1234.5 -> "฿1,235" for compact places like the cart badge and price bars.</summary>
    public static string ToBahtShort(this decimal value) =>
        $"฿{Math.Round(value).ToString("N0", Th)}";

    /// <summary>+150 -> "+150" / -50 -> "-50", used by the gold price ticker.</summary>
    public static string ToSigned(this decimal value) =>
        value >= 0 ? $"+{value:N0}" : value.ToString("N0", Th);
}
