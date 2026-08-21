import { Component, inject, signal } from '@angular/core';

import { AppUpdateService } from '../core/app-update.service';

@Component({
  selector: 'app-update-banner',
  templateUrl: './update-banner.html',
})
export class UpdateBanner {
  protected readonly updates = inject(AppUpdateService);
  // Kalıcı değil (localStorage yok) — kapatılsa bile bu gerçek bir yeni
  // versiyon, kullanıcı sayfayı bir dahaki ziyaretinde/sekme yenilemesinde
  // zaten güncel sürümü alacak. Sadece o anki oturumda "sonra hatırlat"
  // gibi davransın diye component-seviyesinde bir signal yeterli.
  protected readonly dismissed = signal(false);
}
