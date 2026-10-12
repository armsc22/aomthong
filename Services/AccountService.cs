using GoldShopWeb.Data;
using GoldShopWeb.Models;

namespace GoldShopWeb.Services;

/// <summary>
/// Keeps the signed-in storefront customer in the session so the dashboard,
/// profile and cart all resolve to the same persisted member.
/// </summary>
public class AccountService
{
    private const string SessionKey = "customer-id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MemberStore _store;

    public AccountService(IHttpContextAccessor httpContextAccessor, MemberStore store)
    {
        _httpContextAccessor = httpContextAccessor;
        _store = store;
    }

    private ISession Session =>
        _httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("AccountService requires an active HTTP context.");

    public bool IsSignedIn => CurrentCustomerId() is not null;

    public int? CurrentCustomerId() =>
        Session.GetInt32(SessionKey) is int id && Session.GetInt32("pin-version") is int version && version == _store.CredentialVersion(id) ? id : null;

    public Customer? CurrentCustomer()
    {
        var id = CurrentCustomerId();
        return id is null ? null : _store.FindCustomer(id.Value);
    }

    public bool TrySignIn(string phone, string pin)
    {
        var credential = _store.AuthenticatePin(phone, pin);
        if (credential is null)
        {
            return false;
        }

        Session.SetInt32(SessionKey, credential.Value.CustomerId);
        Session.SetInt32("pin-version", credential.Value.Version);
        return true;
    }

    public void SignInAs(int customerId) { Session.SetInt32(SessionKey, customerId); Session.SetInt32("pin-version", _store.CredentialVersion(customerId)); }

    public void SignOut() => Session.Remove(SessionKey);
}

