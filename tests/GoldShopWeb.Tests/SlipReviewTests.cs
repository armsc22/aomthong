using GoldShopWeb.Models;
using GoldShopWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace GoldShopWeb.Tests;
public class SlipReviewTests
{
    private sealed class Env : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    [Fact]
    public void ApprovalIsAtomicAndReceiptSurvivesReopening()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Database:Path"] = Path.Combine(Path.GetTempPath(), "goldshop-tests", Guid.NewGuid().ToString("N"), "members.db")
        }).Build();
        var store = new PaymentSlipStore(config,new Env());
        var snapshot = new CheckoutViewModel { Customer = new() { Id = 42, MemberCode = "M09180001", FirstName = "Test", LastName = "Owner" }, Cart = new() { Items = [new() { ProductId = 1, Name = "Gold", Quantity = 1, Price = 1000m }] }, Shipping = 150m };
        store.Save("one", snapshot, "image/png", [1,2,3]);
        Assert.Null(store.ClaimPendingNotification("one",999));
        var pending = store.ClaimPendingNotification("one",42);
        Assert.NotNull(pending);
        Assert.Equal(1000m,pending.Total);
        Assert.Null(store.ClaimPendingNotification("one",42));
        store.CompletePendingNotification("one",false);
        Assert.NotNull(store.ClaimPendingNotification("one",42));
        store.CompletePendingNotification("one",true);
        Assert.Null(store.ClaimPendingNotification("one",42));
        Assert.Null(store.Receipt("one"));
        var approvals = new bool[2];
        Parallel.For(0,2,i => approvals[i] = store.Review("one",true,"Admin","checked"));
        Assert.Single(approvals, x=>x);
        Assert.False(store.Review("one",false,"Admin","reject"));
        var reopened = new PaymentSlipStore(config,new Env());
        var receipt = reopened.Receipt("one");
        Assert.NotNull(receipt); Assert.Equal(42,receipt.CustomerId);
        Assert.Matches("^C[0-9]{8,}$",receipt.ReceiptNumber);
        store.Save("two",snapshot,"image/png",[1,2,3]);
        Assert.False(store.Review("two",false,"Admin",""));
        Assert.True(store.Review("two",false,"Admin","ยอดไม่ตรง"));
        Assert.Null(store.Receipt("two"));
        Assert.False(store.Review("missing",true,"Admin",""));
    }
}
