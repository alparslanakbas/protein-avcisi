namespace IndirimTakip.Core.Scraping;

// Ürün açıklaması normal taramada (ScrapeAsync, liste/kategori sayfalarından)
// gelmiyorsa — açıklama sadece ürün DETAY sayfasında olduğu için — bu arayüzü
// implemente eden scraper'lar ürün başına ayrı bir istekle açıklama çekebilir.
// HIQ'nun aksine (açıklama zaten normal taramada geliyor, bu arayüze gerek yok)
// SSN/Hardline/ProteinOcean için gerekli. Yalnızca DescriptionBackfillService
// tarafından, haftada bir, sadece açıklaması eksik ürünler için çağrılır —
// normal 6 saatlik fiyat taramasını hiç etkilemez.
public interface IProductDescriptionFetcher
{
    Task<string?> FetchDescriptionAsync(string productUrl, CancellationToken cancellationToken = default);
}
