using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GoldShopWeb.Services
{
    public class LineMessagingService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _channelAccessToken;
        private readonly string _baseUrl;
        private readonly string _liffUrl;
        private readonly ILogger<LineMessagingService> _log;
        private readonly GoldShopWeb.Data.MemberStore? _auditStore;
        private readonly IHttpContextAccessor? _context;
        public string? AdminUserId { get; }
        private string? LineLogoUrl => LineFlexStyle.AssetUrl(_baseUrl, "aomthong-logo-v1.png");
        private string? LinePaperUrl => LineFlexStyle.AssetUrl(_baseUrl, "aomthong-leaf-paper-v1.png");

        public LineMessagingService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<LineMessagingService> log, GoldShopWeb.Data.MemberStore? auditStore=null, IHttpContextAccessor? context=null)
        {
            _auditStore=auditStore; _context=context;
            _channelAccessToken = configuration["LineApi:ChannelAccessToken"];
            AdminUserId = configuration["LineApi:AdminUserId"];
            _baseUrl = configuration["LineApi:BaseUrl"] ?? "https://revisit-overbite-subsonic.ngrok-free.dev";
            _liffUrl = configuration["LineApi:LiffUrl"] ?? "https://liff.line.me/2011581137-WJ7kqrNL";
            _log = log;

            _httpClient = httpClientFactory.CreateClient("line");
            if (!string.IsNullOrEmpty(_channelAccessToken) && _channelAccessToken != "YOUR_LINE_CHANNEL_ACCESS_TOKEN")
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _channelAccessToken);
            }
        }

        private static bool TokenConfigured(string? token) =>
            !string.IsNullOrWhiteSpace(token) && !token.StartsWith("YOUR_", StringComparison.Ordinal);

        private static string MaskToken(string? token) => string.IsNullOrEmpty(token)?"<none>":"<configured>";

        private bool LineResult(bool success,[System.Runtime.CompilerServices.CallerMemberName] string method="")
        {
            var http=_context?.HttpContext;
            var actor=http?.User.Identity?.IsAuthenticated==true?"staff:"+http.User.Identity.Name:http?.Session.GetInt32("customer-id") is int id?"customer:"+id:"system";
            try{_auditStore?.RecordActivity(actor,"LINE/"+method,"",success?"AcceptedByLINE":"NotConfirmed",http?.TraceIdentifier??"");}
            catch(Exception ex){_log.LogError("LINE audit write failed: {Type}",ex.GetType().Name);}
            return success;
        }

        private void LogSkip(string method, string lineUserId, string reason)
        {
            _log.LogInformation(
                "LINE {Method} skipped: {Reason} (token={Token}, to={To})",
                method, reason, MaskToken(_channelAccessToken), lineUserId);
        }

        public async Task<bool> SendRegistrationSuccessAsync(string lineUserId, string memberCode, string phone, string citizenId, string fullName = "")
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrEmpty(lineUserId))
            {
                LogSkip(nameof(SendRegistrationSuccessAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = "แจ้งผลการลงทะเบียนออมทองทวีทรัพย์",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header("ลงทะเบียนสำเร็จ", logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    new { type = "text", text = "ยินดีต้อนรับสู่ครอบครัวออมทองทวีทรัพย์ครับ", size = "sm", color = "#64748B", wrap = true, align = "center", margin = "sm" },
                                    new { type = "separator", margin = "xl" },
                                    new
                                    {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            CreateRow("รหัสสมาชิก", memberCode, "#1E293B", true),
                                            CreateRow("ชื่อ-นามสกุล", string.IsNullOrEmpty(fullName) ? "-" : fullName, "#1E293B", true),
                                            CreateRow("สถานะ", "พร้อมใช้งาน", "#10B981", true)
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new { type = "text", text = "🔐 ข้อมูลสำหรับเข้าสู่ระบบ", margin = "xl", size = "sm", color = "#64748B", weight = "bold", wrap = true },
                                    new
                                    {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "md",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            CreateRow("เบอร์โทร", phone, "#1E293B", true),
                                            CreateRow("เข้าสู่ระบบ", "ใช้ PIN 6 หลักที่ตั้งไว้ตอนสมัคร", "#1E293B", true)
                                        }
                                    },
                                    new { type = "text", text = "*เพื่อความปลอดภัย กรุณาอย่าเปิดเผยรหัสผ่านแก่ผู้อื่น", margin = "xl", size = "xs", color = "#94A3B8", align = "center", wrap = true }
                                }
                            }, LinePaperUrl),
                            footer = new
                            {
                                type = "box",
                                layout = "vertical",
                                spacing = "sm",
                                contents = new object[]
                                {
                                    new
                                    {
                                        type = "button",
                                        style = "primary",
                                        height = "sm",
                                        color = LineFlexStyle.Accent,
                                        action = new
                                        {
                                            type = "uri",
                                            label = "เข้าสู่ระบบ",
                                            uri = _baseUrl + "/login.html?ngrok-skip-browser-warning=true"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendRegistrationSuccessAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendRegistrationSuccessAsync));
                return LineResult(false);
            }
        }

        // Invoke only after saving a payment submission, with the verified customer's LINE ID.
        public Task<bool> SendSavingsRequestAsync(string lineUserId, GoldShopWeb.Models.SavingsRequest request)
        {
            string? url=null;
            if(request.Status=="Approved")
            {
                if(!Uri.TryCreate(_baseUrl,UriKind.Absolute,out var baseUri)||baseUri.Scheme!="https") return Task.FromResult(false);
                url=new Uri(baseUri,"/Account/Login?returnUrl="+Uri.EscapeDataString("/Savings/Receipt?id="+request.Id)).AbsoluteUri;
            }
            return SendOrderPaymentFlexAsync(lineUserId,SavingsFlex.Build(request,url,LinePaperUrl,LineLogoUrl));
        }

        public Task<bool> SendOrderPaymentPendingAsync(string lineUserId, GoldShopWeb.Models.CashSale order)
            => SendOrderPaymentFlexAsync(lineUserId, OrderPaymentFlex.Build(order));

        // Invoke after approval and receipt issuance are committed; use a server-generated receipt URL.
        public Task<bool> SendApprovedSlipAsync(string lineUserId, ApprovedSlip receipt)
        {
            if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != "https") return Task.FromResult(false);
            using var json = JsonDocument.Parse(receipt.Snapshot);
            var data = json.RootElement;
            var order = new GoldShopWeb.Models.CashSale { DocumentNumber=receipt.TransactionNumber, CustomerId=receipt.CustomerId,
                MemberCode=data.GetProperty("MemberCode").GetString() ?? "", CustomerName=data.GetProperty("FullName").GetString() ?? "" };
            foreach(var item in data.GetProperty("Items").EnumerateArray())
                order.Lines.Add(new GoldShopWeb.Models.CashSaleLine { ProductId=item.GetProperty("ProductId").GetInt32(),
                    Sku=item.TryGetProperty("Sku",out var sku) ? sku.GetString() ?? "" : "", Name=item.GetProperty("Name").GetString() ?? "",
                    Quantity=item.GetProperty("Quantity").GetInt32(), UnitPrice=item.GetProperty("Price").GetDecimal() });
            var returnPath = "/Checkout/Receipt?id=" + Uri.EscapeDataString(receipt.Id);
            var url = new Uri(baseUri,"/Account/Login?returnUrl="+Uri.EscapeDataString(returnPath)).AbsoluteUri;
            return SendOrderPaymentApprovedAsync(lineUserId,order,receipt.ReceiptNumber,url);
        }

        public Task<bool> SendOrderPaymentApprovedAsync(string lineUserId, GoldShopWeb.Models.CashSale order,
            string receiptNumber, string receiptUrl)
            => SendOrderPaymentFlexAsync(lineUserId, OrderPaymentFlex.Build(order, receiptNumber, receiptUrl));

        private async Task<bool> SendOrderPaymentFlexAsync(string lineUserId, object message)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrWhiteSpace(lineUserId))
                return LineResult(false);

            if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(message)) > 29000) return LineResult(false);
            using var content = new StringContent(JsonSerializer.Serialize(new
            {
                to = lineUserId,
                messages = new[] { message }
            }), Encoding.UTF8, "application/json");
            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                _log.LogInformation("LINE order payment notification: status={Status}", (int)response.StatusCode);
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (HttpRequestException)
            {
                return LineResult(false);
            }
            catch (TaskCanceledException)
            {
                return LineResult(false);
            }
        }

        public async Task<bool> SendCashSaleSuccessAsync(string lineUserId, string memberName, string memberCode, string accountNo, string productNames, decimal amount, string receiptNumber)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrEmpty(lineUserId))
            {
                LogSkip(nameof(SendCashSaleSuccessAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = "แจ้งชำระเงินสดเต็มจำนวน (รอตรวจสอบ)",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header("แจ้งชำระเงินสดเต็มจำนวน", logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    new { type = "text", text = "เราได้รับแจ้งการชำระเงินของท่านแล้ว", size = "sm", color = "#64748B", align = "center", margin = "sm", wrap = true },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[] {
                                            CreateRow("เลขที่คำสั่งซื้อ", receiptNumber, "#1E293B", true),
                                            CreateRow("ลูกค้า", memberName, "#1E293B", true),
                                            CreateRow("รหัสสมาชิก", memberCode, "#1E293B", false),
                                            CreateRow("เลขที่บัญชี", accountNo, "#1E293B", false),
                                            CreateRow("รหัสสินค้า", productNames, "#1E293B", false)
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "horizontal",
                                        margin = "xl",
                                        contents = new object[] {
                                            new { type = "text", text = "ยอดชำระ", size = "md", color = "#64748B", flex = 2, weight = "bold", wrap = true },
                                            new { type = "text", text = $"฿{amount:N2} บาท", size = "lg", color = "#10B981", flex = 4, weight = "bold", align = "end", wrap = true }
                                        }
                                    },
                                    new { type = "text", text = "โอนเข้า: ธ.กสิกรไทย 012-3-45678-9 บริษัท ออมทองทวีทรัพย์ จำกัด", size = "xs", color = "#64748B", align = "center", margin = "lg", wrap = true },
                                    new { type = "text", text = "(แนบสลิปผ่านหน้าเว็บแล้ว)", size = "xs", color = "#94A3B8", align = "center", margin = "sm", wrap = true },
                                    new { type = "text", text = "⏳ สถานะ: รอเจ้าหน้าที่ตรวจสอบและอนุมัติ", size = "sm", color = "#F59E0B", align = "center", margin = "xl", weight = "bold", wrap = true }
                                }
                            }, LinePaperUrl)
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendCashSaleSuccessAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendCashSaleSuccessAsync));
                return LineResult(false);
            }
        }

        public async Task<bool> SendBookingSuccessAsync(string lineUserId, string memberName, string memberCode, string productNames, decimal totalAmount, decimal depositAmount, int months, decimal monthlyInstallment, string contractNumber)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrEmpty(lineUserId))
            {
                LogSkip(nameof(SendBookingSuccessAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = "แจ้งคำขอจองทองคำ (รอตรวจสอบ)",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header("แจ้งคำขอจองทองคำ", logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    new { type = "text", text = "เราได้รับคำขอจองทองคำของท่านแล้ว", size = "sm", color = "#64748B", align = "center", margin = "sm", wrap = true },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "📝 เลขที่สัญญา", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = contractNumber, color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "👤 ลูกค้า", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = memberName, color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "🛍 สินค้า", color = "#64748B", size = "sm", flex = 1, wrap = true }, new { type = "text", text = productNames, color = "#1E293B", size = "sm", align = "end", wrap = true, flex = 2 } } }
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "ยอดรวมทั้งสิ้น", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = "฿" + totalAmount.ToString("N2"), color = "#1E293B", size = "sm", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "เงินมัดจำ", color = "#64748B", size = "md", weight = "bold", wrap = true }, new { type = "text", text = "฿" + depositAmount.ToString("N2"), color = "#10B981", size = "md", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "ระยะเวลาผ่อน", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = months.ToString() + " เดือน", color = "#1E293B", size = "sm", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "ผ่อนชำระงวดละ", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = "฿" + monthlyInstallment.ToString("N2"), color = "#3B82F6", size = "sm", align = "end", wrap = true } } }
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new { type = "text", text = "⏳ สถานะ: รอเจ้าหน้าที่ตรวจสอบและอนุมัติ", size = "xs", color = "#F59E0B", margin = "md", align = "center", weight = "bold", wrap = true }
                                }
                            }, LinePaperUrl)
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendBookingSuccessAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendBookingSuccessAsync));
                return LineResult(false);
            }
        }

        public async Task<bool> SendDepositSuccessAsync(string lineUserId, string memberName, string contractNumber, decimal amount, int installmentNo, int totalInstallments)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrEmpty(lineUserId))
            {
                LogSkip(nameof(SendDepositSuccessAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = "ใบรับเงินมัดจำ (ออมทอง)",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header("ชำระค่างวดออมทอง", SavingsFlex.ThaiDateTime(DateTimeOffset.UtcNow), logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    new { type = "text", text = "เราได้รับยอดเงินของท่านเรียบร้อยแล้ว", size = "sm", color = "#64748B", align = "center", margin = "sm", wrap = true },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "📝 เลขที่สัญญา", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = contractNumber, color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "👤 ลูกค้า", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = memberName, color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "📅 งวดที่ชำระ", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = $"{installmentNo} / {totalInstallments}", color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } }
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "ยอดเงินฝาก", color = "#64748B", size = "md", weight = "bold", wrap = true }, new { type = "text", text = "฿" + amount.ToString("N2"), color = "#10B981", size = "md", weight = "bold", align = "end", wrap = true } } }
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new { type = "text", text = "✅ ทำรายการสำเร็จ", size = "xs", color = "#10B981", margin = "md", align = "center", weight = "bold", wrap = true }
                                }
                            }, LinePaperUrl)
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendDepositSuccessAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendDepositSuccessAsync));
                return LineResult(false);
            }
        }


        public async Task<bool> SendGoldSavingDepositReceiptAsync(string lineUserId, string memberName, string accountNumber, decimal amount, decimal balance)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrEmpty(lineUserId))
            {
                LogSkip(nameof(SendGoldSavingDepositReceiptAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = "ใบรับเงินมัดจำ (ออมทอง)",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header("ใบรับเงินมัดจำ", SavingsFlex.ThaiDateTime(DateTimeOffset.UtcNow), logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    new { type = "text", text = "เราได้รับยอดเงินของท่านเรียบร้อยแล้ว", size = "sm", color = "#64748B", align = "center", margin = "sm", wrap = true },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "📝 เลขที่บัญชี", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = accountNumber, color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "👤 ลูกค้า", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = memberName, color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } },
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "ยอดเงินสะสม", color = "#64748B", size = "sm", wrap = true }, new { type = "text", text = "฿" + balance.ToString("N2"), color = "#1E293B", size = "sm", weight = "bold", align = "end", wrap = true } } }
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[]
                                        {
                                            new { type = "box", layout = "horizontal", contents = new object[] { new { type = "text", text = "ยอดเงินฝาก", color = "#64748B", size = "md", weight = "bold", wrap = true }, new { type = "text", text = "฿" + amount.ToString("N2"), color = "#10B981", size = "md", weight = "bold", align = "end", wrap = true } } }
                                        }
                                    },
                                    new { type = "separator", margin = "xl" },
                                    new { type = "text", text = "✅ ทำรายการสำเร็จ", size = "xs", color = "#10B981", margin = "md", align = "center", weight = "bold", wrap = true }
                                }
                            }, LinePaperUrl)
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendGoldSavingDepositReceiptAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendGoldSavingDepositReceiptAsync));
                return LineResult(false);
            }
        }


        public async Task<bool> ReplyWelcomeMessageAsync(string replyToken)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrEmpty(replyToken))
            {
                LogSkip(nameof(ReplyWelcomeMessageAsync), replyToken ?? "", "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                replyToken = replyToken,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = "ยินดีต้อนรับสู่ห้างทองทวีทรัพย์",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header("ยินดีต้อนรับ", logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    new { type = "text", text = "ยินดีต้อนรับสู่", size = "sm", color = "#F59E0B", weight = "bold", wrap = true },
                                    new { type = "text", text = "ออมทองทวีทรัพย์", weight = "bold", size = "xl", color = "#1E293B", margin = "sm", wrap = true },
                                    new { type = "text", text = "เริ่มต้นความมั่งคั่งด้วยการออมทองกับเรา สะสมทองคำแท้ด้วยดอกเบี้ยสุดคุ้ม สมัครเปิดบัญชีง่ายๆ ผ่านระบบอัตโนมัติได้ทันที!", wrap = true, color = "#64748B", margin = "md", size = "sm" }
                                }
                            }, LinePaperUrl),
                            footer = new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                spacing = "sm",
                                contents = new object[]
                                {
                                    new
                                    {
                                        type = "button",
                                        style = "primary",
                                        color = LineFlexStyle.Accent,
                                        height = "sm",
                                        action = new
                                        {
                                            type = "uri",
                                            label = "📝 สมัครสมาชิกออมทอง",
                                            uri = _liffUrl
                                        }
                                    },
                                    new
                                    {
                                        type = "button",
                                        style = "secondary",
                                        height = "sm",
                                        action = new
                                        {
                                            type = "message",
                                            label = "💬 ติดต่อสอบถาม",
                                            text = "ต้องการสอบถามข้อมูลเพิ่มเติม"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/reply", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE reply {Method}: status={Status} token={Token} replyToken={ReplyToken} bodyLen={Len}",
                    nameof(ReplyWelcomeMessageAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), MaskToken(replyToken), body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE reply failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE reply {Method} threw", nameof(ReplyWelcomeMessageAsync));
                return LineResult(false);
            }
        }

        public async Task<bool> SendTransactionNotifyAsync(string lineUserId, string transactionName, decimal amount, string accountNo, decimal availableBalance, DateTime transactionDate, bool isMoneyOut)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrWhiteSpace(lineUserId))
            {
                LogSkip(nameof(SendTransactionNotifyAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            string amountColor = isMoneyOut ? "#FF0000" : "#10B981"; // Red for money out, Green for money in
            string amountPrefix = isMoneyOut ? "-" : "+";

            var payload = new
            {
                to = lineUserId,
                messages = new[]
                {
                    new
                    {
                        type = "flex",
                        altText = $"แจ้งเตือนรายการ {transactionName}",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header(transactionName, logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                backgroundColor = "#FFFFFF",
                                spacing = "md",
                                paddingAll = "xl",
                                contents = new object[]
                                {
                                    CreateRow("จำนวน", $"{amountPrefix}{amount:N2} บาท", amountColor, true),
                                    CreateRow("เลขที่บัญชี / ID", accountNo, "#333333", false),
                                    CreateRow("ยอดคงเหลือ", $"{availableBalance:N2} บาท", "#333333", false),
                                    CreateRow("วันที่ทำรายการ", transactionDate.ToString("dd/MM/yy HH:mm"), "#94A3B8", false)
                                }
                            }, LinePaperUrl)
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");


            try
            {
                using var response = await _httpClient.PostAsync("https://api.line.me/v2/bot/message/push", content);
                var body = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendTransactionNotifyAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, body.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(body, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendTransactionNotifyAsync));
                return LineResult(false);
            }
        }

        public async Task<bool> SendOrderStatusNotifyAsync(
            string lineUserId,
            string orderType,
            string referenceNo,
            decimal amount,
            string status,
            string statusDescription)
        {
            if (!TokenConfigured(_channelAccessToken) || string.IsNullOrWhiteSpace(lineUserId))
            {
                LogSkip(nameof(SendOrderStatusNotifyAsync), lineUserId, "channel access token not configured");
                return LineResult(false);
            }

            var payload = new
            {
                to = lineUserId,
                messages = new object[]
                {
                    new
                    {
                        type = "flex",
                        altText = $"อัปเดตสถานะ: {orderType}",
                        contents = new
                        {
                            type = "bubble",
                            header = LineFlexStyle.Header(orderType, logoUrl: LineLogoUrl),
                            body = LineFlexStyle.Paper(new
                            {
                                type = "box",
                                layout = "vertical",
                                paddingAll = "xl",
                                spacing = "md",
                                contents = new object[]
                                {
                                    new { type = "text", text = $"สถานะปัจจุบัน: {status}", size = "md", weight = "bold", color = status == "อนุมัติ" || status == "สำเร็จ" ? "#10B981" : (status == "ยกเลิก" || status == "ไม่อนุมัติ" ? "#EF4444" : "#3B82F6"), align = "center", wrap = true },
                                    new { type = "text", text = statusDescription, size = "sm", color = "#64748B", wrap = true, align = "center", margin = "sm" },
                                    new { type = "separator", margin = "xl" },
                                    new {
                                        type = "box",
                                        layout = "vertical",
                                        margin = "xl",
                                        spacing = "sm",
                                        contents = new object[] {
                                            CreateRow("เลขที่อ้างอิง", referenceNo, "#1E293B", true),
                                            CreateRow("ยอดเงิน", $"{amount:N2} บาท", "#1E293B", true)
                                        }
                                    }
                                }
                            }, LinePaperUrl)
                        }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.PostAsync(
                    "https://api.line.me/v2/bot/message/push", content);
                var respBody = await response.Content.ReadAsStringAsync();
                _log.LogInformation(
                    "LINE push {Method}: status={Status} token={Token} to={To} bodyLen={Len}",
                    nameof(SendOrderStatusNotifyAsync), (int)response.StatusCode,
                    MaskToken(_channelAccessToken), lineUserId, respBody.Length);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("LINE push failed body: {Body}", Truncate(respBody, 512));
                }
                return LineResult(response.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "LINE push {Method} threw", nameof(SendOrderStatusNotifyAsync));
                return LineResult(false);
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

        private object CreateRow(string label, string value, string valueColor, bool isBold)
            => LineFlexStyle.Row(label, value, valueColor, isBold);
    }
}


