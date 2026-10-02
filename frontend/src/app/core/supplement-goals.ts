import { FaqItem } from './category-faqs';

// "Hangi takviyeyi seçmeliyim?" — test ve hedef sayfalarının TEK içerik kaynağı.
//
// YAPI (29 Eylül, seo-gunlugu.md): test tarayıcıda çalışıyor, sonuç HEDEF
// BAŞINA bir SSR sayfası (dört sayfa). Cevap kombinasyonu başına sayfa
// açılmıyor: diğer cevaplar hedef sayfasını adres parametresiyle
// kişiselleştiriyor, canonical temiz adreste kalıyor.
//
// TON: rehber yazılarıyla aynı — dürüst, abartısız, kesin tıbbi iddia yok.
// Her sayfa neyin GEREKMEDİĞİNİ de söylüyor; sitenin "bu indirim gerçek mi?"
// sorusunun takviye tarafındaki karşılığı bu. Sağlık sorusu sorulmuyor;
// her sayfada hekim notu var.

export const FINDER_PATH = '/hangi-takviye';

/** Hüküm seviyesi: sayfadaki listenin sırası da bu. */
export type VerdictLevel = 'oncelikli' | 'istege-bagli' | 'gerek-yok';

export const VERDICT_LABELS: Record<VerdictLevel, string> = {
  oncelikli: 'Öncelikli',
  'istege-bagli': 'İsteğe bağlı',
  'gerek-yok': 'Gerek yok',
};

export interface GoalLink {
  label: string;
  path: string;
}

export interface GoalVerdict {
  level: VerdictLevel;
  name: string;
  reason: string;
  /** Yaygın kullanılan miktar; yalnızca yerleşik bir aralık varsa. */
  dose?: string;
  link?: GoalLink;
  /** Bu takviyenin ürün listesi sayfada varsa, o bölümün kimliği. */
  pickSectionId?: string;
}

/** Hedef sayfasındaki bir ürün listesi (/api/value-picks). */
export interface GoalPickSection {
  id: string;
  category: string;
  /** Uçtaki daraltma türü (ör. kilo-hacimde "gainer"). */
  type?: string;
  title: string;
  note?: string;
  /** Protein tozunda izole/bitkisel çipleri gösterilsin mi. */
  proteinFilter?: boolean;
  allLink: GoalLink;
}

export type QuizBranch = 'protein' | 'dayaniklilik';

export interface SupplementGoal {
  slug: string;
  /** Testteki seçenek ve bağlantı metni. */
  label: string;
  /** Testteki seçeneğin açıklaması. */
  description: string;
  icon: string;
  h1: string;
  metaTitle: string;
  metaDescription: string;
  /** Sayfanın açılışındaki tek paragraflık cevap. */
  answer: string;
  verdicts: GoalVerdict[];
  picks: GoalPickSection[];
  faqs: FaqItem[];
  quizBranch: QuizBranch;
}

const KREATIN_PICKS: GoalPickSection = {
  id: 'kreatin',
  category: 'kreatin',
  title: 'Kreatin',
  allLink: { label: 'Tüm kreatin fiyatları', path: '/kategori/kreatin' },
};

const PROTEIN_PICKS: GoalPickSection = {
  id: 'protein-tozu',
  category: 'protein-tozu',
  title: 'Protein tozu',
  proteinFilter: true,
  allLink: { label: 'Tüm protein tozu fiyatları', path: '/kategori/protein-tozu' },
};

const PROTEIN_CALCULATOR: GoalLink = { label: 'Protein ihtiyacını hesapla', path: '/hesaplama/protein-ihtiyaci' };
const CREATINE_DOSAGE: GoalLink = { label: 'Kreatin dozu', path: '/hesaplama/kreatin-dozu' };

