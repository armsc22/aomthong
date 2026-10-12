using GoldShopWeb.Models;
using System.Globalization;

namespace GoldShopWeb.Services;

public static class SavingsFlex
{
    public static string ThaiDateTime(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("th-TH")) + " น. (เวลาไทย)";

    private static object Text(string text, string color = LineFlexStyle.Ink, string size = "sm", string weight = "regular")
        => new { type = "text", text = string.IsNullOrWhiteSpace(text) ? "—" : text, color, size, weight, wrap = true };

    private static object Detail(string label, string value) => new
    {
        type = "box", layout = "vertical", spacing = "xs",
        contents = new[] { Text(label, LineFlexStyle.Muted, "xs"), Text(value, weight: "bold") }
    };

    public static object Build(SavingsRequest request, string? receiptUrl = null, string? backgroundUrl = null, string? logoUrl = null)
    {
        var approved = request.Status == "Approved";
        if (request.Status is not ("Pending" or "Approved")) throw new ArgumentException("Notification status is not supported.");
        if (approved && (string.IsNullOrEmpty(request.ReceiptNumber) || !Uri.TryCreate(receiptUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new ArgumentException("Issued receipt and HTTPS URL required.");

        var body = new List<object>
        {
            new
            {
                type = "box", layout = "vertical", backgroundColor = LineFlexStyle.Surface,
                cornerRadius = "12px", paddingAll = "lg", spacing = "sm",
                contents = new[]
                {
                    Text("เงินฝากออมทอง", LineFlexStyle.Muted, "sm"),
                    Text(request.Amount.ToString("N2", CultureInfo.InvariantCulture) + " บาท", LineFlexStyle.Ink, "xxl", "bold")
                }
            },
            Detail("เลขที่รายการ", request.DisplayNumber),
            Detail("รหัสลูกค้า", request.MemberCode),
            Detail("ชื่อ–นามสกุล", request.CustomerName),
            Detail("เลขบัญชีออมทอง", request.AccountNumber)
        };

        var timestamp = approved ? request.ReviewedAt : request.Created;
        if (DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var occurredAt))
            body.Add(Detail(approved ? "วันที่รับชำระ" : "วันที่แจ้งฝาก", ThaiDateTime(occurredAt)));

        if (approved)
        {
            body.Add(new { type = "separator", margin = "lg", color = "#E2E8DA" });
            body.Add(Detail("เลขที่ใบรับเงินมัดจำ", request.ReceiptNumber));
            body.Add(LineFlexStyle.Row("ยอดคงเหลือหลังรายการ", request.Balance.ToString("N2", CultureInfo.InvariantCulture) + " บาท", bold: true));
        }

        body.Add(new
        {
            type = "box", layout = "vertical", paddingAll = "md", cornerRadius = "8px",
            backgroundColor = approved ? "#EEF5DF" : "#FFF7E7",
            contents = new[] { Text(approved ? "ชำระเรียบร้อย" : "รอเจ้าหน้าที่ตรวจสอบ", approved ? LineFlexStyle.Accent : "#88651F", "sm", "bold") }
        });

        var bubble = new Dictionary<string, object>
        {
            ["type"] = "bubble", ["size"] = "giga",
            ["header"] = LineFlexStyle.Header(approved ? "ใบรับเงินมัดจำ" : "ได้รับแจ้งฝากเงินออมทองแล้ว", logoUrl: logoUrl),
            ["body"] = LineFlexStyle.Paper(new { type = "box", layout = "vertical", paddingAll = "xl", spacing = "lg", backgroundColor = "#FFFFFF", contents = body }, backgroundUrl)
        };
        if (approved)
            bubble["footer"] = new
            {
                type = "box", layout = "vertical", paddingAll = "xl", paddingTop = "none",
                contents = new[] { new { type = "button", style = "primary", color = LineFlexStyle.Accent, height = "sm", action = new { type = "uri", label = "ดูใบรับเงินมัดจำ", uri = receiptUrl } } }
            };
        return new { type = "flex", altText = approved ? "ใบรับเงินมัดจำ · " + request.ReceiptNumber : "แจ้งฝากออมทอง · ตรวจสอบ : รอผล", contents = bubble };
    }
}
