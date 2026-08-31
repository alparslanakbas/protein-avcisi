import { filterSelectValue, readFilterSelection } from './filter-select';

describe('filterSelectValue', () => {
  it('filtre yokken placeholder seçeneğini gösterir', () => {
    expect(filterSelectValue(0)).toBe('');
  });

  it('REGRESYON: seçim sayısı değişince DEĞER de değişir', () => {
    // Değer sabit kalsaydı ikinci marka eklendiğinde Angular bağlamayı DOM'a
    // geri yazmaz, kutu son tıklanan markanın adında ("SSN") donup kalırdı.
    // Tarayıcıda birebir bu görüldü.
    expect(filterSelectValue(1)).not.toBe(filterSelectValue(2));
    expect(filterSelectValue(2)).not.toBe(filterSelectValue(3));
  });

  it('filtre varken placeholder ile ASLA karışmaz', () => {
    // Aynı olsalardı "Tüm markalar"ı seçmek bir değişiklik sayılmaz ve
    // tarayıcı change olayını hiç tetiklemezdi — kullanıcının bildirdiği hata.
    for (const n of [1, 2, 10]) expect(filterSelectValue(n)).not.toBe('');
  });
});

describe('readFilterSelection', () => {
  it('gerçek bir değer seçilince o filtreyi ekler/çıkarır', () => {
    expect(readFilterSelection('HIQ')).toEqual({ kind: 'toggle', value: 'HIQ' });
  });

  it('REGRESYON: boş değer "tümü" demektir, yok sayılmaz', () => {
    // Eski handler `if (value)` diyordu; "Tüm markalar" hiçbir şey yapmıyordu.
    expect(readFilterSelection('')).toEqual({ kind: 'clear' });
  });

  it('kutunun kendi durum seçeneği filtre olarak uygulanmaz', () => {
    // Aksi halde "3 marka seçili" diye bir marka filtresi eklenirdi.
    expect(readFilterSelection(filterSelectValue(3))).toEqual({ kind: 'ignore' });
  });
});
