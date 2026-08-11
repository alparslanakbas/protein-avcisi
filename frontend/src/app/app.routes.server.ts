import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    // Fiyatlar sık değiştiği için build-anında statik prerender yerine
    // her istekte taze veriyle sunucu tarafında render ediyoruz.
    path: '**',
    renderMode: RenderMode.Server,
  },
];
