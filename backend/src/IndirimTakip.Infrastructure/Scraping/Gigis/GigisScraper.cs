using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Gigis;

/// <summary>
/// gigis.com.tr — yirmi yedinci kaynak. ikas mağazası; ürün adresleri
/// <c>products.xml</c> sitemap'inden, veriler ürün sayfasındaki schema.org
/// bloğundan geliyor (ikisi de sunucu tarafında render ediliyor).
///
/// <b>NİŞ NOTU.</b> Gigi's bir ATIŞTIRMALIK markası: granola, bal ile glaze
/// edilmiş "crunchie"ler, smoothie ve kolajenli protein bar. Kataloğun
/// tamamı gıda; klasik takviye (protein tozu, kreatin, amino asit) YOK.
/// Kapsama giren kısım protein barlar ve granolalar —
/// <c>saglikli-atistirmaliklar</c> kategorimiz zaten var. Vitabear'daki
/// kararın devamı; ölçüm kullanıcıya sunuldu.
///
/// <b>ELENENLER (ölçüldü, 3 Eylül):</b> 79 üründen 11'i takviye dışı — 8'i
/// el yapımı seramik kase / kuru yemişlik, 2'si bez çanta. Ayrıca "Kendi
/// Paketini Kendin Yap" 0 TL ile geliyor (yapılandırıcı sayfası, gerçek ürün
/// değil) ve fiyat şartına takılıp eleniyor. Kalan: 68 ürün.
/// </summary>
public sealed class GigisScraper(HttpClient httpClient, ILogger<GigisScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger)
{
    public override string BrandName => "Gigi's";
    public override string BaseUrl => "https://gigis.com.tr";
    protected override string SitemapUrl => "https://gigis.com.tr/products.xml";

    // Katalogda "Gigi's" ve "Gigi's Smoothie" olmak üzere iki marka adı
    // geçiyor; ikisi de aynı markanın ürünleri, o yüzden sayfadan OKUNMUYOR,
    // hepsi tek marka altında toplanıyor. Aksi halde dizinde aynı markanın
    // iki satırı olurdu.
    protected override bool ReadBrandFromPage => false;
}
