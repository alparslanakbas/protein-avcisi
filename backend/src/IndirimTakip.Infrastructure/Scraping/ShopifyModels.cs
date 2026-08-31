using System.Text.Json.Serialization;

namespace IndirimTakip.Infrastructure.Scraping.Hiq;

internal sealed class ShopifyProductsResponse
{
    [JsonPropertyName("products")]
    public List<ShopifyProduct> Products { get; set; } = [];
}

internal sealed class ShopifyProduct
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("handle")]
    public required string Handle { get; set; }

    [JsonPropertyName("product_type")]
    public string? ProductType { get; set; }

    [JsonPropertyName("body_html")]
    public string? BodyHtml { get; set; }

    [JsonPropertyName("images")]
    public List<ShopifyImage> Images { get; set; } = [];

    [JsonPropertyName("variants")]
    public List<ShopifyVariant> Variants { get; set; } = [];

    // HIQ mağazasının kendi ürün etiketleme sistemi (ör. "type:wearable",
    // "type:equipment", "type:protein") — takviye olmayan ürünleri (tişört,
    // hoodie, shaker) ayıklamak için kullanılıyor, bkz. ScrapeAsync.
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}

internal sealed class ShopifyImage
{
    [JsonPropertyName("src")]
    public required string Src { get; set; }
}

internal sealed class ShopifyVariant
{
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("compare_at_price")]
    public decimal? CompareAtPrice { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }
}
