using System.Collections.Concurrent;
using IndirimTakip.Core.Entities;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IndirimTakip.Infrastructure.Scraping;

public class ScrapeIngestionService(
    AppDbContext db,
    ProductWatchNotifier watchNotifier,
    IndexNowClient indexNow,
    IConfiguration configuration)
{
    /// <summary>
    /// Kaynak başına eşzamanlılık kilidi. Aynı kaynağın iki taraması aynı anda
    /// çalışmamalı: protein7'de gerçekten yaşandı — 15 dakika süren elle
    /// başlatılmış bir tarama sürerken günlük servis de devreye girdi, karşı
    /// sunucuya iki kat istek gitti ve İKİSİ BİRDEN hız sınırına takıldı.
    ///
    /// Kilit süreç içi: tek konteyner çalıştığı için yeterli. Birden fazla
    /// örnek çalıştırılacak olursa veritabanı tabanlı bir kilit gerekir.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ScrapeLocks = new();

    public async Task<int> IngestAsync(IBrandScraper scraper, CancellationToken cancellationToken = default)
    {
        var gate = ScrapeLocks.GetOrAdd(scraper.BrandName, _ => new SemaphoreSlim(1, 1));

        // Beklemiyoruz, doğrudan reddediyoruz: sıraya girmek ikinci taramanın
        // birincisi bittikten hemen sonra baştan başlaması demek olurdu ve
        // zaten taze olan veriyi tekrar çekerdi.
        if (!await gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException($"{scraper.BrandName} taraması zaten çalışıyor, bu tetikleme atlandı.");

        try
        {
            var scrapedProducts = await scraper.ScrapeAsync(cancellationToken);
            return await IngestCoreAsync(scraper, scrapedProducts, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Ürünleri BAŞKA BİR YERDE toplanmış olarak alır ve aynı yutma yolundan
    /// geçirir.
    ///
    /// NEDEN VAR: Supplementler.com sunucumuzun bulunduğu datacenter'dan
    /// Cloudflare managed challenge'ı ile karşılanıyor (403 gövdesi "Just a
    /// moment..." sayfası), ev bağlantısından ise normal 200 dönüyor. Tarama
    /// bu yüzden geliştirme makinesinde çalışıp sonucu buraya gönderiyor.
    /// Toplama yeri değişiyor, yutma mantığı DEĞİŞMİYOR — marka çözümlemesi,
    /// kategori çıkarımı, fiyat geçmişi ve bayatlama hep aynı kod.
    ///
    /// Kilit paylaşılıyor: aynı kaynak için sunucu içi bir tarama sürerken
    /// dışarıdan gelen gönderim de reddediliyor.
    /// </summary>
    public async Task<int> IngestAsync(
        IBrandScraper scraper,
        IReadOnlyList<ScrapedProduct> scrapedProducts,
        CancellationToken cancellationToken = default)
    {
        var gate = ScrapeLocks.GetOrAdd(scraper.BrandName, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException($"{scraper.BrandName} taraması zaten çalışıyor, bu gönderim atlandı.");

        try
        {
            return await IngestCoreAsync(scraper, scrapedProducts, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<int> IngestCoreAsync(
        IBrandScraper scraper,
        IReadOnlyList<ScrapedProduct> scrapedProducts,
        CancellationToken cancellationToken)
    {
        var scrapedAt = DateTimeOffset.UtcNow;

        // Markalar ada göre önbelleğe alınıyor: çok markalı bir kaynakta
        // (bayi kataloğu) ürün başına marka çözmek gerekiyor ve her ürün için
        // ayrı sorgu atmak yüzlerce gidiş-geliş demek olurdu.
        // Aynı ada sahip birden fazla marka kaydı olabiliyor (iki tarama aynı
        // anda çalışıp ikisi de "marka yok, oluştur" dediğinde oluşuyor), bu
        // yüzden doğrudan sözlüğe çevrilmiyor: aynı ad ikinci kez gelirse
        // ArgumentException fırlatır ve tüm tarama düşerdi. İlk kayıt esas
        // alınıyor; yinelenen kayıtlar zaten listelerde tekilleştiriliyor.
        var brandsByName = new Dictionary<string, Brand>();
        // İkinci indeks: Türkçe harfleri katlanmış, büyük/küçük harf duyarsız
        // ad. Sebebi ölçülmüş bir hata: bayiler aynı üreticiyi farklı yazıyor
        // ve yukarıdaki sözlük ORDINAL, yani "TREC" ile "Trec" ayrı kabul
        // edilip İKİNCİ bir marka kaydı oluşuyordu. .NET'in kültürden bağımsız
        // karşılaştırması ayrıca Türkçe noktalı İ'yi hiç katlamıyor, yani
        // "PRİME NUTRİTİON" da "Prime Nutrition" ile eşleşmiyordu.
        //
        // Bu, BrandNameNormalizer'ın yerini almıyor — orası "Proteinocean →
        // ProteinOcean" gibi GERÇEKTEN farklı yazımlar için. Buradaki indeks
        // yalnızca harf/aksan farkını kapatıyor ve takma ad listesinin her
        // yeni bayide onlarca satır büyümesini engelliyor.
        //
        // Mevcut 96 markada tek bir katlama çakışması bile yok (ölçüldü,
        // 4 Eylül), yani bu indeks var olan hiçbir markayı birleştirmiyor.
        var brandsByFoldedName = new Dictionary<string, Brand>();
        foreach (var existingBrand in await db.Brands.ToListAsync(cancellationToken))
        {
            brandsByName.TryAdd(existingBrand.Name, existingBrand);
            brandsByFoldedName.TryAdd(FoldBrandName(existingBrand.Name), existingBrand);
        }

        Brand ResolveBrand(string rawName)
        {
            // Takma ad sözlüğü BURADA uygulanıyor, scraper'larda değil.
            // Scraper'ların tek tek çağırması gerekiyordu ve bu sessizce
            // atlanabiliyor: Supplementler scraper'ı BrandNameNormalizer'dan
            // ÖNCE yazılmıştı, hiç çağırmıyordu ve o kaynakta takma adların
            // HİÇBİRİ çalışmıyordu ("Kingsize Nutrition" 65 ürünle ikinci bir
            // marka kaydı oluşturdu). Merkezî çağrı bunu her kaynak için
            // garanti ediyor; scraper'ların kendi çağrıları zararsız, çünkü
            // normalizasyon idempotent.
            var name = BrandNameNormalizer.Normalize(rawName);

            if (brandsByName.TryGetValue(name, out var existing))
                return existing;

            var folded = FoldBrandName(name);
            if (brandsByFoldedName.TryGetValue(folded, out var sameBrandDifferentCase))
            {
                // Adı DEĞİŞTİRMİYORUZ, yalnızca mevcut kayda bağlanıyoruz:
                // marka adı değişimi slug'ı ve /marka/... adresini kırar.
                brandsByName[name] = sameBrandDifferentCase;
                return sameBrandDifferentCase;
            }

            var created = new Brand { Name = name, BaseUrl = scraper.BaseUrl, IsActive = true };
            db.Brands.Add(created);
            brandsByName[name] = created;
            brandsByFoldedName[folded] = created;
            return created;
        }

        // Mevcut ürünler MARKAYA değil ADRESE göre yükleniyor: çok markalı bir
        // taramada marka filtresi ürünlerin çoğunu kaçırırdı. Döngü zaten
        // yalnızca taranan adreslere bakıyor, tek markalı scraper'larda
        // davranış değişmiyor.
        var scrapedUrls = scrapedProducts.Select(sp => sp.Url).Distinct().ToList();
        // Bu taramada hangi adresin birden fazla ürüne (varyanta) karşılık
        // geldiği — aşağıda eşleştirme stratejisini seçmek için gerekiyor.
        var scrapedCountByUrl = scrapedProducts
            .GroupBy(sp => sp.Url)
            .ToDictionary(g => g.Key, g => g.Count());

        // Son kaydedilen fiyat — <lastmod> için "içerik gerçekten değişti mi"
        // kararında kullanılıyor. Korelasyonlu alt sorgu + FirstOrDefault
        // kalıbı bu projede EF Core'un sorunsuz çevirdiği, kanıtlanmış yol
        // (bkz. DealsQueryService'teki DealRow notu).
        var lastPrices = await db.Products
            .Where(p => scrapedUrls.Contains(p.Url))
            .Select(p => new
            {
                p.Id,
                LastPrice = (decimal?)p.PriceHistories
                    .OrderByDescending(ph => ph.ScrapedAt)
                    .Select(ph => ph.Price)
                    .FirstOrDefault(),
            })
            .ToDictionaryAsync(x => x.Id, x => x.LastPrice, cancellationToken);
        // Adres BENZERSİZ DEĞİL: Yeşilmarka'nın mağaza API'sinde her aroma ayrı
        // bir ürün kaydı (kendi stoğu ve fiyatı var) ama hepsi aynı sayfa
        // slug'ını paylaşıyor — "Whey Protein Tozu - Ananas" ile "- Elma" aynı
        // /whey-protein adresine çıkıyor. Burada doğrudan ToDictionary(p => p.Url)
        // kullanılıyordu ve ikinci taramadan itibaren "An item with the same key
        // has already been added" ile markanın TÜM taraması iptal oluyordu
        // (Yeşilmarka 28 Ağustos'tan 30 Ağustos'a kadar hiç veri üretmedi).
        //
        // Sözlük yerine adres başına liste tutuluyor. Tek kayıtlı adreslerde
        // davranış AYNEN eskisi gibi (isim değişikliği yerinde güncelleniyor);
        // yalnızca aynı adreste birden fazla kayıt varsa isimle ayrıştırılıyor.
        // Bu ayrım önemli: her zaman isimle eşleştirmek, markanın bir ürünü
        // yeniden adlandırdığı durumda eski satırı öksüz bırakıp yenisini
        // oluştururdu.
        var existingByUrl = (await db.Products
                .Where(p => scrapedUrls.Contains(p.Url))
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.Url)
            .ToDictionary(g => g.Key, g => g.ToList());

        var touchedProducts = new List<Product>();
        // Yalnızca bu taramada İLK KEZ görülen ürünler — IndexNow'a yeni
        // adresleri bildirmek için. Protokol, değişmeyen adresleri tekrar
        // tekrar göndermemeyi şart koşuyor; fiyat değişimi adresi
        // değiştirmediği için burada yalnızca yeni ürünler toplanıyor.
        var newProducts = new List<Product>();

        foreach (var scraped in scrapedProducts)
        {
            // Marka kendi kategorisini vermiyorsa (HIQ/Hardline/ProteinOcean) isimden tahmin et
            // — arama kutusunun markadan bağımsız çalışması buna dayanıyor.
            // Marka adı kategori çıkarımından ÇIKARILIYOR: bayi kaynakları
            // ürün adına markayı da yazıyor ve "Proteinocean" içindeki
            // "protein" tüm ürünleri protein tozu sanmaya yol açıyordu.
            var brandForCategory = scraped.BrandName ?? scraper.BrandName;
            var category = scraped.Category ?? ProductAttributeParser.InferCategory(scraped.Name, brandForCategory);
            var size = ProductAttributeParser.ExtractSize(scraped.Name);
            var flavor = ProductAttributeParser.ExtractFlavor(scraped.Name);

            Product? product = null;
            if (existingByUrl.TryGetValue(scraped.Url, out var sameUrlProducts))
            {
                // Adres bu markada gerçekten tekil mi? Hem veritabanında hem bu
                // taramada tek karşılığı varsa eski davranış korunuyor: adresle
                // eşleştir, böylece marka ürünü yeniden adlandırdığında satır
                // öksüz kalmadan yerinde güncellenir.
                //
                // İki taraftan biri bile çoğulsa isimle eşleştirmek ZORUNLU.
                // Yalnızca veritabanı tarafına bakmak yetmezdi: adres ilk kez
                // çoğullaştığında (DB'de 1 satır, taramada 2 varyant) her iki
                // varyant da aynı satıra yazar, biri diğerini sessizce ezerdi.
                var tekil = sameUrlProducts.Count == 1
                    && scrapedCountByUrl.GetValueOrDefault(scraped.Url) == 1;

                product = tekil
                    ? sameUrlProducts[0]
                    : sameUrlProducts.Find(p => p.Name == scraped.Name);
            }

            if (product is null)
            {
                product = new Product
                {
                    Brand = ResolveBrand(scraped.BrandName ?? scraper.BrandName),
                    Name = scraped.Name,
                    Url = scraped.Url,
                    ImageUrl = scraped.ImageUrl,
                    Category = category,
                    Size = size,
                    Flavor = flavor,
                    InStock = scraped.InStock,
                    Seller = scraped.Seller,
                    // Porsiyon: önce scraper'ın yapısal olarak verdiği değer
                    // (HIQ'nun besin tablosu — en güvenilir kaynak), o yoksa
                    // markanın açıklama metninden çıkarım.
                    ServingSizeGrams = scraped.ServingSizeGrams
                        ?? ProductAttributeParser.ExtractServingSizeGrams(scraped.Description),
                    ServingsPerPackage = scraped.ServingsPerPackage,
                    Description = scraped.Description,
                    NutritionJson = scraped.NutritionJson,
                    ProteinPerServingGrams = scraped.ProteinPerServingGrams,
                };
                product.ContentUpdatedAt = DateTimeOffset.UtcNow;
                db.Products.Add(product);
                newProducts.Add(product);

                // Yeni kayıt aramaya da giriyor: aynı tarama içinde aynı
                // (adres, isim) ikinci kez gelirse ikinci bir satır açılmasın.
                // Mevcut çift kayıtlar tam olarak böyle oluşmuştu — ilk turda
                // hiçbiri veritabanında yoktu, hepsi "yeni" sayıldı.
                if (existingByUrl.TryGetValue(scraped.Url, out var bucket))
                    bucket.Add(product);
                else
                    existingByUrl[scraped.Url] = [product];
            }
            else
            {
                // İçerik gerçekten değişti mi? Fiyatın aynı değerde yeniden
                // ölçülmesi değişiklik SAYILMAZ — sitemap'teki <lastmod>
                // buna bağlı ve her taramada tüm katalogu "değişti" diye
                // işaretlemek Google'ın sinyali tamamen yok saymasına yol
                // açıyordu.
                lastPrices.TryGetValue(product.Id, out var previousPrice);
                var meaningfulChange =
                    previousPrice != scraped.Price
                    || product.Name != scraped.Name
                    || product.Category != category
                    || product.Size != size
                    || (scraped.Description is not null && product.Description != scraped.Description)
                    || (scraped.NutritionJson is not null && product.NutritionJson != scraped.NutritionJson)
                    // Stok durumu değişimi de gerçek bir içerik değişimi:
                    // sayfada "Tükendi" rozeti belirip kayboluyor. Bu, her
                    // taramada tüm katalogu "değişti" işaretleyen eski
                    // davranıştan farklı — ürün başına nadir gerçekleşiyor,
                    // dolayısıyla lastmod sinyalini bozmuyor.
                    || product.InStock != scraped.InStock
                    || product.Seller != scraped.Seller;

                if (meaningfulChange)
                    product.ContentUpdatedAt = DateTimeOffset.UtcNow;

                product.Name = scraped.Name;
                // Marka da güncelleniyor. Eskiden yalnızca ürün İLK kaydedilirken
                // atanıyordu, yani kaynaktaki yazım düzelse bile eski kayıt eski
                // markada kalıyordu: protein7 "Proteinocean" yazdığı için 67 ürün
                // ayrı bir markaya düşmüş ve marka ikiye bölünmüştü. Artık
                // normalizasyon (bkz. BrandNameNormalizer) mevcut ürünlere de
                // işliyor, elle DB müdahalesi gerekmiyor.
                //
                // Tek markalı kaynaklarda davranış DEĞİŞMİYOR: onlar
                // scraped.BrandName göndermiyor, değer scraper'ın kendi adına
                // düşüyor ve zaten aynı markayı veriyor.
                product.Brand = ResolveBrand(scraped.BrandName ?? scraper.BrandName);
                product.ImageUrl = scraped.ImageUrl;
                product.Category = category;
                product.Size = size;
                product.Flavor = flavor;
                product.InStock = scraped.InStock;
                product.Seller = scraped.Seller;
                // Açıklamayı henüz çekmeyen scraper'lar (SSN/Hardline) scraped.Description
                // hiç göndermiyor — bu durumda var olan değeri SIFIRLAMIYORUZ. Açıklama
                // çeken markalarda (HIQ) ise her taramada güncel tutuluyor.
                if (scraped.Description is not null)
                    product.Description = scraped.Description;

                // Porsiyon, Description atamasından SONRA hesaplanıyor — güncel
                // açıklamayı kullanabilmek için. Scraper yapısal bir değer
                // veriyorsa (HIQ) o kazanır, yoksa açıklamadan çıkarılır.
                product.ServingSizeGrams = scraped.ServingSizeGrams
                    ?? ProductAttributeParser.ExtractServingSizeGrams(product.Description);

                // Sadece marka bu bilgiyi veriyorsa güncelle — vermeyen
                // markalarda (SSN/Hardline/HIQ) mevcut değer sıfırlanmasın.
                if (scraped.ServingsPerPackage is not null)
                    product.ServingsPerPackage = scraped.ServingsPerPackage;

                // Besin değeri normal taramada sadece HIQ'dan geliyor; diğer
                // 3 marka için ayrı bir backfill servisi var (Description ile
                // aynı desen — göndermeyen markada mevcut değer korunuyor).
                if (scraped.NutritionJson is not null)
                {
                    product.NutritionJson = scraped.NutritionJson;
                    product.ProteinPerServingGrams = scraped.ProteinPerServingGrams;
                }
            }

            product.PriceHistories.Add(new PriceHistory
            {
                Price = scraped.Price,
                StoreOldPrice = scraped.StoreOldPrice,
                ScrapedAt = scrapedAt,
            });
            touchedProducts.Add(product);
        }

        await db.SaveChangesAsync(cancellationToken);

        // "Haber Ver" bildirimleri — yeni ürünlerin Id'si de ancak
        // SaveChangesAsync sonrası kesinleşiyor, bu yüzden burada.
        await watchNotifier.CheckAndNotifyAsync(touchedProducts.Select(p => p.Id).ToList(), cancellationToken);

        // Yeni ürün adreslerini arama motorlarına bildir. Ürün kimliği de
        // ancak kayıt sonrası kesinleştiği için burada.
        if (newProducts.Count > 0 && indexNow.IsEnabled)
        {
            var frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "https://www.proteinavcisi.com.tr").TrimEnd('/');
            var urls = newProducts
                .Select(p => $"{frontendBaseUrl}/urun/{p.Id}/{Slugifier.Slugify(p.Name)}")
                .ToList();
            await indexNow.SubmitAsync(urls, cancellationToken);
        }

        return scrapedProducts.Count;
    }

    /// <summary>
    /// Marka adını yalnızca EŞLEŞTİRME için sadeleştirir; saklanan ada
    /// dokunulmaz.
    ///
    /// Türkçe harfler ELLE eşleniyor, kültüre bırakılmıyor. İki taraf da
    /// tuzaklı: <c>ToLowerInvariant</c> Türkçe noktalı İ'yi hiç küçültmüyor
    /// ("VİTAMİN" → "vİtamİn"), tr-TR kültürü ise İngilizce kelimeleri
    /// bozuyor ("CREATINE" → "creatıne"). Elle eşleme ikisinden de kaçınıyor.
    ///
    /// Boşluk ve nokta da atılıyor: "Dr. Pan" ile "Dr Pan", "Big Joy" ile
    /// "BigJoy" aynı üretici ve ikisi de canlıda kopya marka üretmişti.
    /// TİRE ATILMIYOR — "Z-Konzept" gibi adlarda tire markanın kendi
    /// yazımının parçası ve atmak farklı üreticileri birleştirme riskini
    /// gereksiz yere artırırdı.
    /// </summary>
    internal static string FoldBrandName(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        var length = 0;

        foreach (var ch in name)
        {
            var mapped = ch switch
            {
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ı' or 'İ' or 'I' => 'i',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                _ => ch,
            };

            if (mapped is ' ' or '.' or '\t')
                continue;

            buffer[length++] = char.ToLowerInvariant(mapped);
        }

        return new string(buffer[..length]);
    }
}
