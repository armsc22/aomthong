using GoldShopWeb.Models;

namespace GoldShopWeb.Areas.Admin.Models;

public class AdminDepositViewModel
{
    public Customer Customer { get; set; } = new();
    public DepositAccount Account { get; set; } = new();
    public DepositSummary Summary { get; set; } = new();
    public string TransactionNo { get; set; } = string.Empty;
    public string TodayThai { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = "admin";
    public string Institution { get; set; } = "สถาบันการเงิน";
    public string Branch { get; set; } = "01";
    public decimal AnnualDividendPercent { get; set; }
    public decimal DailyDividend { get; set; }
}
