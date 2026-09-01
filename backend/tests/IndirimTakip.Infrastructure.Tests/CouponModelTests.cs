using IndirimTakip.Core.Entities;
using IndirimTakip.Infrastructure.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IndirimTakip.Infrastructure.Tests;

public class CouponModelTests
{
    [Theory]
    [InlineData("SSN", null, true)]
    [InlineData(null, "provitamin.com.tr", true)]
    [InlineData("SSN", "provitamin.com.tr", false)]
    [InlineData(null, null, false)]
    [InlineData(" ", " ", false)]
    public void KuponIstegiTamBirHedefIster(string? brandName, string? seller, bool expected)
    {
        var request = new CreateCouponRequest(brandName, seller, "KOD", "Açıklama", null);

        Assert.Equal(expected, request.HasExactlyOneTarget);
    }

    [Fact]
    public void KuponYalnizcaMarkaVeyaSaticidanBirineBaglanir()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=model_test;Password=model_test")
            .Options;

        using var db = new AppDbContext(options);
        var designTimeModel = db.GetService<IDesignTimeModel>().Model;
        var coupon = designTimeModel.FindEntityType(typeof(Coupon));

        Assert.NotNull(coupon);
        Assert.True(coupon.FindProperty(nameof(Coupon.BrandId))!.IsNullable);
        Assert.Equal(200, coupon.FindProperty(nameof(Coupon.Seller))!.GetMaxLength());

        // Kod OPSİYONEL: her kampanyanın kodu yok. Swiss Nutrition'ın "yeni
        // üyeye ilk alışverişte ek %5" kampanyası üyelikle otomatik uygulanıyor;
        // zorunlu tutulsaydı ya kampanya hiç gösterilemez ya da boş bir kod
        // rozeti çizilip kullanıcı olmayan bir kodu arardı.
        Assert.True(coupon.FindProperty(nameof(Coupon.Code))!.IsNullable);

        var constraint = Assert.Single(
            coupon.GetCheckConstraints(),
            c => c.Name == "CK_Coupons_ExactlyOneTarget");
        Assert.Equal("(\"BrandId\" IS NULL) <> (\"Seller\" IS NULL)", constraint.Sql);
    }
}
