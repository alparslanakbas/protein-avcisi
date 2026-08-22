// Kategori sayfalarına eklenen uzun-format bilimsel rehber içeriği.
// Rakip analizinde (bkz. CLAUDE.md "Rakip analizi") en göze çarpan fark
// buydu: onların kategori sayfaları ~2500-4800 kelimelik, H2/H3 yapılı
// birer rehber, bizimkiler sadece ürün tablosu + kısa bir intro
// (CATEGORY_INTROS) idi. Kademeli olarak genişletiliyor — önce en
// yüksek hacimli 3 kategori (protein-tozu, kreatin, pre-workout),
// kalan 6'sı için bu obje henüz tanımlı değil (sayfa o durumda bu
// bölümü hiç göstermiyor).
//
// Ton: mevcut rehber yazılarıyla (article) aynı — dürüst, abartısız,
// kesin tıbbi iddia yok, belirsizlik varsa açıkça belirtiliyor.

export interface CategoryGuideSection {
  heading: string;
  paragraphs: string[];
  table?: { headers: string[]; rows: string[][] };
}

export interface CategoryGuide {
  // Sayfanın en üstüne, "zero-click-answer" olarak işaretlenen tek
  // paragraflık tanım — Google/AI motorlarının doğrudan alıntılaması
  // için (schema.org Speakable ile eşleşiyor, bkz. category-page.ts).
  zeroClickAnswer: string;
  sections: CategoryGuideSection[];
  // İlgili rehber yazısına gerçek bir link (iç linkleme) — düz metin
  // içinde "rehberimize bakabilirsiniz" yazıp tıklanamaz bırakmak yerine.
  relatedArticleSlug: string;
  relatedArticleTitle: string;
}

