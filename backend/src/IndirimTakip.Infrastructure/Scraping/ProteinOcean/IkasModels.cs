using System.Text.Json.Serialization;

namespace IndirimTakip.Infrastructure.Scraping.ProteinOcean;

internal sealed class SearchProductsGraphQlResponse
{
    [JsonPropertyName("data")]
    public SearchProductsData? Data { get; set; }
}

internal sealed class SearchProductsData
{
    [JsonPropertyName("searchProducts")]
    public SearchProductsResult? SearchProducts { get; set; }
}

internal sealed class SearchProductsResult
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("results")]
    public List<IkasProduct> Results { get; set; } = [];
}

internal sealed class IkasProduct
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("metaData")]
    public IkasMetaData? MetaData { get; set; }

    [JsonPropertyName("variants")]
    public List<IkasVariant> Variants { get; set; } = [];
}

internal sealed class IkasMetaData
{
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}

internal sealed class IkasVariant
{
    [JsonPropertyName("prices")]
    public List<IkasPrice> Prices { get; set; } = [];

    [JsonPropertyName("images")]
    public List<IkasImage> Images { get; set; } = [];

    [JsonPropertyName("stocks")]
    public List<IkasStock> Stocks { get; set; } = [];

    // Variant başına ürün özellikleri — bizi ilgilendiren tek alan
    // "Servis" (paketten kaç servis çıktığı). ProteinOcean'da paket
    // gramajı (Size) hiç gelmediği için servis başı fiyat ancak buradan
    // hesaplanabiliyor.
    // NULLABLE olmalı: API bazı variant'larda bu alanı açıkça `null`
    // gönderiyor, bu da `= []` başlangıç değerini eziyor (deserializer
    // explicit null'ı yazar) — null kontrolü olmadan LINQ patlıyordu.
    [JsonPropertyName("attributes")]
    public List<IkasAttributeValue>? Attributes { get; set; }
}

internal sealed class IkasAttributeValue
{
    [JsonPropertyName("productAttribute")]
    public IkasProductAttribute? ProductAttribute { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

internal sealed class IkasProductAttribute
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class IkasPrice
{
    [JsonPropertyName("sellPrice")]
    public decimal SellPrice { get; set; }
}

internal sealed class IkasImage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; set; }

    [JsonPropertyName("isMain")]
    public bool IsMain { get; set; }
}

internal sealed class IkasStock
{
    [JsonPropertyName("stockCount")]
    public int StockCount { get; set; }
}
