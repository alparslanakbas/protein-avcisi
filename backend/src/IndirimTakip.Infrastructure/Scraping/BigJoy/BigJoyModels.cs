using System.Text.Json.Serialization;

namespace IndirimTakip.Infrastructure.Scraping.BigJoy;

/// <summary>Listeleme ucunun yanıtı: <c>GET /api/products?limit=..&amp;page=..</c>.</summary>
internal sealed class BigJoyCategoryResponse
{
    [JsonPropertyName("products")]
    public List<BigJoyProduct> Products { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

/// <summary>
/// Listedeki bir ürün GRUBU. Aroma ve gramaj seçenekleri
/// <see cref="VariantAttributes"/> içinde, her biri kendi sayfasıyla.
/// </summary>
internal sealed class BigJoyProduct
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("seo_keyword")]
    public string? SeoKeyword { get; set; }

    [JsonPropertyName("thumb")]
    public string? Thumb { get; set; }

    [JsonPropertyName("manufacturer_name")]
    public string? ManufacturerName { get; set; }

    /// <summary>
    /// Yüzde olarak KDV. Varyant fiyatları KDV'SİZ geliyor; sitede görünen
    /// fiyat bununla hesaplanıyor (ürün düzeyindeki
    /// <c>price</c>/<c>price_with_tax</c> çiftiyle doğrulandı).
    /// </summary>
    [JsonPropertyName("tax_rate")]
    public decimal? TaxRate { get; set; }

    [JsonPropertyName("category_ids")]
    public List<int> CategoryIds { get; set; } = [];

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("special")]
    public decimal? Special { get; set; }

    [JsonPropertyName("is_in_stock")]
    public bool? IsInStock { get; set; }

    [JsonPropertyName("subgroup_value")]
    public string? SubgroupValue { get; set; }

    [JsonPropertyName("variant_attributes")]
    public List<BigJoyVariant> VariantAttributes { get; set; } = [];
}

/// <summary>Bir aroma/gramaj seçeneği: kendi adresi, fiyatı ve stoğu var.</summary>
internal sealed class BigJoyVariant
{
    [JsonPropertyName("seo_keyword")]
    public string? SeoKeyword { get; set; }

    /// <summary>Gramaj ("915g", "30 Kapsül").</summary>
    [JsonPropertyName("subgroup_value")]
    public string? SubgroupValue { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("special")]
    public decimal? Special { get; set; }

    [JsonPropertyName("is_in_stock")]
    public bool? IsInStock { get; set; }
}
