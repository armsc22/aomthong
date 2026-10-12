namespace GoldShopWeb.Models;

public class CustomerBalance
{
    public int CustomerId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string ContractNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public decimal DepositBalance { get; set; }
}
