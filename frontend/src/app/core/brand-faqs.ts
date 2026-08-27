import { FaqItem } from './category-faqs';

export interface BrandFaqInput {
  brandName: string;
  couponCodes: string[];
  totalProducts: number | null;
  averageDiscountPercent: number | null;
  topCategoryLabel: string | null;
}

// "{marka} indirim kodu" araması, marka sayfalarımıza gelen en yüksek hacimli
// sorgu grubu (28 Ağustos GSC analizi: 14 sorgu, 62 gösterim, çoğu 10-11.
// sırada). Ama sayfa o güne kadar aslında bir ürün listesiydi — gövde metninde
// "kupon" kelimesi yalnızca üç kez geçiyordu ve kupona dair tek bir başlık
// yoktu. Arama sonucundan gelen kişi aradığını bulamıyor, arama motoru da
// sayfayı sorguya zayıf eşleştiriyordu.
//
// Buradaki sorular o boşluğu kapatıyor. İki kural:
// 1. Uydurma kupon YOK. Kod bulamadığımızda bunu açıkça söylüyoruz — süresi
//    geçmiş bir kodla ödeme sayfasında karşılaşmak, hiç kod olmamasından kötü.
// 2. Cevaplar markanın kendisi hakkında spekülasyon yapmıyor (kampanya
//    takvimini bilmiyoruz), yalnızca KENDİ verimize dayanıyor.
export interface BrandCategoryFaqInput {
  brandName: string;
  categoryLabel: string;
  productCount: number | null;
  averagePrice: number | null;
  categoryAveragePrice: number | null;
  averageDiscountPercent: number | null;
}

// Marka × kategori sayfaları ("hardline creatine", "proteinocean kreatin"
// gibi sorgular — 28 Ağustos GSC analizinde 21 sorgu, 60 gösterim, 17-18.
// sıra). O sayfalar 150-270 kelimeydi, yani neredeyse yalnızca ürün
// listesiydi; sıralamanın düşük kalmasının sebebi buydu.
//
// Sorular kategori sayfasındakilerden AYRI tutuluyor (aynı metni iki sayfada
// tekrarlamak ikisini de zayıflatırdı) ve tamamı kesişime özgü: markanın o
// kategorideki fiyat konumu, ürün sayısı, indirim derinliği. Hepsi kendi
// verimizden geliyor — marka hakkında hiçbir varsayım yok.
export function buildBrandCategoryFaqs(input: BrandCategoryFaqInput): FaqItem[] {
  const { brandName, categoryLabel, productCount, averagePrice, categoryAveragePrice, averageDiscountPercent } = input;
  const lower = categoryLabel.toLocaleLowerCase('tr');
  const faqs: FaqItem[] = [];

  // Sayfanın en özgün içeriği: markanın o kategorideki fiyat konumu.
  if (averagePrice && categoryAveragePrice) {
    const diff = Math.round(Math.abs(averagePrice - categoryAveragePrice) / categoryAveragePrice * 100);
    const cheaper = averagePrice < categoryAveragePrice;
    const priceText = (v: number) => v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    faqs.push({
      question: `${brandName} ${lower} ürünleri pahalı mı?`,
      answer: diff < 3
        ? `${brandName} markasının takip ettiğimiz ${lower} ürünlerinin ortalama fiyatı ${priceText(averagePrice)} TL; kategorinin geneli ise ${priceText(categoryAveragePrice)} TL. Yani marka bu kategoride ortalamaya çok yakın konumlanıyor. Ortalama tek başına yeterli bir ölçüt değil: paket boyutları farklı olduğu için servis başına düşen maliyete bakmak daha doğru sonuç verir.`
        : `${brandName} markasının takip ettiğimiz ${lower} ürünlerinin ortalama fiyatı ${priceText(averagePrice)} TL; kategorinin geneli ise ${priceText(categoryAveragePrice)} TL — yani ortalamadan yaklaşık %${diff} daha ${cheaper ? 'uygun' : 'yüksek'}. Ortalama tek başına yeterli bir ölçüt değil: paket boyutları farklı olduğu için servis başına düşen maliyete bakmak daha doğru sonuç verir.`,
    });
  }

  if (productCount) {
    faqs.push({
      question: `${brandName} markasının kaç ${lower} ürünü takip ediliyor?`,
      answer: `Şu anda ${brandName} kataloğundan ${productCount} ${lower} ürününü takip ediyoruz ve her birinin fiyatını günde dört kez kaydediyoruz. Marka kataloğundan bir ürünü kaldırdığında biz de listelemeyi bırakıyoruz, ama o ürünün birikmiş fiyat geçmişini siliyor değiliz.`,
    });
  }

  faqs.push({
    question: `${brandName} ${lower} ürünlerinde indirim ne sıklıkla oluyor?`,
    answer: averageDiscountPercent
      ? `Şu an bu kategoride doğruladığımız indirimlerin ortalama derinliği %${averageDiscountPercent}. Bu oran sabit bir kampanya vaadi değil, her taramada yeniden hesaplanan anlık durum — fiyatlar değiştikçe değişiyor.`
      : `Şu anda bu kategoride doğrulanmış bir fiyat düşüşü görünmüyor. Bu, markanın kampanya yapmadığı anlamına gelmiyor; yalnızca bizim topladığımız fiyat geçmişinde henüz gerçek bir düşüş oluşmadı demek. Sayfayı takip listene ekleyerek fiyat düştüğünde haberdar olabilirsin.`,
  });

  faqs.push({
    question: `Buradaki ${lower} fiyatları güncel mi?`,
    answer:
      'Fiyatları markanın kendi sitesinden günde dört kez topluyoruz; her ürün kartında son kontrolün ne zaman yapıldığı yazıyor. Yine de nihai fiyat markanın ödeme sayfasında geçerlidir — kargo, kampanya koşulu veya sepet indirimi gibi ayrıntılar orada değişebilir.',
  });

  return faqs;
}

