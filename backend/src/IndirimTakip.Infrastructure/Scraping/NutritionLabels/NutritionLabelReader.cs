namespace IndirimTakip.Infrastructure.Scraping.NutritionLabels;

/// <summary>
/// Etiket görselini birkaç hazırlık/okuma biçimiyle dener; kontrolleri geçen
/// İLK okumayı döndürür.
/// </summary>
/// <remarks>
/// <b>Neden tek biçim yetmedi (15 Eylül, canlı konteynerde ölçüldü).</b> 2 kat
/// büyütme Nois'in küçük görsellerinde (750-1080 px) gerekliydi, ama West'in
/// 1500 px'lik "Whey 2400" etiketinde protein satırını her iki modda da
/// yanlış okuttu ("73,8 g 22" → "13,8 9 22"); aynı görsel büyütülmeden
/// doğru okundu. Tersine West'in PNG etiketleri yalnızca büyütülünce geçti.
/// West'te büyütmeli 5, büyütmesiz 10 etiket kabul edildi; birleşimi 14.
///
/// <b>Sıra bilerek büyütmeli önce:</b> Nois bu biçimle ölçülüp doğrulandı,
/// sonuçları değişmesin. Deneme sayısı yalnızca reddedilen görselde artıyor.
///
/// <b>Birden fazla deneme güvenceyi zayıflatmıyor mu?</b> Her okuma yine satır
/// oranı + kalori kontrolünden geçmek zorunda; yanlış okuma bir denemede
/// tutarlı çıkabilmek için iki bağımsız sayıyı AYNI oranda yanlış okumalı.
/// </remarks>
internal static class NutritionLabelReader
{
    private static readonly (bool Upscale, int Mode)[] Attempts = [(true, 6), (true, 3), (false, 6), (false, 3)];

    public static async Task<TurkishLabelTextParser.Result?> ReadAsync(
        INutritionLabelOcr ocr, byte[] image, CancellationToken cancellationToken)
    {
        foreach (var (upscale, mode) in Attempts)
        {
            var text = await ocr.ReadAsync(image, mode, upscale, cancellationToken);
            if (text is not null && TurkishLabelTextParser.Parse(text) is { } label)
                return label;
        }

        return null;
    }
}
