using System.Globalization;
using System.Text;
using GoldShopWeb.Models;

namespace GoldShopWeb.Services;

public static class OrderPaymentMessage
{
    // Build from the saved order snapshot, never from client-submitted prices.
    public static string Build(CashSale order)
        => BuildMessage(order, null, null);

    // Call with the issued receipt only after the administrator's approval is saved.
    public static string BuildApproved(CashSale order, string receiptNumber, string receiptUrl)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException("An issued receipt number is required.", nameof(receiptNumber));
        if (!Uri.TryCreate(receiptUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("An HTTPS receipt URL is required.", nameof(receiptUrl));
        return BuildMessage(order, receiptNumber, receiptUrl);
    }

    private static string BuildMessage(CashSale order, string? receiptNumber, string? receiptUrl)
    {
        if (string.IsNullOrWhiteSpace(order.DocumentNumber) || order.Lines.Count == 0)
            throw new ArgumentException("An order number and order lines are required.", nameof(order));

        var text = new StringBuilder()
            .AppendLine(receiptNumber is null ? "ได้รับแจ้งการชำระเงินแล้ว" : "ยืนยันการชำระเงิน")
            .AppendLine($"เลขที่รายการ : {order.DocumentNumber}");
        if (receiptNumber is not null)
            text.AppendLine($"เลขที่ใบเสร็จ/ใบกำกับภาษี : {receiptNumber}");
        text.AppendLine($"รหัสลูกค้า : {order.MemberCode}")
            .AppendLine($"ชื่อ นามสกุล : {order.CustomerName}")
            .AppendLine()
            .AppendLine("รายการสั่งซื้อ");
        foreach (var line in order.Lines)
        {
            text.AppendLine($"{line.Name}")
                .AppendLine($"รหัสสินค้า : {line.Sku}")
                .AppendLine($"ราคา : {Money(line.UnitPrice)} บาท × {line.Quantity}")
                .AppendLine($"รวมรายการ : {Money(line.Total)} บาท");
        }
        text.AppendLine()
            .AppendLine($"รวมค่าสินค้า : {Money(order.Total)} บาท")
            .Append(receiptNumber is null ? "ตรวจสอบ : รอผล" : "ชำระเรียบร้อย");
        if (receiptNumber is not null)
            text.AppendLine().AppendLine("ใบเสร็จ/ใบกำกับภาษี :").Append(receiptUrl);
        return text.ToString();
    }

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}
