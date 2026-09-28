// Yönetim panelindeki ürün verisi düzenleyicisinin formu ve kaydedilmemiş
// girdileri. Düzenleyici dışına tıklayınca kapanıyor, yeniden açılınca da kayıtlı
// değerlerden kuruluyordu: yarım bırakılan girdi (ör. yalnızca protein ve
// karbonhidrat) sessizce kayboluyordu.

import {
  BesinSatiriFormu,
  MakroAlani,
  katla,
  kayitliDigerSatirlar,
  makroAlani,
  makroDegeri,
  tabloTabaniMi,
} from './besin-satirlari';
import { ElleBesin, YonetimUrun } from './yonetim.service';

/** Düzenleyicinin çalışma kopyası; girdiler kaydedilene kadar metin olarak tutuluyor. */
export interface UrunVeriFormu extends Record<MakroAlani, string> {
  urun: YonetimUrun;
  /** '' = otomatik. */
  kategori: string;
  /** Paketten çıkan porsiyon sayısı; boşsa site paket boyutundan hesaplıyor. */
  paketPorsiyon: string;
  /** Porsiyon kutusu porsiyon değil tablonun gram tabanı ("100 g başına"). */
  porsiyonBeyanYok: boolean;
  /** Makroların dışındaki etiket satırları (kreatin, vitamin, kafein…). */
  digerSatirlar: BesinSatiriFormu[];
}

/** Kategori ile besin tablosu ayrı düğmelerle, ayrı isteklerle kaydediliyor. */
export type KaydedilenVeri = 'kategori' | 'besin';

/** Porsiyonun adetle sayılabildiği birimler: backend'in listesi, aynı yazımla. */
export const PORSIYON_BIRIMLERI = ['kapsül', 'tablet', 'softjel', 'saşe'] as const;

// Etiketlerin sayılı porsiyon için yazdığı kelimeler ("1 kapsül", "1 saşe x 7,5 gr").
const PORSIYON_KELIMELERI: Record<string, (typeof PORSIYON_BIRIMLERI)[number]> = {
  kap: 'kapsül',
  kaps: 'kapsül',
  kapsul: 'kapsül',
  kapsül: 'kapsül',
  kapsüller: 'kapsül',
  tab: 'tablet',
  tablet: 'tablet',
  tabletler: 'tablet',
  softjel: 'softjel',
  softgel: 'softjel',
  saşe: 'saşe',
  sase: 'saşe',
  sachet: 'saşe',
};
// "7,5 gr" de "7.5 g" de yazılıyor (28 Eylül: WheyProof'ta "7,5g" reddedilmişti).
const GRAM = String.raw`\s*(?:g|gr|gram)`;
const SAYI = String.raw`(\d+(?:[.,]\d+)?)`;
const PARANTEZ_GRAM = String.raw`(?:\(\s*${SAYI}${GRAM}\s*\))?`;
const GRAM_PORSIYON = new RegExp(`^${SAYI}(?:${GRAM})?$`, 'i');
const ML_PORSIYON = new RegExp(String.raw`^${SAYI}\s*ml\s*${PARANTEZ_GRAM}$`, 'i');
const SAYILI_PORSIYON = new RegExp(String.raw`^(\d+)\s*([a-zçğıöşü]+)\s*${PARANTEZ_GRAM}$`, 'i');

type Porsiyon = Pick<ElleBesin, 'porsiyonGram' | 'porsiyonAdedi' | 'porsiyonBirimi' | 'porsiyonMl'>;
const PORSIYON_YOK: Porsiyon = {
  porsiyonGram: null,
  porsiyonAdedi: null,
  porsiyonBirimi: null,
  porsiyonMl: null,
};

// Kutu artık metin: "30,5" de yazılabiliyor.
const ondalik = (metin: string) => Number(metin.replace(',', '.'));

/**
 * Porsiyon kutusu: gram ("30"), sıvıda ml ("20 ml"), ya da kapsül ve tablet
 * etiketlerinin yazdığı gibi adet ("1 kapsül", "2 tablet (1,2 g)"); bunların gramı
 * etikette nadiren yazıyor. Hiçbiri değilse null.
 */
