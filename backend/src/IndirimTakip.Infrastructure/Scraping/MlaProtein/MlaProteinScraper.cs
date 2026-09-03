using IndirimTakip.Core.Scraping;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping.MlaProtein;

/// <summary>
/// mlaprotein.com — yirmi sekizinci kaynak. ikas mağazası; Gigi's ile aynı
/// desen (products.xml + ürün sayfasındaki schema.org bloğu).
///
/// <b>HİBRİT KAYNAK — marka SAYFADAN okunuyor.</b> Mağaza kendi markasının
/// yanında başka üreticilerin ürünlerini de satıyor. 3 Eylül ölçümü, 86
/// üründe: mla protein 32, Nutraxin 23, Dr. Pan 9, Seed'n Grains 7,
/// Fitnut 5, detoksfit 4, markasız 6. Dr Pan, Fitnut ve Seed'n Grains
/// katalogda ZATEN var (bayilerden geliyorlar); marka adları
/// <c>BrandNameNormalizer</c>'dan geçtiği için aynı Brand kaydına
/// düşüyorlar, kopya marka oluşmuyor.
///
/// Swiss / Dr Supplement / Muscle Pump / Nois ile aynı hibrit desen. Seller
/// alanı bilinçli olarak DOLDURULMUYOR: bunlar mağazanın kendi listelemeleri
/// ve mağaza aynı zamanda üretici — bayi ayrımı burada anlamlı değil.
///
/// <b>ELENENLER (ölçüldü):</b> 86 üründen 14'ü takviye dışı — çeşni ve
/// baharat grubu (BBQ Sos, Şekersiz Ketçap, Garlic powder, Hot Chili,
/// Cajun/Chicken/Vegetable/BBQ Mix, Sprey Yağ, Aromalı Tatlandırıcı,
/// Sriracha Sos, Bal Aromalı Hardal, Himalaya Tuzu) ve bir Protein Shaker.
/// Kalan: 72 ürün.
/// </summary>
public sealed class MlaProteinScraper(HttpClient httpClient, ILogger<MlaProteinScraper> logger)
    : SitemapSchemaOrgScraper(httpClient, logger)
{
    public override string BrandName => "MLA Protein";
    public override string BaseUrl => "https://mlaprotein.com";
    protected override string SitemapUrl => "https://mlaprotein.com/products.xml";

    protected override bool ReadBrandFromPage => true;
}
