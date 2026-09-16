import { API_BASE_URL } from './api.config';
import { toInternalApiUrl } from './internal-api';

describe('toInternalApiUrl', () => {
  it('iç adres yoksa dokunmuyor (yerel geliştirme, tarayıcı)', () => {
    expect(toInternalApiUrl(`${API_BASE_URL}/api/deals`, null)).toBe(`${API_BASE_URL}/api/deals`);
  });

  it('API adresinin önekini iç adresle değiştiriyor, yol ve sorgu korunuyor', () => {
    expect(toInternalApiUrl(`${API_BASE_URL}/api/deals?page=2`, 'http://backend:8080/')).toBe(
      'http://backend:8080/api/deals?page=2',
    );
  });

  it('başka adreslere ve öneki yalnızca benzeyen hostlara dokunmuyor', () => {
    expect(toInternalApiUrl('https://example.com/api/deals', 'http://backend:8080')).toBe('https://example.com/api/deals');
    expect(toInternalApiUrl(`${API_BASE_URL}.evil.test/x`, 'http://backend:8080')).toBe(`${API_BASE_URL}.evil.test/x`);
    expect(toInternalApiUrl('/yonetim/api/durum', 'http://backend:8080')).toBe('/yonetim/api/durum');
  });
});
