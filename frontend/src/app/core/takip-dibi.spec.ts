import { takipBaslangici, takipDibiAciklamasi, takipDibiEtiketi } from './takip-dibi';

describe('takipDibiEtiketi', () => {
  const ekim = new Date('2026-10-07T03:00:00Z');

  it('başlangıcı gün ve ekli ay adıyla söylüyor', () => {
    expect(takipDibiEtiketi('2026-08-10T19:41:35.790415+00:00', ekim)).toBe("10 Ağustos'tan beri en düşük");
    expect(takipDibiEtiketi('2026-09-05T08:00:00Z', ekim)).toBe("5 Eylül'den beri en düşük");
  });

  it('günü Türkiye saatine göre alıyor', () => {
    // 31 Ağustos 22:30 UTC = 1 Eylül 01:30 TSİ.
    expect(takipDibiEtiketi('2026-08-31T22:30:00Z', ekim)).toBe("1 Eylül'den beri en düşük");
  });

  it('bir yılı geçen takipte yılı doğru ekle yazıyor', () => {
    expect(takipDibiEtiketi('2026-08-10T19:41:35Z', new Date('2027-09-01T00:00:00Z'))).toBe(
      "10 Ağustos 2026'dan beri en düşük",
    );
    expect(takipDibiEtiketi('2027-03-05T09:00:00Z', new Date('2028-04-01T00:00:00Z'))).toBe(
      "5 Mart 2027'den beri en düşük",
    );
    expect(takipDibiEtiketi('2030-01-15T09:00:00Z', new Date('2031-02-01T00:00:00Z'))).toBe(
      "15 Ocak 2030'dan beri en düşük",
    );
    expect(takipDibiEtiketi('2033-12-01T09:00:00Z', new Date('2035-01-01T00:00:00Z'))).toBe(
      "1 Aralık 2033'ten beri en düşük",
    );
  });

  it('bir yıldan kısa takipte önceki yılın ayını yılsız yazıyor', () => {
    expect(takipDibiEtiketi('2026-08-10T19:41:35Z', new Date('2027-02-01T00:00:00Z'))).toBe(
      "10 Ağustos'tan beri en düşük",
    );
  });
});

describe('takipDibiAciklamasi', () => {
  it('tam tarihi ek gerektirmeyen cümleyle veriyor', () => {
    expect(takipBaslangici('2026-08-10T19:41:35Z')).toBe('10 Ağustos 2026');
    expect(takipDibiAciklamasi('2026-08-10T19:41:35Z')).toBe(
      'Fiyatını 10 Ağustos 2026 tarihinden beri takip ediyoruz; bugünkü fiyat o günden beri gördüğümüz en düşük fiyat.',
    );
  });
});
