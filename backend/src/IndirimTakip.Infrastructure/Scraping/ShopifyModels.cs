using System.Text.Json.Serialization;

// Shopify'ın public products.json uç noktasının şekli. Markaya özel bir yanı
// yok, bu yüzden tek bir markanın klasörü altında değil ortak seviyede duruyor:
// HIQ ve Commander Nutrition aynı tipleri kullanıyor, Shopify kullanan bir
// marka daha eklenirse o da kullanacak.
namespace IndirimTakip.Infrastructure.Scraping;

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

    // Mağazanın kendi etiketleri. HIQ bunu "type:wearable"/"type:equipment"
    // biçiminde kullanıp takviye olmayan ürünleri ayıklıyor. Her mağaza
    // etiketlemiyor: Commander Nutrition'da yalnızca "NOREVIEW" var, orada
    // ayıklama isim bazlı yapılıyor (NonSupplementProductFilter).
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
