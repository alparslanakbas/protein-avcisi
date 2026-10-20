import { efsaneCuma, sezonGeriSayimi } from './kampanya';

describe('efsaneCuma', () => {
  it("Kasım'ın son cumasını veriyor", () => {
    expect(efsaneCuma(2025).toISOString().slice(0, 10)).toBe('2025-11-28');
    expect(efsaneCuma(2026).toISOString().slice(0, 10)).toBe('2026-11-27');
    expect(efsaneCuma(2027).toISOString().slice(0, 10)).toBe('2027-11-26');
  });

  it('30 Kasım cumaysa o günü veriyor', () => {
    // 30 Kasım 2029 cuma.
    expect(efsaneCuma(2029).toISOString().slice(0, 10)).toBe('2029-11-30');
    expect(efsaneCuma(2029).getUTCDay()).toBe(5);
  });
});

describe('sezonGeriSayimi', () => {
  // Türkiye UTC+3: 21:30 UTC, Türkiye'de ertesi günün 00:30'u.
  const an = (iso: string) => sezonGeriSayimi(new Date(iso));

  it('Ekim öncesi ve Efsane Cuma haftası bittikten sonra şerit yok', () => {
    expect(an('2026-09-30T12:00:00Z')).toBeNull();
    expect(an('2026-12-01T12:00:00Z')).toBeNull();
  });

  it('11.11 öncesi iki geri sayımı da veriyor', () => {
    expect(an('2026-10-20T12:00:00Z')).toEqual(["11.11'e 22 gün", "Efsane Cuma'ya 38 gün"]);
  });

  it('günü Türkiye saatiyle sayıyor', () => {
    // 10 Kasım 21:30 UTC = 11 Kasım 00:30 TSİ.
    expect(an('2026-11-10T21:30:00Z')).toEqual(['11.11 bugün', "Efsane Cuma'ya 16 gün"]);
  });

  it("11.11'den sonra yalnız Efsane Cuma, o gün ve hafta sonu ayrı metin", () => {
    expect(an('2026-11-12T12:00:00Z')).toEqual(["Efsane Cuma'ya 15 gün"]);
    expect(an('2026-11-27T12:00:00Z')).toEqual(['Efsane Cuma bugün']);
    expect(an('2026-11-30T12:00:00Z')).toEqual(['Efsane Cuma indirimleri sürüyor']);
  });
});
