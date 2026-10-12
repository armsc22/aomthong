using GoldShopWeb.Data;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace GoldShopWeb.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class SavingsController(MemberStore store,AccountService account,LineMessagingService line) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var customer=account.CurrentCustomerId();
        if(customer is null) return RedirectToAction("Login","Account",new {returnUrl="/Savings"});
        HttpContext.Session.SetString("savings-submit",HttpContext.Session.GetString("savings-submit")??Guid.NewGuid().ToString("N"));
        ViewData["SubmissionId"]=HttpContext.Session.GetString("savings-submit");
        ViewData["AccountNumber"]=store.FindAccountFor(customer.Value)?.AccountNumber;
        return View(store.SavingsRequests(customer));
    }
    [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(6*1024*1024)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public async Task<IActionResult> Submit(string id,decimal amount,IFormFile? slip)
    {
        var customer=account.CurrentCustomerId();
        if(customer is null) return Unauthorized();
        if(id!=HttpContext.Session.GetString("savings-submit")) return BadRequest();
        if(!ModelState.IsValid || slip is null || slip.Length<=0 || slip.Length>5*1024*1024)
        {TempData["SavingsMessage"]="กรุณาระบุจำนวนเงินและแนบภาพ JPG/PNG ไม่เกิน 5 MB";return RedirectToAction(nameof(Index));}
        using var buffer=new MemoryStream(); await slip.CopyToAsync(buffer);
        var image=buffer.ToArray(); var mime=PaymentSlipStore.DetectImage(image);
        var request=mime is null ? null : store.SubmitSavings(id,customer.Value,amount,image,mime);
        if(request is null){TempData["SavingsMessage"]="บันทึกไม่ได้: ตรวจยอดเงิน บัญชี และชนิดไฟล์ สลิปนี้อาจถูกส่งไว้แล้ว";return RedirectToAction(nameof(Index));}
        HttpContext.Session.Remove("savings-submit");
        TempData["SavingsMessage"]="รับรายการแล้ว ตรวจสอบ : รอผล ยอดยังไม่เข้าบัญชี";
        if(store.ClaimSavingsMessage(id,"Pending"))
        {
            var sent=await line.SendSavingsRequestAsync(store.VerifiedLineUserId(customer.Value),request);
            store.CompleteSavingsMessage(id,"Pending",sent);
            TempData["SavingsMessage"]+=sent?" · ส่ง LINE แล้ว":" · ส่ง LINE ไม่สำเร็จ สามารถกดส่งอีกครั้งจากประวัติ";
        }
        return RedirectToAction(nameof(Index));
    }
    [Authorize(AuthenticationSchemes="AdminCookie")]
    public IActionResult Review() => View(store.SavingsRequests());
    [HttpPost,ValidateAntiForgeryToken,Authorize(AuthenticationSchemes="AdminCookie")]
    public async Task<IActionResult> Decide(string id,string decision,bool verified,string? note)
    {
        if(!verified || decision is not ("approve" or "reject")) return BadRequest();
        var ok=store.ReviewSavings(id,decision=="approve",User.Identity?.Name??"",note??"");
        HttpContext.Items["AuditOutcome"]=ok?(decision=="approve"?"SavingsApproved":"SavingsRejected"):"ReviewNotApplied";
        TempData["SavingsMessage"]=ok?"บันทึกผลแล้ว":"บันทึกไม่ได้: รายการถูกตรวจแล้ว บัญชีไม่พร้อม หรือไม่ได้ระบุเหตุผล";
        if(ok && decision=="approve") await Notify(id);
        return RedirectToAction(nameof(Review));
    }
    [HttpPost,ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public async Task<IActionResult> RetryMessage(string id)
    {
        var request=store.SavingsRequest(id);
        if(request is null || (!IsAdmin && account.CurrentCustomerId()!=request.CustomerId)) return NotFound();
        if(request.Status is "Pending" or "Approved") await Notify(id);
        return RedirectToAction(IsAdmin?nameof(Review):nameof(Index));
    }
    private async Task Notify(string id)
    {
        var request=store.SavingsRequest(id)!;
        if(!store.ClaimSavingsMessage(id,request.Status)){TempData["SavingsMessage"]="ข้อความส่งแล้วหรือกำลังส่ง";return;}
        var sent=await line.SendSavingsRequestAsync(store.VerifiedLineUserId(request.CustomerId),request);
        store.CompleteSavingsMessage(id,request.Status,sent);
        TempData["SavingsMessage"]=sent?"บันทึกรายการและส่ง LINE แล้ว":"รายการบันทึกแล้ว แต่ส่ง LINE ไม่สำเร็จ กดส่งอีกครั้งได้";
    }
    private bool IsAdmin => User.Identity?.IsAuthenticated==true && User.Identity.AuthenticationType=="AdminCookie";
    [HttpGet]
    public IActionResult Receipt(string id)
    {
        var request=store.SavingsRequest(id);
        if(request is null || request.Status!="Approved" || (!IsAdmin&&account.CurrentCustomerId()!=request.CustomerId)) return NotFound();
        return View(request);
    }
    [Authorize(AuthenticationSchemes="AdminCookie")]
    public IActionResult Slip(string id)
    {
        var file=store.SavingsSlip(id); if(file is null) return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(file.Value.Image,file.Value.Mime,"savings-slip"+(file.Value.Mime=="image/png"?".png":".jpg"));
    }
}
