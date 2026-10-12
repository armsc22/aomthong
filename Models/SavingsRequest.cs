namespace GoldShopWeb.Models;

public class SavingsRequest
{
    public string Id { get; set; } = "";
    public string TransactionNumber { get; set; } = "";
    public string DisplayNumber => string.IsNullOrEmpty(TransactionNumber) ? Id : TransactionNumber;
    public int CustomerId { get; set; }
    public int AccountId { get; set; }
    public string AccountNumber { get; set; } = "";
    public string MemberCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public long AmountMinor { get; set; }
    public string Created { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string ReceiptNumber { get; set; } = "";
    public string ReviewedAt { get; set; } = "";
    public string ReviewedBy { get; set; } = "";
    public string Note { get; set; } = "";
    public long BalanceMinor { get; set; }
    public decimal Amount => AmountMinor / 100m;
    public decimal Balance => BalanceMinor / 100m;
}
