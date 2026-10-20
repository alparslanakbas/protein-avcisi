// AdSense betiği yalnız editoryal sayfalarda.
//
// Google'ın ret sayfası (support.google.com/adsense/answer/81904): "Otomatik olarak
// oluşturulmuş sayfalara veya çok az özgün içerik barındıran sayfalara reklam kodu
// yerleştirmeyin." Betik index.html'deydi, yani şablondan üretilen ~6.800 ürün,
// inceleme, karşılaştırma ve marka sayfasının hepsinde; AdSense dört kez "içeriğinize
// odaklanın" diye reddetti (19 Ekim ölçümü: adreslerin ~%99'u şablon).
//
// Ana sayfa listede: AdSense site doğrulaması kodu orada arıyor. Karşılaştırma ve
// indirim kodu sayfaları gerçek özgün veriyle zenginleşince listeye eklenecek.
//
// Karar SSR'da da veriliyor (App'in NavigationEnd aboneliği): AdSense tarayıcısı her
// adresi sunucunun HTML'iyle görüyor. Betik bir kez yüklenince kaldırılmıyor; istemci
// tarafında editoryal sayfadan ürün sayfasına geçen ziyaretçide kalması zararsız
// (sayfaya reklam birimi yerleştirmiyoruz, otomatik reklam ayarı panelde).

export const ADSENSE_CLIENT = 'ca-pub-9776376467089674';

const EXACT_PATHS = new Set(['/', '/efsane-kasim', '/sozluk']);
const SECTION_PREFIXES = ['/rehber', '/hesaplama', '/hangi-takviye'];

/** routePath() ile normalleştirilmiş yol (sorgu ve # yok) editoryal bir sayfa mı? */
export function allowsAdsenseScript(path: string): boolean {
  return EXACT_PATHS.has(path) || SECTION_PREFIXES.some((p) => path === p || path.startsWith(`${p}/`));
}

/** İzinli bir sayfadaysa betiği head'e bir kez ekler; değilse hiçbir şey yapmaz. */
export function ensureAdsenseScript(document: Document, path: string): void {
  if (!allowsAdsenseScript(path) || document.head.querySelector('script[data-adsense]')) {
    return;
  }

  // Öznitelikler setAttribute ile: SSR'ın DOM'u (domino) özellik atamasını her zaman
  // özniteliğe çevirmiyor, çıktıda async/crossorigin eksik kalmasın.
  const script = document.createElement('script');
  script.setAttribute('async', '');
  script.setAttribute('src', `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${ADSENSE_CLIENT}`);
  script.setAttribute('crossorigin', 'anonymous');
  script.setAttribute('data-adsense', '');
  document.head.appendChild(script);
}
