using GoldShopWeb.Data;
using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

/// <summary>
/// The "Core DB" endpoints that the back office screen (Back_D.html) talks to.
/// Backed by the SQLite <see cref="MemberStore"/>.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
[AutoValidateAntiforgeryToken]
[Route("api/core")]
public class CoreController : ControllerBase
{
    private readonly MemberStore _store;
    private readonly LineMessagingService _line;

    public CoreController(MemberStore store, LineMessagingService line)
    {
        _store = store;
        _line = line;
    }

    [HttpGet("status")]
    public IActionResult Status() => Ok(new { connected = _store.IsConnected });

    [HttpGet("customers/search")]
    public IActionResult SearchCustomers([FromQuery] string? q)
    {
        var customers = _store.Search(q)
            .Select(c => new
            {
                id = c.Id,
                memberCode = c.MemberCode,
                firstName = c.FirstName,
                lastName = c.LastName,
                citizenId = c.CitizenId,
                phone = c.Phone,
                account = _store.FindAccountFor(c.Id),
            })
            .ToList();

        return Ok(customers);
    }

    [HttpGet("deposits/{customerId:int}")]
    public IActionResult GetSummary(int customerId)
    {
        if (_store.FindCustomer(customerId) is null)
        {
            return NotFound();
        }

        return Ok(ToSummary(_store.GetSummary(customerId)));
    }

    [HttpPost("deposits/{customerId:int}/deposit")]
    public async Task<IActionResult> Deposit(int customerId, [FromBody] AmountRequest? request)
    {
        var summary = _store.Deposit(customerId, request?.Amount ?? 0m);
        HttpContext.Items["AuditOutcome"]=summary is null?"DepositRejected":"DepositPosted";
        if (summary is null)
        {
            return BadRequest(new { error = "ยอดเงินไม่ถูกต้อง" });
        }

        var customer = _store.FindCustomer(customerId);
        var account = customer is null ? null : _store.FindAccountFor(customer.Id);
        if (customer is not null && account is not null)
        {
            await _line.SendGoldSavingDepositReceiptAsync(
                _store.VerifiedLineUserId(customer.Id), customer.FullName, account.AccountNumber,
                request?.Amount ?? 0m, summary.TotalBalance);
        }

        return Ok(ToSummary(summary));
    }

    [HttpPost("deposits/{customerId:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int customerId, [FromBody] AmountRequest? request)
    {
        var summary = _store.Withdraw(customerId, request?.Amount ?? 0m);
        HttpContext.Items["AuditOutcome"]=summary is null?"WithdrawalRejected":"WithdrawalPosted";
        if (summary is null)
        {
            return BadRequest(new { error = "ยอดเงินไม่ถูกต้องหรือยอดคงเหลือไม่พอ" });
        }

        var customer = _store.FindCustomer(customerId);
        var account = customer is null ? null : _store.FindAccountFor(customer.Id);
        if (customer is not null && account is not null)
        {
            await _line.SendTransactionNotifyAsync(
                _store.VerifiedLineUserId(customer.Id), "ถอนเงินออมทอง", request?.Amount ?? 0m,
                account.AccountNumber, summary.TotalBalance, DateTime.Today, true);
        }

        return Ok(ToSummary(summary));
    }

    private static object ToSummary(DepositSummary summary) => new
    {
        principalBalance = summary.PrincipalBalance,
        totalInterest = summary.TotalInterest,
        totalDeposit = summary.TotalDeposit,
        totalWithdrawal = summary.TotalWithdrawal,
        totalBalance = summary.TotalBalance,
        entries = summary.Entries.Select(e => new
        {
            seq = e.Seq,
            date = e.Date.ToString("yyyy-MM-dd"),
            deposit = e.Deposit,
            withdrawal = e.Withdrawal,
            interest = e.Interest,
            balanceAfter = e.BalanceAfter,
            status = e.Status,
        }),
    };

    public record AmountRequest(decimal Amount);
}