export function porsiyonCoz(metin: string): Porsiyon | null {
  const m = metin.trim();
  if (m === '') return { ...PORSIYON_YOK };
  const gram = m.match(GRAM_PORSIYON);
  if (gram) return { ...PORSIYON_YOK, porsiyonGram: ondalik(gram[1]) };
  const sivi = m.match(ML_PORSIYON);
  if (sivi) {
    return {
      ...PORSIYON_YOK,
      porsiyonGram: sivi[2] ? ondalik(sivi[2]) : null,
      porsiyonMl: ondalik(sivi[1]),
    };
  }
  const sayili = m.match(SAYILI_PORSIYON);
  const birim = sayili && PORSIYON_KELIMELERI[katla(sayili[2])];
  if (!sayili || !birim) return null;
  return {
    ...PORSIYON_YOK,
    porsiyonGram: sayili[3] ? ondalik(sayili[3]) : null,
    porsiyonAdedi: Number(sayili[1]),
    porsiyonBirimi: birim,
  };
}

function besinTablosu(json: string | null): Record<string, string> {
  if (!json) return {};
  try {
    return JSON.parse(json) as Record<string, string>;
  } catch {
    return {};
  }
}

// Adetli ya da sıvı porsiyon ("2 kapsül", "20 ml") ve ml tabanı ("100 ml başına")
// yalnızca tabloda; gram ayrıca sütunda da duruyor. ml tabanı "100" diye geri
// okunsaydı ikinci kayıt onu sessizce "100 g başına"ya çevirirdi.
function kayitliPorsiyon(urun: YonetimUrun, tablo: Record<string, string>): string {
  const satir = Object.entries(tablo).find(([ad]) => makroAlani(ad) === 'porsiyon')?.[1] ?? '';
  const metin = satir.replace(/\s*başına\s*$/i, '').trim();
  const cozulen = porsiyonCoz(metin);
  if (cozulen?.porsiyonBirimi || cozulen?.porsiyonMl) return metin;
  return urun.servingSizeGrams?.toString() ?? makroDegeri(tablo, 'porsiyon');
}

/**
 * Ürünün kayıtlı verisinden kurulan form. Otomatik bir okuma bu düzenleyicinin
 * yazamayacağı satırlar ("%10") taşıyabiliyor; onlar adlarıyla dönüyor.
 */
export function kayitliVeriFormu(urun: YonetimUrun): {
  form: UrunVeriFormu;
  atlananlar: string[];
} {
  const tablo = besinTablosu(urun.nutritionJson);
  const diger = kayitliDigerSatirlar(tablo);
  return {
    form: {
      urun,
      kategori: urun.categoryIsManual ? (urun.category ?? '') : '',
      porsiyon: kayitliPorsiyon(urun, tablo),
      paketPorsiyon: urun.servingsPerPackage?.toString() ?? '',
      porsiyonBeyanYok: tabloTabaniMi(tablo),
      enerji: makroDegeri(tablo, 'enerji'),
      protein: makroDegeri(tablo, 'protein'),
      karbonhidrat: makroDegeri(tablo, 'karbonhidrat'),
      yag: makroDegeri(tablo, 'yag'),
      lif: makroDegeri(tablo, 'lif'),
      digerSatirlar: diger.satirlar,
    },
    atlananlar: diger.atlananlar,
  };
}

/** Formdan kurulan kayıt isteği, ya da istek yerine kişiye söylenecek şey. */
export function besinIstegi(
  form: UrunVeriFormu,
  etiketBoyleYaziyor: boolean,
): { govde: ElleBesin } | { hata: string } {
  const porsiyon = porsiyonCoz(form.porsiyon);
  // Yazılanı söylüyor ve örnek veriyor; çıplak bir örnek sayı ("30") kutunun
  // istediği en az değer gibi okunmuştu.
  if (!porsiyon) {
    return {
      hata: `Porsiyon "${form.porsiyon.trim()}" okunamadı: gram ("7,5 g"), ml ("20 ml") ya da adet ("1 kapsül", "1 saşe (7,5 g)") yaz.`,
    };
  }

  // Boş alan 0 değil null kalıyor: boş lif "girilmedi" demek; dört temel değerin
  // birlikte girilmesini backend kendisi istiyor.
  const sayi = (metin: string) => (metin.trim() === '' ? null : Number(metin));
  const govde = {
    ...porsiyon,
    kalori: sayi(form.enerji),
    proteinGram: sayi(form.protein),
    karbonhidratGram: sayi(form.karbonhidrat),
    yagGram: sayi(form.yag),
    lifGram: sayi(form.lif),
    etiketBoyleYaziyor,
    paketPorsiyonSayisi: sayi(form.paketPorsiyon),
    porsiyonBeyanYok: form.porsiyonBeyanYok,
  };
  // Miktarı boş bırakılan şablon satırı etikette yok demek: hata olarak
  // gönderilmiyor, atlanıyor. Adı ve miktarı olan satır backend kontrolüne gidiyor.
  const digerSatirlar = form.digerSatirlar
    .filter((s) => s.miktar.trim() !== '')
    .map((s) => ({ ad: s.ad, miktar: sayi(s.miktar), birim: s.birim }));
  if (
    Object.values(govde).some((v) => typeof v === 'number' && Number.isNaN(v)) ||
    digerSatirlar.some((s) => s.miktar !== null && Number.isNaN(s.miktar))
  ) {
    return { hata: 'Yalnızca sayı gir.' };
  }
  return { govde: { ...govde, digerSatirlar } };
}

