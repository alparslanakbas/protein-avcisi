import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { SubscribeService } from '../core/subscribe.service';

@Component({
  selector: 'app-newsletter-signup',
  imports: [FormsModule],
  templateUrl: './newsletter-signup.html',
})
export class NewsletterSignup {
  private readonly subscribeService = inject(SubscribeService);

  protected readonly email = signal('');
  protected readonly submitting = signal(false);
  protected readonly statusMessage = signal<string | null>(null);
  protected readonly statusIsError = signal(false);

  protected onSubscribe(): void {
    const value = this.email().trim();
    if (!value) return;

    this.submitting.set(true);
    this.statusMessage.set(null);

    this.subscribeService.subscribe(value).subscribe({
      next: (result) => {
        this.statusMessage.set(result.message);
        this.statusIsError.set(false);
        this.email.set('');
        this.submitting.set(false);
      },
      error: () => {
        this.statusMessage.set('Bir şeyler ters gitti, birazdan tekrar dener misin?');
        this.statusIsError.set(true);
        this.submitting.set(false);
      },
    });
  }
}
