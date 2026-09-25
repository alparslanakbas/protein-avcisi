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
});
