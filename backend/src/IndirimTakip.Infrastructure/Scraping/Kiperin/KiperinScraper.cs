using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Kiperin;

/// <summary>
/// kiperinturkiye.com — otuz birinci kaynak. ikas; Gigi's/MLA/Grizzone ile
/// aynı desen.
///
/// <b>ÖLÇÜM (3 Eylül):</b> 48 adresin 48'i de veri verdi, hata yok.
/// Süzgeç HİÇBİR ürünü elemiyor — katalogun tamamı takviye: kolajen, B/D3K2
/// vitaminleri, B12 sprey, multivitamin. Vitamin ağırlıklı bir marka
/// (Vitabear ve GNC ile aynı kategori).
/// </summary>
public sealed class KiperinScraper(HttpClient httpClient, ILogger<KiperinScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger)
{
    public override string BrandName => "Kiperin";
    public override string BaseUrl => "https://kiperinturkiye.com";
    protected override string SitemapUrl => "https://kiperinturkiye.com/products.xml";
}
