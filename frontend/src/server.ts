import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

import { API_BASE_URL } from './app/core/api.config';
import { slugify } from './app/core/slugify';
import { BODY_CALCULATORS } from './app/core/body-calculators';
import { SUPPLEMENT_DOSAGES } from './app/core/supplement-dosages';

const browserDistFolder = join(import.meta.dirname, '../browser');

// Site iki host'ta erişilebilir: gerçek domain (canonical) + eski
// protein-avcisi.onrender.com (geriye dönük uyumluluk için bilinçli açık
// bırakıldı, bkz. CLAUDE.md). Sitemap/robots ÖNCEDEN isteğin Host header'ından
// origin üretiyordu (`${req.protocol}://${req.get('host')}`) — bu da
// onrender.com'a gidildiğinde o adresin KENDİ sitemap'ini/robots'unu ilan
// etmesine yol açıyordu; Google bu ikinci kopyayı da tarayıp indeksleyebilir
// (canonical <link> tek başına bunu engellemez, botlar sitemap/robots'a
// canonical'dan bağımsız davranabilir). Artık sitemap/robots HER ZAMAN bu
// sabit origin'i kullanıyor, host'tan bağımsız.
const CANONICAL_HOST = 'www.proteinavcisi.com.tr';
const CANONICAL_ORIGIN = `https://${CANONICAL_HOST}`;

const app = express();
// Render'ın edge proxy'si (Cloudflare) TLS'i kendi ucunda sonlandırıp
// bize düz HTTP olarak iletiyor — bu ayar olmadan req.protocol her zaman
// "http" dönüyordu (X-Forwarded-Proto header'ı yok sayılıyordu), bu da
// sitemap.xml/robots.txt'teki tüm URL'lerin yanlışlıkla http:// ile
// üretilmesine yol açıyordu.
app.set('trust proxy', true);

// Canonical host DIŞINDA bir adresten (onrender.com, www'siz kök domain vb.)
// gelen HER isteğe noindex header'ı ekliyoruz — sadece sitemap/robots değil,
// Angular SSR'ın ürettiği TÜM sayfalar (ürün/kategori/marka/rehber) dahil.
// Kök domain zaten Render'da www'ye 301 atıyor (bu middleware'e hiç
// düşmüyor), asıl hedef onrender.com'un kendi kopyasının indekslenmesini
// engellemek.
app.use((req, res, next) => {
  if ((req.hostname || '').toLowerCase() !== CANONICAL_HOST) {
    res.setHeader('X-Robots-Tag', 'noindex, nofollow');
  }
  next();
});

const angularApp = new AngularNodeAppEngine();

interface SitemapEntry {
  id: number;
  name: string;
  lastScrapedAt: string;
  hasReviewContent: boolean;
}

interface FilterOptions {
  brands: string[];
  categories: string[];
}

interface ArticleSitemapEntry {
  slug: string;
  publishedAt: string;
}

interface BrandCategoryPair {
  brandName: string;
  category: string;
  productCount: number;
}

// Marka × kategori kesişim sayfası, ancak yeterince ürün varsa sitemap'e
// giriyor. 1-2 ürünlük bir sayfa Google için "ince içerik" — tam da
// kaçınmaya çalıştığımız şey (bkz. GSC "Tarandı ama dizine eklenmedi").
// Sayfanın kendisi yine erişilebilir, sadece taranmaya sunulmuyor.
const MIN_PRODUCTS_FOR_SITEMAP = 3;

