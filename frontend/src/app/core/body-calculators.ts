export type BodyField = 'gender' | 'age' | 'height' | 'weight' | 'activity';

export interface BodyCalcInput {
  gender: 'male' | 'female';
  age: number | null;
  height: number | null;
  weight: number | null;
  activityId: string;
}

export interface BodyCalcResult {
  // Büyük puntoyla gösterilen ana sonuç
  primaryValue: string;
  primaryUnit: string;
  primaryLabel: string;
  // Ana sonucun altındaki ek satırlar (hedefe göre kalori, BMI sınıfı vb.)
  details: { label: string; value: string }[];
  note?: string;
}

export interface ActivityOption {
  id: string;
  label: string;
  description: string;
  factor: number;
}

// Aktivite katsayıları — Mifflin-St Jeor ile birlikte yaygın kullanılan
// standart çarpanlar.
export const ACTIVITY_OPTIONS: ActivityOption[] = [
  { id: 'sedentary', label: 'Hareketsiz', description: 'Masa başı, spor yok', factor: 1.2 },
  { id: 'light', label: 'Hafif aktif', description: 'Haftada 1-3 gün', factor: 1.375 },
  { id: 'moderate', label: 'Orta aktif', description: 'Haftada 3-5 gün', factor: 1.55 },
  { id: 'active', label: 'Çok aktif', description: 'Haftada 6-7 gün', factor: 1.725 },
  { id: 'athlete', label: 'Sporcu', description: 'Günde iki antrenman / ağır fiziksel iş', factor: 1.9 },
];

export interface BodyCalculator {
  slug: string;
  name: string;
  title: string;
  description: string;
  h1: string;
  intro: string;
  fields: BodyField[];
  disclaimer: string;
  calculate: (input: BodyCalcInput) => BodyCalcResult | null;
}

function activityFactor(id: string): number {
  return ACTIVITY_OPTIONS.find((a) => a.id === id)?.factor ?? 1.375;
}

