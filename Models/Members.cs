namespace GoldShopWeb.Models;

public class Customer
{
    public int Id { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LineUserId { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class DepositAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string DepositTypeCode { get; set; } = string.Empty;
    public string DepositTypeName { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    public string Branch { get; set; } = "01";
    public bool IsActive { get; set; } = true;
}

public class LedgerEntry
{
    public int Seq { get; set; }
    public int AccountId { get; set; }
    public DateTime Date { get; set; }
    public decimal Deposit { get; set; }
    public decimal Withdrawal { get; set; }
    public decimal Interest { get; set; }
    public decimal BalanceAfter { get; set; }
    public int Status { get; set; }
    public bool Printed { get; set; }
}

public class DepositSummary
{
    public decimal PrincipalBalance { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal TotalDeposit { get; set; }
    public decimal TotalWithdrawal { get; set; }
    public decimal TotalBalance => PrincipalBalance + TotalInterest;
    public IReadOnlyList<LedgerEntry> Entries { get; set; } = [];
}
