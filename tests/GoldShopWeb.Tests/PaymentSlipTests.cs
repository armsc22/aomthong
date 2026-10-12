using GoldShopWeb.Services;
using Xunit;

namespace GoldShopWeb.Tests;

public class PaymentSlipTests
{
    [Fact]
    public void RejectsExecutableHtmlAndEmptyDataRegardlessOfFilename()
    {
        Assert.Null(PaymentSlipStore.DetectImage([]));
        Assert.Null(PaymentSlipStore.DetectImage(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>")));
        Assert.Null(PaymentSlipStore.DetectImage([77, 90, 0, 0]));
        Assert.Null(PaymentSlipStore.DetectImage([255, 216, 255, 0]));
    }
}
