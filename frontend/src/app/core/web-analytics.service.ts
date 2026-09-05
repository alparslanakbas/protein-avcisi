import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { InjectionToken, Injectable, PLATFORM_ID, effect, inject } from '@angular/core';

import { CookieConsentService } from './cookie-consent.service';

/**
 * Cloudflare panelinden alınan beacon jetonu (Web Analytics → manuel kurulum).
 *
 * BOŞ olduğu sürece beacon HİÇ yüklenmiyor — jeton girilene kadar özellik
 * tamamen kapalı, yarım yapılandırmayla sessizce ölçüm yapmıyor.
 *
 * InjectionToken olmasının sebebi test edilebilirlik: sabit olsaydı "onay
 * verilince yükleniyor mu" yolu hiç sınanamazdı ve bu servisin tek işi o
 * karar.
 *
 * <b>JETON GİRİLİRKEN ÇEREZ POLİTİKASI DA GÜNCELLENMELİ.</b>
 * `cookie-policy-page` şu an "Şu an çerez kullanmıyoruz" diyor — Cloudflare'in
 * ürünü çerezsiz olduğu için o cümle doğru KALIYOR, ama aynı sayfa
 * "değişiklik yapılmadan önce politika ve çerez listesi güncellenir" diye söz
 * veriyor. Ölçüm açılıp politika susarsa verilmiş bir söz tutulmamış olur.
 * İkisi AYNI değişiklikte yapılmalı.
 */
export const CLOUDFLARE_BEACON_TOKEN = new InjectionToken<string>('CLOUDFLARE_BEACON_TOKEN', {
  providedIn: 'root',
  factory: () => '',
});

/**
 * Cloudflare Web Analytics beacon'ı — YALNIZCA çerez onayı verilmişse.
 *
 * <b>NEDEN ONAYA BAĞLI.</b> Cloudflare'in bu ürünü çerez kullanmıyor,
 * parmak izi çıkarmıyor ve kişiyi tanımlamıyor; KVKK/GDPR açısından onay
 * gerektirmesi beklenmez. Buna rağmen onaya bağlandı, çünkü ÇEREZ BANDIMIZ
 * kullanıcıya şunu söylüyor: "Reklam veya analiz etkinleştirilirse bu
 * seçimine uyacağız." Yasal zorunluluk olmasa da verilmiş bir söz var ve
 * onu tutmamak, sitenin bütün iddiasıyla (markaların söyledikleriyle
 * yaptıkları arasındaki farkı göstermek) çelişirdi.
 *
 * `CookieConsentService` zaten tam bunun için kurulmuştu — kendi yorumunda
 * "ileride bir script eklenirse yükleme kararı status() === 'accepted'
 * kontrolüne bağlanacak" yazıyor. Banner'a hiç dokunulmadı.
 *
 * <b>OTOMATİK KURULUM KULLANILMIYOR.</b> Cloudflare, proxy'lediği sitelere
 * beacon'ı kendisi enjekte edebiliyor ve o yol kod gerektirmiyor — ama o
 * enjeksiyonu bizim onay kontrolümüz durduramaz. Manuel kurulum bilerek
 * seçildi.
 *
 * <b>JETON GİZLİ DEĞİL.</b> Sayfa kaynağında zaten görünüyor; Cloudflare'in
 * kendi dokümanı da onu herkese açık kabul ediyor. Bu yüzden depoda durması
 * bir sır sızıntısı değil.
 */
@Injectable({ providedIn: 'root' })
export class WebAnalyticsService {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly consent = inject(CookieConsentService);
  private readonly beaconToken = inject(CLOUDFLARE_BEACON_TOKEN);

  private scriptEl: HTMLScriptElement | null = null;

  constructor() {
    effect(() => {
      const onay = this.consent.status();
      if (!this.isBrowser) return;

      if (onay === 'accepted') {
        this.yukle();
      } else {
        this.kaldir();
      }
    });
  }

  private yukle(): void {
    if (this.scriptEl || !this.beaconToken) return;

    const el = this.document.createElement('script');
    el.defer = true;
    el.src = 'https://static.cloudflareinsights.com/beacon.min.js';
    el.setAttribute('data-cf-beacon', JSON.stringify({ token: this.beaconToken }));
    this.document.head.appendChild(el);
    this.scriptEl = el;
  }

  /**
   * Onay geri alınırsa script DOM'dan çıkarılıyor, böylece sonraki sayfa
   * görüntülemeleri ölçülmüyor.
   *
   * DÜRÜST SINIR: o ana kadar gönderilmiş isteği geri alamayız. Tek sayfa
   * uygulaması olduğu için tam temizlik ancak sayfa yenilenince oluyor.
   * Kullanıcıyı zorla yenilemek yerine bu sınır kabul edildi — gönderilen
   * veri zaten kişiyi tanımlamıyor.
   */
  private kaldir(): void {
    this.scriptEl?.remove();
    this.scriptEl = null;
  }
}
