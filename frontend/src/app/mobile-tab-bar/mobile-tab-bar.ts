import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { FavoritesService } from '../core/favorites.service';

// Nocturne tasarımının kendi kapsamındaki mobil alt sekme çubuğu — dar
// ekranlarda (sm altı) sabit, header'daki nav linklerinin küçük bir alt
// kümesini (en sık gidilen 4 hedef) her sayfada erişilebilir kılıyor.
// Masaüstünde tamamen gizli (`sm:hidden`), header'daki nav zaten yeterli.
@Component({
  selector: 'app-mobile-tab-bar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './mobile-tab-bar.html',
})
export class MobileTabBar implements OnInit {
  private readonly favoritesService = inject(FavoritesService);

  // Servisteki paylaşılan signal'e doğrudan referans — favori eklenince/
  // çıkarılınca (bu sayfadan ya da başka bir sayfadan) otomatik güncellenir.
  protected readonly favoritesCount = this.favoritesService.count;

  ngOnInit(): void {
    this.favoritesService.ensureCount();
  }
}
