using GoldShopWeb.Areas.Admin.Models;
using GoldShopWeb.Data;
using GoldShopWeb.Extensions;
using GoldShopWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
public class DepositController : Controller
{
    private readonly MemberStore _store;
    private readonly ShopSettings _settings;

    public DepositController(MemberStore store, ShopSettings settings)
    {
        _store = store;
        _settings = settings;
    }

    public IActionResult Index(int? customerId)
    {
        var customer = customerId.HasValue ? _store.FindCustomer(customerId.Value) : _store.FindCustomer(1) ?? _store.Search(null).FirstOrDefault();
        if (customerId.HasValue && customer is null) return NotFound();
        if (customer is null)
        {
            return View(new AdminDepositViewModel
            {
                TodayThai = DateTime.Today.ToThaiDate(),
                AnnualDividendPercent = _settings.SavingsAnnualDividendPercent,
            });
        }

        var account = _store.FindAccountFor(customer.Id) ?? new DepositAccount();
        var summary = _store.GetSummary(customer.Id);

        return View(new AdminDepositViewModel
        {
            Customer = customer,
            Account = account,
            Summary = summary,
            TransactionNo = $"DW{summary.Entries.Count + 1:D6}",
            TodayThai = DateTime.Today.ToThaiDate(),
            AnnualDividendPercent = _settings.SavingsAnnualDividendPercent,
            DailyDividend = SavingsRules.DailyDividend(summary.TotalBalance, _settings.SavingsAnnualDividendPercent),
        });
    }
}
