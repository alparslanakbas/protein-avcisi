import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { CATEGORY_LABELS } from './core/category-labels';
import { DealsService } from './core/deals.service';
import { NewsletterSignup } from './newsletter-signup/newsletter-signup';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, NewsletterSignup],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly dealsService = inject(DealsService);

  protected readonly currentYear = new Date().getFullYear();
  protected readonly brands = signal<string[]>([]);
  protected readonly categories = signal<{ slug: string; label: string }[]>([]);

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.brands.set(options.brands);
      this.categories.set(options.categories.map((slug) => ({ slug, label: CATEGORY_LABELS[slug] ?? slug })));
    });
  }
}
