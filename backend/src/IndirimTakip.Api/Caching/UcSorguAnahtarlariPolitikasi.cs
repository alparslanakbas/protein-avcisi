using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace IndirimTakip.Api.Caching;

/// <summary>
/// Önbellek anahtarına yalnızca ucun GERÇEKTEN bağladığı sorgu parametrelerini
/// koyar; liste her ucun kendi imzasından çıkarılıyor.
///
/// <b>NEDEN (güvenlik incelemesi, 26 Eylül).</b> Politika
/// <c>SetVaryByQuery("*")</c> kullanıyordu: <c>?r=&lt;rastgele&gt;</c> eklemek
/// her istekte yeni bir girdi açıp önbelleği atlatıyordu.
/// <c>/api/category-product-counts</c> ıskada kategori başına bir liste
/// sorgusu çalıştırıyor (~9 sorgu), yani tek parametreyle her istekte o iş
/// yaptırılabiliyordu.
///
/// <b>Neden elle yazılmış liste değil:</b> genel politika bütün uçlarda ortak.
/// Tek bir birleşik liste yetmezdi — <c>?search=x</c> başka uçlar için geçerli
/// olduğundan parametresiz uçları yine atlatırdı; uç başına elle liste ise bir
/// parametre unutulduğunda <c>page=2</c>'nin <c>page=1</c>'in yanıtını alması
/// demek (sessiz ve ciddi). İmzadan çıkarınca unutmak mümkün değil.
///
/// <b>Emin olunamayan yerde eski davranış (<c>*</c>):</b> uç <c>HttpContext</c>
/// ya da <c>HttpRequest</c> alıyorsa ham sorguyu kendisi okuyabilir; bir tip
/// <c>BindAsync</c> ile kendini bağlıyorsa ya da <c>[AsParameters]</c>
/// kullanılıyorsa hangi anahtarları okuduğu imzadan görünmez. Bu durumlarda
/// bütün parametreler anahtarda kalıyor — atlatılabilir ama hiçbir zaman
/// YANLIŞ yanıt vermez.
/// </summary>
public sealed class UcSorguAnahtarlariPolitikasi : IOutputCachePolicy
{
    private static readonly StringValues Tumu = new("*");

    // Uç kümesi uygulama ömrü boyunca sabit; yansıma her istekte tekrarlanmasın.
    private static readonly ConcurrentDictionary<Endpoint, StringValues> Hesaplanan = new();

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var uc = context.HttpContext.GetEndpoint();
        context.CacheVaryByRules.QueryKeys = uc is null ? Tumu : Hesaplanan.GetOrAdd(uc, Anahtarlar);
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    /// <summary>Ucun sorgudan bağladığı parametre adları (testler de bunu okuyor).</summary>
    internal static StringValues Anahtarlar(Endpoint uc)
    {
        // Minimal API uçları işleyicinin MethodInfo'sunu meta veriye koyuyor.
        if (uc.Metadata.GetMetadata<MethodInfo>() is not { } metot)
            return Tumu;

        var rotaParametreleri = (uc as RouteEndpoint)?.RoutePattern.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var anahtarlar = new List<string>();
        foreach (var parametre in metot.GetParameters())
        {
            var nitelikler = parametre.GetCustomAttributes(inherit: true);
            if (nitelikler.OfType<IFromQueryMetadata>().FirstOrDefault() is { } sorgu)
            {
                anahtarlar.Add(sorgu.Name ?? parametre.Name!);
                continue;
            }
            if (nitelikler.OfType<AsParametersAttribute>().Any())
                return Tumu;
            if (nitelikler.Any(n => n is IFromRouteMetadata or IFromServiceMetadata or IFromHeaderMetadata
                                    or IFromBodyMetadata or IFromFormMetadata))
                continue;

            var tip = Nullable.GetUnderlyingType(parametre.ParameterType) ?? parametre.ParameterType;
            if (tip == typeof(HttpContext) || tip == typeof(HttpRequest) || KendiniBagliyor(tip))
                return Tumu;
            if (rotaParametreleri.Contains(parametre.Name!))
                continue;
            if (SorgudanBaglanir(tip))
                anahtarlar.Add(parametre.Name!);
            // Geri kalanı DI'dan gelen servisler ve CancellationToken gibi özel tipler.
        }
        return new StringValues([.. anahtarlar]);
    }

    private static bool KendiniBagliyor(Type tip) =>
        tip.GetMethod("BindAsync", BindingFlags.Public | BindingFlags.Static) is not null;

    // Minimal API'nin GET'te sorgudan bağladığı tipler: metin, sayılar, enum'lar,
    // statik TryParse'ı olanlar (DateTime, Guid...) ve bunların dizileri.
    private static bool SorgudanBaglanir(Type tip)
    {
        var oge = tip.IsArray ? tip.GetElementType()! : tip;
        oge = Nullable.GetUnderlyingType(oge) ?? oge;
        if (oge == typeof(string) || oge == typeof(StringValues) || oge.IsPrimitive || oge.IsEnum || oge == typeof(decimal))
            return true;
        if (oge == typeof(CancellationToken))
            return false;
        return oge.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.Name == "TryParse");
    }
}
