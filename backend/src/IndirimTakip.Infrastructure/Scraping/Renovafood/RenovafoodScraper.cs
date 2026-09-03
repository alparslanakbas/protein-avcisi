using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Renovafood;

/// <summary>
/// renovafood.com.tr — otuz ikinci kaynak. Altyapı TICIMAX, yani ikas değil —
/// ama sitemap + schema.org deseni birebir aynı çalıştığı için aynı taban
/// sınıf kullanılıyor. Desen platformdan bağımsız: "ürün adreslerini bir
/// sitemap'ten al, veriyi sayfadaki schema.org bloğundan oku".
///
/// <b>ÖLÇÜM (3 Eylül):</b> 38 adresin 38'i de veri verdi, hata yok, süzgeç
/// hiçbir ürünü elemiyor, hepsi stokta. Katalog kolajen, bitkisel çay ve
/// "challenge/detox" paketlerinden oluşuyor — wellness ağırlıklı bir marka.
///
/// Sitemap adresi ikas'takinden farklı: <c>/sitemap/products/0.xml</c>.
///
/// <b>ŞU AN DEVRE DIŞI — site sunucumuzun IP'sini ENGELLİYOR.</b> Deploy
/// sonrası ölçüldü: sitemap VM'den 200 dönüyor ama ürün sayfaları 403
/// (User-Agent'lı da, UA'sız da). Geliştirme makinesinden 38/38 ürün
/// sorunsuz alınıyordu, yani kod doğru — engel ağ tarafında.
/// Supplementler.com ile aynı durum; DI kaydı yorumda bekliyor.
/// </summary>
public sealed class RenovafoodScraper(HttpClient httpClient, ILogger<RenovafoodScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger)
{
    public override string BrandName => "Renovafood";
    public override string BaseUrl => "https://renovafood.com.tr";
    protected override string SitemapUrl => "https://renovafood.com.tr/sitemap/products/0.xml";
}
