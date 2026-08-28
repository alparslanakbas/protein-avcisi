using System.Text.Json.Serialization;

namespace IndirimTakip.Infrastructure.Scraping.BigJoy;

internal sealed class BigJoyCategoryResponse
{
    [JsonPropertyName("products")]
    public List<BigJoyProduct> Products { get; set; } = [];
}

internal sealed class BigJoyProduct
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("thumb")]
    public string? Thumb { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Paket bilgisi: çoğunlukla "253g", bazen "21 Servis".</summary>
    [JsonPropertyName("gramaj")]
    public string? Gramaj { get; set; }

    /// <summary>
    /// Liste fiyatı ("4.480,00 TL"). Bazı ürünlerde metin yerine <c>false</c>
    /// geliyor, bu yüzden string olarak değil nesne olarak okunuyor.
    /// </summary>
    [JsonPropertyName("price")]
    public object? Price { get; set; }

    /// <summary>
    /// İndirim varsa gerçek satış fiyatı, yoksa <c>false</c> geliyor — bu
    /// yüzden string değil, JsonElement olarak okunuyor.
    /// </summary>
    [JsonPropertyName("special_price")]
    public object? SpecialPrice { get; set; }
}
