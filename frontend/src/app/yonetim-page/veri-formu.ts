// Yönetim panelindeki ürün verisi düzenleyicisinin formu ve kaydedilmemiş
// girdileri. Düzenleyici dışına tıklayınca kapanıyor, yeniden açılınca da kayıtlı
// değerlerden kuruluyordu: yarım bırakılan girdi (ör. yalnızca protein ve
// karbonhidrat) sessizce kayboluyordu.

import {
  BesinSatiriFormu,
  MakroAlani,
  kayitliDigerSatirlar,
  makroDegeri,
  tabloTabaniMi,
} from './besin-satirlari';
import { YonetimUrun } from './yonetim.service';

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

function besinTablosu(json: string | null): Record<string, string> {
  if (!json) return {};
  try {
    return JSON.parse(json) as Record<string, string>;
  } catch {
    return {};
  }
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
      porsiyon: urun.servingSizeGrams?.toString() ?? makroDegeri(tablo, 'porsiyon'),
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
