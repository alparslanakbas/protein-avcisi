import { environment } from '../../environments/environment';

// Site artık iki domain'de erişilebilir (gerçek domain + eski onrender.com
// alt domain'i, geriye dönük uyumluluk için bilinçli olarak açık bırakıldı) —
// Google'ın bunu "duplicate content" sanmaması için hangi domain'den
// geldiğine bakılmaksızın hep GERÇEK domain'e işaret eden bir <link
// rel="canonical"> ekliyoruz. environment.canonicalOrigin production'da
// sabit gerçek domain, yerel geliştirmede null (o zaman document.location
// kullanılır, localhost testinde yanlış URL üretmesin diye).
export function canonicalOrigin(document: Document): string {
  return environment.canonicalOrigin ?? document.location.origin;
}

export function setCanonicalLink(document: Document, path: string): void {
  let link = document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  if (!link) {
    link = document.createElement('link');
    link.rel = 'canonical';
    document.head.appendChild(link);
  }
  link.href = `${canonicalOrigin(document)}${path}`;
}
