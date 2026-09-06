import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { PageMetaService } from '../core/page-meta.service';
import { SITE_NAME } from '../core/site-identity';
import { Durum, Kupon, OlayYaniti, YonetimService } from './yonetim.service';

type Sekme = 'durum' | 'olaylar' | 'kuponlar';

/**
 * Yönetim paneli.
 *
 * IKI AYRI KAPI VAR, IKISI DE GEREKLI. Dıştaki Cloudflare Access: kimliği
 * doğrulanmayan istek sunucuya hiç ulaşmıyor. İçteki bu ekran: admin
 * anahtarını bir kez alıp HttpOnly çerezle oturum açıyor. Access tek kapı
 * olsaydı, yanlış yapılandırılmış bir kural her şeyi sessizce açardı.
 *
 * ANAHTAR HICBIR YERE YAZILMIYOR. localStorage/sessionStorage kullanılmıyor;
 * anahtar yalnızca giriş isteğinin gövdesinde gidiyor, sonrası çerezle
 * yürüyor ve o çerezi JavaScript okuyamıyor. Bu anahtar bütün abonelere
 * e-posta gönderebiliyor.
 *
 * SUNUCUDA RENDER EDILMIYOR (bkz. app.routes.server.ts). SSR sunucusu
 * ziyaretçinin çerezini taşımıyor; sunucuda render edilseydi her istek 401
 * döner ve panel her açılışta "yetkisiz" ekranıyla gelirdi.
 */
