import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { DealsService } from './core/deals.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly dealsService = inject(DealsService);

  protected readonly currentYear = new Date().getFullYear();
  protected readonly brands = signal<string[]>([]);

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => this.brands.set(options.brands));
  }
}
