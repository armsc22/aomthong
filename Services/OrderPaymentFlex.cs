using GoldShopWeb.Models;
using System.Globalization;

namespace GoldShopWeb.Services;
public static class OrderPaymentFlex
{
    private static object Text(string value, string color = "#24364B", string size = "sm", string weight = "regular")
        => new { type="text", text=string.IsNullOrWhiteSpace(value) ? "—" : value, color, size, weight, wrap=true };
    private static object Row(string label,string value) => new { type="box", layout="vertical", spacing="xs", contents=new[]{Text(label,"#8492A6","xs"),Text(value,weight:"bold")} };
    private static string Money(decimal amount) => amount.ToString("N2",CultureInfo.InvariantCulture)+" บาท";
    public static object Build(CashSale order, string? receiptNumber = null, string? receiptUrl = null)
    {
        if(receiptNumber is null) _ = OrderPaymentMessage.Build(order);
        else _ = OrderPaymentMessage.BuildApproved(order,receiptNumber,receiptUrl!);
        var approved = receiptNumber is not null;
        var rows = new List<object> { Row("เลขที่รายการ",order.DocumentNumber), Row("รหัสลูกค้า",order.MemberCode), Row("ชื่อ–นามสกุล",order.CustomerName) };
        if(approved) rows.Add(Row("เลขที่ใบเสร็จรับเงิน",receiptNumber!));
        rows.Add(new {type="separator",margin="lg"});
        rows.Add(Text("รายการสั่งซื้อ",weight:"bold"));
        foreach(var item in order.Lines)
        {
            if(string.IsNullOrWhiteSpace(item.Sku)) item.Sku = GoldShopWeb.Data.ShopCatalog.FindById(item.ProductId)?.Sku ?? "";
            if(string.IsNullOrWhiteSpace(item.Sku)) item.Sku = $"PRODUCT-{item.ProductId}";
            rows.Add(new { type="box", layout="vertical", spacing="xs", contents=new[]{Text(item.Name,weight:"bold"),Text("รหัสสินค้า : "+(string.IsNullOrWhiteSpace(item.Sku) ? "ไม่ระบุ" : item.Sku),"#8492A6","xs"),Text($"{Money(item.UnitPrice)} × {item.Quantity}"),Text("รวมรายการ : "+Money(item.Total))} });
        }
        rows.Add(new {type="separator",margin="lg"});
        rows.Add(Row("รวมค่าสินค้า",Money(order.Total)));
        rows.Add(Text(approved ? "ชำระเรียบร้อย" : "ตรวจสอบ : รอผล",approved ? "#10A34A" : "#B7791F","md","bold"));
        var bubble = new Dictionary<string,object> {
            ["type"]="bubble", ["size"]="giga",
            ["header"]=new {type="box",layout="vertical",spacing="md",backgroundColor=approved ? "#10A34A" : "#996B22",paddingAll="xl",contents=new[]{Text("✨ ออมทองทวีทรัพย์ ✨","#FFE08A","sm","bold"),Text(approved ? "ชำระเงินเรียบร้อย" : "ได้รับแจ้งการชำระเงินแล้ว","#FFFFFF","lg","bold")}},
            ["body"]=new {type="box",layout="vertical",spacing="md",paddingAll="xl",contents=rows}
        };
        if(approved) bubble["footer"]=new {type="box",layout="vertical",contents=new[]{new {type="button",style="primary",color="#10A34A",action=new {type="uri",label="ดูใบเสร็จ",uri=receiptUrl}}}};
        return new {type="flex",altText=approved ? "ชำระเรียบร้อย · ใบเสร็จ "+receiptNumber : "ได้รับแจ้งการชำระเงินแล้ว · ตรวจสอบ : รอผล",contents=bubble};
    }
}
