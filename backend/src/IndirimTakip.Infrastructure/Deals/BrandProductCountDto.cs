namespace IndirimTakip.Infrastructure.Deals;

// Markalar dizini (/markalar) için: markanın takip edilen ürün sayısı.
//
// AYRI BİR UÇ GEREKTİ çünkü dizin sayfası bu sayıyı önce
// BrandCategoryPairDto'ları toplayarak hesaplıyordu ve o liste yalnızca
// KATEGORİSİ OLAN ürünleri içeriyor. Sonuç: aynı marka için iki farklı
// sayı görünüyordu — /markalar "HIQ 85 ürün", /marka/hiq "113 ürün"
// (schema.org SSS bloğunda da 113). Katalog genelinde 414 ürün (%16)
// sayılmıyordu ve kategorisi hiç olmayan üç marka (BioBee, Dr Pan, SiS)
// "0 ürün" görünüyordu.
//
// Buradaki sayı GetBrandStatsAsync ile AYNI tanımı kullanıyor (aktif marka +
// bayat olmayan ürün), böylece iki sayfa aynı rakamı veriyor.
public record BrandProductCountDto(string BrandName, int ProductCount);
