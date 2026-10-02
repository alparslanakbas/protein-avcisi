import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { combineLatest } from 'rxjs';

import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { showNotFound } from '../core/not-found-navigation';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import {
  FINDER_PATH,
  SUPPLEMENT_GOALS,
  SupplementGoal,
  VERDICT_LABELS,
  VerdictLevel,
  findSupplementGoal,
  personalNotes,
  proteinTypeFor,
  validAnswers,
} from '../core/supplement-goals';
import { SiteHeader } from '../site-header/site-header';
import { ValuePickList } from '../value-pick-list/value-pick-list';

const LEVELS: { level: VerdictLevel; icon: string }[] = [
  { level: 'oncelikli', icon: 'ph-check-circle' },
  { level: 'istege-bagli', icon: 'ph-circle-dashed' },
  { level: 'gerek-yok', icon: 'ph-minus-circle' },
];

// "Hangi takviye?" testinin sonuç sayfası — hedef başına bir SSR sayfası
// (bkz. core/supplement-goals.ts). Testten gelindiyse cevaplar adres
// parametresinde; sayfa onlara göre kısa bir özet ekliyor ve protein
// listesini daraltıyor. Canonical her zaman parametresiz adres.
@Component({
  selector: 'app-supplement-goal-page',
  imports: [RouterLink, SiteHeader, ValuePickList],
  templateUrl: './supplement-goal-page.html',
})
export class SupplementGoalPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  private breadcrumbEl: HTMLScriptElement | null = null;
  private faqEl: HTMLScriptElement | null = null;

  protected readonly finderPath = FINDER_PATH;
  protected readonly verdictLabels = VERDICT_LABELS;

  protected readonly goal = signal<SupplementGoal | null>(null);
  private readonly answers = signal<Record<string, string>>({});

  protected readonly notes = computed(() => {
    const goal = this.goal();
    return goal ? personalNotes(goal, this.answers()) : [];
  });
  protected readonly proteinType = computed(() => proteinTypeFor(this.answers()));

  /** Hüküm tablosunun satırları: yalnızca bu hedefte karşılığı olan seviyeler. */
  protected readonly verdictGroups = computed(() => {
    const goal = this.goal();
    if (!goal) return [];
    return LEVELS.map(({ level, icon }) => ({
      level,
      icon,
      items: goal.verdicts.filter((verdict) => verdict.level === level),
    })).filter((group) => group.items.length > 0);
  });

  protected readonly otherGoals = computed(() => SUPPLEMENT_GOALS.filter((goal) => goal.slug !== this.goal()?.slug));

  ngOnInit(): void {
    const subscription = combineLatest([this.route.paramMap, this.route.queryParamMap]).subscribe(([params, query]) => {
      const goal = findSupplementGoal(params.get('slug') ?? '');
      if (!goal) {
        showNotFound(this.router);
        return;
      }
      const raw: Record<string, string | undefined> = {};
      for (const key of query.keys) raw[key] = query.get(key) ?? undefined;

      this.answers.set(validAnswers(goal, raw));
      if (this.goal()?.slug !== goal.slug) {
        this.goal.set(goal);
        this.setMeta(goal);
      }
    });
    this.destroyRef.onDestroy(() => {
      subscription.unsubscribe();
      this.breadcrumbEl?.remove();
      this.faqEl?.remove();
    });
  }

  // Hüküm satırından aynı sayfadaki ürün listesine. Çapa bağlantısı değil:
  // <base href="/"> yüzünden "#kreatin" ana sayfaya gider, router'da da çapa
  // kaydırması kapalı.
  protected scrollToPicks(sectionId: string): void {
    const target = this.document.getElementById(sectionId);
    if (!target) return;
    const reduceMotion = this.document.defaultView?.matchMedia('(prefers-reduced-motion: reduce)').matches ?? true;
    target.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
  }

  /** Liste bileşeni yalnızca açılışta türü okuyor; tür değişince yeniden kurulsun. */
  protected pickTrackKey(sectionId: string): string {
    return `${sectionId}:${this.proteinType() ?? ''}`;
  }

  private setMeta(goal: SupplementGoal): void {
    const path = `${FINDER_PATH}/${goal.slug}`;
    this.pageMeta.set({
      title: goal.metaTitle,
      description: goal.metaDescription,
      canonicalPath: path,
    });

    this.breadcrumbEl = upsertJsonLdScript(
      this.document,
      this.breadcrumbEl,
      buildBreadcrumbJsonLd(this.document, [
        { name: 'Ana Sayfa', path: '/' },
        { name: 'Hangi takviye?', path: FINDER_PATH },
        { name: goal.label, path },
      ]),
    );

    this.faqEl = upsertJsonLdScript(this.document, this.faqEl, {
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: goal.faqs.map((item) => ({
        '@type': 'Question',
        name: item.question,
        acceptedAnswer: { '@type': 'Answer', text: item.answer },
      })),
    });
  }
}
