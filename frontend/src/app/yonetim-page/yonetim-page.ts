import { isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { PageMetaService } from '../core/page-meta.service';
import { SITE_NAME } from '../core/site-identity';
import {
  Durum,
  Kupon,
  OlayYaniti,
  YonetimMarka,
  YonetimService,
  YonetimUrun,
} from './yonetim.service';

type Sekme = 'durum' | 'olaylar' | 'kuponlar' | 'gorunurluk';
type GorunurlukGorunumu = 'markalar' | 'urunler';
type MarkaFiltresi = 'tumu' | 'gorunur' | 'gizli';

interface BekleyenGorunurlukDegisikligi {
  tip: 'marka' | 'urun';
  id: number;
  name: string;
}

const MARKA_SAYFA_BOYUTU = 5;
const GORUNURLUK_KILIT_BITISI = Date.parse('2026-09-19T00:00:00+03:00');

/**
 * Yönetim paneli.
 *
 * Dıştaki Cloudflare Access ve içerideki HttpOnly yönetim oturumu iki ayrı
 * güvenlik katmanıdır. Yönetim anahtarı hiçbir tarayıcı deposuna yazılmaz.
 * Bu rota yalnızca istemcide render edilir (bkz. app.routes.server.ts).
 */
@Component({
  selector: 'app-yonetim-page',
  imports: [FormsModule],
  templateUrl: './yonetim-page.html',
  styleUrls: ['./yonetim-page.css', './yonetim-page-support.css'],
})
export class YonetimPage implements OnInit {
  private readonly api = inject(YonetimService);
  private readonly pageMeta = inject(PageMetaService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private markalarIlkKezYuklendi = false;

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

  /** Satır içi düzenlenen kupon; null ise düzenleme kapalı. */
  readonly duzenlenenKupon = signal<{
    id: number;
    code: string;
    description: string;
    validUntil: string;
  } | null>(null);
  readonly kuponKaydediliyor = signal(false);

  readonly gorunurlukGorunumu = signal<GorunurlukGorunumu>('markalar');
  readonly markalar = signal<YonetimMarka[]>([]);
  readonly markalarYukleniyor = signal(false);
  readonly markaArama = signal('');
  readonly markaFiltresi = signal<MarkaFiltresi>('tumu');
  readonly markaSayfa = signal(1);
  readonly urunler = signal<YonetimUrun[]>([]);
  readonly urunlerYukleniyor = signal(false);
  readonly urunArama = signal('');
  readonly yalnizGizliUrunler = signal(false);
  readonly urunAramaYapildi = signal(false);
  readonly gorunurlukMesaji = signal<string | null>(null);
  readonly gorunurlukSonGuncelleme = signal<Date | null>(null);
  readonly gorunurlukKilitli = signal(Date.now() < GORUNURLUK_KILIT_BITISI);
  readonly bekleyenDegisiklik = signal<BekleyenGorunurlukDegisikligi | null>(null);
  readonly degisiklikYapiliyor = signal(false);

  readonly suzulenMarkalar = computed(() => {
    const query = this.normalize(this.markaArama());
    const filtre = this.markaFiltresi();
    return this.markalar().filter((marka) => {
      const aramaylaEslesiyor = !query || this.normalize(marka.name).includes(query);
      const filtreyleEslesiyor =
        filtre === 'tumu' || (filtre === 'gorunur' ? marka.isActive : !marka.isActive);
      return aramaylaEslesiyor && filtreyleEslesiyor;
    });
  });

  readonly markaToplamSayfa = computed(() =>
    Math.max(1, Math.ceil(this.suzulenMarkalar().length / MARKA_SAYFA_BOYUTU)),
  );

  readonly sayfadakiMarkalar = computed(() => {
    const baslangic = (this.markaSayfa() - 1) * MARKA_SAYFA_BOYUTU;
    return this.suzulenMarkalar().slice(baslangic, baslangic + MARKA_SAYFA_BOYUTU);
  });

  readonly gorunenMarkaSayfalari = computed(() => {
    const toplam = this.markaToplamSayfa();
    const mevcut = this.markaSayfa();
    const baslangic = Math.min(Math.max(1, mevcut - 2), Math.max(1, toplam - 4));
    return Array.from({ length: Math.min(5, toplam) }, (_, index) => baslangic + index);
  });

  readonly aktifMarkaSayisi = computed(
    () => this.markalar().filter((marka) => marka.isActive).length,
  );
  readonly gizliMarkaSayisi = computed(
    () => this.markalar().filter((marka) => !marka.isActive).length,
  );
  readonly gorunurlukToplamUrun = computed(
    () =>
      this.durum()?.urun.toplam ??
      this.markalar().reduce((toplam, marka) => toplam + marka.urunSayisi, 0),
  );

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Yönetim | ' + SITE_NAME,
      description: 'Yönetim paneli.',
      canonicalPath: '/',
      noIndex: true,
    });

    if (!this.isBrowser) return;

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
        this.duzenlenenKupon.set(null);
        this.markalar.set([]);
        this.urunler.set([]);
        this.markalarIlkKezYuklendi = false;
      },
    });
  }

  sekmeSec(yeniSekme: Sekme): void {
    this.sekme.set(yeniSekme);
    this.hata.set(null);
    // Kupon sekmesinde de marka listesi lazım: marka adı ELLE yazılıyordu ve
    // katalogdaki yazımı tutturmak gerekiyordu ("Dr Supplement" mi
    // "DrSupplement" mi). Öneri kutusu bu tahmini ortadan kaldırıyor.
    const markaListesiGerekli = yeniSekme === 'gorunurluk' || yeniSekme === 'kuponlar';
    if (markaListesiGerekli && !this.markalarIlkKezYuklendi) {
      this.markalariYukle(false);
    }
  }

  durumYukle(): void {
    this.api.durum().subscribe({
      next: (d) => this.durum.set(d),
      error: (e) => this.hata.set(this.hataMetni(e, 'Durum bilgisi alınamadı.')),
    });
  }

  olaylariYukle(): void {
    this.suzulenAdres.set(null);
    this.api.olaylar(this.olayGun(), this.olayTur()).subscribe({
      next: (o) => this.olaylar.set(o),
      error: (e) => this.hata.set(this.hataMetni(e, 'Olaylar alınamadı.')),
    });
  }

  kuponlariYukle(): void {
    this.api.kuponlar().subscribe({
      next: (k) => this.kuponlar.set(k),
      error: (e) => this.hata.set(this.hataMetni(e, 'Kuponlar alınamadı.')),
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

  adreseGoreFiltrele(ip: string): void {
    this.sekme.set('olaylar');
    this.api.olaylar(90, null).subscribe({
      next: (o) => {
        this.olaylar.set({ ...o, events: o.events.filter((e) => e.ip === ip) });
        this.suzulenAdres.set(ip);
      },
    });
  }

  kuponDuzenlemeBaslat(kupon: Kupon): void {
    this.kuponMesaji.set(null);
    this.duzenlenenKupon.set({
      id: kupon.id,
      code: kupon.code ?? '',
      description: kupon.description,
      // <input type="date"> yalnızca YYYY-MM-DD kabul ediyor; API tam
      // zaman damgası döndürdüğü için kırpılıyor.
      validUntil: kupon.validUntil ? kupon.validUntil.slice(0, 10) : '',
    });
  }

  kuponDuzenlemeIptal(): void {
    if (this.kuponKaydediliyor()) return;
    this.duzenlenenKupon.set(null);
  }

  kuponAlanGuncelle(alan: 'code' | 'description' | 'validUntil', deger: string): void {
    const mevcut = this.duzenlenenKupon();
    if (!mevcut) return;
    this.duzenlenenKupon.set({ ...mevcut, [alan]: deger });
  }

  kuponDuzenlemeKaydet(): void {
    const d = this.duzenlenenKupon();
    if (!d) return;

    if (!d.description.trim()) {
      this.kuponMesaji.set('Açıklama boş olamaz.');
      return;
    }

    this.kuponKaydediliyor.set(true);
    this.api
      .kuponGuncelle(d.id, {
        // BOŞ METİN gönderiliyor, null DEĞİL. Bu uçta null "bu alana
        // dokunma" demek; kullanıcı kodu silmek isteyip alanı boşalttığında
        // null göndersek kod olduğu gibi kalır ve "kaydettim ama değişmedi"
        // olurdu. Boş metin backend'de kodu gerçekten siliyor.
        code: d.code.trim(),
        description: d.description.trim(),
        validUntil: d.validUntil || null,
        // Tarih için boş metin diye bir şey yok, açık bayrak gerekiyor.
        validUntilTemizle: d.validUntil ? undefined : true,
        // isActive BİLEREK gönderilmiyor: metni düzeltmek yayın durumunu
        // değiştirmemeli.
      })
      .subscribe({
        next: () => {
          this.kuponKaydediliyor.set(false);
          this.duzenlenenKupon.set(null);
          this.kuponMesaji.set('Kupon güncellendi.');
          this.kuponlariYukle();
        },
        error: (e) => {
          this.kuponKaydediliyor.set(false);
          this.kuponMesaji.set(this.hataMetni(e, 'Kupon güncellenemedi.'));
        },
      });
  }

  /**
   * Kupon ekleme hatası — BACKEND'İN KENDİ MESAJINI ÖNE ALIYOR.
   *
   * Panel önce her başarısızlıkta "Kupon eklenemedi." diyordu. 8 Eylül'de
   * kullanıcı marka adını "DrSupplement" yazdı, katalogdaki ad
   * "Dr Supplement" olduğu için backend 404 ve tam olarak
   * "'DrSupplement' adında marka bulunamadı." döndü — ama panel o cümleyi
   * çöpe atıp genel mesajı gösterdi. Sebep ekranda yazıyordu ve biz onu
   * sakladık; kullanıcı neden olduğunu bilemedi.
   */
  private kuponEklemeHatasi(e: unknown): string {
    const yanit = e as { status?: number; error?: unknown } | null;

    // Backend düz metin ya da { message } döndürebiliyor; ikisini de al.
    const govde = yanit?.error;
    const mesaj =
      typeof govde === 'string'
        ? govde
        : ((govde as { message?: string } | null)?.message ?? null);

    if (mesaj && mesaj.trim().length > 0) return mesaj.trim();
    if (yanit?.status === 400) return 'Marka veya satıcıdan yalnızca biri dolu olmalı.';
    return this.hataMetni(e, 'Kupon eklenemedi.');
  }

  /**
   * Hata kodunu kullanıcının anlayacağı bir cümleye çevirir.
   *
   * NEDEN: panel her başarısızlıkta "500" diyordu. Kullanıcı bir deploy
   * sırasında 502 aldı (backend yenilenirken üç saniyelik pencere) ama
   * ekranda 500 yazdığı için kalıcı bir arıza sandı. Geçici olanı kalıcı
   * sanmak, olmayan bir hatayı aramaya yol açıyor.
   */
  private hataMetni(e: unknown, varsayilan: string): string {
    const kod = (e as { status?: number } | null)?.status;

    if (kod === 0) return 'Sunucuya ulaşılamadı. Bağlantını kontrol et.';
    if (kod === 401) return 'Oturum kapandı. Sayfayı yenileyip tekrar gir.';
    if (kod === 429) return 'Çok fazla istek gönderildi, biraz bekle.';
    if (kod === 502 || kod === 503 || kod === 504)
      return 'Sunucu şu an güncelleniyor. Birkaç saniye sonra tekrar dene.';

    return kod ? varsayilan + ' (kod ' + kod + ')' : varsayilan;
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
        error: (e) => this.kuponMesaji.set(this.hataMetni(e, 'Kupon güncellenemedi.')),
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
        brandName: f.hedefTip === 'marka' ? f.hedef.trim() : null,
        seller: f.hedefTip === 'satici' ? f.hedef.trim() : null,
        code: f.code.trim() || null,
        description: f.description.trim(),
        validUntil: f.validUntil || null,
      })
      .subscribe({
        next: () => {
          this.kuponMesaji.set('Kupon eklendi.');
          this.yeniKupon.set({
            hedefTip: 'marka',
            hedef: '',
            code: '',
            description: '',
            validUntil: '',
          });
          this.kuponlariYukle();
        },
        error: (e) => this.kuponMesaji.set(this.kuponEklemeHatasi(e)),
      });
  }

  markalariYukle(mesajiTemizle = true): void {
    this.markalarYukleniyor.set(true);
    if (mesajiTemizle) this.gorunurlukMesaji.set(null);
    this.api.markalar().subscribe({
      next: (markalar) => {
        this.markalar.set(markalar);
        this.markalarIlkKezYuklendi = true;
        this.markalarYukleniyor.set(false);
        this.gorunurlukSonGuncelleme.set(new Date());
        this.markaSayfasiniDuzelt();
      },
      error: () => {
        this.markalarYukleniyor.set(false);
        this.gorunurlukMesaji.set('Marka görünürlükleri alınamadı.');
      },
    });
  }

  gorunurlukGorunumuSec(gorunum: GorunurlukGorunumu): void {
    this.gorunurlukGorunumu.set(gorunum);
    this.gorunurlukMesaji.set(null);
  }

  markaAramaDegisti(value: string): void {
    this.markaArama.set(value);
    this.markaSayfa.set(1);
  }

  markaFiltresiSec(filtre: MarkaFiltresi): void {
    this.markaFiltresi.set(filtre);
    this.markaSayfa.set(1);
  }

  markaSayfasinaGit(sayfa: number): void {
    this.markaSayfa.set(Math.min(Math.max(1, sayfa), this.markaToplamSayfa()));
  }

  urunAra(): void {
    const query = this.urunArama().trim();
    if (!query && !this.yalnizGizliUrunler()) {
      this.urunler.set([]);
      this.urunAramaYapildi.set(false);
      return;
    }

    this.urunlerYukleniyor.set(true);
    this.urunAramaYapildi.set(true);
    this.gorunurlukMesaji.set(null);
    this.api.urunler(query, this.yalnizGizliUrunler()).subscribe({
      next: (urunler) => {
        this.urunler.set(urunler);
        this.urunlerYukleniyor.set(false);
        this.gorunurlukSonGuncelleme.set(new Date());
      },
      error: () => {
        this.urunlerYukleniyor.set(false);
        this.gorunurlukMesaji.set('Ürünler aranamadı.');
      },
    });
  }

  yalnizGizliDegisti(value: boolean): void {
    this.yalnizGizliUrunler.set(value);
    this.urunAra();
  }

  gorunurlukDegisikligiIste(
    tip: 'marka' | 'urun',
    id: number,
    name: string,
    suAndaAktif: boolean,
  ): void {
    const gizlenecek = suAndaAktif;
    if (gizlenecek && this.gorunurlukKilitli()) {
      this.gorunurlukMesaji.set('Gözlem dönemi boyunca marka ve ürün gizleme işlemleri kilitli.');
      return;
    }

    if (gizlenecek) {
      this.bekleyenDegisiklik.set({ tip, id, name });
      return;
    }

    this.gorunurlukDegisikliginiUygula({ tip, id, name }, true);
  }

  degisikligiOnayla(): void {
    const degisiklik = this.bekleyenDegisiklik();
    if (!degisiklik) return;
    this.gorunurlukDegisikliginiUygula(degisiklik, false);
  }

  degisikligiIptal(): void {
    if (this.degisiklikYapiliyor()) return;
    this.bekleyenDegisiklik.set(null);
  }

  tarih(deger: string | null | undefined): string {
    if (!deger) return '—';
    return new Date(deger).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
  }

  para(deger: number | null | undefined): string {
    if (deger == null) return '—';
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(deger);
  }

  bugunEtiketi(): string {
    return new Intl.DateTimeFormat('tr-TR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(new Date());
  }

  saatFarki(deger: string | Date | null | undefined): string {
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

  private gorunurlukDegisikliginiUygula(
    degisiklik: BekleyenGorunurlukDegisikligi,
    isActive: boolean,
  ): void {
    this.degisiklikYapiliyor.set(true);
    this.gorunurlukMesaji.set(null);
    const istek =
      degisiklik.tip === 'marka'
        ? this.api.markaDurumuGuncelle(degisiklik.id, isActive)
        : this.api.urunDurumuGuncelle(degisiklik.id, isActive);

    istek.subscribe({
      next: () => {
        this.degisiklikYapiliyor.set(false);
        this.bekleyenDegisiklik.set(null);
        this.gorunurlukMesaji.set(
          `${degisiklik.name} ${isActive ? 'yeniden yayına alındı.' : 'gizlendi.'}`,
        );
        this.markalariYukle(false);
        if (degisiklik.tip === 'urun') this.urunAra();
      },
      error: () => {
        this.degisiklikYapiliyor.set(false);
        this.gorunurlukMesaji.set('Görünürlük değişikliği kaydedilemedi.');
      },
    });
  }

  private markaSayfasiniDuzelt(): void {
    if (this.markaSayfa() > this.markaToplamSayfa()) {
      this.markaSayfa.set(this.markaToplamSayfa());
    }
  }

  private normalize(value: string): string {
    return value
      .toLocaleLowerCase('tr-TR')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');
  }
}
