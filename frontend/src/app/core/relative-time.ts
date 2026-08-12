const ABSOLUTE_DATE_FORMATTER = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short' });

// Sunucu/istemci farkı yok — sadece Date matematiği, SSR'da da güvenli.
// Sekmede uzun süre açık kalan bir sayfada metin bayatlayabilir (canlı
// güncellenmiyor) ama "son kontrol ne zamandı" bilgisi için bu kadarı
// MVP'de yeterli, saniyede bir tekrar hesaplayan bir zamanlayıcıya gerek yok.
export function formatRelativeTime(isoDate: string): string {
  const diffMs = Date.now() - new Date(isoDate).getTime();
  const diffMin = Math.floor(diffMs / 60_000);

  if (diffMin < 1) return 'az önce';
  if (diffMin < 60) return `${diffMin} dakika önce`;

  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour} saat önce`;

  const diffDay = Math.floor(diffHour / 24);
  if (diffDay === 1) return 'dün';
  if (diffDay < 7) return `${diffDay} gün önce`;

  return ABSOLUTE_DATE_FORMATTER.format(new Date(isoDate));
}
