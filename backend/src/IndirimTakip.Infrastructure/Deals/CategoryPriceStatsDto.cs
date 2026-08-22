namespace IndirimTakip.Infrastructure.Deals;

// Ürün incelemesi sayfasındaki "bu ürün kategorisinde nasıl konumlanıyor"
// bölümü için — o kategorideki aktif ürünlerin güncel fiyatının ortalaması.
// Tek bir ürünün fiyatını uydurma bir "puan"a çevirmek yerine, gerçek
// kategori ortalamasına göre nesnel bir kıyas sunuyor.
public record CategoryPriceStatsDto(int ProductCount, decimal AveragePrice, decimal MinPrice, decimal MaxPrice);
