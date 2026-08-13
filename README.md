# ProteinAvcısı

🔗 **Canlı site:** [proteinavcisi.com.tr](https://proteinavcisi.com.tr)
*(ücretsiz katmanda barınıyor — birkaç dakika hareketsiz kalırsa ilk istekte uyanması
biraz zaman alabilir)*

Türkiye'deki spor takviyesi / protein tozu markalarının indirimlerini otomatik olarak
takip edip tek bir sayfada listeleyen bir web sitesi.

Markaların kendi beyan ettiği "eski fiyat / yeni fiyat" bilgisine değil, düzenli olarak
toplanan **gerçek fiyat geçmişine** dayanan bir indirim tespiti yapıyor.

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
- **Paylaşım** — site veya tekil ürün linkini WhatsApp/X/Facebook üzerinden veya
  doğrudan linki kopyalayarak paylaşma (mobilde native paylaşım penceresi).
- **SSR (Angular Universal) + SEO** — her ürün kendi URL'ine sahip (`/urun/:id`),
  dinamik title/meta/Open Graph, schema.org `Product`/`Offer` structured data,
  dinamik `sitemap.xml` ve `robots.txt`.
- **Açık/koyu/sistem tema desteği.**

## Tech Stack

**Backend**
- .NET 10, ASP.NET Core minimal API
- PostgreSQL + EF Core
- HtmlAgilityPack (HTML parse), doğrudan `HttpClient` tabanlı scraper'lar (JSON/GraphQL
  API'ler dahil — hiçbiri tarayıcı otomasyonu gerektirmiyor)

**Frontend**
- Angular 22 (standalone components, Signals)
- Angular SSR (Universal) + hydration
- Tailwind CSS v4

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

## Deployment

- **Backend:** [Render](https://render.com) (Docker, ücretsiz katman)
- **Frontend:** [Render](https://render.com) (Node/SSR, ücretsiz katman)
- **Veritabanı:** [Neon](https://neon.tech) (serverless PostgreSQL, ücretsiz katman)
- **Uptime:** [UptimeRobot](https://uptimerobot.com) ile periyodik ping (ücretsiz katmanın
  uykuya geçmesini önlemek için)

## Yerel Çalıştırma

**Backend**
```bash
cd backend/src/IndirimTakip.Api
dotnet user-secrets set "ConnectionStrings:Default" "<postgres-bağlantı-dizeniz>"
dotnet run
```

**Frontend**
```bash
cd frontend
npm install
npm start
```

Varsayılan olarak backend `http://localhost:5156`, frontend `http://localhost:4200`
üzerinde çalışır.
