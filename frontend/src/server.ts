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
// Arama motoru botlarını user-agent'tan tanıyoruz. Kesin bir yöntem değil ama
// amaç güvenlik değil, tıklama sayacını gerçek kullanıcıya yakın tutmak.
const BOT_USER_AGENT = /bot|crawler|spider|crawling|bingpreview|slurp|duckduck|yandex|baidu|facebookexternalhit|embedly|quora|pinterest|whatsapp|telegram/i;

app.get('/go/:id', async (req, res) => {
  // /go/{id} bir içerik sayfası değil, dışarıya 302 atan bir uç. Daha önce
  // robots.txt'te Disallow ile kapatılmıştı — ama Disallow bir adresin
  // DİZİNE GİRMESİNİ engellemiyor, yalnızca içeriğinin okunmasını engelliyor.
  // Sonuç: arama motoru adresi başka yoldan öğrenip dizine ekledi ama içeriği
  // okuyamadığı için açıklama yerine "site izin vermiyor" bastı; marka
  // aramasında ana sayfamız yerine bu adres çıkar oldu (28 Ağustos'ta Bing ve
  // DuckDuckGo'da birebir görüldü).
  //
  // Doğru sinyal bu başlık: bot sayfayı çekebiliyor, "dizine ekleme" talimatını
  // görüyor ve adresi dizinden düşürüyor. Bu yüzden robots.txt'teki Disallow da
  // kaldırıldı — yoksa bot bu başlığı hiç göremezdi.
  res.set('X-Robots-Tag', 'noindex, nofollow');

  const isBot = BOT_USER_AGENT.test(req.get('user-agent') ?? '');

  try {
    const response = await fetch(`${API_BASE_URL}/go/${req.params.id}`, {
      redirect: 'manual',
      // Bot isteklerinde tıklama sayacı artmasın: bu sayaç markalarla
      // paylaşılan tıklama raporunu besliyor, bot trafiğiyle şişmesi veriyi
      // doğrudan yanıltıcı yapar.
      headers: isBot ? { 'X-Bot-Request': '1' } : undefined,
    });
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
  // /go/{id} için BİLİNÇLİ olarak Disallow YOK. Disallow yalnızca içeriğin
  // okunmasını engelliyordu, adresin dizine girmesini değil — sonuçta marka
  // aramasında ana sayfamızın yerini "açıklama gösterilemiyor" diyen bir
  // yönlendirme adresi aldı. Bunun yerine o uç, isteğe X-Robots-Tag: noindex
  // başlığı basıyor; botun bu başlığı görebilmesi için sayfayı çekebilmesi
  // gerekiyor, dolayısıyla taramayı engellememek DOĞRU davranış.
  res.send(`User-agent: *\nAllow: /\n\nSitemap: ${CANONICAL_ORIGIN}/sitemap.xml\n`);
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
 * Sunucuda render edilen sayfalar için kısa ömürlü bellek içi önbellek.
 *
 * Ana sayfanın ilk bayta kadar geçen süresi ~1,8 saniyeydi ve bunun büyük
 * kısmı backend'i beklemek değil, Angular'ın render işiydi (24 ürün kartı,
 * grafikler, sık sorulanlar). Backend uçlarındaki 60 saniyelik çıktı
 * önbelleğiyle aynı süre kullanılıyor: aynı anda iki katmandan farklı
 * tazelikte veri dönmesin.
 *
 * Kasıtlı sınırlar:
 * - Yalnızca GET ve yalnızca 200 dönen HTML. Yönlendirmeler (eski ürün
 *   adresleri, kanonik slug) ve hata yanıtları hiç önbelleğe girmiyor;
 *   geçici bir 503'ün bir dakika boyunca herkese servis edilmesi
 *   arama motorlarına yanlış sinyal verirdi.
 * - Kişiye özel hiçbir şey girmiyor: takip listesi sayfası ve kurtarma
 *   bağlantısı taşıyan istekler dışarıda.
 * - Çerez yazan bir yanıt önbelleğe alınmıyor.
 */
const SSR_CACHE_TTL_MS = 60_000;
// Sayfalar ~0,5 MB olduğu için tavan bilinçli olarak düşük: bu sayı
// yaklaşık 70 MB'lık bir üst sınır demek. Ömür zaten 60 saniye, bu
// yüzden daha büyük bir tavanın isabet oranına katkısı olmazdı;
// asıl işlevi, dakikalar içinde binlerce adres gezen bir tarayıcının
// belleği doldurmasını engellemek.
const SSR_CACHE_MAX_ENTRIES = 120;
/** Tek bir dev sayfa önbelleği doldurmasın; bu boyutun üstü hiç saklanmıyor. */
const SSR_CACHE_MAX_BYTES = 1_000_000;

interface SsrCacheEntry {
  expiresAt: number;
  status: number;
  headers: [string, string][];
  body: string;
}

/** Ekleme sırası korunduğu için en eski giriş her zaman ilk anahtar. */
const ssrCache = new Map<string, SsrCacheEntry>();

function ssrCacheKey(req: express.Request): string | null {
  if (req.method !== 'GET') return null;
  const path = req.path;
  // Takip listesi tarayıcıdaki anahtara bağlı; kurtarma bağlantısı ise
  // tek kişiye ait bir belirteç taşıyor.
  if (path.startsWith('/favorilerim')) return null;
  if (req.query['recover'] !== undefined) return null;
  return req.originalUrl;
}

app.use((req, res, next) => {
  const key = ssrCacheKey(req);

  if (key) {
    const hit = ssrCache.get(key);
    if (hit && hit.expiresAt > Date.now()) {
      // En son kullanılanı sona taşı ki eleme sırası doğru kalsın.
      ssrCache.delete(key);
      ssrCache.set(key, hit);
      for (const [name, value] of hit.headers) res.setHeader(name, value);
      res.setHeader('X-SSR-Cache', 'HIT');
      res.status(hit.status).send(hit.body);
      return;
    }
    if (hit) ssrCache.delete(key);
  }

  angularApp
    .handle(req)
    .then(async (response) => {
      if (!response) return next();

      const contentType = response.headers.get('content-type') ?? '';
      const setsCookie = response.headers.has('set-cookie');
      const cacheable =
        key !== null && response.status === 200 && contentType.includes('text/html') && !setsCookie;

      if (!cacheable) {
        return writeResponseToNodeResponse(response, res);
      }

      // Gövdeyi kendimiz okuduğumuz için yanıtı da elle yazıyoruz;
      // writeResponseToNodeResponse akışı tüketip bize bir şey bırakmıyor.
      const body = await response.text();
      const headers: [string, string][] = [];
      response.headers.forEach((value, name) => {
        // Uzunluk ve kodlama başlıkları gövdeyi kendimiz yazdığımız an
        // geçersizleşiyor; olduğu gibi kopyalamak gövdeyle uyuşmayan bir
        // Content-Length'e yol açıyordu. Express doğrusunu kendi hesaplıyor.
        const lower = name.toLowerCase();
        if (lower === 'content-length' || lower === 'content-encoding' || lower === 'transfer-encoding') {
          return;
        }
        headers.push([name, value]);
      });

      if (Buffer.byteLength(body) <= SSR_CACHE_MAX_BYTES) {
        if (ssrCache.size >= SSR_CACHE_MAX_ENTRIES) {
          const oldest = ssrCache.keys().next().value;
          if (oldest !== undefined) ssrCache.delete(oldest);
        }
        ssrCache.set(key, {
          expiresAt: Date.now() + SSR_CACHE_TTL_MS,
          status: response.status,
          headers,
          body,
        });
      }

      for (const [name, value] of headers) res.setHeader(name, value);
      res.setHeader('X-SSR-Cache', 'MISS');
      res.status(response.status).send(body);
      return;
    })
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
