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

  urunler(ara: string, yalnizGizli: boolean): Observable<YonetimUrun[]> {
    const params = new URLSearchParams();
    if (ara.trim()) params.set('ara', ara.trim());
    if (yalnizGizli) params.set('yalnizGizli', 'true');
    const sorgu = params.toString();
    return this.http.get<YonetimUrun[]>(`${this.base}/urunler${sorgu ? `?${sorgu}` : ''}`);
  }

  urunDurumuGuncelle(id: number, isActive: boolean): Observable<YonetimDurumGuncelleme> {
    return this.http.put<YonetimDurumGuncelleme>(`${this.base}/urunler/${id}`, { isActive });
  }
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
}

export interface YonetimDurumGuncelleme {
  id: number;
  name: string;
  isActive: boolean;
}
