using IndirimTakip.Core.Entities;
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

    // SIRA KARARI ÜRÜN DAMGASINDAN DEĞİL, TURUN KENDİ TAMAMLANMA KAYDINDAN
    // VERİLİYOR (6 Eylül'de değiştirildi).
    //
    // Önceden MAX(Products.NutritionCheckedAt) kullanılıyordu ve gerekçesi
    // makuldü: o damgayı yalnızca bu servis yazıyor. Ama bir durumu
    // kaçırıyordu — tur yarıda kesilirse. 6 Eylül'de canlıda yaşandı: tur
    // başladı, TEK ürün işledi, deploy konteyneri yenileyince iptal oldu ve
    // o tek damga sırayı tam bir aralık öteledi. Yoğun deploy yapılan bir
    // günde iş hiç ilerlemeden sürekli ertelenebilirdi.
    //
    // Zamanlama yine VERİTABANINDA (bellekte değil): periyot günler
    // mertebesinde ve bellekte tutulsaydı her deploy sayacı sıfırlardı.
    public async Task<bool> IsDueAsync(int intervalDays, CancellationToken cancellationToken = default)
    {
        var lastCompleted = await db.BackgroundJobRuns
            .Where(j => j.JobName == BackgroundJobNames.DetayTamamlama)
            .Select(j => (DateTimeOffset?)j.LastCompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return lastCompleted is null || lastCompleted < DateTimeOffset.UtcNow.AddDays(-intervalDays);
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var totalUpdated = 0;

        var fetchers = scrapers.OfType<IProductDetailFetcher>().ToList();
        if (fetchers.Count == 0)
            return 0;

        var markaAdlari = fetchers.Select(f => ((IBrandScraper)f).BrandName).ToList();

        // Kota, markaların GERÇEK iş yüküne göre dağıtılıyor (bkz. PayDagit).
        var ihtiyaclar = await IhtiyacSorgusu(db, markaAdlari).ToListAsync(cancellationToken);

        var paylar = PayDagit(ihtiyaclar, MaxProductsPerRun);

        foreach (var scraper in fetchers)
        {
            var brandScraper = (IBrandScraper)scraper;
            var remaining = paylar.GetValueOrDefault(brandScraper.BrandName);
            if (remaining <= 0)
                continue;

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
                try
                {
                    var details = await scraper.FetchDetailsAsync(product.Url, cancellationToken);

                    // ??= bilinçli: var olan (daha güvenilir) değeri ezmiyor.
                    product.Description ??= details.Description;

                    // Besin tarafına elle karar verilmişse hiç dokunulmuyor. ??= tek
                    // başına yetmiyordu: "bu üründe tablo yok" denen ürünün alanı
                    // NULL kalıyor ve ??= onu ilk turda yeniden dolduruyordu, yani
                    // kalıcı olması gereken karar sessizce geri alınıyordu.
                    if (!product.NutritionIsManual)
                    {
                        product.NutritionJson ??= details.NutritionJson;
                        product.ProteinPerServingGrams ??= details.ProteinPerServingGrams;
                    }

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

        // TAMAMLANMA DAMGASI BURADA, DÖNGÜNÜN SONUNDA ATILIYOR.
        //
        // Buraya yalnızca tur baştan sona bittiyse geliniyor: iptal edilen bir
        // tur (deploy, konteyner yenileme) ürünler arasındaki `Task.Delay`
        // noktasında istisna fırlatıp metottan çıkıyor ve bu satıra hiç
        // ulaşmıyor. Yani yarıda kesilen tur sırayı İLERLETMİYOR, bir sonraki
        // kontrolde yeniden "sırası geldi" diyor.
        await TamamlandiIsaretleAsync(cancellationToken);

        return totalUpdated;
    }

    internal sealed record MarkaIhtiyaci(string Marka, int HicBakilmamis, int YenidenKontrol);

    // Aşağıdaki tur sorgusuyla AYNI koşul (Seller == null, açıklama ya da
    // besin damgası eksik): pay, markanın gerçekten seçilebilecek ürün
    // sayısını aşmasın. Ayrı metot, çünkü SQL'e çevrilebildiği testle
    // sınanıyor (EF gruplu koşullu sayımı çeviremezse hata ancak çalışma
    // anında çıkardı).
    internal static IQueryable<MarkaIhtiyaci> IhtiyacSorgusu(AppDbContext db, IReadOnlyCollection<string> markaAdlari) =>
        db.Products
            .Where(p => p.Seller == null
                && markaAdlari.Contains(p.Brand!.Name)
                && (p.Description == null || p.NutritionCheckedAt == null))
            .GroupBy(p => p.Brand!.Name)
            .Select(g => new MarkaIhtiyaci(
                g.Key,
                g.Count(p => p.NutritionCheckedAt == null),
                g.Count(p => p.NutritionCheckedAt != null)));

    // Hiç bakılmamış ürünlerde bir markaya bir turda verilecek en fazla pay.
    // West/Nois etiket görselini OCR'dan geçiriyor (ürün başına ~5 sn, ölçüldü
    // 15 Eylül); tavan hem VM'i hem marka sitesini tek turda yormamak için.
    internal const int YeniUrunMarkaTavani = 60;

    // Zaten bakılmış ürünlerin yeniden kontrolünde marka başına pay: eski eşit
    // bölüşümün değeri (150 / 13 çekici ≈ 12). Yeniden kontrol tazeleme işi,
    // hiç bakılmamış ürünlerle yarışmamalı.
    internal const int YenidenKontrolMarkaTavani = 12;

    // KOTA İŞ YÜKÜNE GÖRE DAĞITILIYOR (15 Eylül).
    //
    // Önceden her markaya sabit pay veriliyordu (150 / 13 = 12). Payını
    // kullanmayan marka onu kimseye devretmiyordu, bu yüzden canlıda ölçülen
    // durum şuydu: West'te 161, ProteinOcean'da 121 hiç bakılmamış ürün
    // beklerken ikisi de günde 12 alıyordu (West ~14 gün). Aynı turda
    // açıklaması kaynakta hiç olmayan markalar (Torq 156, Fellas 118,
    // Grizzone 80...) kendi 12'lerini aynı sayfaları yeniden indirmeye
    // harcıyordu, yani kotanın ~100'ü tazelemeye gidiyordu.
    //
    // İki kademe: önce hiç bakılmamış ürünler, sonra artan kota yeniden
    // kontrole. Her kademede "su doldurma": ihtiyacı küçük marka ihtiyacı
    // kadar alır, artan pay kalan markalara eşit bölünür.
    internal static Dictionary<string, int> PayDagit(IReadOnlyList<MarkaIhtiyaci> ihtiyaclar, int toplam)
    {
        var paylar = ihtiyaclar.ToDictionary(i => i.Marka, _ => 0);

        var kalan = SuDoldur(
            ihtiyaclar.ToDictionary(i => i.Marka, i => Math.Min(i.HicBakilmamis, YeniUrunMarkaTavani)),
            toplam, paylar);

        SuDoldur(
            ihtiyaclar.ToDictionary(i => i.Marka, i => Math.Min(i.YenidenKontrol, YenidenKontrolMarkaTavani)),
            kalan, paylar);

        return paylar;
    }

    // `talepler` her markanın bu kademede alabileceği en fazla pay. Dağıtılan
    // paylar `paylar`a eklenir, dağıtılamayan kota döner.
    private static int SuDoldur(Dictionary<string, int> talepler, int kota, Dictionary<string, int> paylar)
    {
        // Sıralama sabit olsun diye adla: aynı veriyle her tur aynı dağılım.
        var bekleyen = talepler.Where(t => t.Value > 0)
            .OrderBy(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < bekleyen.Count && kota > 0; i++)
        {
            var esitPay = kota / (bekleyen.Count - i);
            var verilen = Math.Min(bekleyen[i].Value, Math.Max(esitPay, 1));
            paylar[bekleyen[i].Key] += verilen;
            kota -= verilen;
        }

        return kota;
    }

    private async Task TamamlandiIsaretleAsync(CancellationToken cancellationToken)
    {
        var kayit = await db.BackgroundJobRuns
            .FirstOrDefaultAsync(j => j.JobName == BackgroundJobNames.DetayTamamlama, cancellationToken);

        if (kayit is null)
        {
            db.BackgroundJobRuns.Add(new BackgroundJobRun
            {
                JobName = BackgroundJobNames.DetayTamamlama,
                LastCompletedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            kayit.LastCompletedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