export const SUPPLEMENT_GOALS: SupplementGoal[] = [
  {
    slug: 'kas-kazanimi',
    label: 'Kas ve güç kazanmak',
    description: 'Ağırlık antrenmanı yapıyorum, güçlenmek istiyorum',
    icon: 'ph-barbell',
    h1: 'Kas kazanmak için hangi takviye?',
    metaTitle: 'Kas Kazanmak İçin Hangi Takviye? | ProteinAvcısı',
    metaDescription:
      'Kas ve güç için kanıtı güçlü iki takviye: kreatin ve gerekirse protein tozu. Gerekmeyenler ve kilogram fiyatı en uygun güncel ürünler.',
    answer:
      'Kas kazanımını antrenman, yeterli kalori ve protein belirler; takviye bunların yerini tutmaz. Kanıtı güçlü ' +
      'iki takviye var: kreatin monohidrat ve protein hedefini yemekle tutturamıyorsan protein tozu. Gerisi ' +
      'isteğe bağlı ya da gereksiz.',
    verdicts: [
      {
        level: 'oncelikli',
        name: 'Kreatin monohidrat',
        reason:
          'Güç ve kas kazanımı için en çok araştırılmış takviye. Pahalı formlara gerek yok, monohidrat yeterli; ' +
          'önemli olan her gün kullanmak.',
        dose: 'Günde 3–5 g',
        link: CREATINE_DOSAGE,
        pickSectionId: 'kreatin',
      },
      {
        level: 'oncelikli',
        name: 'Protein tozu (gerekirse)',
        reason:
          'Kas için günlük protein toplamı önemli. Yemekle tutturamıyorsan en pratik tamamlayıcı; tutturuyorsan ' +
          'ek fayda sağlamaz.',
        dose: 'Hedef: kilo başına 1,6–2,2 g protein',
        link: PROTEIN_CALCULATOR,
        pickSectionId: 'protein-tozu',
      },
      {
        level: 'istege-bagli',
        name: 'Kafein ya da pre-workout',
        reason:
          'Antrenman performansını biraz artırabilir. Pre-workout ürünlerinde asıl etkili madde çoğunlukla kafein; ' +
          'kafeine hassassan ya da akşam antrenman yapıyorsan atla.',
        link: { label: 'Pre-workout nasıl kullanılır?', path: '/rehber/pre-workout-nasil-kullanilir' },
      },
      {
        level: 'gerek-yok',
        name: 'BCAA',
        reason:
          'Günlük protein hedefini tutturan birinde ek kas kazanımı sağladığı gösterilmedi; protein tozu zaten ' +
          'BCAA içerir.',
        link: { label: 'BCAA mı EAA mı?', path: '/rehber/bcaa-mi-eaa-mi-amino-asit-rehberi' },
      },
      {
        level: 'gerek-yok',
        name: 'Bitkisel testosteron destekleri',
        reason: 'Tribulus gibi ürünlerin sağlıklı erkeklerde kas ve güç kazanımını artırdığına dair güçlü kanıt yok.',
      },
    ],
    picks: [KREATIN_PICKS, PROTEIN_PICKS],
    faqs: [
      {
        question: 'Kas yapmak için takviye şart mı?',
        answer:
          'Hayır. Kas kazanımını antrenman, yeterli kalori ve protein belirler. Takviyeler bunları kolaylaştırır; ' +
          'kreatin dışındakilerin çoğunun etkisi küçüktür.',
      },
      {
        question: 'Kreatin ve protein tozu birlikte kullanılır mı?',
        answer:
          'Evet. İkisi farklı işler görür ve birlikte kullanılabilir. Kreatini her gün aynı dozda almak zamanlamasından ' +
          'daha önemlidir.',
      },
      {
        question: 'Spora yeni başlayan biri hangi takviyeyle başlamalı?',
        answer:
          'İlk aylarda en büyük kazanç antrenmandan gelir. Takviye istiyorsan kreatin monohidrat ve protein hedefini ' +
          'yemekle tutturamıyorsan protein tozu yeterli.',
      },
    ],
    quizBranch: 'protein',
  },
  {
    slug: 'kilo-alma',
    label: 'Kilo almak',
    description: 'Yeterince yiyemiyorum, kilo almakta zorlanıyorum',
    icon: 'ph-scales',
    h1: 'Kilo almak için hangi takviye?',
    metaTitle: 'Kilo Almak İçin Hangi Takviye? | ProteinAvcısı',
    metaDescription:
      'Kilo almakta zorlanıyorsan gainer mı, karbonhidrat tozu mu? Hangisi ne zaman işe yarar, hangisi gereksiz; kilogram fiyatına göre güncel ürünler.',
    answer:
      'Kilo almak için belirleyici olan kalori fazlası. Yemekle yetişemiyorsan gainer pratik bir çözüm; ' +
      'karbonhidrat tozu ile protein tozunu karıştırmak çoğu zaman daha ucuz. Kreatin kas kazanımını destekler.',
    verdicts: [
      {
        level: 'oncelikli',
        name: 'Gainer ya da karbonhidrat tozu (gerekirse)',
        reason:
          'Yemekle yeterli kalori alamıyorsan kalori açığını kapatır. Gainer hazır karışım; karbonhidrat tozu ' +
          '(cream of rice, maltodekstrin) + protein tozu aynı işi genelde daha ucuza görür.',
        link: { label: 'Gainer nasıl kullanılır?', path: '/rehber/kilo-aldirici-gainer-nasil-kullanilir' },
        pickSectionId: 'gainer',
      },
      {
        level: 'oncelikli',
        name: 'Kreatin monohidrat',
        reason:
          'Kas kazanımını destekler. İlk haftalarda kaslarda tutulan su tartıda genelde 1–2 kg olarak görünür.',
        dose: 'Günde 3–5 g',
        link: CREATINE_DOSAGE,
        pickSectionId: 'kreatin',
      },
      {
        level: 'istege-bagli',
        name: 'Protein tozu',
        reason: 'Gainer kullanmıyorsan ya da protein hedefin eksik kalıyorsa kalori fazlasına protein ekler.',
        link: PROTEIN_CALCULATOR,
      },
      {
        level: 'gerek-yok',
        name: 'BCAA ve EAA',
        reason: 'Kalori ve protein açığını kapatmaz; aynı parayla protein tozu ya da gıda daha çok iş görür.',
      },
    ],
    picks: [
      {
        id: 'gainer',
        category: 'kilo-hacim',
        type: 'gainer',
        title: 'Gainer',
        allLink: { label: 'Tüm kilo-hacim ürünleri', path: '/kategori/kilo-hacim' },
      },
      {
        id: 'karbonhidrat',
        category: 'kilo-hacim',
        type: 'karbonhidrat',
        title: 'Karbonhidrat tozu',
        note: 'Cream of rice ve maltodekstrin gibi saf karbonhidrat kaynakları; protein tozuyla karıştırılır.',
        allLink: { label: 'Tüm kilo-hacim ürünleri', path: '/kategori/kilo-hacim' },
      },
      KREATIN_PICKS,
    ],
    faqs: [
      {
        question: 'Gainer mı, protein tozu mu?',
        answer:
          'Kilo almakta zorlanıyorsan eksik olan çoğunlukla kalori; gainer kalori ve protein birlikte verir. Protein ' +
          'hedefini zaten tutturuyorsan karbonhidrat tozu daha ucuz bir yol.',
      },
      {
        question: 'Gainer yağlandırır mı?',
        answer:
          'Gainer da bir kalori kaynağı; ihtiyacından fazla kalori hangi kaynaktan gelirse gelsin yağ olarak depolanır. ' +
          'Haftada 0,25–0,5 kg artış çoğu kişi için makul bir hız.',
      },
      {
        question: 'Kilo almak için kreatin işe yarar mı?',
        answer:
          'Kreatin kas kazanımını destekler ve ilk haftalarda tartıda 1–2 kg su ağırlığı olarak görünür. Kalori ' +
          'fazlasının yerini tutmaz.',
      },
    ],
    quizBranch: 'protein',
  },
  {
    slug: 'yag-yakimi',
    label: 'Yağ yakmak',
    description: 'Kilo vermek istiyorum, kas kaybetmeden',
    icon: 'ph-fire',
    h1: 'Yağ yakmak için hangi takviye?',
    metaTitle: 'Yağ Yakmak İçin Hangi Takviye? | ProteinAvcısı',
    metaDescription:
      'Yağ yakmak için gerçekten işe yarayan takviye az: protein tozu ve kafein. Yağ yakıcı ve L-karnitin neden listede yok; güncel protein tozu fiyatları.',
    answer:
      'Yağ kaybını kalori açığı belirler; hiçbir takviye onu yaratmaz. Protein hedefini tutturmak tokluk ve kas ' +
      'kaybını azaltmak için önemli, protein tozu bunun en düşük kalorili yolu. Yağ yakıcıların anlamlı bir etkisi ' +
      'gösterilmedi.',
    verdicts: [
      {
        level: 'oncelikli',
        name: 'Protein tozu (gerekirse)',
        reason:
          'Kalori açığında kas kaybını azaltmak ve tok kalmak için protein hedefini tutturmak önemli. Yemekle ' +
          'zorlanıyorsan en düşük kalorili yol.',
        dose: 'Hedef: kilo başına 1,6–2,2 g protein',
        link: PROTEIN_CALCULATOR,
        pickSectionId: 'protein-tozu',
      },
      {
        level: 'istege-bagli',
        name: 'Kafein',
        reason:
          'Yorgunluğu ve iştahı biraz azaltabilir. Yağ yakıcıların çoğunda asıl etkili madde zaten kafein; kahve ' +
          'aynı işi görür.',
      },
      {
        level: 'istege-bagli',
        name: 'Kreatin',
        reason:
          'Diyette gücü korumaya yardım eder. Yağ kaybını engellemez; tartıdaki 1–2 kg artış kaslarda tutulan sudur.',
        dose: 'Günde 3–5 g',
        link: CREATINE_DOSAGE,
      },
      {
        level: 'gerek-yok',
        name: 'Yağ yakıcı (termojenik) ürünler',
        reason: 'Kalori açığı olmadan anlamlı yağ kaybı sağladıkları gösterilmedi.',
        link: { label: 'Yağ yakıcılar işe yarar mı?', path: '/rehber/yag-yakici-takviyeler-gercekten-ise-yarar-mi' },
      },
      {
        level: 'gerek-yok',
        name: 'L-karnitin ve CLA',
        reason: 'Sağlıklı kişilerde yağ kaybına etkileri ya çok küçük ya da tutarsız çıktı.',
        link: { label: 'L-karnitin işe yarar mı?', path: '/rehber/l-karnitin-yag-yakiminda-ise-yarar-mi' },
      },
    ],
    picks: [PROTEIN_PICKS],
    faqs: [
      {
        question: 'Yağ yakıcılar işe yarar mı?',
        answer:
          'Kalori açığı olmadan anlamlı bir yağ kaybı sağladıkları gösterilmedi. Etkisi olanların çoğunda asıl madde ' +
          'kafein.',
      },
      {
        question: 'Diyette protein tozu kullanmalı mıyım?',
        answer:
          'Protein hedefini yemekle tutturamıyorsan evet; düşük kalorili bir protein kaynağı ve tokluk sağlar. ' +
          'Tutturuyorsan gerek yok.',
      },
      {
        question: 'L-karnitin yağ yakar mı?',
        answer: 'Sağlıklı kişilerde yağ kaybına etkisi ya çok küçük ya da tutarsız çıktı.',
      },
    ],
    quizBranch: 'protein',
  },
  {
    slug: 'dayaniklilik',
    label: 'Dayanıklılık',
    description: 'Koşu, bisiklet, yüzme gibi uzun süreli sporlar',
    icon: 'ph-person-simple-run',
    h1: 'Koşu ve dayanıklılık sporları için hangi takviye?',
    metaTitle: 'Koşu ve Dayanıklılık İçin Hangi Takviye? | ProteinAvcısı',
    metaDescription:
      'Koşu, bisiklet ve uzun antrenmanlar için karbonhidrat, elektrolit ve kafein: ne zaman gerekir, ne zaman gerekmez. Güncel sporcu içeceği fiyatları.',
    answer:
      'Dayanıklılık sporunda performansı en çok destekleyen şey uzun eforda alınan karbonhidrat. 60–90 dakikayı ' +
      'aşan antrenmanda jel ya da sporcu içeceği, sıcakta ve çok terleyince elektrolit işe yarar. Kısa antrenmanda ' +
      'su yeterli.',
    verdicts: [
      {
        level: 'oncelikli',
        name: 'Karbonhidrat: jel ya da sporcu içeceği (uzun eforda)',
        reason:
          '60–90 dakikayı aşan antrenman ve yarışlarda performansı en çok destekleyen takviye. Daha kısa antrenmanda ' +
          'gerek yok.',
        dose: 'Saatte 30–60 g; 2,5 saati aşınca 90 g’a kadar',
        pickSectionId: 'sporcu-icecegi',
      },
      {
        level: 'oncelikli',
        name: 'Elektrolit (özellikle sodyum)',
        reason: 'Sıcakta ve uzun antrenmanda terle kaybedilen sodyumu yerine koyar; çok terleyenlerde daha önemli.',
        pickSectionId: 'sporcu-icecegi',
      },
      {
        level: 'istege-bagli',
        name: 'Kafein',
        reason: 'Dayanıklılık performansını artırdığı iyi gösterilmiş. Yarıştan önce antrenmanda dene.',
      },
      {
        level: 'istege-bagli',
        name: 'Beta-alanin',
        reason:
          'Birkaç dakika süren yüksek yoğunluklu eforlarda küçük bir fayda sağlayabilir; uzun ve sabit tempoda ' +
          'etkisi sınırlı.',
        link: { label: 'Beta-alanin dozu', path: '/hesaplama/beta-alanine-dozu' },
      },
      {
        level: 'gerek-yok',
        name: 'BCAA',
        reason: 'Dayanıklılık performansını artırdığı gösterilmedi; uzun eforda yakıt karbonhidrat.',
      },
    ],
    picks: [
      {
        id: 'sporcu-icecegi',
        category: 'enerji-jeli-sporcu-icecekleri',
        title: 'Toz sporcu içecekleri ve elektrolitler',
        note: 'Jeller adetle satıldığı için kilogram karşılaştırmasına girmiyor; hepsi kategori sayfasında.',
        allLink: { label: 'Tüm jel ve sporcu içecekleri', path: '/kategori/enerji-jeli-sporcu-icecekleri' },
      },
    ],
    faqs: [
      {
        question: 'Koşu için jel ne zaman gerekir?',
        answer: '60–90 dakikayı aşan koşularda ve yarışlarda. Daha kısa koşuda öncesinde yenen öğün ve su yeterli.',
      },
      {
        question: 'Elektrolit içeceğine ihtiyacım var mı?',
        answer: 'Uzun ve terli antrenmanlarda sodyum kaybı artar. Kısa ve serin antrenmanlarda su yeterli.',
      },
      {
        question: 'Yarışta yeni bir jel denemeli miyim?',
        answer: 'Hayır. Mideyi yormaması için jeli ve içeceği önce antrenmanda dene.',
      },
    ],
    quizBranch: 'dayaniklilik',
  },
];

