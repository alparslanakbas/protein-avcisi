export interface Deal {
  productId: number;
  productName: string;
  productUrl: string;
  imageUrl: string | null;
  category: string | null;
  size: string | null;
  flavor: string | null;
  // Son taramada mağazada satın alınabilir miydi?
  //
  // null = "bu kaynak stok bilgisi vermiyor", false ile KARIŞTIRILMAMALI.
  // Sekiz kaynaktan üçü (HIQ, ProteinOcean, Yeşilmarka) bu bilgiyi veriyor;
  // diğerlerinde hiçbir rozet gösterilmemeli. Bilinmeyeni "stokta var"
  // saymak uydurma veri olurdu.
  inStock: boolean | null;
  // Ürünü SATAN mağaza; null ise markanın kendi sitesinden alınıyor.
  // Bayi kaynaklarında marka (üretici) ile satıcı farklı olabiliyor:
  // ürün "BigJoy" markası altında görünür ama protein7.com'dan satılır.
  seller: string | null;
  // Ortaklık kodu eklenmiş mağaza adresi. "Mağazaya git" bağlantısı buraya
  // gidiyor — araya kendi sitemizdeki /go/{id} yönlendirmesi GİRMİYOR,
  // çünkü kurulu PWA'da o yönlendirme geri tuşunu bozuyordu.
  storeUrl: string | null;
  servingSizeGrams: number | null;
  // Paketten kaç servis çıktığı — markanın doğrudan beyanı (şimdilik
  // yalnızca ProteinOcean; o markada paket gramajı hiç gelmiyor).
  servingsPerPackage: number | null;
  // Markanın kendi sitesinden gelen gerçek ürün açıklaması — sadece marka
  // bunu sağlıyorsa dolu (şimdilik HIQ), yoksa null.
  description: string | null;
  // Gerçek besin değeri tablosu, normalize edilmiş JSON string ({"Protein":
  // "24 g", ...} gibi) — sadece marka güvenilir sağlıyorsa dolu (HIQ + SSN/
  // Hardline'da haftalık backfill; ProteinOcean bilinçli olarak dışarıda).
  nutritionJson: string | null;
  proteinPerServingGrams: number | null;
  brandName: string;
  currentPrice: number;
  referencePrice: number;
  discountPercent: number;
  storeOldPrice: number | null;
  storeDiscountPercent: number | null;
  scrapedAt: string;
  isAtThirtyDayLow: boolean;
  // Yalnızca tekil ürün ucundan gelir; listelerde donmuş kayıtlar zaten
  // gizlendiği için orada hep varsayılan değerdedir.
  // Markanın KENDİ sitesindeki müşteri puanı — bizim değerlendirmemiz değil,
  // arayüzde de öyle etiketleniyor. Yalnızca yorum toplayan markalarda dolu.
  // Markalar arası kıyaslanabilir değil: her biri farklı bir yorum sistemi
  // kullanıyor, bu yüzden sıralama puana değil yorum sayısına dayanıyor.
  ratingValue: number | null;
  ratingCount: number | null;
  isStale?: boolean;
  replacementProductId?: number | null;
}
