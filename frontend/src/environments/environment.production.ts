export const environment = {
  apiBaseUrl: 'https://protein-avcisi-api.onrender.com',
  // Site iki domain'de erişilebilir (gerçek domain + eski onrender.com alt
  // domain'i, geriye dönük uyumluluk için açık) — canonical URL'ler ve
  // paylaşım linkleri hangi domain'den geldiğine bakılmaksızın hep buraya
  // işaret etsin diye sabitlendi.
  canonicalOrigin: 'https://www.proteinavcisi.com.tr' as string | null,
};
