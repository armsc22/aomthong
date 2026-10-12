namespace GoldShopWeb.Models;

/// <summary>
/// Bound from the "ShopSettings" section of appsettings.json so the brand can be
/// renamed in one place without touching views.
/// </summary>
public class ShopSettings
{
    public string ShopName { get; set; } = "บริษัท ออมทองทวีทรัพย์ จำกัด";
    public string ShopNameEn { get; set; } = "AOMTHONG GOLD";
    public string Tagline { get; set; } = "ออมทองและค้าทองคำ เชื่อถือได้มากกว่า 30 ปี";
    public string Phone { get; set; } = "092-2724363";
    public string Hotline { get; set; } = "092-2724363";
    public string Email { get; set; } = "contact@aomthonggold.example";
    public string Address { get; set; } = "87/14-15 ถ.เศรษฐสัมพันธ์ ต.หนองแค อ.หนองแค จ.สระบุรี 18140";
    public string OpeningHours { get; set; } = "เปิดทุกวัน 09:30 - 18:30 น.";
    public string LineId { get; set; } = "@aomthonggold";
    public decimal FreeShippingThreshold { get; set; } = 20000m;

    /// <summary>Flat yearly dividend paid on any gold-savings contract deposit.</summary>
    public decimal SavingsAnnualDividendPercent { get; set; } = 9.9m;
}
