using IndirimTakip.Core.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Scraping;

// Açıklaması ve besin değeri normal taramada gelmeyen markalar (SSN/Hardline/
// ProteinOcean — IProductDetailFetcher implemente ediyorlar) için, ürün başına
// TEK bir HTTP isteğiyle ikisini birden tamamlar. HIQ'ya hiç dokunmuyor
// (Shopify body_html'inde ikisi de normal taramada geliyor).
//
// Bir kez doldurulan açıklama kalıcıdır. Besin değeri için ise NutritionCheckedAt
// damgası kullanılıyor: çoğu üründe (aksesuar, bar, atıştırmalık) gerçekten
// tablo yok, bu damga olmadan aynı ürünler her hafta sonsuza kadar tekrar
// denenirdi.
public class ProductDetailBackfillService(
    AppDbContext db,
    IEnumerable<IBrandScraper> scrapers,
    ILogger<ProductDetailBackfillService> logger)
{
    // Marka sitesini yormamak için ürün istekleri arası nezaket beklemesi.
    private static readonly TimeSpan DelayBetweenProducts = TimeSpan.FromMilliseconds(750);

    // Tek bir çalışmada en fazla bu kadar ürün denenir — tüm eksikleri tek
    // seferde çekmek yerine kademeli ilerlemek hem bir çalışmanın süresini
    // makul tutar hem de bir sorun çıkarsa etkiyi sınırlar.
    //
    // 60'tan 150'ye çıkarıldı (5 Eylül). Gerekçe ölçüm: katalog 4.918 ürüne
    // büyümüşken haftada 60 ürünlük hız, o günkü birikmiş eksiği (Hardline
    // 287 + ProteinOcean 241 + yeni eklenen BigJoy 143 = ~671) ancak 11
    // haftada kapatırdı. Yük yine küçük: 150 ürün dört markaya bölününce
    // marka başına ~38 istek, aralarında 750 ms bekleme — kaynak başına
    // yaklaşık yarım dakikalık trafik.
    private const int MaxProductsPerRun = 150;

    // Bu işin "en son ne zaman çalıştığı" için ayrı bir kayıt tutmaya gerek yok:
    // NutritionCheckedAt damgasını YALNIZCA bu servis yazdığı için, en yeni damga
    // doğrudan son çalışma zamanını veriyor. Zamanlamanın süreç belleğinde değil
    // burada durması önemli — periyot günler mertebesinde olduğu için, bellekte
    // tutulsaydı deploy'lar periyodun dolmasına hiç izin vermezdi (bültende tam
    // olarak bu yaşandı, bkz. DigestBackgroundService).
    public async Task<bool> IsDueAsync(int intervalDays, CancellationToken cancellationToken = default)
    {
        var lastRun = await db.Products.MaxAsync(p => p.NutritionCheckedAt, cancellationToken);
        return lastRun is null || lastRun < DateTimeOffset.UtcNow.AddDays(-intervalDays);
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var totalUpdated = 0;
        var totalAttempted = 0;

        // KOTA MARKALAR ARASINDA EŞİT BÖLÜŞÜLÜYOR.
        //
        // Önceden döngü kotayı SIRAYLA tüketiyordu: listedeki ilk markanın
        // eksiği bitmediği sürece sonrakilere hiç sıra gelmiyordu. Canlıda
        // ölçüldü (5 Eylül) — bakılan ürün sayısı Hardline 278, SSN 114,
        // ProteinOcean 47; ProteinOcean 288 ürününün %84'üne haftalarca
        // sıra gelmemişti. Tur başına 60 ürün ve haftalık periyotla bu,
        // sıradaki markanın aylarca beklemesi demek.
        //
        // Pay tavan bölme ile veriliyor: 3 marka / 60 ürün = 20. Payını
        // kullanmayan marka (eksiği bitmiş olan) kotayı serbest bırakıyor,
        // çünkü `remaining` gerçekleşen denemeye göre yeniden hesaplanıyor.
        var fetchers = scrapers.OfType<IProductDetailFetcher>().ToList();
        if (fetchers.Count == 0)
            return 0;

        var perBrandQuota = (int)Math.Ceiling((double)MaxProductsPerRun / fetchers.Count);

        foreach (var scraper in fetchers)
        {
            var brandScraper = (IBrandScraper)scraper;
            var remaining = Math.Min(perBrandQuota, MaxProductsPerRun - totalAttempted);
            if (remaining <= 0)
                break;

            // Açıklaması VEYA besin değeri henüz hiç bakılmamış ürünler.
            // (Açıklama backfill'i daha önce çalıştığı için bir kısmında
            // açıklama dolu ama NutritionCheckedAt null — onlar da hedefte.)
            // Seller == null ŞART: bu, ürünün MARKANIN KENDİ SİTESİNDEN
            // geldiği anlamına geliyor ve scraper yalnızca o sitenin
            // yapısını tanıyor.
            //
            // Bu koşul yokken (5 Eylül'e kadar) seçim sadece marka adına
            // bakıyordu ve bayilerin listelediği kopyalar da hedefe
            // giriyordu: canlıda ölçüldü, BigJoy için bakılan 38 üründen
            // 37'si protein7.com adresiydi ve BigJoy parser'ıyla çekildiği
            // için hepsi boş döndü. İki ayrı zarar veriyordu — üçüncü
            // tarafın sitesine boşuna istek gidiyor, ve o satırlar
            // "bakıldı" damgası yediği için BİR DAHA HİÇ denenmiyordu.
            // SIRALAMA ŞART, yoksa liste ilerlemiyor.
            //
            // Koşuldaki "Description == null" bazı markalarda HİÇBİR ZAMAN
            // yanlış olmuyor: Torq'un açıklaması sunucu HTML'inde yok ve
            // çekici bilerek null dönüyor. Sırasız sorgu her turda aynı ilk
            // satırları getiriyordu — canlıda ölçüldü, Torq'a 25 istek gitti
            // ve bakılan ürün sayısı 30'dan hiç artmadı; aynı 25 sayfa
            // tekrar tekrar indiriliyordu.
            //
            // Hiç bakılmamışlar önce, sonra en eski bakılanlar: her tur
            // ilerliyor ve zamanla eski kayıtlar da tazeleniyor (marka
            // sonradan besin tablosu eklemiş olabilir).
            var missingProducts = await db.Products
                .Where(p => p.Brand!.Name == brandScraper.BrandName
                    && p.Seller == null
                    && (p.Description == null || p.NutritionCheckedAt == null))
                .OrderBy(p => p.NutritionCheckedAt == null ? 0 : 1)
                .ThenBy(p => p.NutritionCheckedAt)
                .ThenBy(p => p.Id)
                .Take(remaining)
                .ToListAsync(cancellationToken);

            if (missingProducts.Count == 0)
                continue;

            logger.LogInformation(
                "{Brand}: {Count} üründe eksik detay var, tamamlanıyor.", brandScraper.BrandName, missingProducts.Count);

            foreach (var product in missingProducts)
            {
                totalAttempted++;
                try
                {
                    var details = await scraper.FetchDetailsAsync(product.Url, cancellationToken);

                    // ??= bilinçli: var olan (daha güvenilir) değeri ezmiyor.
                    product.Description ??= details.Description;
                    product.NutritionJson ??= details.NutritionJson;
                    product.ProteinPerServingGrams ??= details.ProteinPerServingGrams;

                    // Kaynağın DOĞRUDAN beyan ettiği porsiyon bilgisi önce
                    // geliyor; metinden çıkarım yalnızca o yoksa devreye
                    // giriyor (türetilmiş değer, beyanı ezmemeli).
                    product.ServingSizeGrams ??= details.ServingSizeGrams;
                    product.ServingsPerPackage ??= details.ServingsPerPackage;

                    // Açıklama metninde porsiyon büyüklüğü de geçiyor olabilir
                    // ("1 ölçek (30 g)" gibi) — scraper yapısal bir değer
                    // vermediyse buradan çıkarıyoruz.
                    if (details.Description is not null)
                        product.ServingSizeGrams ??= ProductAttributeParser.ExtractServingSizeGrams(details.Description);

                    // Sayfaya başarıyla bakıldı — tablo bulunmuş olsun olmasın
                    // damgalıyoruz ki bir daha sonsuza kadar denenmesin.
                    product.NutritionCheckedAt = DateTimeOffset.UtcNow;
                    totalUpdated++;
                }
                catch (Exception ex)
                {
                    // Tek bir ürünün hatası (404, geçici ağ sorunu vb.) diğerlerini
                    // durdurmasın. Damga da atılmıyor — sonraki çalışmada tekrar denenir.
                    logger.LogWarning(ex, "{Brand} - {Url} detayları çekilemedi.", brandScraper.BrandName, product.Url);
                }

                await Task.Delay(DelayBetweenProducts, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return totalUpdated;
    }
}