// sitemap.xml ürün sayısına göre büyüyor, statik dosya olamaz — ham veriyi
// backend'den (/api/products/sitemap) çekip burada XML'e çeviriyoruz. Bu
// sunucu zaten kendi public origin'ini (req üzerinden) bildiği için domain'i
// ayrıca config'lemeye gerek yok.
app.get('/sitemap.xml', async (req, res) => {
  const origin = CANONICAL_ORIGIN;

  try {
    const [productsResponse, filtersResponse, articlesResponse, pairsResponse] = await Promise.all([
      fetch(`${API_BASE_URL}/api/products/sitemap`),
      fetch(`${API_BASE_URL}/api/filters`),
      fetch(`${API_BASE_URL}/api/articles`),
      fetch(`${API_BASE_URL}/api/brand-category-pairs`),
    ]);
    const products = (await productsResponse.json()) as SitemapEntry[];
    const filters = (await filtersResponse.json()) as FilterOptions;
    const articles = (await articlesResponse.json()) as ArticleSitemapEntry[];
    const brandCategoryPairs = (await pairsResponse.json()) as BrandCategoryPair[];

    const productUrls = products
      .map(
        (p) =>
          `<url><loc>${origin}/urun/${p.id}/${slugify(p.name)}</loc><lastmod>${new Date(p.lastScrapedAt).toISOString()}</lastmod><changefreq>daily</changefreq><priority>0.8</priority></url>`,
      )
      .join('');

    // Ürün incelemesi sayfaları — sadece gerçek bir içerik kaynağı (marka
    // açıklaması veya besin değeri tablosu) olan ürünler için (bkz. backend
    // HasReviewContent). Sayfa veri olmayan ürünler için de açık ama
    // sitemap'e "ince içerik" olarak sunulmuyor.
    const reviewUrls = products
      .filter((p) => p.hasReviewContent)
      .map(
        (p) =>
          `<url><loc>${origin}/urun-inceleme/${p.id}/${slugify(p.name)}</loc><lastmod>${new Date(p.lastScrapedAt).toISOString()}</lastmod><changefreq>weekly</changefreq><priority>0.6</priority></url>`,
      )
      .join('');

    // Marka indirim kodu sayfaları — "[Marka] indirim kodu" araması için
    // hedeflenen SEO içerik sayfaları.
    const brandUrls = filters.brands
      .map(
        (brand) =>
          `<url><loc>${origin}/marka/${brand.toLowerCase()}/indirim-kodu</loc><changefreq>daily</changefreq><priority>0.9</priority></url>`,
      )
      .join('');

    // Marka × kategori kesişimleri — "hardline protein tozu fiyatları"
    // tarzı aramalar için. Yalnızca ürünü olan (ve yeterince ürünü olan)
    // çiftler; liste backend'den geliyor, elle bakım gerektirmiyor.
    const brandCategoryUrls = brandCategoryPairs
      .filter((pair) => pair.productCount >= MIN_PRODUCTS_FOR_SITEMAP)
      .map(
        (pair) =>
          `<url><loc>${origin}/marka/${pair.brandName.toLowerCase()}/${pair.category}</loc><changefreq>daily</changefreq><priority>0.7</priority></url>`,
      )
      .join('');

    // Kategori sayfaları — "[kategori] fiyatları" gibi aramalar için.
    // /kategoriler, tüm kategorileri tek bir yerde listeleyen indeks sayfası.
    const categoryUrls =
      `<url><loc>${origin}/kategoriler</loc><changefreq>weekly</changefreq><priority>0.6</priority></url>` +
      filters.categories
        .map(
          (category) =>
            `<url><loc>${origin}/kategori/${category}</loc><changefreq>daily</changefreq><priority>0.8</priority></url>`,
        )
        .join('');

    const legalUrls =
      `<url><loc>${origin}/gizlilik-politikasi</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>` +
      `<url><loc>${origin}/cerez-politikasi</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>` +
      `<url><loc>${origin}/nasil-calisiyoruz</loc><changefreq>monthly</changefreq><priority>0.5</priority></url>` +
      `<url><loc>${origin}/hakkimizda</loc><changefreq>monthly</changefreq><priority>0.5</priority></url>` +
      `<url><loc>${origin}/sozluk</loc><changefreq>monthly</changefreq><priority>0.6</priority></url>` +
      // Hesaplama araçları — her biri kendi aramasını hedefliyor
      // ("kreatin dozu hesaplama" gibi), index sayfası da dahil.
      `<url><loc>${origin}/hesaplama</loc><changefreq>weekly</changefreq><priority>0.6</priority></url>` +
      `<url><loc>${origin}/hesaplama/protein-ihtiyaci</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>` +
      SUPPLEMENT_DOSAGES.map(
        (s) => `<url><loc>${origin}/hesaplama/${s.slug}</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>`,
      ).join('') +
      BODY_CALCULATORS.map(
        (c) => `<url><loc>${origin}/hesaplama/${c.slug}</loc><changefreq>monthly</changefreq><priority>0.7</priority></url>`,
      ).join('');

    // Marka karşılaştırma sayfaları — tüm marka ikilileri, alfabetik
    // sırayla (brand-comparison-page.ts'teki canonical URL mantığıyla
    // aynı) tek bir kanonik URL üretiliyor.
    const comparisonPairs: string[] = [];
    const sortedBrands = [...filters.brands].map((b) => b.toLowerCase()).sort();
    for (let i = 0; i < sortedBrands.length; i++) {
      for (let j = i + 1; j < sortedBrands.length; j++) {
        comparisonPairs.push(`${sortedBrands[i]}-vs-${sortedBrands[j]}`);
      }
    }
    const comparisonUrls = comparisonPairs
      .map((pair) => `<url><loc>${origin}/karsilastir/${pair}</loc><changefreq>weekly</changefreq><priority>0.6</priority></url>`)
      .join('');

    // Rehber yazıları — bilgi amaçlı SEO içerikleri, ürün/kategori
    // sayfalarıyla aynı önem seviyesinde (0.7).
    const articleUrls =
      `<url><loc>${origin}/rehber</loc><changefreq>weekly</changefreq><priority>0.6</priority></url>` +
      articles
        .map(
          (a) =>
            `<url><loc>${origin}/rehber/${a.slug}</loc><lastmod>${new Date(a.publishedAt).toISOString()}</lastmod><changefreq>monthly</changefreq><priority>0.7</priority></url>`,
        )
        .join('');

    const xml =
      `<?xml version="1.0" encoding="UTF-8"?>` +
      `<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">` +
      `<url><loc>${origin}/</loc><changefreq>hourly</changefreq><priority>1.0</priority></url>` +
      brandUrls +
      brandCategoryUrls +
      categoryUrls +
      productUrls +
      reviewUrls +
      legalUrls +
      articleUrls +
      comparisonUrls +
      `</urlset>`;

    res.set('Content-Type', 'application/xml');
    res.send(xml);
  } catch (error) {
    console.error('sitemap.xml üretilemedi:', error);
    res.status(502).send('Sitemap şu anda üretilemiyor.');
  }
});

