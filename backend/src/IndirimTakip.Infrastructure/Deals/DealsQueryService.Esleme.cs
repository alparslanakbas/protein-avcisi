using IndirimTakip.Infrastructure.Images;

namespace IndirimTakip.Infrastructure.Deals;

// Sorgu sonucunu DealDto'ya çeviren bellek içi eşleme. Ana dosyadan ayrı, çünkü o dosya boyut tavanında
// (scripts/boyut-siniri.sh) ve burada SQL yok: EF'in çevirdiği hiçbir şeye dokunmuyor.
public partial class DealsQueryService
{
    private DealDto MapToDealDto(DealRow row)
    {
        var latest = row.Latest;
        // Tekil sorgularda güncel fiyat canlı, referans özetten: tarama sürerken eksi indirim olmasın.
        var referencePrice = Math.Max(row.ReferencePrice, latest.Price);
        var otuzGununEnDusugu = latest.Price <= row.ThirtyDayLowPrice && row.ThirtyDayLowPrice < referencePrice;
        return new DealDto(
            row.Product.Id, row.Product.Name, row.Product.Url,
            // Yerel kopya varsa o, yoksa kaynak adres. Sorgu değil, bellek
            // içi eşleme — bu dosyanın SQL üreten kısmına dokunulmuyor.
            ProductImageStore.GenelAdres(row.Product.LocalImagePath, gorselTabanAdresi) ?? row.Product.ImageUrl,
            row.Product.Category, row.Product.Size, row.Product.Flavor, row.Product.ServingSizeGrams,
            row.Product.ServingsPerPackage,
            row.Product.Description,
            row.Product.NutritionJson,
            row.Product.ProteinPerServingGrams,
            row.BrandName, latest.Price, referencePrice,
            // Referans fiyat sıfır olabiliyor (fiyatı girilmemiş bir ürün);
            // hemen aşağıdaki mağaza indirimi satırı bunu zaten koruyordu ama
            // burası korumasızdı ve fiyata göre sıralandığında o ürün başa
            // geldiği için tüm liste sıfıra bölme hatasıyla düşüyordu.
            referencePrice > 0 ? Math.Round((referencePrice - latest.Price) / referencePrice * 100, 1) : 0m,
            latest.StoreOldPrice,
            latest.StoreOldPrice is decimal storeOld && storeOld > 0
                ? Math.Round((storeOld - latest.Price) / storeOld * 100, 1)
                : null,
            latest.ScrapedAt,
            otuzGununEnDusugu,
            IsStale: false,
            ReplacementProductId: null,
            RatingValue: row.Product.RatingValue,
            RatingCount: row.Product.RatingCount,
            InStock: row.Product.InStock,
            Seller: row.Product.Seller,
            AffiliateLinkBuilder.Apply(row.Product.Url, row.BrandName, affiliateOptions.Value),
            // Liste sorguları ürün satırını bütünüyle yüklüyor; takip özeti ek sorgu istemiyor.
            LowestSince: TakipDibi.Baslangic(
                otuzGununEnDusugu, latest.Price, row.Product.TrackedSince, row.Product.LowestTrackedPrice, DateTimeOffset.UtcNow));
    }
}
