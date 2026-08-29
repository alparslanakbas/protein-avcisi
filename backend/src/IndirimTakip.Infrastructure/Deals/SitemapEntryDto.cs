namespace IndirimTakip.Infrastructure.Deals;

// HasReviewContent — ürün incelemesi sayfasının (/urun-inceleme/{id}/{slug})
// sitemap'e girmesi için en az bir gerçek içerik kaynağı (marka açıklaması
// veya besin değeri tablosu) olması gerekiyor; aksi halde "ince içerik"
// sayfaları Google'a taranmaya sunulmuş olurdu. Sayfa kendisi yine de
// (linklenirse) her ürün için açık kalıyor, sadece sitemap'e girmiyor.
public record SitemapEntryDto(int Id, string Name, DateTimeOffset LastModifiedAt, bool HasReviewContent);
