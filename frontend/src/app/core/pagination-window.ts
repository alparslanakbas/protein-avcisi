import { clampTitle } from './meta-description';

/**
 * Sayfalama çubuğunda gösterilecek sayfa numaraları.
 *
 * <b>NEDEN VAR.</b> 5 Eylül'de ölçüldü: liste sayfalarının sayfalaması
 * yalnızca `<button (click)>` idi, yani sunucudan gelen HTML'de hiçbir
 * sayfalama BAĞLANTISI yoktu. Google her listede sadece ilk 24 ürünü
 * görebiliyordu — 9 kategori sayfası toplam 216 benzersiz ürüne link
 * veriyordu, katalogda 4.713 ürün sayfası varken. Kalan ürünler yalnızca
 * sitemap'te duruyordu ve GSC onları "Keşfedildi - şu anda dizine eklenmiş
 * değil" diye işaretliyordu: sitemap keşif sağlar, ÖNCELİK sağlamaz;
 * önceliği iç bağlantılar verir.
 *
 * <b>NEDEN SADECE İLERİ/GERİ YETMİYOR.</b> Ana sayfada 4.900+ ürün ve
 * 24'lük sayfa boyutu ~205 sayfa demek. Yalnızca "sonraki" bağlantısı
 * olsaydı son sayfa 205 tıklama derinliğinde kalırdı ve hiçbir tarayıcı
 * o zinciri sonuna kadar yürümez. Numaralı pencere + ilk/son bağlantısı
 * derinliği kabaca dörtte birine indiriyor.
 *
 * `null` değerler araya giren "…" işaretini temsil ediyor.
 */
export type SayfaOgesi = number | null;

export function sayfaPenceresi(mevcut: number, toplam: number, yaricap = 2): SayfaOgesi[] {
  if (!Number.isFinite(toplam) || toplam < 1) return [];

  const gecerliToplam = Math.floor(toplam);
  const gecerliMevcut = Math.min(Math.max(Math.floor(mevcut) || 1, 1), gecerliToplam);

  // İlk ve son sayfa HER ZAMAN listede: kullanıcı için "başa/sona dön",
  // tarayıcı için ise derinliği kısaltan iki sabit uç.
  const numaralar = new Set<number>([1, gecerliToplam]);
  for (let i = gecerliMevcut - yaricap; i <= gecerliMevcut + yaricap; i++) {
    if (i >= 1 && i <= gecerliToplam) numaralar.add(i);
  }

  const sirali = [...numaralar].sort((a, b) => a - b);
  const sonuc: SayfaOgesi[] = [];
  let onceki = 0;
  for (const numara of sirali) {
    const bosluk = numara - onceki;
    if (onceki > 0 && bosluk === 2) {
      // Aradaki TEK sayfayı "…" ile gizlemek anlamsız: yer kazandırmıyor
      // ve bedavaya gelen bir iç bağlantıyı harcıyor.
      sonuc.push(numara - 1);
    } else if (onceki > 0 && bosluk > 2) {
      sonuc.push(null);
    }
    sonuc.push(numara);
    onceki = numara;
  }
  return sonuc;
}

/**
 * Başlığa sayfa numarasını ekler.
 *
 * <b>NEDEN BU KADAR UĞRAŞ.</b> Sayfa numarası, 200 küsur sayfalanmış
 * adresi birbirinden ayıran TEK şey; kaybolursa hepsi aynı başlığı taşır
 * ve Google onları kopya sayar — sayfalamayı taranabilir yapmanın amacı
 * tam da bunu önlemekti.
 *
 * `clampTitle` sığmayan başlıkta önce son " | " ayıracından sonrasını
 * atıyor, sonra kelime sınırından kırpıyor. Eki sona koymak da ayıraçtan
 * önce koymak da YETMİYOR: ilk denemede ek ayıraçtan önce kondu ve 87
 * karakterlik gerçek bir marka×kategori başlığında yine kırpıldı (test
 * yakaladı). Bu yüzden ek "eklenmiyor", kendisine YER AYRILIYOR: gerekirse
 * önce marka kuyruğu, sonra konunun sonu feda ediliyor.
 *
 * `max` varsayılanı `clampTitle` ile aynı olmalı.
 */
export function sayfaliBaslik(baslik: string, sayfa: number, max = 65): string {
  if (sayfa <= 1) return baslik;

  const ek = ` – Sayfa ${sayfa}`;
  const ayirac = baslik.lastIndexOf(' | ');
  const konu = ayirac < 0 ? baslik : baslik.slice(0, ayirac);
  const kuyruk = ayirac < 0 ? '' : baslik.slice(ayirac);

  // 1. tercih: her şey sığıyor.
  if (konu.length + ek.length + kuyruk.length <= max) return `${konu}${ek}${kuyruk}`;

  // 2. tercih: marka kuyruğunu bırak. Marka zaten her başlıkta aynı,
  // ayırt edici bilgi taşımıyor; sayfa numarası taşıyor.
  if (konu.length + ek.length <= max) return `${konu}${ek}`;

  // 3. tercih: konuyu kırp. clampTitle burada ayıraçsız bir metin gördüğü
  // için doğrudan kelime sınırından kırpıyor.
  return `${clampTitle(konu, max - ek.length)}${ek}`;
}

/**
 * Adresteki `page` parametresini okur.
 *
 * Bozuk/eksik değer 1'e düşüyor — sayfa numarası adresten geldiği için
 * ziyaretçi elle "?page=abc" yazabilir ve liste boş kalmamalı.
 */
export function adrestenSayfa(ham: string | null): number {
  const deger = Number(ham);
  return Number.isFinite(deger) && deger >= 1 ? Math.floor(deger) : 1;
}
