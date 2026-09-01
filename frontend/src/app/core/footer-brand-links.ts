/**
 * Footer'daki 68 markalık bağlantı listesi bu sayfada gösterilsin mi?
 *
 * ÜRÜN ve İNCELEME sayfalarında GÖSTERİLMİYOR. Ölçüldü (1 Eylül, canlı ürün
 * sayfası): sayfanın tamamı 612 kelime, footer 334 (%54), bunun 241'i —
 * yani sayfanın %39'u — yalnızca marka bağlantısı listesi. Ürüne özgü içerik
 * ise 34-86 kelime. Aynı blok 2500+ sayfada birebir tekrarlanıyor.
 *
 * GSC'de "Tarandı - şu anda dizine eklenmiş değil" 405 sayfada; ince ve
 * birbirinin aynısı sayfalar bunun bilinen sebebi. Listeyi ürün
 * sayfalarından kaldırmak, ürüne özgü içeriğin sayfadaki payını yaklaşık
 * iki katına çıkarıyor.
 *
 * Liste ana sayfada ve marka/kategori sayfalarında AYNEN duruyor: Google
 * marka sayfalarını oradan zaten buluyor, kaybolan bir giriş noktası yok.
 */
export function showFooterBrandLinks(path: string): boolean {
  return !path.startsWith('/urun/') && !path.startsWith('/urun-inceleme/');
}