export function buildBrandFaqs(input: BrandFaqInput): FaqItem[] {
  const { brandName, couponCodes, totalProducts, averageDiscountPercent, topCategoryLabel } = input;
  const hasCoupon = couponCodes.length > 0;

  const couponAnswer = hasCoupon
    ? `Evet. Şu anda ${brandName} için doğruladığımız ${couponCodes.length === 1 ? 'bir kod' : `${couponCodes.length} kod`} var: ${couponCodes.join(', ')}. Kodları otomatik toplamıyoruz — her birini elle kontrol edip ekliyoruz ve süresi dolduğunda kaldırıyoruz. Yine de markanın kampanya koşulları ödeme sayfasında değişebilir.`
    : `Şu anda ${brandName} için doğruladığımız aktif bir indirim kodu yok. Bulamadığımızda uydurma kod listelemiyoruz: süresi geçmiş bir kodu ödeme sayfasında denemek, hiç kod olmamasından daha can sıkıcı. Kod yerine bu sayfada markanın gerçek fiyat düşüşlerini takip edebilirsin — çoğu zaman iyi zamanlanmış bir alım, kodun sağladığı indirimden daha fazlasını kazandırıyor.`;

  const trackingAnswer = totalProducts
    ? `${brandName} kataloğundan ${totalProducts} ürünü günde dört kez tarıyoruz ve her taramada fiyatı kaydediyoruz. Bir ürünün fiyatı düştüğünde bunu markanın duyurmasını beklemeden görüyoruz.`
    : `${brandName} ürünlerini günde dört kez tarıyor ve her taramada fiyatı kaydediyoruz. Bir ürünün fiyatı düştüğünde bunu markanın duyurmasını beklemeden görüyoruz.`;

  const depthAnswer = averageDiscountPercent
    ? `Şu an ${brandName} tarafında doğruladığımız indirimlerin ortalama derinliği %${averageDiscountPercent}. ${topCategoryLabel ? `Markanın bizde en çok ürünü olan kategorisi ${topCategoryLabel}.` : ''} Bu oran her taramada yeniden hesaplanıyor; sabit bir kampanya vaadi değil, o anki gerçek durum.`.trim()
    : `Şu anda ${brandName} tarafında doğrulanmış bir fiyat düşüşü görünmüyor. Bu, markanın kampanya yapmadığı anlamına gelmiyor — yalnızca bizim topladığımız fiyat geçmişinde henüz gerçek bir düşüş oluşmadı demek. ${topCategoryLabel ? `Markanın bizde en çok ürünü olan kategorisi ${topCategoryLabel}.` : ''}`.trim();

  return [
    {
      question: `${brandName} indirim kodu var mı?`,
      answer: couponAnswer,
    },
    {
      question: `İndirim kodu olmadan ${brandName} ürünlerini uygun fiyata nasıl alırım?`,
      answer:
        'Bir ürünün fiyatı yıl boyunca sabit kalmaz. Sayfadaki "30 günün en düşüğü" etiketi, o ürünün son bir aydaki en ucuz haline şu anda ulaşabildiğini gösterir — alım için en mantıklı an genellikle burasıdır. Acele etmiyorsan ürünü takip listene ekleyip fiyat düştüğünde haber almayı da seçebilirsin.',
    },
    {
      question: 'Buradaki indirimler markanın kendi kampanyası mı?',
      answer:
        '"Gerçek indirim" sekmesindeki oranlar bizim kendi topladığımız fiyat geçmişinden hesaplanıyor: ürünün şu anki fiyatı, son 30 günde gördüğümüz en yüksek fiyattan düşükse indirim sayılıyor. "Mağaza kampanyası" sekmesi ise markanın kendi sitesinde gösterdiği eski/yeni fiyat farkı — onu doğrulamıyoruz, ayrı etiketliyoruz. İkisini bilinçli olarak karıştırmıyoruz.',
    },
    {
      question: `${brandName} fiyatları ne sıklıkla güncelleniyor?`,
      answer: trackingAnswer,
    },
    {
      question: `${brandName} ürünlerinde indirimler ne kadar derin oluyor?`,
      answer: depthAnswer,
    },
    {
      question: 'İndirim kodları neden her sitede farklı görünüyor?',
      answer:
        'Kupon sitelerinin çoğu kodları otomatik topluyor ve süresi dolanları kaldırmıyor; bu yüzden aynı marka için birbiriyle çelişen onlarca "kod" görebiliyorsun. Biz yalnızca elle kontrol ettiğimiz kodları listeliyoruz ve doğrulayamadığımızda sayfayı kodla doldurmak yerine boş bırakmayı tercih ediyoruz.',
    },
  ];
}
