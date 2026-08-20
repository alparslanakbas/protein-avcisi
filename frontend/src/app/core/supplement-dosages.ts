export interface SupplementDosage {
  slug: string;
  // Sayfa/başlık metinleri
  name: string;
  title: string;
  description: string;
  h1: string;
  intro: string;
  // Günlük doz aralığı (gram). Bu takviyelerin dozu KİLOYA GÖRE
  // ölçeklenmez — literatürde ve pratikte sabit aralıklar kullanılır.
  // "Kiloya göre doz hesapla" gibi bir araç yapmak uydurma olurdu.
  minDailyGrams: number;
  maxDailyGrams: number;
  defaultDailyGrams: number;
  // Dozun neye dayandığı — kullanıcıya açıkça söylüyoruz, "biz uydurduk"
  // izlenimi bırakmamak için.
  dosageNote: string;
  // Ürünleri çekmek için kullanılacak kategori + arama terimi. Kategori
  // tek başına yetmiyor (ör. beta-alanine "amino-asitler" içinde ama
  // kategorinin tamamı beta-alanine değil).
  category: string;
  searchTerm: string;
  // İlgili rehber yazısı (varsa) — iç linkleme.
  guideSlug?: string;
  guideLabel?: string;
}

export const SUPPLEMENT_DOSAGES: SupplementDosage[] = [
  {
    slug: 'kreatin-dozu',
    name: 'Kreatin',
    title: 'Kreatin Dozu Hesaplama: Günde Kaç Gram? | ProteinAvcısı',
    description:
      'Günlük kreatin dozunu ve seçtiğin ürünün kaç gün yeteceğini hesapla. Güncel fiyatlarla servis başı ve günlük maliyeti gör.',
    h1: 'Kreatin Dozu Hesaplama',
    intro:
      'Kreatin dozu kiloya göre ölçeklenen bir şey değil — yaygın kullanım sabit bir aralıkta. Aşağıda günlük dozunu seçip, gerçek ürün fiyatlarıyla o paketin kaç gün yeteceğini ve günlük maliyetini görebilirsin.',
    minDailyGrams: 3,
    maxDailyGrams: 5,
    defaultDailyGrams: 5,
    dosageNote:
      'Günde 3-5 gram, kreatin için en yaygın kullanılan aralıktır ve ürünlerin çoğu 5 gramlık ölçekle gelir. Daha fazlası doygunluğu hızlandırmaz, fazlası vücuttan atılır. Belirleyici olan miktardan çok her gün düzenli kullanmaktır.',
    category: 'kreatin',
    searchTerm: 'creatine',
    guideSlug: 'kreatin-nasil-kullanilir',
    guideLabel: 'Kreatin Nasıl Kullanılır?',
  },
  {
    slug: 'beta-alanine-dozu',
    name: 'Beta-Alanine',
    title: 'Beta-Alanine Dozu Hesaplama: Günde Kaç Gram? | ProteinAvcısı',
    description:
      'Günlük beta-alanine dozunu ve paketinin kaç gün yeteceğini hesapla. Güncel fiyatlarla günlük maliyetini gör.',
    h1: 'Beta-Alanine Dozu Hesaplama',
    intro:
      'Beta-alanine dozu kiloya göre değil, sabit bir aralıkta kullanılır. Günlük dozunu seç, gerçek ürün fiyatlarıyla paketin kaç gün yeteceğini hesapla.',
    minDailyGrams: 3,
    maxDailyGrams: 6,
    defaultDailyGrams: 3.2,
    dosageNote:
      'Günde 3-6 gram yaygın olarak kullanılan aralıktır. Etkisi kreatin gibi zamanla birikime dayanır, tek seferlik alımda beklenen bir katkısı yoktur. Ciltte hissedilen karıncalanma (parestezi) bu bileşene bağlı, zararsız ve geçici bir yan etkidir; tek seferde alınan miktarı bölmek bu hissi azaltabilir.',
    category: 'amino-asitler',
    searchTerm: 'alanine',
  },
  {
    slug: 'sitrulin-dozu',
    name: 'Sitrülin',
    title: 'Sitrülin (Citrulline) Dozu Hesaplama | ProteinAvcısı',
    description:
      'Günlük sitrülin dozunu ve paketinin kaç gün yeteceğini hesapla. Güncel fiyatlarla günlük maliyetini gör.',
    h1: 'Sitrülin Dozu Hesaplama',
    intro:
      'Sitrülin dozu kiloya göre ölçeklenmez. Aşağıda günlük dozunu seçip, gerçek ürün fiyatlarıyla paketin kaç gün yeteceğini ve günlük maliyetini görebilirsin.',
    minDailyGrams: 3,
    maxDailyGrams: 8,
    defaultDailyGrams: 6,
    dosageNote:
      'Saf L-sitrülin için 3-6 gram, sitrülin malat için 6-8 gram yaygın olarak kullanılan aralıklardır — ürünün hangi formu içerdiği etikette yazar ve bu iki form aynı miktarda değildir. Antrenmandan yaklaşık bir saat önce alınması yaygın bir tercihtir.',
    category: 'amino-asitler',
    searchTerm: 'citrulline',
  },
  // BETAİN BİLİNÇLİ OLARAK YOK: takip ettiğimiz 4 markada yalnızca 2 betain
  // ürünü var, ikisinin de paket gramajı gelmiyor (kategori bile boş) ve
  // biri farklı amaçlı (Betain HCL + Pepsin, sindirim enzimi). Bu veriyle
  // "kaç gün yeter / günlük maliyet" tablosu kurulamıyor — içi boş bir
  // sayfa açmak tam da kaçındığımız "ince içerik" olurdu. Markalarda
  // gerçek betain ürünü çoğalırsa buraya eklenebilir.
  {
    slug: 'eaa-dozu',
    name: 'EAA',
    title: 'EAA Dozu Hesaplama: Günde Kaç Gram? | ProteinAvcısı',
    description:
      'Günlük EAA dozunu ve paketinin kaç gün yeteceğini hesapla. Güncel fiyatlarla günlük maliyetini gör.',
    h1: 'EAA Dozu Hesaplama',
    intro:
      'EAA dozu kiloya göre değil, porsiyon bazında kullanılır. Günlük dozunu seç, gerçek ürün fiyatlarıyla paketin kaç gün yeteceğini hesapla.',
    minDailyGrams: 5,
    maxDailyGrams: 15,
    defaultDailyGrams: 10,
    dosageNote:
      'Porsiyon başına 5-15 gram yaygın kullanılan aralıktır. Önemli bir not: günlük protein hedefini zaten karşılıyorsan ayrıca EAA almanın ek fayda sağladığını gösteren güçlü bir kanıt yoktur — whey protein zaten yüksek oranda esansiyel amino asit içerir.',
    category: 'amino-asitler',
    searchTerm: 'eaa',
    guideSlug: 'bcaa-mi-eaa-mi-amino-asit-rehberi',
    guideLabel: 'BCAA mı EAA mı?',
  },
];

export function findSupplementDosage(slug: string): SupplementDosage | undefined {
  return SUPPLEMENT_DOSAGES.find((s) => s.slug === slug);
}
