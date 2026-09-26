using Microsoft.AspNetCore.OutputCaching;

namespace IndirimTakip.Api.Caching;

/// <summary>
/// Genel veri önbelleğinin kuralları. Program.cs ve testler AYNI kurulumu
/// kullanıyor; testte kopyası olsaydı sıra ya da bir kural değiştiğinde test
/// gerçeği değil kopyayı sınardı.
/// </summary>
public static class GenelVeriOnbellegi
{
    public static void Uygula(OutputCachePolicyBuilder politika, TimeSpan sure) => politika
        .Expire(sure)
        // Tarama sonrası toplu temizleme bu etikete göre yapılıyor.
        .Tag(OutputCacheRefresher.Tag)
        // Anahtarda yalnızca ucun bağladığı sorgu parametreleri var (26 Eylül).
        // Önceden "*" idi: filtreler birbirinin sonucunu görmesin diye doğruydu
        // ama rastgele bir parametre önbelleği atlatıyordu. Gerekçe politikanın
        // kendisinde.
        .AddPolicy<UcSorguAnahtarlariPolitikasi>()
        // HOST ANAHTARDAN ÇIKARILDI (16 Eylül). SSR artık API'ye Docker iç
        // ağından (http://backend:8080) gidiyor, Cloudflare'den dolaşmıyor.
        // Node'un fetch'i Host başlığını değiştirmeye izin vermiyor (ölçüldü:
        // özel Host gönderildi, sunucu yine 127.0.0.1 gördü), yani iç istek
        // Host: backend:8080 ile geliyor. Host anahtarda kalsaydı SSR ısıtılmış
        // girdileri (Host: api.proteinavcisi.com.tr) hiç görmez, her sayfa soğuk
        // sorguya düşerdi. Bu uçların yanıtı Host'a bağlı değil ve API tek bir
        // genel adresten sunuluyor. Şema anahtarda KALIYOR: SSR da ısıtma da
        // X-Forwarded-Proto: https gönderiyor.
        .SetVaryByHost(false);
}
