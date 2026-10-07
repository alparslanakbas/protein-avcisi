import { upsertJsonLdScript } from './page-meta.service';

describe('upsertJsonLdScript', () => {
  // Mağazadan kazınan bir ürün adı "</script>" içerirse SSR etiketi kapatıp
  // sonrasını sayfada çalıştırıyordu; yönetim paneli aynı origin'de olduğu
  // için yöneticinin oturumuyla (güvenlik incelemesi, 25 Eylül).
  it('ürün adındaki </script> etiketi kapatamaz', () => {
    const ad = 'X </script><script>alert(1)</script>';

    const el = upsertJsonLdScript(document, null, { name: ad });

    expect(document.head.innerHTML).not.toContain('</script><script>');
    expect(el.textContent).not.toContain('<');
    expect(JSON.parse(el.textContent!).name).toBe(ad);
    el.remove();
  });

  // Hidrasyon (7 Ekim): bileşen istemcide referanssız yeniden kuruluyor; sunucunun yazdığı bloğun yanına ikincisi
  // ekleniyordu (canlıda her blok iki kez). İlk çağrı sunucu, ikincisi istemci.
  describe('aynı türden blok', () => {
    const bloklar = () => [...document.head.querySelectorAll('script[type="application/ld+json"]')];
    afterEach(() => bloklar().forEach((b) => b.remove()));

    it('referans olmadan da tekrar eklenmiyor, var olan güncelleniyor', () => {
      const sunucu = upsertJsonLdScript(document, null, { '@type': 'BreadcrumbList', ad: 'eski' });
      const istemci = upsertJsonLdScript(document, null, { '@type': 'BreadcrumbList', ad: 'yeni' });

      expect(istemci).toBe(sunucu);
      expect(bloklar()).toHaveLength(1);
      expect(JSON.parse(istemci.textContent!).ad).toBe('yeni');
    });

    it('farklı türler ayrı blok', () => {
      upsertJsonLdScript(document, null, { '@type': 'BreadcrumbList' });
      upsertJsonLdScript(document, null, { '@type': 'FAQPage' });

      expect(bloklar().map((b) => b.getAttribute('data-ld'))).toEqual(['BreadcrumbList', 'FAQPage']);
    });

    it('türü olmayan veri eskisi gibi her çağrıda yeni blok', () => {
      upsertJsonLdScript(document, null, [{ '@type': 'Product' }]);
      upsertJsonLdScript(document, null, [{ '@type': 'Product' }]);

      expect(bloklar()).toHaveLength(2);
    });

    it('kaldırılan bloğun yerine yenisi açılıyor', () => {
      upsertJsonLdScript(document, null, { '@type': 'Product' }).remove();

      const yeni = upsertJsonLdScript(document, null, { '@type': 'Product' });

      expect(yeni.isConnected).toBe(true);
      expect(bloklar()).toHaveLength(1);
    });
  });
});
