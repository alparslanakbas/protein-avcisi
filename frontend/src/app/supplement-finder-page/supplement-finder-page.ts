import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import {
  FINDER_PATH,
  GOAL_QUESTION,
  QUIZ_FOLLOW_UPS,
  QuizOption,
  SUPPLEMENT_GOALS,
  SupplementGoal,
  findSupplementGoal,
} from '../core/supplement-goals';
import { SiteHeader } from '../site-header/site-header';

interface QuizStep {
  key: string;
  question: string;
  options: QuizOption[];
}

// Hedef dışındaki sorular hedefe göre dallanıyor ama sayısı her dalda iki;
// ilerleme göstergesi bu yüzden baştan "3 adım" diyebiliyor.
const STEP_COUNT = 3;

// "Hangi takviyeyi seçmeliyim?" — adım adım test. Test tamamen tarayıcıda;
// sonuç hedef başına bir SSR sayfası (bkz. core/supplement-goals.ts). SSR
// çıktısında ilk soru ve dört hedef sayfasının bağlantıları var: sayfa
// JavaScript'siz de işe yarar, arama motoru da hedef sayfalarını buradan bulur.
@Component({
  selector: 'app-supplement-finder-page',
  imports: [RouterLink, SiteHeader],
  templateUrl: './supplement-finder-page.html',
})
export class SupplementFinderPage implements OnInit {
  private readonly router = inject(Router);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private breadcrumbEl: HTMLScriptElement | null = null;

  private readonly questionHeading = viewChild<ElementRef<HTMLElement>>('questionHeading');

  protected readonly finderPath = FINDER_PATH;
  protected readonly goals = SUPPLEMENT_GOALS;
  protected readonly stepCount = STEP_COUNT;
  protected readonly stepIndexes = Array.from({ length: STEP_COUNT }, (_, i) => i);

  private readonly goal = signal<SupplementGoal | null>(null);
  private readonly answers = signal<Record<string, string>>({});
  /** 0 = hedef sorusu. */
  protected readonly stepIndex = signal(0);

  protected readonly step = computed<QuizStep>(() => {
    const index = this.stepIndex();
    const goal = this.goal();
    if (index === 0 || !goal) {
      return {
        key: 'hedef',
        question: GOAL_QUESTION,
        options: SUPPLEMENT_GOALS.map((g) => ({ value: g.slug, label: g.label, description: g.description, icon: g.icon })),
      };
    }
    return QUIZ_FOLLOW_UPS[goal.quizBranch][index - 1];
  });

  protected readonly selected = computed(() => {
    const step = this.step();
    return step.key === 'hedef' ? (this.goal()?.slug ?? null) : (this.answers()[step.key] ?? null);
  });

  protected readonly isLastStep = computed(() => this.stepIndex() === STEP_COUNT - 1);

  ngOnInit(): void {
    this.pageMeta.set({
      title: 'Hangi Takviyeyi Seçmeliyim? 3 Soruluk Test | ProteinAvcısı',
      description:
        'Üç soruda hedefine göre gerçekten işe yarayan takviyeleri öğren: kas, kilo alma, yağ yakımı ve dayanıklılık. Gerekmeyenler ve kilogram fiyatı en uygun ürünler.',
      canonicalPath: FINDER_PATH,
    });
    this.breadcrumbEl = upsertJsonLdScript(
      this.document,
      this.breadcrumbEl,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Hangi takviye?', path: FINDER_PATH },
      ]),
    );
    this.destroyRef.onDestroy(() => this.breadcrumbEl?.remove());
  }

  protected choose(value: string): void {
    const step = this.step();
    if (step.key === 'hedef') {
      const goal = findSupplementGoal(value) ?? null;
      // Hedef değişirse sonraki soruların cevapları başka bir dala ait olabilir.
      if (goal?.slug !== this.goal()?.slug) this.answers.set({});
      this.goal.set(goal);
    } else {
      this.answers.update((answers) => ({ ...answers, [step.key]: value }));
    }
  }

  protected next(): void {
    const goal = this.goal();
    if (!this.selected() || !goal) return;

    if (this.isLastStep()) {
      void this.router.navigate([FINDER_PATH, goal.slug], { queryParams: this.answers() });
      return;
    }
    this.stepIndex.update((index) => index + 1);
    this.focusQuestion();
  }

  protected back(): void {
    if (this.stepIndex() === 0) return;
    this.stepIndex.update((index) => index - 1);
    this.focusQuestion();
  }

  // Adım değişince odak yeni soruya: ekran okuyucu soruyu okusun, klavyeyle
  // gelen kişi seçeneklerin başında olsun.
  private focusQuestion(): void {
    setTimeout(() => this.questionHeading()?.nativeElement.focus());
  }
}
