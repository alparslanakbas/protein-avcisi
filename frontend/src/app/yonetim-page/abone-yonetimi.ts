import { computed, signal } from '@angular/core';

import { Sayfalama, gorunenSayfalar } from './sayfalama';
import { Abone, AboneFiltresi, AbonelerYaniti, YonetimService } from './yonetim.service';

/** Onay bekleyen abone işlemi: pasife almak ya da kalıcı silmek. */
export interface BekleyenAboneIslemi {
  abone: Abone;
  tur: 'pasife-al' | 'sil';
}

/**
 * Yönetim panelinin Aboneler sekmesi. 4 Ekim'de sayfa bileşeninden ayrıldı: panel
 * dosyaları boyut tavanındaydı, sayfalama ve kalıcı silme oraya sığmıyordu.
 *
 * Liste SUNUCUDA sayfalanıyor ve süzülüyor (ürün listesiyle aynı desen). Eskiden en
 * yeni 1000 kayıt bir kerede geliyor, arama tarayıcıda yapılıyordu; 1000'i geçen
 * abone listede hiç görünmezdi.
 */
export class AboneYonetimi {
  readonly veri = signal<AbonelerYaniti | null>(null);
  readonly yukleniyor = signal(false);
  readonly arama = signal('');
  readonly filtre = signal<AboneFiltresi>('tumu');
  readonly mesaj = signal<string | null>(null);
  readonly bekleyenIslem = signal<BekleyenAboneIslemi | null>(null);
  /** İsteği süren satırın kimliği; yalnızca o satırın düğmeleri kilitlensin. */
  readonly islemdeId = signal<number | null>(null);
  readonly sayfa = signal(1);

  readonly toplamSayfa = computed(() => {
    const veri = this.veri();
    return veri ? Math.max(1, Math.ceil(veri.toplam / Math.max(1, veri.sayfaBoyutu))) : 1;
  });

  readonly sayfalama = computed<Sayfalama>(() => {
    const veri = this.veri();
    const sayfa = this.sayfa();
    const ilk = veri && veri.aboneler.length ? (sayfa - 1) * veri.sayfaBoyutu + 1 : 0;
    return {
      ozet: `${ilk}–${ilk ? ilk + (veri?.aboneler.length ?? 1) - 1 : 0} / ${veri?.toplam ?? 0} abone`,
      sayfa,
      toplamSayfa: this.toplamSayfa(),
      sayfalar: gorunenSayfalar(sayfa, this.toplamSayfa()),
      mesgul: this.yukleniyor(),
      git: (hedef) => this.yukle(hedef),
    };
  });

  private aramaZamanlayicisi: ReturnType<typeof setTimeout> | undefined;

  constructor(
    private readonly api: YonetimService,
    private readonly hataMetni: (e: unknown, varsayilan: string) => string,
  ) {}

  /** Bir kez değil HER ziyarette: panel açıkken biri gelen kutusundan onaylayabilir. */
  yukle(sayfa = this.sayfa()): void {
    this.yukleniyor.set(true);
    this.api.aboneler({ ara: this.arama(), durum: this.filtre(), sayfa }).subscribe({
      next: (veri) => {
        this.veri.set(veri);
        this.sayfa.set(veri.sayfa);
        this.yukleniyor.set(false);
      },
      error: (e) => {
        this.yukleniyor.set(false);
        this.mesaj.set(this.hataMetni(e, 'Aboneler alınamadı.'));
      },
    });
  }

  /** Yazarken her harfte istek atmasın; kısa bir duraklamadan sonra ilk sayfa. */
  aramaDegisti(deger: string): void {
    this.arama.set(deger);
    clearTimeout(this.aramaZamanlayicisi);
    this.aramaZamanlayicisi = setTimeout(() => this.yukle(1), 300);
  }

  filtreSec(filtre: AboneFiltresi): void {
    this.filtre.set(filtre);
    this.yukle(1);
  }

  /** Pasife almak da silmek de birinin e-postasını keser; önce sorulur. */
  islemIste(abone: Abone, tur: BekleyenAboneIslemi['tur']): void {
    this.mesaj.set(null);
    this.bekleyenIslem.set({ abone, tur });
  }

  islemIptal(): void {
    if (this.islemdeId() !== null) return;
    this.bekleyenIslem.set(null);
  }

  islemOnayla(): void {
    const islem = this.bekleyenIslem();
    if (!islem) return;
    const { abone, tur } = islem;
    const silme = tur === 'sil';

    this.islemdeId.set(abone.id);
    (silme ? this.api.aboneSil(abone.id) : this.api.abonePasifeAl(abone.id)).subscribe({
      next: () => {
        this.islemdeId.set(null);
        this.bekleyenIslem.set(null);
        this.mesaj.set(
          silme ? `${abone.email} kalıcı olarak silindi.` : `${abone.email} artık abone değil.`,
        );
        // Sayfanın tek satırı silindiyse o sayfa artık boş: bir öncekine dön.
        const sayfadaKalan = (this.veri()?.aboneler.length ?? 0) - (silme ? 1 : 0);
        this.yukle(sayfadaKalan > 0 ? this.sayfa() : Math.max(1, this.sayfa() - 1));
      },
      error: (e) => {
        this.islemdeId.set(null);
        this.bekleyenIslem.set(null);
        this.mesaj.set(this.hataMetni(e, silme ? 'Abone silinemedi.' : 'Abone pasife alınamadı.'));
      },
    });
  }

  onayGonder(abone: Abone): void {
    this.mesaj.set(null);
    this.islemdeId.set(abone.id);
    this.api.aboneOnayGonder(abone.id).subscribe({
      next: () => {
        this.islemdeId.set(null);
        this.mesaj.set(`${abone.email} adresine onay e-postası gönderildi.`);
        this.yukle();
      },
      error: (e) => {
        this.islemdeId.set(null);
        // Bekleme süresini ve sağlayıcı hatasını backend kendi cümlesiyle
        // anlatıyor; genel bir "gönderilemedi" hangisinin olduğunu gizlerdi
        // (kupon eklemede yaşanan hatanın aynısı, bkz. kuponEklemeHatasi).
        const govde = (e as { error?: unknown } | null)?.error;
        const mesaj =
          typeof govde === 'string' ? govde : (govde as { message?: string } | null)?.message;
        this.mesaj.set(mesaj?.trim() || this.hataMetni(e, 'Onay e-postası gönderilemedi.'));
      },
    });
  }

  durumEtiketi(durum: AboneFiltresi): string {
    switch (durum) {
      case 'tumu':
        return 'Tümü';
      case 'aktif':
        return 'Aktif';
      case 'bekliyor':
        return 'Onay bekliyor';
      default:
        return 'Ayrıldı';
    }
  }
}
