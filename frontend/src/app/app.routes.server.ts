import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // YONETIM PANELI SUNUCUDA RENDER EDILMIYOR.
    //
    // SSR sunucusu ziyaretcinin oturum cerezini tasimiyor; sunucuda render
    // edilseydi paneldeki her istek 401 doner ve panel HER ACILISTA "yetkisiz"
    // ekraniyla gelirdi. Ayrica burasi arama motoruna hic gorunmemesi gereken
    // bir arac ekrani - sunucuda uretilmis HTML'e ihtiyaci yok.
    path: 'yonetim',
    renderMode: RenderMode.Client,
  },
  {
    // Fiyatlar sık değiştiği için build-anında statik prerender yerine
    // her istekte taze veriyle sunucu tarafında render ediyoruz.
    path: '**',
    renderMode: RenderMode.Server,
  },
];