export function findSupplementGoal(slug: string): SupplementGoal | undefined {
  return SUPPLEMENT_GOALS.find((goal) => goal.slug === slug);
}

// ---- Test ------------------------------------------------------------------

export interface QuizOption {
  value: string;
  label: string;
  description?: string;
  icon?: string;
}

/** Hedeften sonraki sorular; cevaplar hedef sayfasına adres parametresi olarak gidiyor. */
export interface QuizQuestion {
  /** Adres parametresinin adı. */
  key: string;
  question: string;
  options: QuizOption[];
}

export const GOAL_QUESTION = 'Asıl hedefin ne?';

export const QUIZ_FOLLOW_UPS: Record<QuizBranch, QuizQuestion[]> = {
  protein: [
    {
      key: 'protein',
      question: 'Günlük protein hedefini yemekle tutturabiliyor musun?',
      options: [
        { value: 'evet', label: 'Evet, çoğu gün' },
        { value: 'hayir', label: 'Hayır, genelde eksik kalıyor' },
        { value: 'bilmiyorum', label: 'Bilmiyorum', description: 'Sonuçta hesaplama aracına bağlantı vereceğiz' },
      ],
    },
    {
      key: 'tercih',
      question: 'Protein tozunda süt konusunda bir tercihin var mı?',
      options: [
        { value: 'farketmez', label: 'Fark etmez' },
        { value: 'laktozsuz', label: 'Laktozu düşük olsun', description: 'İzole ve hidrolize whey' },
        { value: 'bitkisel', label: 'Bitkisel olsun', description: 'Bezelye, pirinç, soya proteini' },
      ],
    },
  ],
  dayaniklilik: [
    {
      key: 'sure',
      question: 'Antrenmanların genelde ne kadar sürüyor?',
      options: [
        { value: 'kisa', label: '1 saatten kısa' },
        { value: 'orta', label: '1–2 saat' },
        { value: 'uzun', label: '2 saatten uzun' },
      ],
    },
    {
      key: 'terleme',
      question: 'Çok terler misin ya da sıcakta mı antrenman yaparsın?',
      options: [
        { value: 'evet', label: 'Evet' },
        { value: 'hayir', label: 'Hayır' },
      ],
    },
  ],
};

