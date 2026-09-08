using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndirimTakip.Infrastructure.Images;

/// <summary>
/// Yerel kopyası olmayan ürün görsellerini indirir.
/// </summary>
/// <remarks>
/// <b>NEDEN DURUM TUTMUYOR.</b> "Yerel kopyası olmayanı indir" işlemi
/// etkisiz-tekrarlanabilir: kaçırılan bir tur bir sonrakinde telafi oluyor,
/// tamamlanan iş tekrar yapılmıyor. Bu yüzden bülten ve detay tamamlamada
/// iki kez yaşanan "periyodu timer tutunca her deploy sıfırlıyor" tuzağı
/// burada hiç doğmuyor ve ayrı bir damga kaydına gerek yok.
///
/// <b>NEDEN KOTALI.</b> İlk turda ~4.900 görsel var. Hepsini tek seferde
/// çekmek 36 kaynağa aynı anda yüklenmek demek; tur başına sınır hem karşı
/// tarafa saygılı hem de deploy sırasında yarıda kesilmeyi ucuzlatıyor.
///
/// <b>TEMİZLİK AYRI VE SEYREK.</b> Artık dosyaları silmek bütün kataloğu
/// okumayı gerektiriyor; her turda yapmak gereksiz. Yalnızca indirilecek
/// bir şey kalmadığında çalışıyor — yani iş bittiğinde.
/// </remarks>
public sealed class ProductImageBackgroundService(
    IServiceScopeFactory scopeFactory,
    ProductImageStore store,
    ProductImageOptions options,
    ILogger<ProductImageBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Ürün görseli indirme kapalı.");
            return;
        }

        // Açılışta biraz beklemek bilinçli: konteyner yeni kalktığında
        // migration ve ilk tarama turu çalışıyor, görsel indirme onların
        // önüne geçmemeli.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.AralikDakika));

        do
        {
            try
            {
                var kalan = await TurCalistirAsync(stoppingToken);
                if (kalan == 0)
                    await ArtiklariTemizleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ürün görseli turu başarısız.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Bu turda indirilen görsel sayısını döndürür.</summary>
    private async Task<int> TurCalistirAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Gizlenen ürünler de alınıyor (IgnoreQueryFilters): görünürlük
        // geri açıldığında görselin hazır olması gerekiyor, o an indirmeye
        // başlamak kartı bir tur boş bırakırdı.
        var adaylar = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.ImageUrl != null && p.ImageUrl != "" && p.LocalImagePath == null)
            .OrderByDescending(p => p.ClickCount)
            .ThenByDescending(p => p.Id)
            .Select(p => new { p.Id, p.ImageUrl })
            .Take(options.TurBasinaAdet)
            .ToListAsync(cancellationToken);

        if (adaylar.Count == 0)
            return 0;

        var basarili = 0;
        foreach (var aday in adaylar)
        {
            var dosyaAdi = await store.IndirAsync(aday.ImageUrl!, cancellationToken);
            if (dosyaAdi is null)
                continue;

            // Tek tek yazılıyor: tur yarıda kesilirse o ana kadarki iş
            // kaydedilmiş oluyor. Toplu kayıt, kesilen turda indirilmiş
            // dosyaları "hiç indirilmemiş" gibi bırakırdı.
            await db.Products
                .IgnoreQueryFilters()
                .Where(p => p.Id == aday.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LocalImagePath, dosyaAdi), cancellationToken);

            basarili++;
        }

        logger.LogInformation(
            "Ürün görseli: {Denenen} denendi, {Basarili} indirildi.", adaylar.Count, basarili);

        return basarili;
    }

    private async Task ArtiklariTemizleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var kullanilanlar = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.LocalImagePath != null)
            .Select(p => p.LocalImagePath!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var silinen = store.KullanilmayanlariSil(kullanilanlar.ToHashSet(StringComparer.Ordinal));
        if (silinen > 0)
            logger.LogInformation("Ürün görseli temizliği: {Silinen} artık dosya silindi.", silinen);
    }
}
