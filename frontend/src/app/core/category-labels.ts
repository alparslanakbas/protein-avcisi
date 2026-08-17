// Kategori slug'larının okunabilir Türkçe karşılıkları — footer, nav
// dropdown'ı ve kategori sayfaları arasında paylaşılan tek kaynak.
export const CATEGORY_LABELS: Record<string, string> = {
  'protein-tozu': 'Protein Tozu',
  kreatin: 'Kreatin',
  'amino-asitler': 'Amino Asitler',
  'pre-workout': 'Pre-Workout',
  'yag-yakici': 'Yağ Yakıcı',
  'kilo-hacim': 'Kilo & Hacim (Gainer)',
  vitamin: 'Vitamin & Mineral',
  'saglikli-atistirmaliklar': 'Sağlıklı Atıştırmalıklar',
  'l-carnitine-cla': 'L-Carnitine & CLA',
};

// Bir geliştiricinin SEO geri bildirimi üzerine eklendi: kategori sayfaları
// şablon metin yerine her kategori için gerçekten farklı, açıklayıcı bir
// giriş metni taşıyor — "ince içerik" (thin content) riskini azaltmak için
// bilinçli. Hem /kategori/:slug hem de /kategoriler index sayfası kullanıyor.
export const CATEGORY_INTROS: Record<string, string> = {
  'protein-tozu':
    'Protein tozu, kas gelişimi ve günlük protein ihtiyacını karşılamak için en yaygın kullanılan spor takviyesi. Whey (peynir altı suyu) protein hızlı emilimiyle bilinirken, kazein daha yavaş salınım sağlar.',
  kreatin:
    'Kreatin, güç ve patlayıcı performans gerektiren antrenmanlarda en çok araştırılmış takviyelerden biri. Genellikle kreatin monohidrat formunda satılır ve düzenli günlük kullanımla etkisini gösterir.',
  'amino-asitler':
    'BCAA, EAA, glutamin ve arginin gibi amino asit takviyeleri, kas onarımı ve antrenman sonrası toparlanma sürecini desteklemek amacıyla tercih ediliyor.',
  'pre-workout':
    'Pre-workout ürünleri, antrenman öncesi enerji ve odaklanmayı artırmak amacıyla kafein, beta-alanin ve nitrik oksit destekleyici içerikler barındırır.',
  'yag-yakici':
    'Yağ yakıcı takviyeler, termojenik bileşenler içeren ve diyet ile antrenman programını desteklemek amacıyla kullanılan ürünler.',
  'kilo-hacim':
    'Kilo/hacim (gainer) ürünleri, yüksek kalori ve karbonhidrat içeriğiyle kilo almakta zorlanan veya hacim antrenmanı yapan kullanıcılar için tasarlandı.',
  vitamin:
    'Vitamin ve mineral takviyeleri, günlük beslenmedeki eksiklikleri tamamlamak amacıyla kullanılır — multivitaminden omega-3\'e, magnezyumdan çinkoya kadar geniş bir yelpazeyi kapsar.',
  'saglikli-atistirmaliklar':
    'Protein bar, kurabiye ve pirinç pastası gibi sağlıklı atıştırmalıklar, günlük protein/kalori hedefini pratik bir şekilde tamamlamak isteyenler için alternatif sunuyor.',
  'l-carnitine-cla':
    'L-Carnitine ve CLA (konjuge linoleik asit), yağ metabolizmasını desteklemek amacıyla kullanılan, genellikle diyet döneminde tercih edilen takviyeler.',
};