export const CATEGORY_GUIDES: Partial<Record<string, CategoryGuide>> = {
  'protein-tozu': {
    zeroClickAnswer:
      'Protein tozu karşılaştırması, farklı markaların protein takviyelerini servis başına protein miktarı, ' +
      'gram başına maliyet, protein türü (whey konsantre, izole, kazein, bitkisel) ve saflık oranı gibi ' +
      'nesnel kriterlere göre yan yana değerlendirme sürecidir. Sadece paket üzerindeki toplam gramaja değil, ' +
      'bir porsiyonda gerçekten kaç gram protein olduğuna ve bunun fiyata oranına bakmak, en yanıltıcı ' +
      'karşılaştırma hatasını (büyük paket = iyi fiyat sanmak) önler.',
    sections: [
      {
        heading: 'Protein Tozu Türleri ve Aralarındaki Farklar',
        paragraphs: [
          'Piyasadaki protein tozlarının büyük çoğunluğu süt kaynaklıdır: whey (peynir altı suyu) ve kazein. ' +
            'Whey konsantre (WPC), en yaygın ve genelde en uygun fiyatlı türdür — protein oranı genellikle ' +
            '%70-80 civarındadır, geri kalanı az miktarda yağ ve laktozdan oluşur. Whey izole (WPI), ek bir ' +
            'filtreleme adımından geçtiği için protein oranı %85-95\'e çıkar, laktoz neredeyse sıfıra iner — ' +
            'bu yüzden laktoz hassasiyeti olanlar genelde izole tercih eder.',
          'Kazein, yine sütten gelir ama sindirimi çok daha yavaştır (whey dakikalar içinde, kazein saatler ' +
            'içinde sindirilir) — bu yüzden genelde gece, uzun süre protein akışı istenen durumlarda tercih ' +
            'edilir, antrenman sonrası hızlı toparlanma için değil.',
          'Bitkisel protein tozları (bezelye, pirinç, kenevir, soya veya bunların karışımı) süt proteini ' +
            'içermez, vegan/laktoz intoleranslı kullanıcılar için tek seçenektir. Tek bir bitkisel kaynağın ' +
            'amino asit profili genelde eksiktir (örn. pirinç proteininde lisin az, bezelyede metiyonin az) — ' +
            'bu yüzden kaliteli bitkisel ürünler genelde birden fazla kaynağı karıştırır.',
        ],
        table: {
          headers: ['Tür', 'Protein Oranı', 'Laktoz', 'Sindirim Hızı', 'En Uygun Kullanım'],
          rows: [
            ['Whey Konsantre (WPC)', '%70-80', 'Düşük-orta', 'Hızlı', 'Genel/günlük kullanım, uygun fiyat'],
            ['Whey İzole (WPI)', '%85-95', 'Neredeyse yok', 'Hızlı', 'Laktoz hassasiyeti, düşük kalori hedefi'],
            ['Kazein', '%80-90', 'Düşük', 'Yavaş', 'Gece, uzun açlık aralıkları'],
            ['Bitkisel (karışık)', '%70-80', 'Yok', 'Orta', 'Vegan/laktoz intoleransı'],
          ],
        },
      },
      {
        heading: 'Fiyat Karşılaştırmasında Neye Bakılmalı?',
        paragraphs: [
          'İki ürünü paket fiyatına göre karşılaştırmak yanıltıcıdır — 2 kg\'lık bir paket, 900 g\'lık bir ' +
            'paketten "ucuz" görünse bile porsiyon başına protein miktarı farklıysa gerçek maliyet tam tersi ' +
            'olabilir. Doğru karşılaştırma birimi servis başına protein maliyeti: paket fiyatı ÷ (paketten ' +
            'çıkan servis sayısı × servis başına protein gramı).',
          'ProteinAvcısı, markanın kendi beyan ettiği servis büyüklüğü ve porsiyon başına protein bilgisi ' +
            'ulaşılabildiğinde bu hesabı otomatik yapıp ürün kartlarında "servis başı fiyat" olarak gösteriyor ' +
            '— bu bilgi markanın sitesinde yoksa tahmini bir rakam üretmiyoruz, alan boş kalıyor.',
        ],
      },
      {
        heading: 'Biyoyararlanım ve Amino Asit Profili Nedir?',
        paragraphs: [
          'Biyoyararlanım, alınan proteinin vücut tarafından ne kadarının gerçekten kullanılabildiğini ifade ' +
            'eder — sadece toplam protein gramajı bu konuda tam bir fikir vermez. Whey proteini, yüksek lösin ' +
            'içeriği (kas protein sentezini tetikleyen dallı zincirli bir amino asit) ve hızlı emilimi ' +
            'nedeniyle genellikle yüksek biyoyararlanımlı kabul edilir.',
          'International Society of Sports Nutrition (ISSN), karşılaştırma yaparken servis başına lösin ' +
            'miktarının (yaklaşık 2-3 g eşik değeri) dikkate alınmasını öneriyor — bu, sadece "kaç gram ' +
            'protein" değil, "bu proteinin ne kadarı gerçekten işe yarıyor" sorusuna daha yakın bir cevap.',
        ],
      },
      {
        heading: 'Üçüncü Taraf Test Sertifikaları Neden Önemli?',
        paragraphs: [
          'Takviye sektöründe üretici beyanı ile gerçek içerik arasında fark çıkabiliyor (bu duruma "amino ' +
            'spiking" deniyor — ucuz serbest amino asitlerin protein oranını yapay olarak şişirmek için ' +
            'eklenmesi). Informed Sport veya NSF Certified for Sport gibi bağımsız sertifikalar, ürünün ' +
            'etikette yazan içeriği gerçekten taşıdığını ve yasaklı madde içermediğini üçüncü bir tarafın ' +
            'test ettiği anlamına gelir.',
          'ProteinAvcısı şu an için bu sertifikaları ürün verisinde ayrı bir alan olarak takip etmiyor — ' +
            'bir ürünü değerlendirirken markanın kendi ürün sayfasında bu sertifikalardan bahsedip ' +
            'bahsetmediğine bakmak, ekstra bir güven katmanı ekler.',
        ],
      },
      {
        heading: 'Konsantre mi İzole mi: Hangisini Seçmeli?',
        paragraphs: [
          'İkisi de kaliteli bir seçenektir, doğru cevap kişisel ihtiyaca göre değişir. Bütçe öncelikliyse ve ' +
            'laktoz hassasiyeti yoksa whey konsantre genelde daha iyi bir gram-başına-fiyat sunar. Laktoza ' +
            'duyarlıysanız, kalori kısıtlı bir dönemdeyseniz (izole daha az yağ/karbonhidrat içerir) veya ' +
            'daha yüksek protein saflığı istiyorsanız izole tercih sebebi olabilir.',
        ],
      },
    ],
    relatedArticleSlug: 'whey-protein-nasil-secilir',
    relatedArticleTitle: 'Whey Protein Nasıl Seçilir?',
  },
  kreatin: {
    zeroClickAnswer:
      'Kreatin karşılaştırması yapılırken en çok karıştırılan iki konu şudur: kreatin türleri arasındaki ' +
      'gerçek fark ve yükleme fazının gerekliliği. Bilimsel literatürde en çok araştırılan ve etkinliği en ' +
      'iyi kanıtlanmış form kreatin monohidrattır — daha pahalı alternatiflerin (HCL, kre-alkalyn gibi) ' +
      'monohidrata karşı anlamlı bir üstünlüğü olduğuna dair güçlü bir kanıt yok.',
    sections: [
      {
        heading: 'Kreatin Türleri Arasındaki Gerçek Farklar',
        paragraphs: [
          'Kreatin monohidrat, en eski, en çok araştırılan ve genelde en uygun fiyatlı formdur. Mikronize ' +
            'monohidrat ise aynı molekül, sadece daha küçük partikül boyutuna öğütülmüş hali — suda daha ' +
            'kolay çözünür ama vücuttaki etkinliği aynıdır, "daha güçlü" bir versiyon değildir.',
          'Kreatin HCL (hidroklorür) ve kre-alkalyn gibi formlar, "daha az su tutar" veya "daha az mide ' +
            'rahatsızlığı yapar" iddiasıyla pazarlanır — ama bu iddiaları doğrulayan bağımsız, geniş ölçekli ' +
            'çalışma sayısı monohidrata kıyasla çok azdır. Fiyat farkının bilimsel bir üstünlükle ' +
            'desteklenmediğini bilerek karar vermek önemli.',
          'Creapure, bir marka değil bir üretim standardıdır (Almanya menşeli, yüksek saflıkta kreatin ' +
            'monohidrat üreten bir tedarikçinin tescilli adı) — birçok Türk marka kendi ürününde Creapure ' +
            'kullandığını belirtir, bu bir saflık/kalite güvencesi olarak değerlendirilebilir.',
        ],
      },
      {
        heading: 'Yükleme Fazı Gerekli mi?',
        paragraphs: [
          'Geleneksel protokol, ilk 5-7 gün günde 20 g (4 doza bölünmüş) "yükleme", ardından günde 3-5 g ' +
            '"idame" şeklindeydi. Yükleme fazının tek faydası kas kreatin depolarının daha HIZLI dolmasıdır ' +
            '— atlanırsa da (doğrudan günde 3-5 g ile başlanırsa) aynı doygunluk noktasına yaklaşık 3-4 ' +
            'hafta içinde ulaşılır, sonuç aynıdır.',
          'Bu yüzden yükleme fazı zorunlu değil, sadece bir hız tercihi. Mide rahatsızlığı yaşamak ' +
            'istemeyenler doğrudan idame dozuyla başlayabilir.',
        ],
      },
      {
        heading: 'Su Tutma ve Kilo Artışı Neden Olur?',
        paragraphs: [
          'Kreatin, kas hücrelerinin içine su çeker (hücre içi hidrasyon) — bu, "şişkinlik" değil, kasın ' +
            'kendisinin daha dolgun görünmesine yol açan, hücre içinde kalan bir su tutumudur. İlk 1-2 hafta ' +
            'içinde 1-2 kg\'lık bir kilo artışı genelde bu su tutumundan kaynaklanır, yağ artışı değildir.',
          'Bu etki geri dönüşümlüdür — kreatin kullanımı bırakıldığında birkaç hafta içinde kaybolur.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Saflık ve Gramaj',
        paragraphs: [
          'Kreatin ürünlerinde fiyat karşılaştırması nispeten basittir çünkü etkin madde tek bir bileşendir ' +
            '— karmaşık bir amino asit profiline bakmaya gerek yok. Asıl dikkat edilmesi gereken, paketin ' +
            'gerçekten saf kreatin monohidrat mı yoksa "kreatin kompleksi" adı altında başka (ve genelde daha ' +
            'ucuz) bileşenlerle seyreltilmiş bir karışım mı olduğudur — ürün adında/etiketinde "monohidrat" ' +
            'yazmayan bir kreatin ürünü karşılaştırırken bu ayrımı gözden kaçırmamak gerekir.',
        ],
      },
      {
        heading: 'Kimler Dikkatli Kullanmalı?',
        paragraphs: [
          'Böbrek fonksiyon bozukluğu olan kişiler kreatin kullanmadan önce mutlaka bir hekime danışmalı — ' +
            'sağlıklı böbreklerde kreatinin güvenli olduğuna dair geniş bir literatür olsa da, önceden var ' +
            'olan bir böbrek rahatsızlığı durumunda bu genellenemez.',
        ],
      },
    ],
    relatedArticleSlug: 'kreatin-alirken-nelere-dikkat-edilmeli',
    relatedArticleTitle: 'Kreatin Alırken Nelere Dikkat Edilmeli?',
  },
  'pre-workout': {
    zeroClickAnswer:
      'Pre-workout karşılaştırması yapılırken en önemli kriter, etiketteki toplam bileşen sayısı değil, kafein ' +
      'miktarı ve performansı etkilediği bilimsel olarak gösterilmiş birkaç bileşenin (beta-alanin, sitrülin, ' +
      'kreatin) etkili dozda bulunup bulunmadığıdır. Aynı fiyata çok sayıda "özel karışım" bileşeni sıralayan ' +
      'ama etkin dozları belirtmeyen ürünler, genelde her bileşenden az miktarda içerir.',
    sections: [
      {
        heading: 'Pre-Workout İçeriğindeki Temel Bileşenler',
        paragraphs: [
          'Kafein, pre-workout\'ların en yaygın ve etkisi en net kanıtlanmış bileşenidir — uyanıklık, algılanan ' +
            'efor düzeyinde azalma ve kısa süreli performans artışıyla ilişkilendirilir. Servis başı doz ' +
            'markadan markaya büyük farklılık gösterir (150-400 mg arası), bu yüzden karşılaştırma yaparken ' +
            'ilk bakılması gereken rakamdır.',
          'Beta-alanin, kas dokusunda karnosin birikimini artırarak yüksek yoğunluklu, 1-4 dakika süren ' +
            'egzersizlerde yorgunluğu geciktirmeye yardımcı olur — etkili doz genelde günde 3,2-6,4 g arasında ' +
            'kabul edilir (tek seferde değil, zamanla birikimli).',
          'Sitrülin (malat formu dahil), kan akışını destekleyerek "pump" hissini artırır ve bazı çalışmalarda ' +
            'egzersiz kapasitesine küçük bir katkı gösterir; etkili doz genelde 6-8 g civarındadır — birçok ' +
            'ucuz üründe bu dozun çok altında kullanılır.',
        ],
      },
      {
        heading: 'Karıncalanma (Parestezi) Neden Olur?',
        paragraphs: [
          'Beta-alanin alımından sonra yüzde/ellerde hissedilen karıncalanma (parestezi) zararsız ama ' +
            'rahatsız edici bir yan etkidir — beta-alaninin sinir uçlarını geçici olarak uyarmasından ' +
            'kaynaklanır. Dozu bölerek almak (tek seferde büyük doz yerine) bu hissi azaltabilir. Bu, ürünün ' +
            'kalitesiyle ilgili bir sorun değil, beta-alaninin doğal bir yan etkisidir.',
        ],
      },
      {
        heading: 'Kafein Duyarlılığı ve Doz Aralığı',
        paragraphs: [
          'Kafeine duyarlı kişiler için 150 mg\'ın altı "düşük doz" sayılabilirken, deneyimli kullanıcılar ' +
            '300 mg\'ın üzerini tercih edebilir. Antrenman saatinden bağımsız günlük toplam kafein tüketimini ' +
            '(kahve, çay, enerji içeceği dahil) hesaba katmak önemli — pre-workout\'taki dozu tek başına değil, ' +
            'günün geri kalanına eklenen bir miktar olarak düşünmek gerekir.',
          'Akşam antrenmanı yapanlar için yüksek dozlu bir pre-workout uyku kalitesini etkileyebilir — kafein ' +
            'yarı ömrü ortalama 5 saat civarındadır.',
        ],
      },
      {
        heading: 'Kreatin İçeren Pre-Workout\'lara Dikkat',
        paragraphs: [
          'Bazı pre-workout ürünleri formülüne kreatin de ekler — bu kendi içinde sorun değil, ama kreatinin ' +
            'etkili olması için düzenli/günlük kullanım gerektirdiğini (bkz. yukarıdaki kreatin bölümü) göz ' +
            'önünde bulundurmak gerekir. Sadece antrenman günlerinde pre-workout kullanıp diğer günler ' +
            'almıyorsanız, kreatin dozunuz düzensiz kalır ve tam potansiyeline ulaşamaz — bu durumda ayrı bir ' +
            'kreatin takviyesi almak daha tutarlı sonuç verir.',
        ],
      },
      {
        heading: 'Fiyat/Porsiyon Karşılaştırması Nasıl Yapılır?',
        paragraphs: [
          'Pre-workout karşılaştırmasında paket fiyatı yerine porsiyon başına maliyete bakmak burada da ' +
            'geçerli — ama ek olarak porsiyon başına kafein/beta-alanin/sitrülin miktarına bölerek "etkin ' +
            'doz başına maliyet" hesaplamak, aynı görünen iki üründe gerçek farkı ortaya çıkarır.',
        ],
      },
    ],
    relatedArticleSlug: 'pre-workout-nasil-secilir',
    relatedArticleTitle: 'Pre-Workout Nasıl Seçilir?',
  },
  'amino-asitler': {
    zeroClickAnswer:
      'Amino asit takviyesi karşılaştırmasında ilk ayrım BCAA (dallı zincirli 3 amino asit) ile EAA (9 ' +
      'esansiyel amino asidin tamamı) arasındadır. EAA, BCAA\'nın içerdiği tüm amino asitleri de kapsadığı ' +
      'için kas protein sentezini tetiklemede genelde daha eksiksiz kabul edilir — ama zaten yeterli ' +
      'miktarda protein (whey, et, yumurta) tüketen biri için ikisinin de ek bir katkısı sınırlıdır.',
    sections: [
      {
        heading: 'BCAA mı EAA mı? Temel Fark',
        paragraphs: [
          'BCAA (Branched-Chain Amino Acids), lösin, izolösin ve valin olmak üzere 3 amino asitten oluşur ' +
            've kas protein sentezini tetikleyen ana sinyal lösine odaklanır. EAA (Essential Amino Acids) ise ' +
            'vücudun kendisinin üretemediği 9 amino asidin tamamını içerir — BCAA\'nın 3\'ü de bu 9\'un içinde.',
          'Kas protein sentezi için sadece lösin sinyali yetmez, proteini gerçekten inşa etmek için diğer ' +
            '8 esansiyel amino asit de gerekir — bu yüzden son yıllarda spor bilimi literatüründe EAA\'nın ' +
            'BCAA\'ya kıyasla daha eksiksiz bir seçenek olduğu görüşü öne çıkıyor.',
        ],
      },
      {
        heading: 'Zaten Yeterli Protein Alıyorsanız Gerekli mi?',
        paragraphs: [
          'Günlük protein ihtiyacınızı (whey, et, yumurta, bakliyat gibi tam protein kaynaklarından) zaten ' +
            'karşılıyorsanız, ayrıca BCAA/EAA takviyesi almanın ek bir kas gelişimi faydası sınırlıdır — çünkü ' +
            'tam protein kaynakları zaten tüm esansiyel amino asitleri barındırır. Bu takviyeler asıl olarak ' +
            'iki durumda anlamlı olur: açken (fasted) antrenman yapılıyorsa veya günlük protein hedefine ' +
            'tam ulaşmak zor geliyorsa.',
        ],
      },
      {
        heading: 'Amino Asit Profili ve Oranlar',
        paragraphs: [
          'BCAA ürünlerinde sıkça görülen "2:1:1" veya "4:1:1" gibi oranlar, lösin:izolösin:valin oranını ' +
            'ifade eder — yüksek lösin oranı (4:1:1 gibi) genelde daha güçlü bir kas protein sentezi sinyali ' +
            'anlamına gelir ama tek başına toplam amino asit miktarından daha önemli değildir. Ürün ' +
            'karşılaştırırken sadece orana değil, servis başına toplam gram miktarına da bakmak gerekir.',
        ],
      },
      {
        heading: 'Ne Zaman Alınmalı?',
        paragraphs: [
          'BCAA/EAA genelde antrenman sırasında veya antrenman öncesi/sonrası tüketilir — su ile karıştırılıp ' +
            'içildiği için sindirimi hızlıdır. Aç karnına yapılan uzun kardiyo antrenmanlarında kas dokusunun ' +
            'enerji için kullanılmasını (katabolizma) azaltmak amacıyla tercih edilebilir.',
        ],
      },
      {
        heading: 'Glutamin ve Diğer Amino Asitler',
        paragraphs: [
          'Glutamin, bağışıklık ve bağırsak sağlığıyla ilişkilendirilen, ama kas gelişimi üzerindeki doğrudan ' +
            'etkisi BCAA/EAA kadar güçlü kanıtlanmamış ayrı bir amino asittir — bazı ürünlerde BCAA/EAA\'ya ek ' +
            'olarak bulunur. Sitrülin de bir amino asit türevidir ama asıl etkisi kan akışı/pompa hissi ' +
            'üzerinedir, kas protein sentezine katkısı BCAA/EAA\'dan farklı bir mekanizmadır.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Neye Bakılmalı?',
        paragraphs: [
          'Amino asit ürünlerinde de aynı kural geçerli: paket fiyatı değil, servis başına toplam amino asit ' +
            'gramı ve lösin miktarı karşılaştırılmalı. Bazı ürünler "amino asit kompleksi" adı altında düşük ' +
            'dozda birçok farklı amino asidi bir arada listeler — bu genelde etkili dozun altında kalan, ' +
            'pazarlama amaçlı bir liste uzunluğudur.',
        ],
      },
    ],
    relatedArticleSlug: 'bcaa-mi-eaa-mi-amino-asit-rehberi',
    relatedArticleTitle: 'BCAA mı EAA mı? Amino Asit Takviyesi Rehberi',
  },
  'kilo-hacim': {
    zeroClickAnswer:
      'Kilo aldırıcı (gainer) karşılaştırması yapılırken en önemli kriter, ürünün servis başına kalori ' +
      'yoğunluğu ve karbonhidrat/protein/yağ dağılımıdır — gainer\'lar, standart protein tozlarından farklı ' +
      'olarak yüksek kalori almayı kolaylaştırmak için tasarlanmıştır, bu yüzden aynı mantıkla (sadece ' +
      'protein miktarına bakarak) karşılaştırılmamalıdır.',
    sections: [
      {
        heading: 'Gainer Nedir, Kimler İçin Uygun?',
        paragraphs: [
          'Gainer, standart bir protein tozuna kıyasla çok daha fazla karbonhidrat (genelde maltodekstrin ' +
            'veya benzeri hızlı sindirilen bir kaynak) içeren, servis başına 300-1200 kalori arasında ' +
            'değişebilen bir takviyedir. Doğal olarak çok yemek yiyemeyen veya günlük kalori ihtiyacını ' +
            'sadece yemekle karşılamakta zorlanan, kilo almayı hedefleyen kişiler için pratik bir çözümdür.',
          'Zaten kilo almakta zorlanmayan veya yağlanma eğilimi yüksek olan biri için gainer genelde gerekli ' +
            'değildir — bu durumda normal bir protein tozu + yeterli günlük beslenme yeterlidir.',
        ],
      },
      {
        heading: 'Kalori Yoğunluğu ve Makro Dağılımı',
        paragraphs: [
          'Gainer ürünleri arasında kalori yoğunluğu çok değişkendir — bazıları servis başına ~300 kalori ' +
            '(daha "hafif" gainer, gerçekte yüksek proteinli bir ek atıştırmalığa yakın), bazıları 1000 ' +
            'kalorinin üzerindedir. Karşılaştırma yaparken paket fiyatına değil, hedeflenen günlük kalori ' +
            'fazlasına hangi ürünün daha uygun olduğuna bakmak gerekir.',
        ],
      },
      {
        heading: 'Şeker/Maltodekstrin İçeriğine Dikkat',
        paragraphs: [
          'Ucuz gainer ürünlerinin kalorisinin büyük kısmı genelde basit şeker veya maltodekstrinden gelir — ' +
            'bu, hızlı enerji sağlar ama kan şekerinde ani yükselmelere yol açabilir. Daha kaliteli ürünler ' +
            'karbonhidrat kaynağını (yulaf, tatlı patates unu gibi) çeşitlendirerek bu etkiyi azaltmaya ' +
            'çalışır — etiketteki karbonhidrat kaynağına bakmak, sadece toplam kalori/protein rakamına ' +
            'bakmaktan daha fazla bilgi verir.',
        ],
      },
      {
        heading: 'Protein Tozundan Farkı',
        paragraphs: [
          'Standart bir whey protein tozu genelde servis başına 100-150 kalori ve düşük karbonhidrat ' +
            'içerirken, gainer bilinçli olarak kalori yoğunluğunu artırır. Kilo almak istemeyen ama sadece ' +
            'protein ihtiyacını karşılamak isteyen biri için gainer yanlış bir seçim olur — bu iki ürün ' +
            'farklı hedeflere hizmet eder, biri diğerinin yerine geçmez.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Kalori Başına Maliyet',
        paragraphs: [
          'Gainer için en anlamlı karşılaştırma birimi genelde kalori başına maliyettir (protein tozundaki ' +
            'gibi sadece protein gramı başına değil) — çünkü ürünün asıl işlevi kalori sağlamaktır. Aynı ' +
            'paket fiyatına sahip iki gainer, servis başına kalorisi farklıysa gerçekte çok farklı bir ' +
            'maliyet sunuyor olabilir.',
        ],
      },
    ],
    relatedArticleSlug: 'kilo-aldirici-gainer-nasil-kullanilir',
    relatedArticleTitle: 'Kilo Aldırıcı (Gainer) Nasıl Kullanılır?',
  },
  'l-carnitine-cla': {
    zeroClickAnswer:
      'L-Karnitin ve CLA (Konjuge Linoleik Asit), genelde "yağ yakıcı" kategorisinde pazarlanan ama farklı ' +
      'mekanizmalarla çalışan iki ayrı bileşendir. İkisinin de insan çalışmalarındaki etkisi mütevazı ve ' +
      'tutarsızdır — kalori açığı ve düzenli antrenman olmadan tek başlarına anlamlı bir yağ kaybı sağladığına ' +
      'dair güçlü bir kanıt yoktur.',
    sections: [
      {
        heading: 'L-Karnitin Nasıl Çalışır?',
        paragraphs: [
          'L-Karnitin, yağ asitlerini hücrenin enerji üreten kısmına (mitokondri) taşıyan bir bileşiktir — ' +
            'teorik olarak yağın enerjiye dönüştürülmesine "yardımcı" olur. Ama vücut zaten karaciğerde ' +
            'yeterli L-Karnitin üretir, dışarıdan alınan ek miktarın kas dokusuna ulaşan kısmı sınırlıdır — ' +
            'bu yüzden etkisi beklenenden daha mütevazıdır.',
        ],
      },
      {
        heading: 'CLA Nedir, L-Karnitin\'den Farkı',
        paragraphs: [
          'CLA, doğal olarak et ve süt ürünlerinde bulunan bir yağ asidi türevidir, farklı bir mekanizmayla ' +
            '(yağ hücrelerinin büyümesini/depolanmasını etkileyerek) çalıştığı öne sürülür. L-Karnitin ile ' +
            'aynı kategoride satılsa da kimyasal olarak tamamen farklı bir bileşendir, ikisi birbirinin ' +
            'yerine geçmez.',
        ],
      },
      {
        heading: 'Bilimsel Kanıt Ne Diyor?',
        paragraphs: [
          'Hem L-Karnitin hem CLA üzerine yapılan insan çalışmalarının sonuçları karışıktır — bazı ' +
            'çalışmalar küçük bir yağ kaybı farkı gösterirken, birçoğu anlamlı bir fark bulamamıştır. Bu ' +
            'takviyeleri "yağ eritici" gibi göstermek gerçekçi değildir; en iyi ihtimalle kalori açığı ve ' +
            'antrenmanın yanında küçük bir destek olabilirler, bunların yerine geçmezler.',
        ],
      },
      {
        heading: 'Şekil/Form Farkları',
        paragraphs: [
          'L-Karnitin birkaç farklı formda satılır: L-Karnitin Tartrat (genel kullanım), Asetil-L-Karnitin ' +
            '(ALCAR, bilişsel etkileriyle de anılır) ve L-Karnitin L-Tartrat sıvı/shot formları. Formlar ' +
            'arasında emilim hızı farklılık gösterebilir ama hiçbiri "yağ yakma" etkisini kanıtlanmış şekilde ' +
            'artırmaz.',
        ],
      },
      {
        heading: 'Ne Zaman ve Nasıl Kullanılır?',
        paragraphs: [
          'Genelde antrenman öncesi tüketilir (teorik olarak yağ asidi kullanımını desteklemesi umulur). ' +
            'Sıvı shot formları hızlı emilim iddiasıyla satılır ama kapsül/toz formuna göre kanıtlanmış bir ' +
            'üstünlükleri yoktur — fiyat farkı genelde kullanım kolaylığına yöneliktir.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Neye Bakılmalı?',
        paragraphs: [
          'Servis başına L-Karnitin/CLA miktarı ürünler arasında oldukça değişkendir — bazı ürünler etkili ' +
            'kabul edilen dozun (L-Karnitin için genelde 2 g civarı) çok altında kalır. Karşılaştırma ' +
            'yaparken toplam paket fiyatı yerine servis başına aktif madde miktarına bakmak, hangi ürünün ' +
            'gerçekten daha uygun fiyatlı olduğunu netleştirir.',
        ],
      },
    ],
    relatedArticleSlug: 'l-karnitin-yag-yakiminda-ise-yarar-mi',
    relatedArticleTitle: 'L-Karnitin Yağ Yakımında İşe Yarar mı?',
  },
  'saglikli-atistirmaliklar': {
    zeroClickAnswer:
      'Sağlıklı atıştırmalık karşılaştırması yapılırken "protein bar" veya "fit atıştırmalık" etiketi tek ' +
      'başına yeterli bir ölçüt değildir — şeker/tatlandırıcı içeriği, gerçek protein miktarı ve porsiyon ' +
      'başına kalori arasındaki denge, ürünün gerçekten "sağlıklı" olup olmadığını belirler.',
    sections: [
      {
        heading: '"Sağlıklı Atıştırmalık" Ne Anlama Gelir?',
        paragraphs: [
          'Bu kategori genelde protein barları, düşük şekerli granola/müsli çeşitleri ve fonksiyonel ' +
            'atıştırmalıkları kapsar. "Sağlıklı" etiketi standart bir tanıma sahip değildir — bir ürünün ' +
            'gerçekten daha iyi bir seçim olup olmadığını anlamak için besin değeri tablosuna bakmak gerekir, ' +
            'sadece paket üzerindeki pazarlama metnine güvenmek yeterli değildir.',
        ],
      },
      {
        heading: 'Protein Bar Seçerken Nelere Bakılmalı?',
        paragraphs: [
          'Bir protein barında bakılması gereken üç temel rakam: porsiyon başına protein miktarı, toplam ' +
            'şeker miktarı ve toplam kalori. Bazı "protein bar" ürünleri aslında düşük protein/yüksek şeker ' +
            'içerir — sadece isimde "protein" geçmesi yeterli bir kalite göstergesi değildir.',
        ],
      },
      {
        heading: 'Şeker Alkolleri ve Sindirim Rahatsızlığı',
        paragraphs: [
          'Düşük şekerli barlarda şeker yerine sıkça eritritol, maltitol gibi şeker alkolleri kullanılır — ' +
            'bunlar kan şekerini daha az etkiler ama fazla miktarda tüketildiğinde bazı kişilerde şişkinlik ' +
            've sindirim rahatsızlığına yol açabilir. Bu, ürünün kalitesiyle ilgili değil, bireysel toleransla ' +
            'ilgili bir durumdur.',
        ],
      },
      {
        heading: 'Makro Dengesi Nasıl Okunur?',
        paragraphs: [
          'Bir atıştırmalığı değerlendirirken protein/karbonhidrat/yağ dağılımının o anki hedefe (kilo ' +
            'verme, kas kazanımı, genel beslenme desteği) uygun olup olmadığına bakmak, tek bir rakama ' +
            '(sadece kaloriye veya sadece proteine) odaklanmaktan daha sağlıklı bir karşılaştırma sağlar.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Neye Bakılmalı?',
        paragraphs: [
          'Atıştırmalıklarda porsiyon büyüklüğü ürünler arasında oldukça değişkendir — birim fiyat yerine ' +
            'porsiyon başına protein gramı başına maliyete bakmak, gerçekte hangi ürünün daha uygun fiyatlı ' +
            'olduğunu ortaya çıkarır.',
        ],
      },
    ],
    relatedArticleSlug: 'saglikli-atistirmaliklar-nasil-secilir',
    relatedArticleTitle: 'Sağlıklı Atıştırmalıklar Nasıl Seçilir?',
  },
  vitamin: {
    zeroClickAnswer:
      'Vitamin/mineral takviyesi karşılaştırması yapılırken en önemli ayrım, ihtiyacın tek bir eksiklik ' +
      '(örn. D vitamini) mi yoksa genel bir destek mi olduğudur — multivitaminler geniş ama düşük dozlu bir ' +
      'kapsam sunarken, tekli vitaminler hedeflenen bir eksikliği daha yüksek dozda karşılar.',
    sections: [
      {
        heading: 'Multivitamin mi Tekli Vitamin mi?',
        paragraphs: [
          'Multivitaminler, genel beslenme eksikliklerini önlemeye yönelik düşük-orta dozlarda birçok ' +
            'vitamin/mineral içerir — belirli bir eksikliği hedef almaz. Kan tahlilinde belirli bir eksiklik ' +
            '(örn. D vitamini, demir, B12) tespit edilmişse, o eksikliği tekli/yüksek dozlu bir takviyeyle ' +
            'karşılamak genelde daha etkilidir; bir multivitaminin içindeki düşük doz yeterli olmayabilir.',
        ],
      },
      {
        heading: 'Emilim Formları',
        paragraphs: [
          'Bazı vitaminlerin birden fazla kimyasal formu vardır ve emilimleri farklılık gösterebilir — ' +
            'örneğin B12 vitamininde siyanokobalamin (daha yaygın, ucuz) ve metilkobalamin (vücudun doğrudan ' +
            'kullanabildiği aktif form) arasında bir tercih söz konusudur. Form farkı fiyata da yansır, bu ' +
            'yüzden karşılaştırma yaparken sadece "B12 var mı" değil, "hangi form" sorusu da önemlidir.',
        ],
      },
      {
        heading: 'Yağda Eriyen ve Suda Eriyen Vitaminler',
        paragraphs: [
          'A, D, E, K vitaminleri yağda erir ve vücutta depolanabilir — bu yüzden aşırı yüksek dozda uzun ' +
            'süre kullanmak (özellikle A ve D) teorik olarak birikim riski taşır. B grubu ve C vitamini suda ' +
            'erir, fazlası genelde idrarla atılır. Bu fark, "daha fazlası her zaman daha iyidir" varsayımının ' +
            'neden yanlış olduğunu açıklar.',
        ],
      },
      {
        heading: 'Sporcular İçin Özel İhtiyaçlar',
        paragraphs: [
          'Düzenli antrenman yapan kişilerde D vitamini ve magnezyum eksikliği nispeten sık görülür (kapalı ' +
            'ortamda antrenman, terleme yoluyla mineral kaybı gibi sebeplerle) — ama bu genellemeler kişisel ' +
            'bir kan tahlilinin yerini tutmaz, gerçek ihtiyacı belirlemenin en güvenilir yolu budur.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Neye Bakılmalı?',
        paragraphs: [
          'Vitamin/mineral ürünlerinde karşılaştırma, servis başına her bir bileşenin miktarını günlük ' +
            'referans alım değeriyle (%RDA/NRV) karşılaştırmayı gerektirir — bazı ucuz ürünler etiket ' +
            'listesinde çok sayıda vitamin sıralar ama her birinden çok düşük miktar içerir.',
        ],
      },
    ],
    relatedArticleSlug: 'vitamin-mineral-takviyesi-nasil-secilir',
    relatedArticleTitle: 'Vitamin ve Mineral Takviyesi Nasıl Seçilir?',
  },
  'yag-yakici': {
    zeroClickAnswer:
      'Yağ yakıcı takviye karşılaştırması yapılırken bilinmesi gereken en önemli gerçek şu: hiçbir takviye, ' +
      'kalori açığı ve düzenli antrenman olmadan yağ kaybı sağlamaz. Bu ürünler en iyi ihtimalle metabolizmayı ' +
      've iştahı hafifçe destekleyen yardımcılardır, "yağ eritici" değildir — karşılaştırma yaparken bu ' +
      'gerçekçi çerçeveyi korumak önemlidir.',
    sections: [
      {
        heading: 'Yağ Yakıcılar Gerçekten Yağ Yaktırır mı?',
        paragraphs: [
          'Termojenik bileşenler (kafein, yeşil çay ekstresi gibi) metabolizma hızında küçük, geçici bir ' +
            'artış sağlayabilir — ama bu etki, günlük kalori açığının yanında ölçülemeyecek kadar küçüktür. ' +
            'Kalori fazlası tüketen biri hiçbir yağ yakıcıyla yağ kaybedemez; bu ürünlerin gerçek katkısı ' +
            'ancak doğru beslenme ve antrenmanın üzerine, marjinal bir destek olarak değerlendirilmelidir.',
        ],
      },
      {
        heading: 'Termojenik Bileşenler',
        paragraphs: [
          'En sık kullanılan bileşenler kafein, yeşil çay ekstresi (EGCG), yeşil kahve ekstresi ve kapsaisin ' +
            '(acı biber özütü) gibi maddelerdir. Bunların ortak noktası, hafif bir metabolizma/enerji ' +
            'harcaması artışı ile ilişkilendirilmeleridir — ama etkileri kişiden kişiye değişir ve büyük, ' +
            'anlamlı bir yağ kaybı garantisi vermez.',
        ],
      },
      {
        heading: 'Kalori Açığının Önemi',
        paragraphs: [
          'Yağ kaybının tek kanıtlanmış yolu, harcanan kaloriyle alınan kalori arasında sürdürülebilir bir ' +
            'açık oluşturmaktır. Yağ yakıcı takviyeler bu denklemin yerine geçemez — bir ürünü seçerken ' +
            '"bu bana kalori açığı olmadan yağ kaybettirir mi" beklentisiyle değil, mevcut bir beslenme ' +
            'planına küçük bir destek olarak yaklaşmak gerçekçidir.',
        ],
      },
      {
        heading: 'Yan Etkiler ve Dikkat Edilmesi Gerekenler',
        paragraphs: [
          'Çoğu yağ yakıcı ürün yüksek miktarda kafein ve benzeri uyarıcılar içerir — bu, kalp çarpıntısı, ' +
            'uykusuzluk ve kaygı gibi yan etkilere yol açabilir, özellikle günün geri kalanında da kafein ' +
            'tüketiliyorsa. Kalp/tansiyon rahatsızlığı olanların bu tür ürünleri kullanmadan önce bir ' +
            'hekime danışması önemlidir.',
        ],
      },
      {
        heading: 'Fiyat Karşılaştırmasında Neye Bakılmalı?',
        paragraphs: [
          'Bu kategoride fiyat karşılaştırması yaparken servis başına kafein ve diğer aktif bileşen ' +
            'miktarlarına bakmak (pre-workout kategorisindeki mantığın aynısı) en anlamlı yöntemdir — çok ' +
            'sayıda bileşen listeleyip her birinden düşük miktar içeren ürünler, etkisi kanıtlanmamış bir ' +
            '"özel karışım" olmaktan öteye geçmeyebilir.',
        ],
      },
    ],
    relatedArticleSlug: 'yag-yakici-takviyeler-gercekten-ise-yarar-mi',
    relatedArticleTitle: 'Yağ Yakıcı Takviyeler Gerçekten İşe Yarar mı?',
  },
};
