import { efsaneCuma } from './kampanya';

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
