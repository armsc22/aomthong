using GoldShopWeb.Data;
using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GoldShopWeb.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CheckoutController(CartService cartService, AccountService account, ShopSettings shop, PaymentSlipStore slips, MemberStore members, LineMessagingService line) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var cart = cartService.Get();
        if (cart.IsEmpty) return RedirectToAction("Index", "Cart");
        var customer = account.CurrentCustomer();
        if (customer is null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Checkout") });
        foreach (var item in cart.Items)
        {
            var product = ShopCatalog.FindById(item.ProductId);
            if (product is null || item.Quantity < 1 || item.Quantity > product.Stock)
            {
                TempData["CartMessage"] = "สินค้าบางรายการมีจำนวนไม่พร้อมจำหน่าย กรุณาตรวจสอบตะกร้าอีกครั้ง";
                return RedirectToAction("Index", "Cart");
            }
            item.Price = product.Price;
            item.Sku = string.IsNullOrWhiteSpace(product.Sku) ? $"PRODUCT-{product.Id}" : product.Sku;
        }
        var fingerprint = System.Text.Json.JsonSerializer.Serialize(new { customer.Id, Items = cart.Items.Select(i => new { i.ProductId, i.Quantity, i.Price }), Shipping = cart.SubTotal >= shop.FreeShippingThreshold ? 0 : 150m });
        if (HttpContext.Session.GetString("payment-submission") is null || HttpContext.Session.GetString("payment-cart") != fingerprint)
        {
            HttpContext.Session.SetString("payment-submission", Guid.NewGuid().ToString("N"));
            HttpContext.Session.SetString("payment-cart", fingerprint);
        }
        ViewData["SubmissionId"] = HttpContext.Session.GetString("payment-submission");
        return View("Index", new CheckoutViewModel { Customer = customer, Cart = cart,
            Shipping = cart.SubTotal >= shop.FreeShippingThreshold ? 0 : 150m });
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("MemberLogin")]
    public async Task<IActionResult> UploadSlip(IFormFile? slip, string submissionId)
    {
        var result = Index();
        if (result is not ViewResult view || view.Model is not CheckoutViewModel model) return result;
        if (submissionId != HttpContext.Session.GetString("payment-submission")) return BadRequest();
        if (slip is null || slip.Length == 0 || slip.Length > 5 * 1024 * 1024)
            ModelState.AddModelError("", "กรุณาแนบสลิป JPG หรือ PNG ขนาดไม่เกิน 5 MB");
        else
        {
            using var buffer = new MemoryStream();
            await slip.CopyToAsync(buffer);
            var bytes = buffer.ToArray(); var mime = PaymentSlipStore.DetectImage(bytes);
            if (mime is null) ModelState.AddModelError("", "รองรับเฉพาะไฟล์ภาพ JPG หรือ PNG");
            else
            {
                slips.Save(submissionId, model, mime, bytes);
                TempData["SlipMessage"] = $"รับสลิปแล้ว — ตรวจสอบ : รอผล (เลขที่อ้างอิง {submissionId})";
                var order = slips.ClaimPendingNotification(submissionId, model.Customer.Id);
                if (order is not null)
                {
                    TempData["SlipMessage"]=$"รับสลิปแล้ว — ตรวจสอบ : รอผล (เลขที่รายการ {order.DocumentNumber})";
                    var sent = await line.SendOrderPaymentPendingAsync(members.VerifiedLineUserId(model.Customer.Id), order);
                    slips.CompletePendingNotification(submissionId, sent);
                    TempData["SlipMessage"] += sent ? " · ส่งข้อความ LINE แล้ว" : " · ส่ง LINE ไม่สำเร็จ แต่บันทึกสลิปแล้ว สามารถแนบสลิปเดิมอีกครั้งเพื่อส่งข้อความใหม่";
                }
                return RedirectToAction(nameof(Index));
            }
        }
        return view;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
    public IActionResult Slips() => View(slips.List());

    [HttpPost, ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
    public async Task<IActionResult> ReviewSlip(string id, string decision, bool verified, string? note)
    {
        if (decision is not ("approve" or "reject") || !verified)
        {
            TempData["ReviewMessage"] = "กรุณายืนยันว่าได้ตรวจสอบยอดเงินและหลักฐานแล้ว";
            return RedirectToAction(nameof(Slips));
        }
        var ok = slips.Review(id, decision == "approve", User.Identity?.Name ?? "", note ?? "");
        HttpContext.Items["AuditOutcome"]=ok?(decision=="approve"?"PaymentApproved":"PaymentRejected"):"ReviewNotApplied";
        TempData["ReviewMessage"] = ok ? "บันทึกผลตรวจสอบแล้ว" : "บันทึกไม่ได้: รายการถูกตรวจสอบไปแล้ว หรือกรอกเหตุผลไม่ครบ";
        if (ok && decision == "approve" && slips.Receipt(id) is { } receipt)
        {
            var sent = await line.SendApprovedSlipAsync(members.VerifiedLineUserId(receipt.CustomerId),receipt);
            TempData["ReviewMessage"] += sent ? " · ส่ง Flex Message ทาง LINE แล้ว" : " · ส่ง LINE ไม่สำเร็จ แต่ผลอนุมัติและใบเสร็จบันทึกแล้ว";
        }
        return RedirectToAction(nameof(Slips));
    }

    [HttpGet]
    public IActionResult Receipt(string id)
    {
        var receipt = slips.Receipt(id);
        if (receipt is null) return NotFound();
        if (!(User.Identity?.IsAuthenticated ?? false) && account.CurrentCustomerId() != receipt.CustomerId) return NotFound();
        return View(receipt);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "AdminCookie")]
    public IActionResult Slip(string id)
    {
        var file = slips.Get(id);
        if (file is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Value.Image, file.Value.Mime, "payment-slip" + (file.Value.Mime == "image/png" ? ".png" : ".jpg"));
    }
}
