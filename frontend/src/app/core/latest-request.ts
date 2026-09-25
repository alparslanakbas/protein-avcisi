import { Observable, Observer, Subscription } from 'rxjs';

/**
 * Yalnızca EN SON isteğin yanıtını kabul eder: yeni istek başlayınca önceki
 * iptal ediliyor (HTTP isteği de kesiliyor), yani onun yanıtı hiç işlenmiyor.
 *
 * NEDEN (güvenlik/mimari incelemesi, 26 Eylül): liste sayfaları her filtre
 * değişiminde yeni istek atıp öncekini bırakıyordu. Filtre hızlı değişince
 * yavaş gelen ESKİ yanıt yenisinin üstüne yazabiliyordu: ekranda seçili süzgeç
 * başka, listedeki ürünler başka. `switchMap`'in yaptığı iş; sayfalar isteği
 * akış yerine metot çağrısıyla başlattığı için küçük bir sınıf olarak.
 */
export class LatestRequest {
  private active?: Subscription;

  run<T>(request$: Observable<T>, observer: Partial<Observer<T>>): void {
    this.active?.unsubscribe();
    this.active = request$.subscribe(observer);
  }
}
