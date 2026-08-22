// Takviye sözlüğü — rakip analizinde görülen bir fırsat: uzun-kuyruk
// "X nedir" aramalarını hedefleyen, düşük efor/yüksek getirili bir sayfa.
// Tanımlar kısa ama dürüst — kesin tıbbi iddia yok, belirsizlikler açıkça
// belirtiliyor (rehber yazıları/kategori rehberleriyle aynı ton).

export interface GlossaryTerm {
  term: string;
  slug: string;
  definition: string;
  // İlgili kategori sayfasına iç link (varsa) — sözlük ↔ kategori arası
  // çapraz linkleme.
  relatedCategorySlug?: string;
}

export interface GlossaryGroup {
  heading: string;
  terms: GlossaryTerm[];
}

export const GLOSSARY: GlossaryGroup[] = [
  {
    heading: 'Protein Türleri',
    terms: [
      {
        term: 'Whey Konsantre (WPC)',
        slug: 'whey-konsantre',
        definition:
          'Peynir altı suyundan elde edilen, en yaygın ve genelde en uygun fiyatlı protein tozu türü. Protein oranı ' +
          '%70-80 civarındadır, az miktarda yağ ve laktoz içerir.',
        relatedCategorySlug: 'protein-tozu',
      },
      {
        term: 'Whey İzole (WPI)',
        slug: 'whey-izole',
        definition:
          'Whey konsantrenin ek bir filtreleme adımından geçirilmiş hali. Protein oranı %85-95\'e çıkar, laktoz ' +
          'neredeyse sıfıra iner — laktoz hassasiyeti olanlar için tercih edilir.',
        relatedCategorySlug: 'protein-tozu',
      },
      {
        term: 'Whey Hidrolize (WPH)',
        slug: 'whey-hidrolize',
        definition:
          'Proteinin önceden kısmi olarak parçalandığı (hidrolize edildiği) bir whey formu — teorik olarak daha hızlı ' +
          'emilir. Genelde daha pahalıdır, çoğu kullanıcı için izole/konsantreye kıyasla pratik fark küçüktür.',
        relatedCategorySlug: 'protein-tozu',
      },
      {
        term: 'Kazein',
        slug: 'kazein',
        definition:
          'Sütteki ikinci ana protein türü, whey\'e göre çok daha yavaş sindirilir. Genelde gece veya uzun açlık ' +
          'aralıklarında tercih edilir, antrenman sonrası hızlı toparlanma için değil.',
        relatedCategorySlug: 'protein-tozu',
      },
      {
        term: 'Bitkisel Protein',
        slug: 'bitkisel-protein',
        definition:
          'Bezelye, pirinç, kenevir veya soya gibi kaynaklardan elde edilen, süt proteini içermeyen protein tozu. ' +
          'Tek bir bitkisel kaynağın amino asit profili genelde eksiktir, bu yüzden kaliteli ürünler birden fazla ' +
          'kaynağı karıştırır.',
        relatedCategorySlug: 'protein-tozu',
      },
    ],
  },
  {
    heading: 'Amino Asitler',
    terms: [
      {
        term: 'BCAA',
        slug: 'bcaa',
        definition:
          'Dallı zincirli 3 amino asit (lösin, izolösin, valin) — kas protein sentezini tetikleyen lösin sinyaline ' +
          'odaklanır. EAA\'nın bir alt kümesidir.',
        relatedCategorySlug: 'amino-asitler',
      },
      {
        term: 'EAA',
        slug: 'eaa',
        definition:
          'Vücudun kendisinin üretemediği 9 esansiyel amino asidin tamamı (BCAA\'nın 3\'ü de bunların içinde). Kas ' +
          'protein sentezi için sadece lösin sinyali yetmez, diğer 8 amino asit de gerekir.',
        relatedCategorySlug: 'amino-asitler',
      },
      {
        term: 'Glutamin',
        slug: 'glutamin',
        definition:
          'Bağışıklık ve bağırsak sağlığıyla ilişkilendirilen bir amino asit. Kas gelişimi üzerindeki doğrudan etkisi ' +
          'BCAA/EAA kadar güçlü kanıtlanmamıştır.',
        relatedCategorySlug: 'amino-asitler',
      },
      {
        term: 'Sitrülin',
        slug: 'sitrulin',
        definition:
          'Kan akışını destekleyerek antrenman sırasında "pump" hissini artıran bir amino asit türevi. Etkili doz ' +
          'genelde 6-8 g civarındadır — birçok ucuz üründe bu dozun altında kullanılır.',
        relatedCategorySlug: 'pre-workout',
      },
      {
        term: 'Beta-Alanin',
        slug: 'beta-alanin',
        definition:
          'Kas dokusunda karnosin birikimini artırıp yüksek yoğunluklu, kısa süreli egzersizlerde yorgunluğu ' +
          'geciktirmeye yardımcı olan bir amino asit. Cilt/yüzde karıncalanma (parestezi) zararsız ama bilinen bir ' +
          'yan etkisidir.',
        relatedCategorySlug: 'pre-workout',
      },
      {
        term: 'Taurin',
        slug: 'taurin',
        definition:
          'Enerji içeceklerinde ve bazı pre-workout formüllerinde bulunan bir amino asit türevi. Performans ' +
          'üzerindeki etkisi diğer bileşenlere (kafein, beta-alanin gibi) kıyasla daha zayıf kanıtlanmıştır.',
        relatedCategorySlug: 'pre-workout',
      },
      {
        term: 'ALCAR (Asetil-L-Karnitin)',
        slug: 'alcar',
        definition:
          'L-Karnitin\'in bir formu, standart L-Karnitin\'e göre kan-beyin bariyerini daha kolay geçtiği düşünülür. ' +
          'Bilişsel etkileriyle de anılır, ama bu alanda da kanıtlar sınırlıdır.',
        relatedCategorySlug: 'l-carnitine-cla',
      },
    ],
  },
  {
    heading: 'Performans Takviyeleri',
    terms: [
      {
        term: 'Kreatin Monohidrat',
        slug: 'kreatin-monohidrat',
        definition:
          'En eski, en çok araştırılan ve genelde en uygun fiyatlı kreatin formu. Kas gücü ve patlayıcı performans ' +
          'üzerindeki etkisi bilimsel literatürde en sağlam kanıtlanmış takviyelerden biridir.',
        relatedCategorySlug: 'kreatin',
      },
      {
        term: 'Kreatin HCL',
        slug: 'kreatin-hcl',
        definition:
          'Kreatinin hidroklorür formu — "daha az su tutar" veya "daha az mide rahatsızlığı yapar" iddiasıyla ' +
          'pazarlanır. Bu iddiaları monohidrata kıyasla doğrulayan geniş ölçekli çalışma sayısı azdır.',
        relatedCategorySlug: 'kreatin',
      },
      {
        term: 'Yükleme Fazı',
        slug: 'yukleme-fazi',
        definition:
          'İlk 5-7 gün yüksek dozla (günde ~20 g) kreatin kullanıp kas depolarını hızlıca doldurma yöntemi. ' +
          'Atlanırsa da (doğrudan düşük dozla başlanırsa) aynı doygunluğa 3-4 hafta içinde ulaşılır — sonuç aynıdır, ' +
          'yükleme sadece bir hız tercihidir.',
        relatedCategorySlug: 'kreatin',
      },
      {
        term: 'Termojenik',
        slug: 'termojenik',
        definition:
          'Metabolizma hızında hafif, geçici bir artış sağladığı düşünülen bileşenler (kafein, yeşil çay ekstresi ' +
          'gibi). Bu etki günlük kalori açığının yanında ölçülemeyecek kadar küçüktür, tek başına yağ kaybı sağlamaz.',
        relatedCategorySlug: 'yag-yakici',
      },
      {
        term: 'Biyoyararlanım',
        slug: 'biyoyararlanim',
        definition:
          'Alınan bir besin öğesinin vücut tarafından ne kadarının gerçekten kullanılabildiğini ifade eder. Sadece ' +
          'toplam miktar (ör. "24 g protein") bu konuda tam bir fikir vermez.',
      },
    ],
  },
  {
    heading: 'Kilo / Hacim',
    terms: [
      {
        term: 'Gainer',
        slug: 'gainer',
        definition:
          'Standart bir protein tozuna kıyasla çok daha fazla karbonhidrat içeren, kilo almayı kolaylaştırmak için ' +
          'tasarlanmış yüksek kalorili bir takviye. Kilo almakta zorlanmayan biri için genelde gerekli değildir.',
        relatedCategorySlug: 'kilo-hacim',
      },
      {
        term: 'Maltodekstrin',
        slug: 'maltodekstrin',
        definition:
          'Gainer ürünlerinde sıkça kullanılan, hızlı sindirilen bir karbonhidrat kaynağı. Kan şekerinde ani ' +
          'yükselmelere yol açabilir, bazı kaliteli ürünler bunun yerine yulaf gibi daha yavaş kaynaklar kullanır.',
        relatedCategorySlug: 'kilo-hacim',
      },
    ],
  },
  {
    heading: 'Vitamin ve Mineral',
    terms: [
      {
        term: 'Multivitamin',
        slug: 'multivitamin',
        definition:
          'Genel beslenme eksikliklerini önlemeye yönelik, düşük-orta dozlarda birçok vitamin/mineral içeren ' +
          'takviye. Belirli bir eksikliği hedef almaz — kan tahlilinde tespit edilen bir eksiklik varsa tekli/yüksek ' +
          'dozlu bir takviye genelde daha etkilidir.',
        relatedCategorySlug: 'vitamin',
      },
      {
        term: 'ZMA',
        slug: 'zma',
        definition:
          'Çinko, magnezyum ve B6 vitamininin bir kombinasyonu. Uyku kalitesi ve toparlanmayı desteklediği öne ' +
          'sürülür, ama bu iddiaları destekleyen kanıtlar sınırlıdır.',
        relatedCategorySlug: 'vitamin',
      },
    ],
  },
  {
    heading: 'Yağ Yakıcı',
    terms: [
      {
        term: 'CLA (Konjuge Linoleik Asit)',
        slug: 'cla',
        definition:
          'Et ve süt ürünlerinde doğal olarak bulunan bir yağ asidi türevi. Yağ hücrelerinin depolanmasını ' +
          'etkilediği öne sürülür, ama insan çalışmalarındaki sonuçlar tutarsızdır.',
        relatedCategorySlug: 'l-carnitine-cla',
      },
      {
        term: 'L-Karnitin',
        slug: 'l-karnitin',
        definition:
          'Yağ asitlerini hücrenin enerji üreten kısmına taşıyan bir bileşik. Vücut zaten yeterli miktarda üretir, ' +
          'dışarıdan alınan ek miktarın etkisi beklenenden daha mütevazıdır.',
        relatedCategorySlug: 'l-carnitine-cla',
      },
    ],
  },
  {
    heading: 'Fiyat ve Etiket Okuma',
    terms: [
      {
        term: 'Servis Başına Fiyat',
        slug: 'servis-basina-fiyat',
        definition:
          'Paket fiyatının, paketten çıkan servis (porsiyon) sayısına bölünmesiyle bulunan gerçek maliyet. Büyük ' +
          'bir paketin "ucuz" görünmesi, servis başına fiyatı yüksekse yanıltıcı olabilir.',
      },
      {
        term: 'Amino Spiking',
        slug: 'amino-spiking',
        definition:
          'Bazı üreticilerin, ucuz serbest amino asitleri (glisin, taurin gibi) ekleyerek etikette görünen protein ' +
          'oranını yapay olarak yükseltme pratiği. Üçüncü taraf test sertifikaları bu tür sapmaları tespit etmeye ' +
          'yardımcı olur.',
      },
      {
        term: 'Üçüncü Taraf Test',
        slug: 'ucuncu-taraf-test',
        definition:
          'Bir ürünün, üreticiden bağımsız bir laboratuvar tarafından (etikette yazan içeriği gerçekten taşıyıp ' +
          'taşımadığı ve yasaklı madde içerip içermediği açısından) test edilmesi. Informed Sport ve NSF Certified ' +
          'for Sport bilinen örneklerdir.',
      },
      {
        term: 'Gerçek İndirim (ProteinAvcısı tanımı)',
        slug: 'gercek-indirim',
        definition:
          'Bir ürünün son 30 gün içindeki en yüksek fiyatına göre şu anki fiyatının gerçekten düşük olması. ' +
          'Markanın kendi sitesinde yazan "eski fiyat/yeni fiyat" beyanına değil, bizim topladığımız fiyat ' +
          'geçmişine dayanır — ProteinAvcısı bu ayrımı "Gerçek İndirim" ve "Mağaza Kampanyası" olarak iki ayrı ' +
          'etiketle gösterir.',
      },
    ],
  },
];