/** Adresteki cevaplardan yalnızca bu hedefin sorularına ait geçerli olanlar. */
export function validAnswers(goal: SupplementGoal, params: Record<string, string | undefined>): Record<string, string> {
  const answers: Record<string, string> = {};
  for (const question of QUIZ_FOLLOW_UPS[goal.quizBranch]) {
    const value = params[question.key];
    if (value && question.options.some((option) => option.value === value)) answers[question.key] = value;
  }
  return answers;
}

/** Testin süt tercihi -> protein listesinin daraltma türü (/api/value-picks). */
export function proteinTypeFor(answers: Record<string, string>): 'izole' | 'bitkisel' | null {
  if (answers['tercih'] === 'laktozsuz') return 'izole';
  if (answers['tercih'] === 'bitkisel') return 'bitkisel';
  return null;
}

export interface PersonalNote {
  text: string;
  link?: GoalLink;
}

/**
 * Cevaplara göre hedef sayfasının başındaki kişisel özet. Genel içerik
 * (hüküm listesi) değişmiyor; bu notlar yalnızca hangi kısmın bu kişi için
 * geçerli olduğunu söylüyor.
 */
export function personalNotes(goal: SupplementGoal, answers: Record<string, string>): PersonalNote[] {
  const notes: PersonalNote[] = [];

  if (goal.quizBranch === 'protein') {
    const protein = answers['protein'];
    if (protein === 'evet') {
      notes.push({
        text: {
          'kas-kazanimi': 'Protein hedefini yemekle tutturuyorsan protein tozu sana ek kas kazandırmaz; bütçeni önce kreatine ayır.',
          'kilo-alma': 'Protein hedefini tutturuyorsan eksik olan büyük olasılıkla kalori; karbonhidrat tozu ya da gainer bunu kapatır.',
          'yag-yakimi': 'Protein hedefini yemekle tutturuyorsan takviyeye pek ihtiyacın yok; sonucu kalori açığı ve antrenman belirler.',
        }[goal.slug] ?? '',
      });
    } else if (protein === 'hayir') {
      notes.push({
        text: {
          'kas-kazanimi': 'Protein hedefin eksik kalıyorsa protein tozu açığı kapatmanın en pratik yolu. Kreatinle birlikte bu ikisi yeterli.',
          'kilo-alma': 'Gainer kalori ve protein birlikte verir. Daha ucuz yol: karbonhidrat tozu ile protein tozunu karıştırmak.',
          'yag-yakimi': 'Diyette protein hedefini tutturmak tokluk ve kas kaybı için önemli; protein tozu en düşük kalorili yol.',
        }[goal.slug] ?? '',
      });
    } else if (protein === 'bilmiyorum') {
      notes.push({
        text: 'Önce günlük protein ihtiyacını hesapla; yemekle tutturuyorsan protein tozuna gerek kalmayabilir.',
        link: PROTEIN_CALCULATOR,
      });
    }

    const proteinType = proteinTypeFor(answers);
    if (proteinType === 'izole') {
      notes.push({ text: 'Protein tozu listesini izole ve hidrolize ürünlere daralttık; bunlarda laktoz çok düşük.' });
    } else if (proteinType === 'bitkisel') {
      notes.push({ text: 'Protein tozu listesini bitkisel ürünlere daralttık.' });
    }
  } else {
    const sure = answers['sure'];
    if (sure === 'kisa') {
      notes.push({
        text: '1 saatten kısa antrenmanda jel ya da sporcu içeceğine gerek yok, su yeterli. Aşağıdakiler uzun antrenman ve yarış günleri için.',
      });
    } else if (sure === 'orta') {
      notes.push({ text: '1–2 saatlik antrenmanda saatte 30–60 g karbonhidrat yaygın öneri; jel ya da sporcu içeceği bunu kolaylaştırır.' });
    } else if (sure === 'uzun') {
      notes.push({
        text: '2 saati aşan antrenmanda saatte 90 g’a kadar karbonhidrat öneriliyor; bu miktara midenin antrenmanda alıştırılması gerekir.',
      });
    }

    if (answers['terleme'] === 'evet') {
      notes.push({ text: 'Çok terliyorsan ya da sıcakta antrenman yapıyorsan elektrolit, özellikle sodyum, öncelikli.' });
    } else if (answers['terleme'] === 'hayir') {
      notes.push({ text: 'Serin havada ve az terlediğin antrenmanlarda elektrolite genelde gerek yok.' });
    }
  }

  return notes.filter((note) => note.text.length > 0);
}
