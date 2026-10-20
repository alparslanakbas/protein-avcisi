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
  gercekIndirimler: KampanyaIndirimi[];
}

/** Efsane Cuma: Kasım'ın son cuması, Black Friday ile aynı gün (UTC gece yarısı; ay 0'dan başlıyor, Kasım 10). */
export function efsaneCuma(yil: number): Date {
  const otuzKasim = new Date(Date.UTC(yil, 10, 30));
  const geri = (otuzKasim.getUTCDay() - 5 + 7) % 7;
  return new Date(Date.UTC(yil, 10, 30 - geri));
}

const GUN_MS = 24 * 60 * 60 * 1000;

/**
 * Ana sayfadaki sezon şeridinin geri sayımı; sezon dışında null (şerit hiç görünmez). Sezon 1 Ekim'den Efsane
 * Cuma'dan sonraki pazartesiye kadar (Efsane Cuma indirimleri hafta sonu sürüyor). Günler Türkiye saatiyle
 * sayılıyor: UTC'ye göre sayılsa gece 00:00-03:00 arası bir gün geride kalırdı.
 */
export function sezonGeriSayimi(simdi: Date): string[] | null {
  const [yil, ay, gun] = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' })
    .format(simdi)
    .split('-')
    .map(Number);
  const bugun = Date.UTC(yil, ay - 1, gun);
  const kalan = (hedef: number) => Math.round((hedef - bugun) / GUN_MS);
  const onBir = kalan(Date.UTC(yil, 10, 11));
  const efsane = kalan(efsaneCuma(yil).getTime());

  if (bugun < Date.UTC(yil, 9, 1) || efsane < -3) return null;
  if (efsane < 0) return ['Efsane Cuma indirimleri sürüyor'];
  if (efsane === 0) return ['Efsane Cuma bugün'];
  const efsaneParca = `Efsane Cuma'ya ${efsane} gün`;
  if (onBir > 0) return [`11.11'e ${onBir} gün`, efsaneParca];
  if (onBir === 0) return ['11.11 bugün', efsaneParca];
  return [efsaneParca];
}
