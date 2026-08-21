namespace IndirimTakip.Core.Scraping;

// Ürün detay sayfasından çekilen, normal taramada (ScrapeAsync) gelmeyen
// bilgiler. Açıklama ve besin değeri AYNI sayfada durduğu için tek bir
// çağrıda birlikte dönüyorlar — ayrı arayüzler olsaydı her ürün sayfası
// iki kez indirilir, markalar gereksiz yere yorulurdu.
// Bulunamayan alanlar null kalır; tahmin/uydurma değer üretilmez.
public record ProductDetails(string? Description, string? NutritionJson, decimal? ProteinPerServingGrams);

// Bu bilgiler sadece ürün DETAY sayfasında olduğu için, bu arayüzü
// implemente eden scraper'lar ürün başına ayrı bir istek atar.
// HIQ'nun aksine (açıklama + besin tablosu zaten normal taramada, Shopify
// body_html içinde geliyor — bu arayüze gerek yok) SSN/Hardline/ProteinOcean
// için gerekli. Yalnızca ProductDetailBackfillService tarafından, haftada bir,
// sadece eksik ürünler için çağrılır — 6 saatlik fiyat taramasını etkilemez.
public interface IProductDetailFetcher
{
    Task<ProductDetails> FetchDetailsAsync(string productUrl, CancellationToken cancellationToken = default);
}
