// "… beri en düşük" rozeti: güncel fiyat, takip başladığından beri gördüğümüz en düşük fiyat (Deal.lowestSince,
// kural backend TakipDibi'de). Rozet başlangıç tarihini söylüyor, çünkü "tüm zamanların en düşüğü" diyemeyiz:
// takipten öncesini bilmiyoruz.

// Ayrılma hâli eki ay başına sabit (ünlü uyumu ve sert ünsüz benzeşmesi), kuralla üretmeye gerek yok.
const AYDAN = [
  "Ocak'tan",
  "Şubat'tan",
  "Mart'tan",
  "Nisan'dan",
  "Mayıs'tan",
  "Haziran'dan",
  "Temmuz'dan",
  "Ağustos'tan",
  "Eylül'den",
  "Ekim'den",
  "Kasım'dan",
  "Aralık'tan",
];

// Yılın ekini okunuşunun son kelimesi belirliyor: 2026 "yirmi altı" -> 'dan, 2030 "otuz" -> 'dan, 2031 "bir" -> 'den.
// Birler basamağı sıfırsa onlar basamağının kelimesi, o da sıfırsa "bin"/"yüz" ('den).
const BIRLER = ['', "'den", "'den", "'ten", "'ten", "'ten", "'dan", "'den", "'den", "'dan"];
const ONLAR = ["'den", "'dan", "'den", "'dan", "'tan", "'den", "'tan", "'ten", "'den", "'dan"];

const BIR_YIL_MS = 365 * 24 * 60 * 60 * 1000;

// Rozet Türkiye saatine göre: 31 Ağustos 22:30 UTC, İstanbul'da 1 Eylül.
const parcaBicimi = new Intl.DateTimeFormat('tr-TR', {
  day: 'numeric',
  month: 'numeric',
  year: 'numeric',
  timeZone: 'Europe/Istanbul',
});
const tamTarihBicimi = new Intl.DateTimeFormat('tr-TR', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'Europe/Istanbul',
});

function yildan(yil: number): string {
  const birler = yil % 10;
  return `${yil}${birler ? BIRLER[birler] : ONLAR[Math.floor(yil / 10) % 10]}`;
}

/**
 * Kart rozeti: "10 Ağustos'tan beri en düşük". Takip bir yıldan uzunsa yıl da yazılıyor ("10 Ağustos 2026'dan
 * beri"); yılsız hâli o zaman en yakın 10 Ağustos'u anlatır ve fiyatın değerini olduğundan az gösterirdi.
 */
export function takipDibiEtiketi(iso: string, simdi: Date = new Date()): string {
  const baslangic = new Date(iso);
  const parca = (tur: Intl.DateTimeFormatPartTypes) =>
    Number(parcaBicimi.formatToParts(baslangic).find((p) => p.type === tur)?.value);
  const gun = parca('day');
  const ay = parca('month');
  const tarih =
    simdi.getTime() - baslangic.getTime() >= BIR_YIL_MS
      ? `${gun} ${AYDAN[ay - 1].replace(/'.*$/, '')} ${yildan(parca('year'))}`
      : `${gun} ${AYDAN[ay - 1]}`;
  return `${tarih} beri en düşük`;
}

/** Takibin başladığı gün, ekten bağımsız kullanmak için: "10 Ağustos 2026". */
export function takipBaslangici(iso: string): string {
  return tamTarihBicimi.format(new Date(iso));
}

/** Rozetin açıklaması (title): tam tarih, ek gerektirmeyen bir cümleyle. */
export function takipDibiAciklamasi(iso: string): string {
  return `Fiyatını ${takipBaslangici(iso)} tarihinden beri takip ediyoruz; bugünkü fiyat o günden beri gördüğümüz en düşük fiyat.`;
}
