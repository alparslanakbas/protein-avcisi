namespace IndirimTakip.Infrastructure.Deals;

// Marka × kategori kesişim sayfaları için: hangi markanın hangi kategoride
// kaç ürünü var. Yalnızca ürünü OLAN çiftler dönüyor — boş kombinasyonlara
// sayfa açmak "ince içerik" üretmek olurdu.
public record BrandCategoryPairDto(string BrandName, string Category, int ProductCount);