// Ürün kaydı dışındaki her alan. Anahtarlar formun kendisinden okunuyor: forma
// eklenen yeni bir alan burada unutulup taslaktan sessizce düşmesin.
function ayniGirdiler(a: UrunVeriFormu, b: UrunVeriFormu): boolean {
  return (Object.keys(a) as (keyof UrunVeriFormu)[]).every((alan) => {
    if (alan === 'urun') return true;
    if (alan !== 'digerSatirlar') return a[alan] === b[alan];
    const x = a.digerSatirlar;
    const y = b.digerSatirlar;
    return (
      x.length === y.length &&
      x.every((s, i) => s.ad === y[i].ad && s.miktar === y[i].miktar && s.birim === y[i].birim)
    );
  });
}

/**
 * Kaydedilmemiş girdiler, ürün başına. Düzenleyici hangi yoldan kapanırsa
 * kapansın (dışına tıklama, Esc, Kapat) kayıtlı hâlden farklı girdiler saklanıyor
 * ve ürün yeniden açılınca geri geliyor.
 *
 * Yalnızca bellekte; sayfa yenilenince gidiyor. Bilerek: tarayıcı deposundaki bir
 * taslak, ürünün kayıtlı hâli değiştiğinden habersiz günlerce kalır ve açılınca
 * yeni değerlerin üstüne gelirdi.
 */
export class VeriTaslaklari {
  private readonly taslaklar = new Map<number, UrunVeriFormu>();
  /** Açık formun kayıtlı hâli: kapanırken taslak olup olmadığına buna bakılıyor. */
  private kayitli: UrunVeriFormu | null = null;

  /** Düzenleyici açılırken: kayıtlı veriden kurulan form, ürünün taslağı varsa o. */
  ac(urun: YonetimUrun): { form: UrunVeriFormu; taslak: boolean; atlananlar: string[] } {
    const { form, atlananlar } = kayitliVeriFormu(urun);
    this.kayitli = form;
    const taslak = this.taslaklar.get(urun.id);
    if (!taslak || ayniGirdiler(taslak, form)) {
      this.taslaklar.delete(urun.id);
      return { form, taslak: false, atlananlar };
    }
    return { form: { ...taslak, urun }, taslak: true, atlananlar };
  }

  /**
   * Kayıt başarılı olunca gönderilen alanlar kayıtlı hâl sayılıyor. Yalnızca
   * gönderilenler: besin girilip kategori kaydedildiyse besin hâlâ taslak.
   */
  kaydedildi(gonderilen: UrunVeriFormu, neyi: KaydedilenVeri): void {
    const kayitli = this.kayitli;
    if (!kayitli || kayitli.urun.id !== gonderilen.urun.id) return;
    this.kayitli =
      neyi === 'kategori'
        ? { ...kayitli, kategori: gonderilen.kategori }
        : { ...gonderilen, kategori: kayitli.kategori };
  }

  /** Düzenleyici kapanırken: kayıtlı hâlden farklıysa taslak olarak saklanıyor. */
  kapat(form: UrunVeriFormu): void {
    if (this.kayitli && ayniGirdiler(form, this.kayitli)) this.taslaklar.delete(form.urun.id);
    else this.taslaklar.set(form.urun.id, form);
    this.kayitli = null;
  }
}
