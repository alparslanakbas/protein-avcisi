import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Yönetim panelinin API istemcisi.
 *
 * <b>API_BASE_URL BİLEREK KULLANILMIYOR.</b> Sitenin geri kalanı
 * `api.proteinavcisi.com.tr` adresine gidiyor; panel ise KENDİ ORIGIN'i
 * üzerinden `/yonetim/api/...` yolunu kullanıyor ve Caddy bunu backend'e
 * taşıyor. Sebep: Cloudflare Access bir alan adı + yol koruyor. Panel
 * verisini `api.` alt alan adından çekseydik o istekler Access'in DIŞINDA
 * kalırdı — sayfa korunur, verinin kendisi korunmazdı. Ayrıca aynı origin
 * olduğu için oturum çerezi kendiliğinden gidiyor ve CORS'a hiç gerek yok
 * (CORS bu projede bilerek kapalı).
 */
@Injectable({ providedIn: 'root' })
export class YonetimService {
  private readonly http = inject(HttpClient);
  private readonly base = '/yonetim/api';

  girisYap(key: string): Observable<{ ok: boolean }> {
    return this.http.post<{ ok: boolean }>(`${this.base}/session`, { key });
  }

  cikisYap(): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(`${this.base}/session`);
  }

  durum(): Observable<Durum> {
    return this.http.get<Durum>(`${this.base}/durum`);
  }

  olaylar(gun: number, tur: string | null): Observable<OlayYaniti> {
    let yol = `${this.base}/security-events?days=${gun}`;
    if (tur) yol += `&kind=${encodeURIComponent(tur)}`;
    return this.http.get<OlayYaniti>(yol);
  }

  /**
   * Başarısız yönetim işlemleri. Olay ucundan AYRI, çünkü iki liste farklı
   * sorulara cevap veriyor: biri "dışarıdan kim ne deniyor", diğeri "benim
   * işlemim neden olmadı".
   */
  yonetimHatalari(gun: number): Observable<YonetimHatasi[]> {
    return this.http.get<YonetimHatasi[]>(`${this.base}/admin-failures?days=${gun}`);
  }

  kuponlar(): Observable<Kupon[]> {
    return this.http.get<Kupon[]>(`${this.base}/coupons`);
  }

  kuponEkle(kupon: KuponEkleme): Observable<unknown> {
    return this.http.post(`${this.base}/coupons`, kupon);
  }

  kuponGuncelle(id: number, kupon: KuponGuncelleme): Observable<unknown> {
    return this.http.put(`${this.base}/coupons/${id}`, kupon);
  }

  markalar(): Observable<YonetimMarka[]> {
    return this.http.get<YonetimMarka[]>(`${this.base}/markalar`);
  }

  markaDurumuGuncelle(id: number, isActive: boolean): Observable<YonetimDurumGuncelleme> {
    return this.http.put<YonetimDurumGuncelleme>(`${this.base}/markalar/${id}`, { isActive });
  }

  /**
   * Bir sayfa ürün; `toplam` yalnızca bu sayfayı değil bütün eşleşmeleri sayıyor.
   *
   * Parametreler TEK NESNEDE: filtreler konumsal argümandı ve araya eklenen her yeni
   * filtre hem çağrıları hem testlerin sayfa okumasını bir sıra kaydırıyordu ("elle
   * girilmiş" eklenirken üç testi birden kırdı). Adıyla gelen alan mevcut çağrıların
   * hiçbirine dokunmuyor. Filtre adları backend parametre adlarıyla birebir aynı, bu
   * yüzden liste üzerinden dönülüyor: yeni bir filtre tek satır.
   */
  urunler(secenekler: UrunAramaSecenekleri = {}): Observable<YonetimUrunSayfasi> {
    const { ara = '', sayfa = 1 } = secenekler;
    const params = new URLSearchParams();
    if (ara.trim()) params.set('ara', ara.trim());
    for (const filtre of URUN_FILTRELERI) {
      if (secenekler[filtre]) params.set(filtre, 'true');
    }
    if (sayfa > 1) params.set('sayfa', String(sayfa));
    const sorgu = params.toString();
    return this.http.get<YonetimUrunSayfasi>(`${this.base}/urunler${sorgu ? `?${sorgu}` : ''}`);
  }

  urunDurumuGuncelle(id: number, isActive: boolean): Observable<YonetimDurumGuncelleme> {
    return this.http.put<YonetimDurumGuncelleme>(`${this.base}/urunler/${id}`, { isActive });
  }

  /** Kategori kodu ya da otomatik kategoriye dönmek için null. Sayfanın bütün boyutlarına uygulanıyor. */
  kategoriAyarla(id: number, kategori: string | null): Observable<ElleDuzenlemeYaniti> {
    return this.http.put<ElleDuzenlemeYaniti>(`${this.base}/urunler/${id}/kategori`, { kategori });
  }

  /** Markanın etiketinden besin değeri; kalori makrolarla tutmazsa sebebiyle reddediliyor. */
  besinAyarla(id: number, besin: ElleBesin): Observable<ElleDuzenlemeYaniti> {
    return this.http.put<ElleDuzenlemeYaniti>(`${this.base}/urunler/${id}/besin`, besin);
  }

  besinTemizle(id: number): Observable<ElleDuzenlemeYaniti> {
    return this.http.delete<ElleDuzenlemeYaniti>(`${this.base}/urunler/${id}/besin`);
  }

  /**
   * "Kaynakta tablo yok" kararı: tabloyu siler ve ürünü otomatik kaynaklara
   * KAPATIR. Silmeden farkı bu; silmek ürünü yeniden bakılsın diye geri bırakıyor.
   */
  besinYok(id: number): Observable<ElleDuzenlemeYaniti> {
    return this.http.post<ElleDuzenlemeYaniti>(`${this.base}/urunler/${id}/besin-yok`, {});
  }

  aboneler(): Observable<AbonelerYaniti> {
    return this.http.get<AbonelerYaniti>(`${this.base}/aboneler`);
  }

  abonePasifeAl(id: number): Observable<unknown> {
    return this.http.post(`${this.base}/aboneler/${id}/pasife-al`, {});
  }

  /**
   * Bilerek "aktife al" YOK: çift onayda aboneliği yalnızca kişi kendisi
   * açabilir. Panel yalnızca onay e-postasını yeniden gönderebilir.
   */
  aboneOnayGonder(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/aboneler/${id}/onay-gonder`, {});
  }
}

export type AboneDurumu = 'aktif' | 'bekliyor' | 'ayrildi';

export interface Abone {
  id: number;
  email: string;
  durum: AboneDurumu;
  subscribedAt: string;
  confirmedAt: string | null;
  unsubscribedAt: string | null;
  lastConfirmationEmailSentAt: string | null;
  lastDigestSentAt: string | null;
  takipSayisi: number;
  favoriSayisi: number;
}

export interface AbonelerYaniti {
  aboneler: Abone[];
  ozet: { toplam: number; aktif: number; bekleyen: number; ayrilan: number };
}

export interface Durum {
  urun: { toplam: number; besinli: number; markaSayisi: number };
  tiklamaToplam: number;
  besin: { sonTur: string | null; siradakiTur: string | null };
  abone: { onayli: number; bekleyen: number };
  kaynaklar: { kaynak: string; sonTarama: string | null }[];
  sonGunOlaylari: { kind: string; count: number }[];
}

export interface Olay {
  id: number;
  occurredAt: string;
  ip: string;
  kind: string;
  method: string;
  path: string;
  statusCode: number;
  userAgent: string | null;
  country: string | null;
}

export interface OlayYaniti {
  events: Olay[];
  summary: { kind: string; count: number }[];
  topIps: { ip: string; count: number; firstSeen: string; lastSeen: string }[];
}

export interface YonetimHatasi {
  id: number;
  occurredAt: string;
  method: string;
  path: string;
  statusCode: number;
  /** Ucun kendi cevabı ya da istisnanın metni; uç gövdesiz döndüyse null. */
  reason: string | null;
  ip: string | null;
}

export interface Kupon {
  id: number;
  code: string | null;
  description: string;
  brandId: number | null;
  brandName: string | null;
  seller: string | null;
  validUntil: string | null;
  lastVerifiedAt: string;
  isActive: boolean;
}

// Ekleme ve guncelleme SEKILLERI FARKLI, backend'in kayitlariyla birebir:
// ekleme markayi ADIYLA aliyor (id ile degil) ve marka/satici ikilisinden
// YALNIZCA BIRI dolu olabiliyor (DB'de check constraint ile de korunuyor);
// guncelleme ise hedefi hic degistirmiyor, yalnizca icerigi ve durumu.
export interface KuponEkleme {
  brandName: string | null;
  seller: string | null;
  code: string | null;
  description: string;
  validUntil: string | null;
}

export interface KuponGuncelleme {
  code: string | null;
  description: string;
  validUntil: string | null;
  /**
   * Gönderilmezse kuponun yayın durumu DEĞİŞMİYOR — backend yalnızca
   * gelen alanları güncelliyor. Düzenleme sırasında bilerek atlanıyor:
   * metni düzeltmek, pasif bir kuponu yanlışlıkla yayına almamalı.
   */
  isActive?: boolean;
  /**
   * Bitiş tarihini SİLMEK için. `validUntil: null` göndermek yetmiyor:
   * backend'de null "bu alana dokunma" anlamına geliyor, "boşalt" değil.
   */
  validUntilTemizle?: boolean;
}

export interface YonetimMarka {
  id: number;
  name: string;
  isActive: boolean;
  urunSayisi: number;
  gizliUrun: number;
}

export interface YonetimUrun {
  id: number;
  name: string;
  marka: string;
  seller: string | null;
  isActive: boolean;
  latestPrice: number | null;
  category: string | null;
  categoryIsManual: boolean;
  /** Tablo, ör. {"Enerji":"119 kcal","Protein":"24.1 g"}; yoksa null. */
  nutritionJson: string | null;
  nutritionIsManual: boolean;
  servingSizeGrams: number | null;
}

export interface YonetimUrunSayfasi {
  urunler: YonetimUrun[];
  toplam: number;
  sayfa: number;
  sayfaBoyutu: number;
}

/** Ürün listesinin boolean filtreleri; adlar backend parametre adlarıyla aynı. */
export const URUN_FILTRELERI = [
  'yalnizGizli',
  'eksikBesin',
  'kategorisiz',
  'elleGirilmeli',
  'elleGirilmis',
] as const;

export type UrunFiltresi = (typeof URUN_FILTRELERI)[number];

export type UrunAramaSecenekleri = Partial<Record<UrunFiltresi, boolean>> & {
  ara?: string;
  sayfa?: number;
};

export interface ElleBesin {
  porsiyonGram: number | null;
  kalori: number | null;
  proteinGram: number | null;
  karbonhidratGram: number | null;
  yagGram: number | null;
  lifGram: number | null;
  /** Makroların dışındaki satırlar, etikette yazdığı gibi ("Kafein", 200, "mg"). */
  digerSatirlar: { ad: string; miktar: number | null; birim: string }[];
  /**
   * Etiketin kendisi kalori kontrolüyle çelişiyorsa (BCAA'larda sık: 10 g protein,
   * 0 kcal) değerleri olduğu gibi kabul ettiriyor. Kutu yalnızca o ret geldiğinde
   * görünüyor; imkansız değerler bununla da geçmiyor.
   */
  etiketBoyleYaziyor?: boolean;
}

export interface ElleDuzenlemeYaniti {
  guncellenenSatir: number;
}

export interface YonetimDurumGuncelleme {
  id: number;
  name: string;
  isActive: boolean;
}