// "Mağazaya Git" linki artık göreceli (/go/:id) — ziyaretçi bilinmeyen bir
// "api.protein-avcisi..." adresine değil, kendi gördüğü domain'e tıklıyor
// (dikkatli kullanıcılara phishing linki gibi görünüyordu). Burada backend'in
// gerçek /go/{id}'sine sunucu tarafında proxy yapıp aynı 302'yi iletiyoruz —
// tıklama sayacı backend'de aynen çalışmaya devam ediyor.
app.get('/go/:id', async (req, res) => {
  try {
    const response = await fetch(`${API_BASE_URL}/go/${req.params.id}`, { redirect: 'manual' });
    const location = response.headers.get('location');
    res.redirect(302, location ?? '/');
  } catch (error) {
    console.error('/go/:id yönlendirmesi başarısız:', error);
    res.redirect(302, '/');
  }
});

app.get('/robots.txt', (req, res) => {
  res.set('Content-Type', 'text/plain');
  // Canonical host DIŞINDaki bir adresten (onrender.com vb.) istek gelirse
  // tamamen farklı bir robots.txt — o kopyayı hiç taramaya açmıyoruz, kendi
  // sitemap'ini de ilan etmiyor. Yukarıdaki X-Robots-Tag header'ıyla birlikte
  // iki bağımsız sinyal (biri crawl'ı, biri index'i engelliyor).
  if ((req.hostname || '').toLowerCase() !== CANONICAL_HOST) {
    res.send('User-agent: *\nDisallow: /\n');
    return;
  }
  // /go/{id} affiliate yönlendirmeleri gerçek bir içerik sayfası değil,
  // dış siteye 302 atan bir uç nokta — botlar bunu ayrı bir "sayfa" gibi
  // taramaya/indekslemeye çalışmasın diye kapatıyoruz.
  res.send(`User-agent: *\nAllow: /\nDisallow: /go/\n\nSitemap: ${CANONICAL_ORIGIN}/sitemap.xml\n`);
});

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
