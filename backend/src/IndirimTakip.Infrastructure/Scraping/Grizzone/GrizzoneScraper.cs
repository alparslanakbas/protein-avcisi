using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.Grizzone;

/// <summary>
/// grizzone.com.tr — otuzuncu kaynak. ikas; Gigi's/MLA ile aynı desen
/// (products.xml sitemap + ürün sayfasındaki schema.org bloğu).
///
/// <b>ÖLÇÜM (3 Eylül):</b> 116 adresten 114'ü veri verdi. Katalogda 35 ürün
/// takviye dışı — giyim (t-shirt, hoodie, atlet, eşofman), ekipman (strap,
/// kemer, wrist wrap, loop band), aksesuar (shaker, havlu, çanta, anahtarlık,
/// pillbox) ve çeşni (sıkılabilir sos, cajun baharatı). Kalan ~79 ürün gerçek
/// takviye: kreatin, BCAA, arginin, whey.
///
/// <b>Katalog TAMAMEN BÜYÜK HARF</b> ve bu, ortak süzgeçteki Türkçe harf
/// hatasını ortaya çıkardı: "ANAHTARLIK" gibi adlar `anahtarlık` kalıbıyla
/// eşleşmiyordu. Süzgeç artık adı ASCII'ye indirgeyip öyle eşleştiriyor.
/// </summary>
public sealed class GrizzoneScraper(HttpClient httpClient, ILogger<GrizzoneScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger)
{
    public override string BrandName => "Grizzone";
    public override string BaseUrl => "https://grizzone.com.tr";
    protected override string SitemapUrl => "https://grizzone.com.tr/products.xml";
}
