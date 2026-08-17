import { Component, OnInit, inject, signal } from '@angular/core';
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

  protected readonly favoritesCount = signal(0);

  ngOnInit(): void {
    this.favoritesService.list().subscribe((list) => this.favoritesCount.set(list.length));
  }
}
