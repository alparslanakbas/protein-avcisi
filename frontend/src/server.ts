import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';

import { API_BASE_URL } from './app/core/api.config';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
// Render'ın edge proxy'si (Cloudflare) TLS'i kendi ucunda sonlandırıp
// bize düz HTTP olarak iletiyor — bu ayar olmadan req.protocol her zaman
// "http" dönüyordu (X-Forwarded-Proto header'ı yok sayılıyordu), bu da
// sitemap.xml/robots.txt'teki tüm URL'lerin yanlışlıkla http:// ile
// üretilmesine yol açıyordu.
app.set('trust proxy', true);
const angularApp = new AngularNodeAppEngine();

// SsrInternalHeaderInterceptor'daki aynı gerekçe: bu sunucunun backend'e
// attığı istekler Cloudflare Bot Fight Mode'un atladığı bir header taşıyor.
// Angular'ın HttpClient'ı bunu interceptor'dan alıyor, burada elle fetch()
// çağıran uç noktalar (sitemap, /go/:id proxy) için aynı header'ı manuel ekliyoruz.
const ssrInternalHeaders: HeadersInit = process.env['SSR_INTERNAL_SECRET']
  ? { 'X-Internal-Request': process.env['SSR_INTERNAL_SECRET'] }
  : {};

interface SitemapEntry {
  id: number;
  lastScrapedAt: string;
}

interface FilterOptions {
  brands: string[];
  categories: string[];
}

interface ArticleSitemapEntry {
  slug: string;
  publishedAt: string;
}

// sitemap.xml ürün sayısına göre büyüyor, statik dosya olamaz — ham veriyi
// backend'den (/api/products/sitemap) çekip burada XML'e çeviriyoruz. Bu
// sunucu zaten kendi public origin'ini (req üzerinden) bildiği için domain'i
// ayrıca config'lemeye gerek yok.
app.get('/sitemap.xml', async (req, res) => {
  const origin = `${req.protocol}://${req.get('host')}`;

  try {
    const [productsResponse, filtersResponse, articlesResponse] = await Promise.all([
      fetch(`${API_BASE_URL}/api/products/sitemap`, { headers: ssrInternalHeaders }),
      fetch(`${API_BASE_URL}/api/filters`, { headers: ssrInternalHeaders }),
      fetch(`${API_BASE_URL}/api/articles`, { headers: ssrInternalHeaders }),
    ]);
    const products = (await productsResponse.json()) as SitemapEntry[];
    const filters = (await filtersResponse.json()) as FilterOptions;
    const articles = (await articlesResponse.json()) as ArticleSitemapEntry[];

    const productUrls = products
      .map(
        (p) =>
          `<url><loc>${origin}/urun/${p.id}</loc><lastmod>${new Date(p.lastScrapedAt).toISOString()}</lastmod><changefreq>daily</changefreq><priority>0.8</priority></url>`,
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

    // Kategori sayfaları — "[kategori] fiyatları" gibi aramalar için.
    const categoryUrls = filters.categories
      .map(
        (category) =>
          `<url><loc>${origin}/kategori/${category}</loc><changefreq>daily</changefreq><priority>0.8</priority></url>`,
      )
      .join('');

    const legalUrls =
      `<url><loc>${origin}/gizlilik-politikasi</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>` +
      `<url><loc>${origin}/cerez-politikasi</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>` +
      `<url><loc>${origin}/nasil-calisiyoruz</loc><changefreq>monthly</changefreq><priority>0.5</priority></url>`;

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
      categoryUrls +
      productUrls +
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
    const response = await fetch(`${API_BASE_URL}/go/${req.params.id}`, { redirect: 'manual', headers: ssrInternalHeaders });
    const location = response.headers.get('location');
    res.redirect(302, location ?? '/');
  } catch (error) {
    console.error('/go/:id yönlendirmesi başarısız:', error);
    res.redirect(302, '/');
  }
});

app.get('/robots.txt', (req, res) => {
  const origin = `${req.protocol}://${req.get('host')}`;
  res.set('Content-Type', 'text/plain');
  // /go/{id} affiliate yönlendirmeleri gerçek bir içerik sayfası değil,
  // dış siteye 302 atan bir uç nokta — botlar bunu ayrı bir "sayfa" gibi
  // taramaya/indekslemeye çalışmasın diye kapatıyoruz.
  res.send(`User-agent: *\nAllow: /\nDisallow: /go/\n\nSitemap: ${origin}/sitemap.xml\n`);
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
