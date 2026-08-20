export interface FaqItem {
  question: string;
  answer: string;
}

// Her kategori sayfasına O KATEGORİYE ÖZEL sorular. Ana sayfadaki SSS
// platformu anlatıyor ("fiyatlar ne sıklıkla güncelleniyor" gibi); buradaki
// sorular ise ürünün kendisiyle ilgili ve insanların Google'a gerçekten
// yazdığı ifadeleri hedefliyor ("kreatin saç döker mi" gibi). İki fayda:
// (1) uzun kuyruk arama trafiği doğrudan ürün listeleme sayfasına geliyor,
// (2) kategori sayfası "sadece ürün listesi" olmaktan çıkıyor — GSC'de
// "Tarandı ama dizine eklenmedi" sorununun asıl sebebi buydu.
//
// TON: rehber yazılarımızla aynı — dürüst, abartısız, kesin tıbbi iddia yok.
// Bilinmeyen bir şey varsa "değişir/danış" demek, uydurmaktan iyidir.
export const CATEGORY_FAQS: Record<string, FaqItem[]> = {
  'protein-tozu': [
    {
      question: 'Protein tozu seçerken neye bakmalı?',
      answer:
        'Paket fiyatına değil, bir servisin maliyetine ve o serviste kaç gram protein olduğuna bakmak daha doğru sonuç verir. Aynı fiyattaki iki üründen biri porsiyon başına belirgin şekilde daha az protein içerebilir. Etiketteki porsiyon büyüklüğü ve porsiyon sayısı bu karşılaştırmanın temelidir.',
    },
    {
      question: 'İzole mi konsantre mi daha iyi?',
      answer:
        'İkisi de whey proteinidir, fark işlenme derecesindedir. İzole daha çok filtrelendiği için protein oranı yüksek, laktoz ve yağ oranı düşüktür — laktoza duyarlıysan avantaj sağlar. Konsantre daha uygun fiyatlıdır ve çoğu kişi için yeterlidir. "Daha pahalı olan herkes için daha iyidir" demek doğru olmaz.',
    },
    {
      question: 'Protein tozu ne zaman içilmeli?',
      answer:
        'Antrenmandan hemen sonraki 30 dakikanın kritik olduğu görüşü eskisi kadar güçlü değil. Güncel yaklaşım, günlük toplam protein alımının zamanlamadan çok daha belirleyici olduğu yönünde. Antrenman sonrası içmek yine de pratik bir alışkanlıktır.',
    },
    {
      question: 'Günde kaç ölçek protein tozu içilir?',
      answer:
        'Bu, günlük protein hedefine ve öğünlerden ne kadar aldığına bağlı. Çoğu kişi için günde 1-2 ölçek, katı gıdadan kalan açığı kapatmaya yeter. Protein tozunu ana kaynak değil, tamamlayıcı olarak düşünmek daha doğru.',
    },
    {
      question: 'Protein tozu böbreklere zarar verir mi?',
      answer:
        'Sağlıklı bireylerde makul miktarda protein alımının böbrek hasarına yol açtığını gösteren güçlü bir kanıt bulunmuyor. Ancak mevcut bir böbrek rahatsızlığı varsa durum farklıdır — böyle bir durumda protein alımı bir hekimle konuşulmalıdır.',
    },
    {
      question: 'Protein tozu şişkinlik yapar mı?',
      answer:
        'Bazı kişilerde whey konsantre, içerdiği laktoz nedeniyle şişkinlik veya sindirim rahatsızlığı yapabiliyor. Laktoza duyarlıysan izole formlar veya bitkisel protein seçenekleri değerlendirilebilir; bazı ürünler bu şikayeti azaltmak için sindirim enzimi de içerir.',
    },
    {
      question: 'Sütle mi suyla mı karıştırmalı?',
      answer:
        'İkisi de olur; fark kalori ve kıvamdadır. Suyla hazırlandığında sadece tozun kendi kalorisini alırsın. Sütle hazırlandığında karışım kremalaşır ve ekstra kalori/protein eklenir — kilo almaya çalışıyorsan avantaj, kalori takibi yapıyorsan hesaba katman gereken bir şeydir.',
    },
    {
      question: 'Protein tozu fiyatları neden bu kadar farklı?',
      answer:
        'Protein kaynağı (konsantre/izole/hidrolize/bitkisel), paket büyüklüğü, aroma ve markanın konumlandırması fiyatı belirler. Büyük paketler genelde servis başına daha ucuza gelir. Bu yüzden "en ucuz paket" ile "en ucuz servis" çoğu zaman aynı ürün değildir.',
    },
    {
      question: 'Antrenman yapmadığım günlerde de içmeli miyim?',
      answer:
        'Günlük protein hedefine katı gıdayla ulaşamıyorsan evet. Kaslar sadece antrenman günlerinde onarılmaz; protein ihtiyacı her gün devam eder.',
    },
  ],

  kreatin: [
    {
      question: 'Kreatin ne işe yarar?',
      answer:
        'Kreatin, kısa süreli ve yüksek yoğunluklu egzersizlerde (ağırlık kaldırma, sprint gibi) fiziksel performansı artırmaya yardımcı olur. Kas hücrelerinde enerji üretimiyle ilgili bir rol oynar. Üzerinde en çok araştırma yapılmış spor takviyelerinden biridir.',
    },
    {
      question: 'Kreatin nasıl kullanılır, günde kaç gram?',
      answer:
        'Yaygın kullanım günde 3-5 gram civarındadır ve ürünlerin çoğu 5 gramlık ölçekle gelir. Daha fazlası doygunluğu hızlandırmaz, fazlası vücuttan atılır. Belirleyici olan miktardan çok düzenliliktir.',
    },
    {
      question: 'Kreatin kaç günde etki eder?',
      answer:
        'Kreatin anlık etki gösteren bir madde değildir. Kas hücrelerinde belirli bir doygunluğa ulaşması gerekir ve bu, düzenli kullanımla ortalama 3-4 hafta sürer. Bu yüzden bir iki gün kullanıp sonuç beklemek yanıltıcı olur.',
    },
    {
      question: 'Kreatin su tutar mı?',
      answer:
        'Kreatin kas hücrelerinde su tutulmasını artırabilir; bu, tartıda birkaç kiloluk bir artış olarak görünebilir. Bu ağırlık yağ değildir, kas içi su miktarındaki değişimdir. Kullanım döneminde günlük su tüketimine dikkat etmek yaygın bir öneridir.',
    },
    {
      question: 'Kreatin saç döker mi?',
      answer:
        'Bu, internette çok dolaşan bir iddia. Kaynağı, tek bir küçük çalışmada gözlenen bir hormon değişimidir; saç dökülmesinin kendisi o çalışmada ölçülmemiştir. Kreatinin saç dökülmesine yol açtığını gösteren güçlü bir kanıt bulunmuyor, ancak konu hakkında kesin bir yargı için yeterli araştırma da yok.',
    },
    {
      question: 'Yükleme fazı şart mı?',
      answer:
        'Hayır. Yükleme fazı (ilk hafta bölünmüş dozlarda daha yüksek miktar) doygunluğa daha hızlı ulaşmak için kullanılan bir yöntemdir. Doğrudan 3-5 gramla başlamak da aynı sonuca, sadece birkaç hafta daha uzun sürede ulaştırır. Yükleme sırasında bazı kişilerde mide rahatsızlığı görülebilir.',
    },
    {
      question: 'Kreatin ara verilmeli mi?',
      answer:
        'Düzenli kullanımda döngüsel ara vermenin gerekli olduğunu gösteren güçlü bir kanıt yok. Ara verildiğinde kas kreatin seviyeleri zamanla normale döner, tekrar başlandığında doygunluk süreci yeniden işler.',
    },
    {
      question: 'Kreatin ve protein tozu birlikte kullanılır mı?',
      answer:
        'Evet. İkisi vücutta tamamen farklı mekanizmalar üzerinden çalışır ve birbirlerinin emilimini engellemez. Aynı karışımda alınmasının bilinen bir sakıncası yoktur.',
    },
    {
      question: 'Kreatin monohidrat mı yoksa diğer formlar mı?',
      answer:
        'Kreatin monohidrat, üzerinde en çok araştırma yapılmış ve en uygun fiyatlı formdur. Diğer formların (HCL, etil ester gibi) monohidrata belirgin üstünlük sağladığını gösteren güçlü bir kanıt bulunmuyor.',
    },
  ],

  'amino-asitler': [
    {
      question: 'BCAA ile EAA arasındaki fark nedir?',
      answer:
        'EAA (esansiyel amino asitler) vücudun kendi üretemediği dokuz amino asidin tamamını kapsar. BCAA ise bunların içinden üç tanesidir (lösin, izolösin, valin). Yani BCAA, EAA\'nın bir alt kümesidir.',
    },
    {
      question: 'Protein tozu kullanıyorsam BCAA gerekir mi?',
      answer:
        'Genellikle hayır. Whey protein zaten yüksek oranda BCAA içerir; günlük protein hedefini karşılayan biri için ayrıca BCAA almanın ek fayda sağladığını gösteren güçlü bir kanıt yoktur. Bu, BCAA takviyelerinin en çok tartışılan noktasıdır.',
    },
    {
      question: 'Glutamin ne işe yarar?',
      answer:
        'Glutamin vücutta en bol bulunan amino asitlerden biridir ve normal koşullarda vücut bunu yeterli miktarda üretebilir. Sporcularda takviye olarak kullanımının performansa katkısı konusunda kanıtlar sınırlıdır.',
    },
    {
      question: 'Amino asit takviyesi ne zaman alınır?',
      answer:
        'Zamanlama konusunda net bir üstünlük gösteren güçlü bir kanıt yok. Antrenman sırasında veya çevresinde almak yaygın bir tercihtir, ancak günlük toplam protein/amino asit alımı çok daha belirleyicidir.',
    },
    {
      question: 'Arjinin ve sitrulin ne için kullanılır?',
      answer:
        'Bu ikisi, kan akışıyla ilişkili nitrik oksit üretimini destekledikleri için genelde antrenman sırasındaki "pump" hissiyle birlikte anılır. Sitrulinin bu amaçla arjinine göre daha etkili emildiği yaygın bir görüştür.',
    },
    {
      question: 'Amino asit takviyesi kas yapar mı?',
      answer:
        'Tek başına kas yapan bir takviye yoktur. Kas gelişimi antrenman, yeterli toplam protein alımı ve dinlenmeye bağlıdır; amino asit takviyeleri bu tablonun tamamlayıcı bir parçası olabilir, yerine geçmez.',
    },
    {
      question: 'Aç karnına amino asit alınır mı?',
      answer:
        'Alınabilir; amino asitler hızlı emilir ve mideyi genelde rahatsız etmez. Yine de kişiden kişiye değişir — rahatsızlık hissediyorsan hafif bir ara öğünle birlikte almayı deneyebilirsin.',
    },
    {
      question: 'EAA tozu mu kapsül mü?',
      answer:
        'Etken madde aynıdır; fark pratiklikte ve maliyettedir. Toz formlar genelde gram başına daha ucuzdur ve dozu ayarlamak kolaydır; kapsüller taşınabilirlik açısından avantajlıdır ama aynı miktar için daha fazla adet gerekir.',
    },
  ],

  'pre-workout': [
    {
      question: 'Pre-workout ne işe yarar?',
      answer:
        'Antrenman öncesi enerji, odaklanma ve algılanan performans hissini desteklemek için kullanılır. Etkisinin büyük kısmı içerdiği kafeinden gelir; ayrıca "pump" hissiyle ilişkilendirilen sitrulin gibi bileşenler de içerebilir.',
    },
    {
      question: 'Pre-workout antrenmandan kaç dakika önce alınır?',
      answer:
        'Genel öneri 20-30 dakika öncedir. Kafeinin kandaki seviyesi bu sürede yükselmeye başlar. Çok erken alınırsa etkinin zirvesi antrenman bitmeden geçebilir, çok geç alınırsa ısınma bitmeden hiçbir şey hissedilmez.',
    },
    {
      question: 'Pre-workout neden karıncalanma yapar?',
      answer:
        'Ciltte hissedilen karıncalanma genelde beta-alanin içeriğinden kaynaklanır. Zararsız bir yan etkidir ve şiddeti kişiden kişiye değişir. Ürünün "çalıştığının" göstergesi değildir, sadece o bileşene verilen bir tepkidir.',
    },
    {
      question: 'Her gün pre-workout kullanılır mı?',
      answer:
        'Düzenli ve yüksek dozda kafein kullanımı zamanla tolerans geliştirir — aynı etkiyi hissetmek için dozu artırmak yerine, pre-workout\'u gerçekten ihtiyaç duyulan ağır antrenmanlara saklamak daha sürdürülebilir bir yaklaşımdır.',
    },
    {
      question: 'Akşam antrenmanında pre-workout kullanılır mı?',
      answer:
        'Kafeinin vücuttan atılması saatler sürer; akşam geç saatte kafeinli bir ürün almak uyku kalitesini belirgin şekilde etkileyebilir. Geç saatte antrenman yapanlar için stimülansız (kafeinsiz) seçenekler vardır.',
    },
    {
      question: 'Pre-workout yan etkileri nelerdir?',
      answer:
        'Yüksek kafein dozlarında çarpıntı, huzursuzluk ve uykuya dalmakta zorlanma görülebilir. Gün içinde tükettiğin kahve/çay da bu toplama eklenir. Kalp rahatsızlığı veya tansiyon sorunu olanların bir hekime danışması gerekir.',
    },
    {
      question: 'Kreatin mi pre-workout mu?',
      answer:
        'Farklı işler yaparlar, birbirinin alternatifi değildirler. Kreatin düzenli kullanımla zamanla performansa katkı sağlar; pre-workout ise o antrenman için anlık enerji/odak hissi verir. Bazı pre-workout ürünleri zaten kreatin içerir — ikisini birlikte alıyorsan etiketi kontrol et.',
    },
    {
      question: 'Pre-workout yarım doz alınabilir mi?',
      answer:
        'Evet ve ilk kez kullanıyorsan yaygın bir öneridir. Ürünlerin porsiyon başına kafein miktarı 150 mg ile 300 mg üzeri arasında çok değişir, yani "bir ölçek" her üründe aynı şey demek değildir.',
    },
  ],

  'kilo-hacim': [
    {
      question: 'Gainer nedir, protein tozundan farkı ne?',
      answer:
        'Gainer, protein yanında yüksek oranda karbonhidrat içeren, porsiyon başına kalorisi yüksek bir üründür. Protein tozu esas olarak protein açığını kapatmak için kullanılırken, gainer günlük kalori hedefine ulaşmakta zorlananlar için tasarlanmıştır.',
    },
    {
      question: 'Gainer kimler için uygun?',
      answer:
        'Yeterince yemek yemekte zorlanan, kilo almaya çalışan kişiler için pratik bir seçenek olabilir. Zaten kalori fazlasında olan biri için gerekli değildir — o durumda normal protein tozu ve gıda daha kontrollü bir yol sunar.',
    },
    {
      question: 'Gainer yağlandırır mı?',
      answer:
        'Gainer da sonuçta kaloridir. Toplam günlük kalori ihtiyacının üzerine çıkıldığında alınan kilonun bir kısmı yağ olarak depolanır. Ne kadarının kas ne kadarının yağ olacağı, antrenman düzenine ve toplam kalori fazlasının büyüklüğüne bağlıdır.',
    },
    {
      question: 'Gainer nasıl kullanılır?',
      answer:
        'Porsiyonlar genelde büyüktür (bazı ürünlerde 100 gramın üzerinde). Tam porsiyonu tek seferde içmek mide açısından zor gelirse bölerek kullanmak yaygın bir tercihtir. Öğün yerine değil, öğünlere ek olarak düşünülmelidir.',
    },
    {
      question: 'Gainer yerine ne yenebilir?',
      answer:
        'Yulaf, süt, muz, fıstık ezmesi gibi gıdalarla hazırlanan yüksek kalorili karışımlar benzer bir işlevi görebilir. Gainer\'ın avantajı pratikliktir, besleyicilik açısından tek üstün seçenek olduğu anlamına gelmez.',
    },
    {
      question: 'Gainer fiyatları neden değişiyor?',
      answer:
        'Paket büyüklüğü, protein/karbonhidrat oranı ve kullanılan protein kaynağı fiyatı etkiler. Gainer paketleri büyük olduğu için kilogram fiyatı düşük görünebilir; asıl karşılaştırma porsiyon başına maliyet üzerinden yapılmalıdır.',
    },
    {
      question: 'Gainer ne zaman içilmeli?',
      answer:
        'Zamanlama kritik değildir; belirleyici olan günlük toplam kaloridir. Antrenman sonrası veya öğün araları, iştahı bozmadan ek kalori almak açısından pratik zamanlardır.',
    },
  ],

  vitamin: [
    {
      question: 'Vitamin takviyesi herkese gerekli mi?',
      answer:
        'Hayır. Dengeli ve çeşitli besleniyorsan çoğu vitamini gıdalardan alabilirsin. Takviye, belirli bir eksiklik veya artmış ihtiyaç durumunda anlamlıdır — bunu tahminle değil, gerekiyorsa kan tahliliyle belirlemek doğru olur.',
    },
    {
      question: 'Multivitamin mi tekil vitamin mi?',
      answer:
        'Multivitamin geniş ama düşük dozlu bir kapsama sunar. Belirli bir eksiklik varsa (örneğin D vitamini), tekil ve uygun dozlu bir ürün genelde daha anlamlıdır. "Her şeyden biraz" her zaman en iyi çözüm değildir.',
    },
    {
      question: 'D vitamini ne zaman ve nasıl alınır?',
      answer:
        'D vitamini yağda çözünen bir vitamindir, bu yüzden yağ içeren bir öğünle birlikte alınması emilim açısından yaygın olarak önerilir. Doz kişiye göre değişir; yüksek dozlarda hekim kontrolü gerekir.',
    },
    {
      question: 'Magnezyum ne işe yarar?',
      answer:
        'Magnezyum kas ve sinir işlevleri, enerji metabolizması gibi birçok süreçte rol oynar. Sporcular arasında kramp ve uyku kalitesiyle ilişkilendirilerek sık kullanılır; farklı formları (sitrat, bisglisinat gibi) emilim ve sindirim açısından farklılık gösterebilir.',
    },
    {
      question: 'Çinko takviyesi ne zaman gerekir?',
      answer:
        'Çinko bağışıklık ve hormon dengesiyle ilişkilendirilen bir mineraldir. Uzun süreli yüksek doz kullanımı bakır emilimini etkileyebildiği için, uzun vadeli kullanımda bir sağlık profesyoneline danışmak gerekir.',
    },
    {
      question: 'Omega-3 hangi durumda kullanılır?',
      answer:
        'Balık tüketimi düşük olan kişilerde omega-3 açığını kapatmak için tercih edilir. Ürünler arasında EPA/DHA miktarı büyük fark gösterir — kapsül sayısına değil, porsiyon başına EPA/DHA miktarına bakmak daha doğru bir karşılaştırma sağlar.',
    },
    {
      question: 'Vitaminler aç karnına mı alınmalı?',
      answer:
        'Yağda çözünenler (A, D, E, K) yağ içeren bir öğünle birlikte daha iyi emilir. Suda çözünenler aç karnına alınabilir ama bazı kişilerde mide rahatsızlığı yapabilir; o durumda yemekle birlikte almak sorunu genelde çözer.',
    },
  ],

  'l-carnitine-cla': [
    {
      question: 'L-karnitin ne işe yarar?',
      answer:
        'L-karnitin, yağ asitlerinin hücre içinde enerjiye dönüştürülmek üzere taşınmasında rol oynayan bir bileşiktir. Vücut bunu kendisi de üretir ve kırmızı ette bulunur. Takviye olarak alındığında yağ yakımını belirgin şekilde artırdığına dair kanıtlar sınırlıdır.',
    },
    {
      question: 'L-karnitin zayıflatır mı?',
      answer:
        'Tek başına zayıflatan bir madde değildir. Kilo kaybı esas olarak kalori açığına bağlıdır; L-karnitin bu tablonun yerine geçmez. Vücudun zaten yeterli ürettiği bir bileşen olduğu için, eksikliği olmayan kişilerde ek faydası sınırlı kalabilir.',
    },
    {
      question: 'L-karnitin ne zaman içilir?',
      answer:
        'Yaygın kullanım antrenman öncesidir, ancak zamanlamanın belirleyici olduğunu gösteren güçlü bir kanıt yoktur. Sıvı (shot) formlar pratiklik açısından tercih edilir.',
    },
    {
      question: 'CLA nedir?',
      answer:
        'CLA (konjuge linoleik asit), bazı hayvansal gıdalarda doğal olarak bulunan bir yağ asididir. Vücut kompozisyonu üzerindeki etkisine dair çalışmalar karışık sonuçlar vermiştir; kesin ve büyük bir etki beklemek gerçekçi olmaz.',
    },
    {
      question: 'L-karnitin kas kaybettirir mi?',
      answer:
        'Böyle bir etkiye dair bir kanıt yoktur. Kas kaybı genelde aşırı kalori açığı ve yetersiz protein alımıyla ilişkilidir, takviyenin kendisiyle değil.',
    },
    {
      question: 'L-karnitin yan etkisi var mı?',
      answer:
        'Yüksek dozlarda bazı kişilerde mide rahatsızlığı veya vücut kokusunda değişiklik bildirilmiştir. Etiketteki önerilen kullanım miktarını aşmamak ve mevcut bir sağlık sorunu varsa hekime danışmak gerekir.',
    },
  ],

  'yag-yakici': [
    {
      question: 'Yağ yakıcı takviyeler gerçekten işe yarar mı?',
      answer:
        'Bu ürünlerin çoğu, metabolizmayı hafifçe hızlandırdığı veya iştahı baskıladığı öne sürülen bileşenler (çoğunlukla kafein ve bitkisel ekstreler) içerir. Etkileri genelde küçüktür ve kalori açığının yerine geçmez. Ürün isimlerindeki iddialar, gerçek etkiden çok pazarlama dilidir.',
    },
    {
      question: 'Yağ yakıcı olmadan kilo verilir mi?',
      answer:
        'Evet. Kilo kaybının temeli kalori açığıdır; hiçbir takviye bu koşul olmadan yağ kaybı sağlamaz. Takviyeler en iyi ihtimalle küçük bir destek sunar.',
    },
    {
      question: 'Termojenik ne demek?',
      answer:
        'Vücut ısısını ve dolayısıyla enerji harcamasını bir miktar artırdığı öne sürülen ürünler için kullanılan bir terimdir. Bu artış genellikle günlük toplam kalori harcamasında küçük bir paya karşılık gelir.',
    },
    {
      question: 'Yağ yakıcı yan etkileri neler?',
      answer:
        'Çoğu ürün yüksek miktarda kafein içerdiği için çarpıntı, huzursuzluk, uyku bozukluğu ve tansiyon yükselmesi görülebilir. Kalp rahatsızlığı, tansiyon sorunu olanlar veya kafeine hassas kişiler bir hekime danışmadan kullanmamalıdır.',
    },
    {
      question: 'Yağ yakıcı ne zaman kullanılır?',
      answer:
        'Genelde sabah veya antrenman öncesi tercih edilir. Kafein içeriği nedeniyle akşam saatlerinde kullanmak uyku kalitesini bozabilir.',
    },
    {
      question: 'Yağ yakıcı ile pre-workout birlikte kullanılır mı?',
      answer:
        'Dikkatli olmak gerekir: ikisi de yüksek kafein içerebilir ve birlikte alındığında toplam doz hızla yükselir. Etiketleri karşılaştırıp toplam kafein miktarını hesaplamak gerekir.',
    },
  ],

  'saglikli-atistirmaliklar': [
    {
      question: 'Protein bar gerçekten sağlıklı mı?',
      answer:
        'Değişir. Bazı barlar yüksek protein ve makul şeker içerirken, bazıları besin değeri açısından çikolatalı bir bardan çok farklı değildir. Etiketteki protein miktarı, şeker ve toplam kaloriye birlikte bakmak gerekir.',
    },
    {
      question: 'Protein bar öğün yerine geçer mi?',
      answer:
        'Genelde hayır. Barlar pratik bir ara öğün seçeneğidir; dengeli bir öğünün sunduğu lif, mikro besin ve doygunluk hissini çoğu zaman karşılamaz. Zaman baskısı olan durumlarda makul bir alternatiftir.',
    },
    {
      question: 'Protein bar kilo aldırır mı?',
      answer:
        'Toplam kalori hedefinin üzerine çıkılırsa evet — bu her gıda için geçerlidir. Bazı barların kalorisi beklenenden yüksektir, bu yüzden "sağlıklı" etiketine değil gerçek kalori değerine bakmak gerekir.',
    },
    {
      question: 'Şekersiz atıştırmalık gerçekten şekersiz mi?',
      answer:
        '"İlave şeker içermez" ifadesi, üründe hiç karbonhidrat olmadığı anlamına gelmez; tatlandırıcı veya doğal şeker kaynakları bulunabilir. Etiketteki toplam karbonhidrat ve şeker satırına bakmak en doğrusudur.',
    },
    {
      question: 'Antrenman öncesi protein bar yenir mi?',
      answer:
        'Yenebilir, ancak yağ ve lif oranı yüksek barlar mideyi ağırlaştırabilir. Antrenmana yakın zamanda daha hafif ve sindirimi kolay bir seçenek çoğu kişi için daha rahattır.',
    },
  ],
};
