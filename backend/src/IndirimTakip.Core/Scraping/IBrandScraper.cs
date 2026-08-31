namespace IndirimTakip.Core.Scraping;

public interface IBrandScraper
{
    string BrandName { get; }
    string BaseUrl { get; }

    Task<IReadOnlyList<ScrapedProduct>> ScrapeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu kaynak genel tarama turuna (6 saatte bir) DAHİL EDİLMESİN, günde bir
    /// kez ayrı çalıştırılsın mı?
    ///
    /// Varsayılan false — mevcut markaların hepsi tek bir JSON/HTML ucuna tek
    /// istek atıyor, sık taramanın maliyeti yok. Bazı bayi sitelerinde ise
    /// ürün listesi tarayıcıda çiziliyor ve ürün başına ayrı istek gerekiyor;
    /// 900+ ürünü 6 saatte bir çekmek hem yavaş hem karşı sunucuya ağır yük,
    /// üstelik engellenme riskini artırıyor.
    /// </summary>
    bool DailyOnly => false;
}
