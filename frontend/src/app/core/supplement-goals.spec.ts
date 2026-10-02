import { describe, expect, it } from 'vitest';

import {
  QUIZ_FOLLOW_UPS,
  SUPPLEMENT_GOALS,
  findSupplementGoal,
  personalNotes,
  proteinTypeFor,
  validAnswers,
} from './supplement-goals';

const goal = (slug: string) => findSupplementGoal(slug)!;

describe('hedef yapılandırması', () => {
  // Hüküm satırındaki "Güncel fiyatlar" düğmesi aynı sayfadaki listeye
  // kaydırıyor; kimlik yazım hatası düğmeyi sessizce işlevsiz bırakırdı.
  it.each(SUPPLEMENT_GOALS.map((g) => [g.slug, g] as const))('%s: hüküm bağlantıları var olan listelere gidiyor', (_, g) => {
    const ids = new Set(g.picks.map((p) => p.id));
    for (const verdict of g.verdicts) {
      if (verdict.pickSectionId) expect(ids).toContain(verdict.pickSectionId);
    }
  });

  it.each(SUPPLEMENT_GOALS.map((g) => [g.slug, g] as const))('%s: en az bir öncelikli hüküm ve SSS var', (_, g) => {
    expect(g.verdicts.some((v) => v.level === 'oncelikli')).toBe(true);
    expect(g.faqs.length).toBeGreaterThan(0);
  });

  it('her dalın iki takip sorusu var (ilerleme göstergesi 3 adım diyor)', () => {
    expect(QUIZ_FOLLOW_UPS.protein).toHaveLength(2);
    expect(QUIZ_FOLLOW_UPS.dayaniklilik).toHaveLength(2);
  });
});

describe('validAnswers', () => {
  it('yalnızca hedefin dalındaki geçerli cevapları tutar', () => {
    const answers = validAnswers(goal('kas-kazanimi'), {
      protein: 'hayir',
      tercih: 'uydurma',
      sure: 'uzun',
      utm_source: 'x',
    });
    expect(answers).toEqual({ protein: 'hayir' });
  });

  it('dayanıklılık dalı protein sorularını kabul etmez', () => {
    expect(validAnswers(goal('dayaniklilik'), { protein: 'evet', sure: 'kisa' })).toEqual({ sure: 'kisa' });
  });
});

describe('proteinTypeFor', () => {
  it('süt tercihini liste türüne çevirir', () => {
    expect(proteinTypeFor({ tercih: 'laktozsuz' })).toBe('izole');
    expect(proteinTypeFor({ tercih: 'bitkisel' })).toBe('bitkisel');
    expect(proteinTypeFor({ tercih: 'farketmez' })).toBeNull();
    expect(proteinTypeFor({})).toBeNull();
  });
});

describe('personalNotes', () => {
  it('cevap yoksa not üretmez', () => {
    expect(personalNotes(goal('kas-kazanimi'), {})).toEqual([]);
  });

  it('proteini yemekle tutturan kişiye kreatini öne alır', () => {
    const notes = personalNotes(goal('kas-kazanimi'), { protein: 'evet' });
    expect(notes).toHaveLength(1);
    expect(notes[0].text).toContain('kreatine');
  });

  it('"bilmiyorum" diyene hesaplama aracını bağlar', () => {
    const notes = personalNotes(goal('yag-yakimi'), { protein: 'bilmiyorum' });
    expect(notes[0].link?.path).toBe('/hesaplama/protein-ihtiyaci');
  });

  it('her protein dalı hedefinde evet/hayır için metin var', () => {
    for (const g of SUPPLEMENT_GOALS.filter((x) => x.quizBranch === 'protein')) {
      for (const protein of ['evet', 'hayir']) {
        expect(personalNotes(g, { protein })[0]?.text.length ?? 0).toBeGreaterThan(0);
      }
    }
  });

  it('kısa antrenmanda jelin gerekmediğini ve terleyene elektroliti söyler', () => {
    const notes = personalNotes(goal('dayaniklilik'), { sure: 'kisa', terleme: 'evet' }).map((n) => n.text);
    expect(notes[0]).toContain('su yeterli');
    expect(notes[1]).toContain('elektrolit');
  });

  it('süt tercihini listeye uygulandığını söyler', () => {
    const notes = personalNotes(goal('kilo-alma'), { protein: 'hayir', tercih: 'bitkisel' }).map((n) => n.text);
    expect(notes).toHaveLength(2);
    expect(notes[1]).toContain('bitkisel');
  });
});