@Component({
  selector: 'app-yonetim-page',
  imports: [FormsModule],
  templateUrl: './yonetim-page.html',
  styleUrl: './yonetim-page.css',
})
export class YonetimPage implements OnInit {
  private readonly api = inject(YonetimService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly girisYapildi = signal(false);
  readonly yukleniyor = signal(true);
  readonly hata = signal<string | null>(null);
  readonly sekme = signal<Sekme>('durum');

  readonly anahtar = signal('');
  readonly girisHatasi = signal<string | null>(null);
  readonly girisDeneniyor = signal(false);

  readonly durum = signal<Durum | null>(null);
  readonly olaylar = signal<OlayYaniti | null>(null);
  readonly kuponlar = signal<Kupon[]>([]);

  readonly olayGun = signal(7);
  readonly olayTur = signal<string | null>(null);
  readonly suzulenAdres = signal<string | null>(null);

  readonly yeniKupon = signal({
    hedefTip: 'marka' as 'marka' | 'satici',
    hedef: '',
    code: '',
    description: '',
    validUntil: '',
  });
  readonly kuponMesaji = signal<string | null>(null);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Yönetim | ' + SITE_NAME,
      description: 'Yönetim paneli.',
      canonicalPath: '/',
      // Panel arama motorlarına hiç görünmemeli. Sitemap'te de yok ve hiçbir
      // sayfadan bağlantı almıyor.
      noIndex: true,
    });

    if (!this.isBrowser) {
      return;
    }

    // Oturum zaten açıksa doğrudan veriye geç; değilse giriş ekranı.
    this.api.durum().subscribe({
      next: (d) => {
        this.durum.set(d);
        this.girisYapildi.set(true);
        this.yukleniyor.set(false);
        this.olaylariYukle();
        this.kuponlariYukle();
      },
      error: () => {
        this.girisYapildi.set(false);
        this.yukleniyor.set(false);
      },
    });
  }

  girisYap(): void {
    const key = this.anahtar().trim();
    if (!key) return;

    this.girisDeneniyor.set(true);
    this.girisHatasi.set(null);

    this.api.girisYap(key).subscribe({
      next: () => {
        // Anahtar bellekten de siliniyor; ekranda kalmasının faydası yok.
        this.anahtar.set('');
        this.girisDeneniyor.set(false);
        this.girisYapildi.set(true);
        this.durumYukle();
        this.olaylariYukle();
        this.kuponlariYukle();
      },
      error: (e) => {
        this.girisDeneniyor.set(false);
        this.girisHatasi.set(
          e?.status === 429
            ? 'Çok fazla deneme yapıldı. 15 dakika sonra tekrar dene.'
            : 'Anahtar doğrulanmadı.',
        );
      },
    });
  }

  cikisYap(): void {
    this.api.cikisYap().subscribe({
      next: () => {
        this.girisYapildi.set(false);
        this.durum.set(null);
        this.olaylar.set(null);
        this.kuponlar.set([]);
      },
    });
  }

  durumYukle(): void {
    this.api.durum().subscribe({
      next: (d) => this.durum.set(d),
      error: () => this.hata.set('Durum bilgisi alınamadı.'),
    });
  }

  olaylariYukle(): void {
    this.suzulenAdres.set(null);
    this.api.olaylar(this.olayGun(), this.olayTur()).subscribe({
      next: (o) => this.olaylar.set(o),
      error: () => this.hata.set('Olaylar alınamadı.'),
    });
  }

  kuponlariYukle(): void {
    this.api.kuponlar().subscribe({
      next: (k) => this.kuponlar.set(k),
      error: () => this.hata.set('Kuponlar alınamadı.'),
    });
  }

  olayTuruSec(tur: string | null): void {
    this.olayTur.set(tur);
    this.olaylariYukle();
  }

  olayGunSec(gun: number): void {
    this.olayGun.set(gun);
    this.olaylariYukle();
  }

  // Bir adresin tüm geçmişi. Suç duyurusunda ilk sorulan şey bu olduğu için
  // pencere BİLEREK 90 gün: varsayılan 7 günlük görünüm, iki hafta önce
  // başlamış bir saldırıyı gizlerdi.
  adreseGoreFiltrele(ip: string): void {
    this.sekme.set('olaylar');
    this.api.olaylar(90, null).subscribe({
      next: (o) => {
        this.olaylar.set({ ...o, events: o.events.filter((e) => e.ip === ip) });
        this.suzulenAdres.set(ip);
      },
    });
  }

  kuponDurumDegistir(kupon: Kupon): void {
    this.api
      .kuponGuncelle(kupon.id, {
        code: kupon.code,
        description: kupon.description,
        validUntil: kupon.validUntil,
        isActive: !kupon.isActive,
      })
      .subscribe({
        next: () => this.kuponlariYukle(),
        error: () => this.kuponMesaji.set('Kupon güncellenemedi.'),
      });
  }

  kuponEkle(): void {
    const f = this.yeniKupon();
    if (!f.hedef.trim() || !f.description.trim()) {
      this.kuponMesaji.set('Marka/satıcı ve açıklama zorunlu.');
      return;
    }

    this.api
      .kuponEkle({
        // Marka ve satıcıdan YALNIZCA BİRİ dolu olabilir; veritabanında da
        // check constraint ile korunuyor.
        brandName: f.hedefTip === 'marka' ? f.hedef.trim() : null,
        seller: f.hedefTip === 'satici' ? f.hedef.trim() : null,
        // Boş kod "kodu yok, kendiliğinden uygulanıyor" demek. Boş METİN
        // göndermek arayüzde boş bir kod kutusu gösterirdi.
        code: f.code.trim() || null,
        description: f.description.trim(),
        validUntil: f.validUntil || null,
      })
      .subscribe({
        next: () => {
          this.kuponMesaji.set('Kupon eklendi.');
          this.yeniKupon.set({ hedefTip: 'marka', hedef: '', code: '', description: '', validUntil: '' });
          this.kuponlariYukle();
        },
        error: (e) =>
          this.kuponMesaji.set(
            e?.status === 400 ? 'Marka veya satıcıdan yalnızca biri dolu olmalı.' : 'Kupon eklenemedi.',
          ),
      });
  }

  tarih(deger: string | null | undefined): string {
    if (!deger) return '—';
    return new Date(deger).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
  }

  saatFarki(deger: string | null | undefined): string {
    if (!deger) return '—';
    const saat = (Date.now() - new Date(deger).getTime()) / 3600000;
    if (saat < 1) return 'az önce';
    if (saat < 48) return Math.round(saat) + ' saat önce';
    return Math.round(saat / 24) + ' gün önce';
  }

  turEtiketi(tur: string): string {
    switch (tur) {
      case 'unauthorized':
        return 'Yetkisiz deneme';
      case 'rate-limited':
        return 'Hız sınırı';
      case 'probe':
        return 'Açık taraması';
      case 'server-error':
        return 'Sunucu hatası';
      default:
        return tur;
    }
  }

  besinYuzde(): number {
    const d = this.durum();
    if (!d || d.urun.toplam === 0) return 0;
    return Math.round((d.urun.besinli / d.urun.toplam) * 1000) / 10;
  }
}
