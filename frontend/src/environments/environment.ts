export const environment = {
  apiBaseUrl: 'http://localhost:5156',
  // null: canonical URL üretimi document.location'a düşer (yerel testte
  // localhost'u yanlışlıkla production domain'i sanmasın diye).
  canonicalOrigin: null as string | null,
};
