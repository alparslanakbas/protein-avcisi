import { formatRelativeTime } from './relative-time';

describe('formatRelativeTime', () => {
  const minutesAgo = (m: number) => new Date(Date.now() - m * 60_000).toISOString();
  const hoursAgo = (h: number) => new Date(Date.now() - h * 60 * 60_000).toISOString();
  const daysAgo = (d: number) => new Date(Date.now() - d * 24 * 60 * 60_000).toISOString();

  it('1 dakikadan az önceyse "az önce" döner', () => {
    expect(formatRelativeTime(new Date().toISOString())).toBe('az önce');
  });

  it('dakika cinsinden gösterir', () => {
    expect(formatRelativeTime(minutesAgo(5))).toBe('5 dakika önce');
  });

  it('saat cinsinden gösterir', () => {
    expect(formatRelativeTime(hoursAgo(3))).toBe('3 saat önce');
  });

  it('tam 1 gün önceyse "dün" döner', () => {
    expect(formatRelativeTime(daysAgo(1))).toBe('dün');
  });

  it('2-6 gün arasında "X gün önce" döner', () => {
    expect(formatRelativeTime(daysAgo(3))).toBe('3 gün önce');
  });

  it('7 günden eskiyse mutlak tarihe (gün+ay) düşer', () => {
    const result = formatRelativeTime(daysAgo(10));

    expect(result).not.toContain('önce');
    expect(result).not.toBe('dün');
  });
});