export const BODY_CALCULATORS: BodyCalculator[] = [
  {
    slug: 'kalori-ihtiyaci',
    name: 'Günlük Kalori İhtiyacı',
    title: 'Günlük Kalori İhtiyacı Hesaplama (TDEE) | ProteinAvcısı',
    description:
      'Boy, kilo, yaş ve aktivite seviyene göre günlük kalori ihtiyacını (TDEE) hesapla. Kilo alma ve verme hedefleri için gereken kaloriyi gör.',
    h1: 'Günlük Kalori İhtiyacı Hesaplama',
    intro:
      'Mifflin-St Jeor denklemiyle bazal metabolizma hızını ve aktivite seviyene göre günlük toplam kalori ihtiyacını hesaplar. Sonuç bir tahmindir — gerçek ihtiyacın kişiden kişiye değişir.',
    fields: ['gender', 'age', 'height', 'weight', 'activity'],
    disclaimer:
      'Bu hesaplama yaygın kullanılan bir denkleme dayanan bir tahmindir, kişiye özel beslenme planı değildir. Gerçek ihtiyaç kas oranı, hormonal durum ve sağlık geçmişine göre değişir; kesin bir plan için bir diyetisyene danışmak gerekir.',
    calculate: (input) => {
      const { gender, age, height, weight } = input;
      if (!age || !height || !weight) return null;
      if (age < 10 || age > 100 || height < 100 || height > 250 || weight < 30 || weight > 300) return null;

      // Mifflin-St Jeor: bazal metabolizma hızı (BMR)
      const bmr = 10 * weight + 6.25 * height - 5 * age + (gender === 'male' ? 5 : -161);
      const tdee = Math.round(bmr * activityFactor(input.activityId));

      return {
        primaryValue: tdee.toLocaleString('tr-TR'),
        primaryUnit: 'kcal / gün',
        primaryLabel: 'Kilonu korumak için',
        details: [
          { label: 'Bazal metabolizma (BMR)', value: `${Math.round(bmr).toLocaleString('tr-TR')} kcal` },
          { label: 'Kilo vermek için (~500 kcal açık)', value: `${(tdee - 500).toLocaleString('tr-TR')} kcal` },
          { label: 'Kilo almak için (~500 kcal fazla)', value: `${(tdee + 500).toLocaleString('tr-TR')} kcal` },
        ],
        note:
          'Haftada yaklaşık 0,5 kg değişim için günde 500 kcal açık/fazla yaygın bir başlangıç noktasıdır. Çok büyük açıklar kas kaybı riskini artırır.',
      };
    },
  },
  {
    slug: 'vucut-kitle-indeksi',
    name: 'Vücut Kitle İndeksi (BMI)',
    title: 'Vücut Kitle İndeksi (BMI) Hesaplama | ProteinAvcısı',
    description:
      'Boy ve kilona göre vücut kitle indeksini (BMI) hesapla. Sporcularda BMI\'nin neden yanıltıcı olabileceğini öğren.',
    h1: 'Vücut Kitle İndeksi (BMI) Hesaplama',
    intro:
      'BMI, boy ve kiloya dayanan basit bir orandır. Genel nüfus için kaba bir gösterge sunar ama kas ile yağı ayırt edemez — bu yüzden düzenli antrenman yapanlarda yanıltıcı sonuç verebilir.',
    fields: ['height', 'weight'],
    disclaimer:
      'BMI kas kütlesini, yağ oranını ve yağın vücuttaki dağılımını hesaba katmaz. Kas kütlesi yüksek bir sporcu "fazla kilolu" aralığında çıkabilir; bu tek başına bir sağlık sorunu göstergesi değildir. Sağlık değerlendirmesi için hekime danışmak gerekir.',
    calculate: (input) => {
      const { height, weight } = input;
      if (!height || !weight) return null;
      if (height < 100 || height > 250 || weight < 30 || weight > 300) return null;

      const meters = height / 100;
      const bmi = weight / (meters * meters);

      // Dünya Sağlık Örgütü'nün yetişkinler için kullandığı standart aralıklar.
      const category =
        bmi < 18.5 ? 'Zayıf' : bmi < 25 ? 'Normal' : bmi < 30 ? 'Fazla kilolu' : 'Obez';

      const idealMin = (18.5 * meters * meters).toFixed(1);
      const idealMax = (24.9 * meters * meters).toFixed(1);

      return {
        primaryValue: bmi.toFixed(1).replace('.', ','),
        primaryUnit: '',
        primaryLabel: category,
        details: [
          { label: 'Normal aralık (BMI 18,5-24,9)', value: `${idealMin.replace('.', ',')} - ${idealMax.replace('.', ',')} kg` },
        ],
        note:
          'Kas kütlesi yüksek kişilerde BMI olduğundan yüksek çıkar. Vücut kompozisyonunu daha doğru değerlendirmek için yağ oranı ölçümü daha bilgilendiricidir.',
      };
    },
  },
  {
    slug: 'gunluk-su-ihtiyaci',
    name: 'Günlük Su İhtiyacı',
    title: 'Günlük Su İhtiyacı Hesaplama | ProteinAvcısı',
    description: 'Kilona ve aktivite seviyene göre günlük su ihtiyacını hesapla.',
    h1: 'Günlük Su İhtiyacı Hesaplama',
    intro:
      'Günlük su ihtiyacı kiloya ve aktivite düzeyine göre değişir. Aşağıdaki hesap yaygın kullanılan bir aralığa dayanır; sıcak havada veya yoğun terlemede ihtiyaç artar.',
    fields: ['weight', 'activity'],
    disclaimer:
      'Bu hesap genel bir referanstır. Böbrek veya kalp rahatsızlığı gibi sıvı alımının kısıtlanması gereken durumlarda mutlaka hekime danışılmalıdır.',
    calculate: (input) => {
      const { weight } = input;
      if (!weight || weight < 30 || weight > 300) return null;

      // Yaygın referans: 30-35 ml/kg. Aktivite arttıkça terlemeyle kaybedilen
      // sıvı için üstüne ekleniyor.
      const factor = activityFactor(input.activityId);
      const extraMl = Math.round((factor - 1.2) * 1000);
      const minMl = weight * 30 + extraMl;
      const maxMl = weight * 35 + extraMl;

      return {
        primaryValue: `${(minMl / 1000).toFixed(1).replace('.', ',')}–${(maxMl / 1000).toFixed(1).replace('.', ',')}`,
        primaryUnit: 'litre / gün',
        primaryLabel: 'Günlük su ihtiyacın',
        details: [
          { label: 'Bardak olarak (200 ml)', value: `${Math.round(minMl / 200)}-${Math.round(maxMl / 200)} bardak` },
        ],
        note:
          'Kreatin kullanıyorsan su tüketimine ayrıca dikkat etmen yaygın bir öneridir. Çay, kahve ve gıdalardan alınan sıvı da toplam alıma katkı sağlar.',
      };
    },
  },
];

export function findBodyCalculator(slug: string): BodyCalculator | undefined {
  return BODY_CALCULATORS.find((c) => c.slug === slug);
}
