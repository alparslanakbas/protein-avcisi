import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CookieConsentService } from '../core/cookie-consent.service';

@Component({
  selector: 'app-cookie-consent-banner',
  imports: [RouterLink],
  templateUrl: './cookie-consent-banner.html',
})
export class CookieConsentBanner {
  protected readonly consent = inject(CookieConsentService);
}
