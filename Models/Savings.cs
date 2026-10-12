namespace GoldShopWeb.Models;

/// <summary>
/// Dividend rule for gold-savings contracts: a flat yearly rate (9.9% by default)
/// accrues daily, i.e. daily dividend = principal * rate / 100 / 365.
/// </summary>
public static class SavingsRules
{
    public const int DaysPerYear = 365;

    public static decimal DailyDividend(decimal principal, decimal annualPercent) =>
        Math.Round(principal * annualPercent / 100m / DaysPerYear, 4);

    public static decimal MonthlyDividend(decimal principal, decimal annualPercent) =>
        Math.Round(DailyDividend(principal, annualPercent) * 30, 2);

    public static decimal YearlyDividend(decimal principal, decimal annualPercent) =>
        Math.Round(principal * annualPercent / 100m, 2);

    /// <summary>Daily rate in percent, e.g. 9.9% / 365 = 0.0271% per day.</summary>
    public static decimal DailyRatePercent(decimal annualPercent) =>
        Math.Round(annualPercent / DaysPerYear, 4);
}

public class SavingsPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinDeposit { get; set; }
    public int Months { get; set; }
    public decimal InterestRate { get; set; }
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Perks { get; set; } = [];
    public bool IsPopular { get; set; }
}

public class SavingsStep
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public class SavingsFaq
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class SavingsViewModel
{
    public IReadOnlyList<SavingsPlan> Plans { get; set; } = [];
    public IReadOnlyList<SavingsStep> Steps { get; set; } = [];
    public IReadOnlyList<SavingsFaq> Faqs { get; set; } = [];
    public IReadOnlyList<GoldPrice> GoldPrices { get; set; } = [];
}
