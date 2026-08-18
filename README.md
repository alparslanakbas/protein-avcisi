# ProteinAvcısı

🔗 **Canlı site:** [proteinavcisi.com.tr](https://proteinavcisi.com.tr)

## Amaç

Türkiye'deki spor takviyesi / protein tozu markaları sitelerinde sürekli "indirim"
etiketleri gösteriyor — ama bu "eski fiyat" genellikle markanın kendi beyanı,
bağımsız doğrulanmış bir şey değil. ProteinAvcısı, günde birkaç kez otomatik
topladığı gerçek fiyat geçmişine bakıp bir indirimin gerçekten indirim mi yoksa
sadece etiket mi olduğunu gösteriyor.

Kısacası: markanın söylediğine değil, biriktirdiği veriye güveniyor.

## Özellikler

- **Çoklu marka desteği** — HIQ, SSN, Hardline, ProteinOcean; her marka kendi scraper
  implementasyonuna sahip (`IBrandScraper`), ortak bir arayüz üzerinden.
- **Gerçek fiyat geçmişi** — her tarama bir `PriceHistory` kaydı bırakıyor, indirim
  tespiti bu geçmişe dayanıyor.
- **Mağaza kampanyaları** — markanın kendi beyan ettiği indirim, "doğrulanmamış" olarak
  ayrı gösteriliyor.
- **Kupon kodları** — elle doğrulanmış, güncel kampanya kodları.
- **Sunucu taraflı arama/filtre/sayfalama/sıralama** — marka, kategori, fiyat aralığı,
  serbest metin arama; fiyata veya isme göre sıralama.
- **Ürün detay + fiyat grafiği** — zaman aralığı seçilebilir (7 gün / 15 gün / 1 ay /
  6 ay / 1 yıl), hover tooltip'li elle çizilmiş SVG grafik.
- **Favoriler + fiyat alarmı** — hesap gerektirmeden, sadece e-posta ile ürün takibi ve
  fiyat düşünce bildirim.
- **E-posta bülteni** — haftalık öne çıkan indirimler özeti (double opt-in).
- **Paylaşım** — site veya tekil ürün linkini WhatsApp/X/Facebook üzerinden veya
  doğrudan linki kopyalayarak paylaşma (mobilde native paylaşım penceresi).
- **SSR (Angular Universal) + SEO** — her ürün kendi URL'ine sahip (`/urun/:id`),
  dinamik title/meta/Open Graph, schema.org `Product`/`Offer` structured data,
  dinamik `sitemap.xml` ve `robots.txt`.
- **PWA desteği** — ana ekrana eklenebilir, yüklenebilir uygulama.
- **Açık/koyu/sistem tema desteği.**

## Kullanılan Teknolojiler ve Servisler

**Backend**
- .NET 10, ASP.NET Core minimal API
- PostgreSQL + EF Core
- HtmlAgilityPack (HTML parse) + doğrudan `HttpClient` tabanlı scraper'lar (bazı
  markalarda JSON/GraphQL API'leri) — hiçbiri tarayıcı otomasyonu gerektirmiyor
- [Brevo](https://brevo.com) — transactional e-posta (bülten, fiyat düşüş bildirimi,
  favori listesi kurtarma linki)

**Frontend**
- Angular 22 (standalone components, Signals)
- Angular SSR (Universal) + hydration
- Tailwind CSS v4
- PWA (manifest + service worker)

**Altyapı**
- [Render](https://render.com) — backend (Docker) + frontend (Node/SSR) hosting
- [Neon](https://neon.tech) — serverless PostgreSQL
- [Cloudflare](https://cloudflare.com) — DNS, CDN, edge güvenlik kuralları
- [UptimeRobot](https://uptimerobot.com) — periyodik uptime izleme

**SEO / Görünürlük**
- Google Search Console, Bing Webmaster Tools, Yandex Webmaster
- Dinamik `sitemap.xml`, schema.org structured data (`Product`/`Offer`/`FAQPage`)

**Kalite / Test**
- Backend: xUnit (saf mantık — kategori/fiyat ayrıştırma)
- Frontend: Vitest (Angular'ın kendi unit test runner'ı)
- [TestSprite](https://www.testsprite.com) — AI destekli otomatik API/uçtan uca test
  üretimi ve çalıştırma (MCP üzerinden, geliştirme ortamına karşı)

## Proje Yapısı

```
backend/
  src/
    IndirimTakip.Api/            ASP.NET Core Web API (endpoint'ler)
    IndirimTakip.Core/           Entity'ler, IBrandScraper arayüzü
    IndirimTakip.Infrastructure/ EF Core, scraper implementasyonları, servisler
frontend/
  src/
    app/
      core/                      Servisler, modeller
      deals-list/                Ana liste + filtre + sayfalama
      product-modal/             Ürün detay modalı + fiyat grafiği
      share-button/              Paylaşım butonu (WhatsApp/X/Facebook/link kopyala)
```
