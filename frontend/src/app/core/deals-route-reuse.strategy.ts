import { ActivatedRouteSnapshot, DetachedRouteHandle, RouteReuseStrategy } from '@angular/router';

// DealsList bileşeni hem '' hem 'urun/:id' route'unda kullanılıyor (ürün
// modalı artık URL'e bağlı). Angular'ın varsayılan stratejisi farklı
// route config'lere geçişte bileşeni yok edip yeniden kurar — bu da filtre/
// sayfa/arama durumunu sıfırlardı. Aynı bileşene işaret eden route'lar
// arasında geçişte örneği koruyoruz, sadece route param'ları güncellenir.
export class DealsRouteReuseStrategy implements RouteReuseStrategy {
  shouldDetach(): boolean {
    return false;
  }

  store(): void {}

  shouldAttach(): boolean {
    return false;
  }

  retrieve(): DetachedRouteHandle | null {
    return null;
  }

  shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    if (future.routeConfig === curr.routeConfig) return true;
    return !!future.component && future.component === curr.component;
  }
}
