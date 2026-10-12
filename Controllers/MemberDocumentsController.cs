using GoldShopWeb.Data;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
namespace GoldShopWeb.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class MemberDocumentsController(MemberStore store,AccountService account) : Controller
{
    private bool CanRead(int customerId) => (User.Identity?.IsAuthenticated==true && User.Identity.AuthenticationType=="AdminCookie") || account.CurrentCustomerId()==customerId;
    [HttpGet]
    public IActionResult Index(int customerId)
    {
        if(!CanRead(customerId) || store.FindCustomer(customerId) is null) return NotFound();
        return View(customerId);
    }
    [HttpGet]
    public IActionResult Image(int customerId,string kind)
    {
        if(!CanRead(customerId)) return NotFound();
        var image=store.MemberDocument(customerId,kind); if(image is null) return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";
        Response.Headers["Content-Security-Policy"]="default-src 'none'; sandbox";
        return File(image.Bytes,image.Mime);
    }
}
