using GoldShopWeb.Models;
namespace GoldShopWeb.Data;

// Used only when a new development database is explicitly seeded.
internal sealed class DemoMembers
{
    internal readonly List<Customer> _customers = [];
    internal readonly List<DepositAccount> _accounts = [];
    internal readonly List<LedgerEntry> _ledger = [];
    internal DemoMembers() => Seed();
    private void Seed()
    {
        var tester = new Customer
        {
            Id = 1,
            MemberCode = "PC000001",
            FirstName = "นาย ทดสอบ",
            LastName = "ระบบ",
            CitizenId = "1111111111111",
            Phone = "081-000-0001",
        };
        var somchai = new Customer
        {
            Id = 2,
            MemberCode = "PC000002",
            FirstName = "นาง สมหญิง",
            LastName = "ทองดี",
            CitizenId = "2222222222222",
            Phone = "081-000-0002",
        };
        _customers.AddRange([tester, somchai]);

        _accounts.AddRange(
        [
            new DepositAccount
            {
                Id = 1,
                AccountNumber = "1021000001",
                CustomerId = tester.Id,
                DepositTypeCode = "102",
                DepositTypeName = "ออมทรัพย์/เผื่อเรียก พิเศษ",
                InterestRate = 1.00m,
            },
            new DepositAccount
            {
                Id = 2,
                AccountNumber = "1021000002",
                CustomerId = somchai.Id,
                DepositTypeCode = "101",
                DepositTypeName = "ออมทรัพย์ทั่วไป",
                InterestRate = 0.75m,
            },
        ]);

        // Opening deposit, then 30 monthly interest postings that accumulate to the
        // balance shown on the reference screen before the 3,000 baht deposit.
        var running = 0m;
        _ledger.Add(new LedgerEntry
        {
            Seq = 1,
            AccountId = 1,
            Date = LastDayOfMonth(2019, 1),
            Deposit = 1000m,
            BalanceAfter = 1000m,
            Status = 1,
        });
        running = 1000m;

        var monthlyInterest = new decimal[30];
        for (var i = 0; i < 29; i++)
        {
            monthlyInterest[i] = 0.82m;
        }

        monthlyInterest[29] = 24.72m - monthlyInterest.Take(29).Sum();

        for (var i = 0; i < monthlyInterest.Length; i++)
        {
            running += monthlyInterest[i];
            _ledger.Add(new LedgerEntry
            {
                Seq = i + 2,
                AccountId = 1,
                Date = LastDayOfMonth(2019, 2 + i),
                Interest = monthlyInterest[i],
                BalanceAfter = running,
                Status = 4,
            });
        }

        // Recent postings copied from the Back_D.html ledger (Buddhist years 2564-2565).
        var recent = new (DateTime Date, decimal Deposit, decimal Interest, int Status)[]
        {
            (new DateTime(2021, 8, 31), 0m, 0.87m, 4),
            (new DateTime(2021, 9, 30), 0m, 0.84m, 4),
            (new DateTime(2021, 10, 31), 0m, 0.87m, 4),
            (new DateTime(2021, 11, 30), 0m, 0.84m, 4),
            (new DateTime(2021, 12, 31), 0m, 0.87m, 4),
            (new DateTime(2022, 1, 31), 0m, 0.87m, 4),
            (new DateTime(2022, 2, 28), 0m, 0.79m, 4),
            (new DateTime(2022, 3, 15), 3000m, 0m, 1),
        };

        var seq = monthlyInterest.Length + 2;
        foreach (var (date, deposit, interest, status) in recent)
        {
            running += deposit + interest;
            _ledger.Add(new LedgerEntry
            {
                Seq = seq++,
                AccountId = 1,
                Date = date,
                Deposit = deposit,
                Interest = interest,
                BalanceAfter = running,
                Status = status,
            });
        }

        // A small account for the second member so search returns more than one row.
        _ledger.Add(new LedgerEntry
        {
            Seq = 1,
            AccountId = 2,
            Date = new DateTime(2022, 1, 10),
            Deposit = 5000m,
            BalanceAfter = 5000m,
            Status = 1,
        });
    }

    private static DateTime LastDayOfMonth(int year, int monthsFromJanuary)
    {
        var first = new DateTime(year, 1, 1).AddMonths(monthsFromJanuary - 1);
        return first.AddMonths(1).AddDays(-1);
    }
}


