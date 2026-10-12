using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Xunit;

namespace GoldShopWeb.Tests;

public class OrderPaymentMessageTests
{
    [Fact]
    public void IncludesCustomerEveryProductAndPendingStatus()
    {
        var order = new CashSale
        {
            DocumentNumber = "C09180001", MemberCode = "M09180001", CustomerName = "ทดสอบ ระบบ",
            Lines = [new() { Sku = "GP-01", Name = "ทองคำ", Quantity = 2, UnitPrice = 1000.25m },
                     new() { Sku = "GP-02", Name = "แหวน", Quantity = 1, UnitPrice = 500m }]
        };
        var message = OrderPaymentMessage.Build(order);
        Assert.Contains("เลขที่รายการ : C09180001", message);
        Assert.Contains("รหัสลูกค้า : M09180001", message);
        Assert.Contains("ชื่อ นามสกุล : ทดสอบ ระบบ", message);
        Assert.Contains("GP-01", message);
        Assert.Contains("GP-02", message);
        Assert.Contains("1,000.25 บาท × 2", message);
        Assert.Contains("รวมค่าสินค้า : 2,500.50 บาท", message);
        Assert.EndsWith("ตรวจสอบ : รอผล", message);
    }

    [Fact]
    public void RejectsUnnumberedOrEmptyOrders()
    {
        Assert.Throws<ArgumentException>(() => OrderPaymentMessage.Build(new CashSale()));
        Assert.Throws<ArgumentException>(() => OrderPaymentMessage.Build(new CashSale { DocumentNumber = "C09180001" }));
    }

    [Fact]
    public void ApprovedMessageSeparatesOrderAndReceiptAndIncludesLink()
    {
        var order = new CashSale
        {
            DocumentNumber = "ORDER-001", MemberCode = "M001", CustomerName = "ทดสอบ ระบบ",
            Lines = [new() { Sku = "GP-01", Name = "ทองคำ", Quantity = 2, UnitPrice = 1000m }]
        };
        var text = OrderPaymentMessage.BuildApproved(order, "C09180001", "https://shop.example/receipts/1");
        Assert.Contains("เลขที่รายการ : ORDER-001", text);
        Assert.Contains("เลขที่ใบเสร็จ/ใบกำกับภาษี : C09180001", text);
        Assert.Contains("รหัสลูกค้า : M001", text);
        Assert.Contains("ชื่อ นามสกุล : ทดสอบ ระบบ", text);
        Assert.Contains("รหัสสินค้า : GP-01", text);
        Assert.Contains("ราคา : 1,000.00 บาท × 2", text);
        Assert.Contains("ชำระเรียบร้อย", text);
        Assert.DoesNotContain("รอผล", text);
        Assert.EndsWith("https://shop.example/receipts/1", text);
    }

    [Theory]
    [InlineData("", "https://shop.example/receipt")]
    [InlineData("C001", "/receipt")]
    [InlineData("C001", "javascript:alert(1)")]
    public void ApprovedMessageRequiresIssuedReceiptAndHttpsLink(string number, string url)
        => Assert.Throws<ArgumentException>(() => OrderPaymentMessage.BuildApproved(new CashSale(), number, url));
}
