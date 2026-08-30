import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { Observable, of, tap } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { Deal } from './deal.model';

const TOKEN_KEY = 'favorites-token';

// Hesap/login gerektirmeyen "favorilerim" listesi — ilk eklemede backend'in
// döndürdüğü token localStorage'da tutulup sonraki isteklerde kullanılıyor
// (ThemeService/CookieConsentService ile aynı SSR-güvenli desende).
@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private readonly http = inject(HttpClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  // Nav'daki (site-header, mobil tab bar, ana sayfanın kendi header'ı)
  // "Takip listem" rozeti hepsi bu TEK signal'i okuyor — favori eklenince/
  // çıkarılınca sayfa yenilemeden hepsi birden güncellensin diye servis
  // seviyesinde (singleton) tutuluyor, her component kendi kopyasını
  // tutup senkronize etmek zorunda kalmıyor.
  readonly count = signal(0);

  getToken(): string | null {
    return this.isBrowser ? localStorage.getItem(TOKEN_KEY) : null;
  }

  hasToken(): boolean {
    return this.getToken() !== null;
  }

  // Listeyi BU CİHAZDAN ayırır; sunucudaki favoriler olduğu gibi kalır ve
  // kurtarma linkiyle geri alınabilir.
  //
  // Bu düğme olmadan liste bir kez kaydedildiğinde o tarayıcıdan hiç
  // çıkarılamıyordu: ortak kullanılan bir bilgisayarda sonraki kişi önceki
  // kişinin listesini görüyor ve bunu fark etmesinin de bir yolu bulunmuyordu.
  signOut(): void {
    if (this.isBrowser) localStorage.removeItem(TOKEN_KEY);
    this.count.set(0);
    // Bayrak da sıfırlanmalı: kullanıcı listesini kurtarma bağlantısıyla geri
    // aldığında sayacın yeniden çekilebilmesi gerekiyor.
    this.countLoaded = false;
  }

  saveToken(token: string | null): void {
    // token null gelebilir — e-posta zaten başka bir aboneye aitse backend
    // artık o hesabın token'ını ifşa etmiyor (bkz. 2026-08-15 güvenlik
    // düzeltmesi), bu durumda localStorage'a hiçbir şey yazmıyoruz.
    if (this.isBrowser && token) localStorage.setItem(TOKEN_KEY, token);
  }

  // recoverySent: e-posta zaten var olan bir aboneye aitse (bu cihazda hiç
  // token yoksa) backend arka planda bir kurtarma maili gönderiyor — bu
  // cihaz da aynı e-postayla favorilerini görebilsin diye. Kullanıcı
  // gerçek bir testte bunu bulamayınca (favori eklendi ama listede hiç
  // görünmüyordu) eklendi, bkz. FavoriteService.AddAsync.
  // Ekleme sonrası count'u tam bir list() ile (basit +1 yerine) yeniliyoruz —
  // recoverySent=true durumunda bu cihazda daha önce hiç token yoktu, yani
  // yerel sayaç 0'dı ama sunucudaki gerçek favori sayısı 1'den fazla olabilir
  // (başka bir cihazda eklenmiş favoriler de artık bu hesaba dahil).
  add(productId: number, email?: string): Observable<{ token: string | null; recoverySent: boolean }> {
    return this.http
      .post<{ token: string | null; recoverySent: boolean }>(`${API_BASE_URL}/api/products/${productId}/favorite`, {
        token: this.getToken(),
        email: email ?? null,
      })
      .pipe(tap(() => this.refreshCount()));
  }

  remove(productId: number): Observable<void> {
    return this.http
      .delete<void>(`${API_BASE_URL}/api/products/${productId}/favorite`, {
        params: { token: this.getToken() ?? '' },
      })
      .pipe(tap(() => this.count.update((c) => Math.max(0, c - 1))));
  }

  list(): Observable<Deal[]> {
    const token = this.getToken();
    if (!token) return of([]);
    return this.http.get<Deal[]>(`${API_BASE_URL}/api/favorites`, { params: { token } }).pipe(tap((list) => this.count.set(list.length)));
  }

  // Bu cihazda token kaybolduysa (temizlenen tarayıcı verisi, farklı bir
  // tarayıcı/uygulama vb.) — e-postaya token'ı içeren bir link gönderiliyor.
  // Yanıt e-postanın kayıtlı olup olmadığından bağımsız hep aynı (backend
  // enumeration'ı önlüyor), bu yüzden burada da tek bir mesaj döndürülüyor.
  recover(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API_BASE_URL}/api/favorites/recover`, { email });
  }

  // Rozeti gösteren üç bileşen (site-header, mobile-tab-bar ve ana sayfadaki
  // deals-list) daha önce HER BİRİ ayrı ayrı list() çağırıyordu. Sayaç servis
  // seviyesinde paylaşılıyordu ama İSTEK paylaşılmıyordu: tek bir sayfa
  // açılışında üç istek gidiyor, birkaç gezinme + bir favori eklemede on
  // isteği geçiyor ve Cloudflare'in hız sınırı 429 döndürüyordu. Kullanıcıya
  // bu "Bağlantı sorunu / Takip listen yüklenemedi" ekranı olarak yansıyor,
  // Retry-After 10 saniye olduğu için de "bir geliyor bir gelmiyor" şeklinde
  // görünüyordu.
  //
  // Artık sayaç uygulama başına bir kez çekiliyor.
  private countLoaded = false;

  ensureCount(): void {
    // Sunucuda token okunamıyor (localStorage yok); SSR'da istek atmanın
    // anlamı yok.
    if (!this.isBrowser) return;

    if (!this.getToken()) {
      this.count.set(0);
      return;
    }

    if (this.countLoaded) return;

    // Bayrak subscribe'dan ÖNCE, senkron olarak set ediliyor. Üç bileşenin
    // ngOnInit'i aynı tick içinde çalışıyor; sonraya bırakılsaydı üçü de
    // "henüz yüklenmedi" görüp yine üç istek atardı.
    this.countLoaded = true;
    this.list().subscribe({
      // Başarısızsa bayrağı geri al ki bir sonraki gezinmede tekrar denensin.
      error: () => {
        this.countLoaded = false;
      },
    });
  }

  private refreshCount(): void {
    this.countLoaded = false;
    this.ensureCount();
  }
}
