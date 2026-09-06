namespace IndirimTakip.Infrastructure.Security;

public static class SecurityEventKinds
{
    public const string Unauthorized = "unauthorized";
    public const string RateLimited = "rate-limited";
    public const string Probe = "probe";
    public const string ServerError = "server-error";
}

/// <summary>
/// Bir isteğin kaydedilmeye değer olup olmadığına karar verir.
/// </summary>
/// <remarks>
/// Saf fonksiyon olarak ayrıldı, çünkü asıl risk BURADA: kural fazla genişse
/// normal trafiği de kaydeder (hacim + gereksiz kişisel veri), fazla darsa
/// gerçek saldırıyı kaçırır. Saf olduğu için teste bağlanabiliyor.
/// </remarks>
public static class SecurityEventClassifier
{
    // Bilinen açık taraması işaretleri. Hepsi ASCII ve öyle kalmalı —
    // aşağıdaki karşılaştırma da ASCII'ye göre yapılıyor.
    private static readonly string[] ProbeMarkers =
    [
        ".php", ".env", ".git", ".bak", ".sql", ".yml", ".ini",
        "wp-admin", "wp-login", "wp-content", "wp-includes", "xmlrpc",
        "phpmyadmin", "/vendor/", "/cgi-bin/", "/shell", "/.aws", "/.ssh",
        "eval-stdin", "config.json", "credentials", "/actuator", "/solr",
    ];

    /// <summary>
    /// Kaydedilecek olay türü; istek dikkate değer değilse <c>null</c>.
    /// </summary>
    public static string? Classify(int statusCode, string path)
    {
        if (statusCode == 429)
            return SecurityEventKinds.RateLimited;

        if (statusCode is 401 or 403)
            return SecurityEventKinds.Unauthorized;

        if (statusCode >= 500)
            return SecurityEventKinds.ServerError;

        // 404'lerin ÇOĞU masum (silinmiş ürün, eski bağlantı, yazım hatası) ve
        // hepsini kaydetmek tabloyu gürültüyle doldururdu. Yalnızca bilinen
        // saldırı desenlerini taşıyanlar alınıyor.
        if (statusCode == 404 && LooksLikeProbe(path))
            return SecurityEventKinds.Probe;

        return null;
    }

    public static bool LooksLikeProbe(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // BURADA INVARIANT DOĞRU OLAN: işaretlerin tamamı ASCII. Türkçe kültürle
        // küçültmek "I" harfini noktasız "ı" yapar ve ".INI" gibi bir yol
        // eşleşmez hâle gelirdi. Ters yönde bir tuzak yok: yolda geçen Türkçe
        // "İ" olduğu gibi kalıyor, zaten hiçbir ASCII işaretle eşleşmemesi
        // gerekiyor.
        var kucuk = path.ToLowerInvariant();

        foreach (var marker in ProbeMarkers)
        {
            if (kucuk.Contains(marker, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
