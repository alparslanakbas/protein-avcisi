import { Router } from '@angular/router';

/**
 * İstenen içerik yoksa 404 sayfasını göster.
 *
 * <b>NEDEN YÖNLENDİRME DEĞİL.</b> 4 Eylül'e kadar bu durumlarda
 * `router.navigate(['/'])` çağrılıyordu ve koddaki yorum bunu "soft-404'ten
 * kaçınmak" diye gerekçelendiriyordu. Gerekçe TERSİNE dönmüştü: Google'ın
 * tanımında var olmayan bir adresi ana sayfaya yönlendirmek soft 404'ün
 * kendisi — "geçerli sayfa" sinyali veriyor. O karar verildiğinde elimizde
 * gerçek 404 dönen bir sayfa yoktu, seçenekler 200 + "bulunamadı" metni ya da
 * yönlendirmeydi; ikisi de yanlıştı. Artık üçüncü ve doğru seçenek var.
 *
 * <b>skipLocationChange ŞART.</b> Adres çubuğunda İSTENEN adres kalıyor,
 * `/bulunamadi` görünmüyor. Sebep sadece görüntü değil: arama motoruna
 * "bu adres yok" demek istiyoruz. Adres değişseydi istenen adres için
 * dönen cevap bir yönlendirme olurdu ve baştaki soruna geri dönerdik.
 *
 * Durum kodunu (404) sayfanın kendisi `RESPONSE_INIT` ile veriyor, bkz.
 * `NotFoundPage`.
 */
export function showNotFound(router: Router): void {
  void router.navigate(['/bulunamadi'], { skipLocationChange: true });
}
