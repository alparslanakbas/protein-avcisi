import { Deal } from './deal.model';

/** Yönetmelik ölçütüyle gerçek indirimde olan ürün (backend KampanyaIndirimi). */
export interface KampanyaIndirimi {
  urun: Deal;
  oncekiEnDusuk: number;
  gercekYuzde: number;
  indirimBaslangici: string;
}

/**
 * Mağazanın üstü çizili fiyat gösterdiği ürünlerin, Fiyat Etiketi Yönetmeliği ölçütüyle (indirimden önceki 30
 * günün en düşük fiyatı) dökümü. Ölçüt backend'de tek yerde (KampanyaIndirimServisi); rapor yazısı da onu kullanıyor.
 */
export interface KampanyaOzeti {
  olcum: string;
  magazaIndirimDiyor: number;
  gercek: number;
  ucuzlamamis: number;
  kalici: number;
  veriYetersiz: number;
  urunDegismisOlabilir: number;
  gercekIndirimler: KampanyaIndirimi[];
}

/** Efsane Cuma: Kasım'ın son cuması, Black Friday ile aynı gün (UTC gece yarısı; ay 0'dan başlıyor, Kasım 10). */
export function efsaneCuma(yil: number): Date {
  const otuzKasim = new Date(Date.UTC(yil, 10, 30));
  const geri = (otuzKasim.getUTCDay() - 5 + 7) % 7;
  return new Date(Date.UTC(yil, 10, 30 - geri));
}
