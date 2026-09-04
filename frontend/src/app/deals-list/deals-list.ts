import { DOCUMENT, DecimalPipe, Location, isPlatformServer } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, HostListener, OnInit, PLATFORM_ID, RESPONSE_INIT, computed, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router, RouterLink } from '@angular/router';

import { filterSelectValue, readFilterSelection } from '../core/filter-select';
import { buildProductJsonLdDescription } from '../core/product-facts';
import { ArticleSummary } from '../core/article.model';
import { ArticlesService } from '../core/articles.service';
import { canonicalOrigin } from '../core/canonical-link';
import { buildBreadcrumbJsonLd } from '../core/breadcrumb';
import { CATEGORY_LABELS } from '../core/category-labels';
import { CATEGORY_ICON_PATHS, DEFAULT_CATEGORY_ICON, categoryPhosphorIcon } from '../core/nav-icons';
import { ComparisonService } from '../core/comparison.service';
import { Coupon } from '../core/coupon.model';
import { CouponsService } from '../core/coupons.service';
import { Deal } from '../core/deal.model';
import { DealsQuery, DealsService } from '../core/deals.service';
import { displayName } from '../core/display-name';
import { FavoritesService } from '../core/favorites.service';
import { HomepageStats } from '../core/homepage-stats.model';
import { PageMetaService, upsertJsonLdScript } from '../core/page-meta.service';
import { PricePoint } from '../core/price-history.model';
import { PriceHistoryService } from '../core/price-history.service';
import { PwaInstallService } from '../core/pwa-install.service';
import { formatRelativeTime } from '../core/relative-time';
import { slugify } from '../core/slugify';
import { productPath } from '../core/product-link';
import { buildPageTitle, buildProductDescription } from '../core/meta-description';
import { buildAreaPath, buildLinePath, toCoordinates } from '../core/spark-chart';
import { SubscribeService } from '../core/subscribe.service';
import { ThemePreference, ThemeService } from '../core/theme.service';
import { ProductCardSparkline } from '../product-card-sparkline/product-card-sparkline';
import { ProductModal } from '../product-modal/product-modal';
import { PreferredProducts } from '../preferred-products/preferred-products';
import { showNotFound } from '../core/not-found-navigation';

type ViewMode = 'deals' | 'all' | 'store';

// Adrese yalnızca varsayılandan SAPAN görünüm yazılıyor; bu sabit iki yerde
// (yazma ve okuma) aynı olmak zorunda.
const DEFAULT_VIEW_MODE: ViewMode = 'store';

const isMac = typeof navigator !== 'undefined' && /Mac|iPod|iPhone|iPad/.test(navigator.platform);
const PAGE_SIZE = 24;
const SEARCH_DEBOUNCE_MS = 350;

function tekSecimEtiketi(
  secilenler: Set<string>,
  tekil: (ad: string) => string,
  cogul: (adet: number) => string,
): string {
  const [ilk] = secilenler;
  return secilenler.size === 1 ? tekil(ilk) : cogul(secilenler.size);
}



// Hero kartındaki küçük fiyat grafiği — product-modal'ın tam boyutlu
// grafiğinden çok daha küçük, kendi ölçüleri (Nocturne referansı: 280×90).
const HERO_CHART = { width: 280, height: 90, paddingY: 8 };

// timeZone sabit Europe/Istanbul — bkz. product-modal.ts'teki aynı gerekçe
// (kullanıcının cihaz saat dilimine bırakılırsa aynı an farklı ziyaretçilere
// farklı "gün/saat" gösterebilirdi, site sadece TR pazarına hizmet ediyor).
const SCAN_TIME_FORMATTER = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul' });
const SCAN_DATE_FORMATTER = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', timeZone: 'Europe/Istanbul' });

// Title/description'da bilinçli olarak "Protein Avcısı" (boşluklu) kullanılıyor
// — logodaki bitişik "ProteinAvcısı" yazımı marka kimliği olarak kalıyor, ama
// insanlar arama kutusuna doğal olarak boşluklu yazıyor; arama motoruna dönük
// metinlerde bu ayrımı güçlendirmek ucuz ve düşük riskli bir SEO düzeltmesi.
const DEFAULT_TITLE = 'Protein Avcısı | Güncel İndirim ve Kampanyalar — Spor Takviyesi Fiyat Takibi';
const DEFAULT_DESCRIPTION =
  'Protein Avcısı; protein tozu, kreatin, pre-workout ve diğer spor takviyelerinde markanın beyanına değil, gerçek fiyat geçmişine dayanan doğrulanmış indirimleri gösterir. HIQ, SSN, Hardline ve ProteinOcean tek yerde.';

// Tasarım güncellemesi (bir geliştiricinin "daha fazla içerik/güven
// unsuru" geri bildirimi üzerine): gerçek, uydurma olmayan sorular —
// hepsi zaten sitede var olan davranışları açıklıyor, pazarlama amaçlı
// abartı yok. Aynı zamanda FAQPage structured data için de kullanılıyor.
const FAQ_ITEMS: { question: string; answer: string }[] = [
  {
    question: '"İndirimdekiler" ile "Mağaza Kampanyaları" arasındaki fark nedir?',
    answer:
      '"İndirimdekiler", bizim topladığımız gerçek fiyat geçmişine dayanır — bir ürünün güncel fiyatı son 30 günün en yüksek fiyatından düşükse burada listelenir. "Mağaza Kampanyaları" ise markanın kendi sitesinde beyan ettiği eski/yeni fiyat farkıdır, henüz bizim tarafımızdan doğrulanmamıştır.',
  },
  {
    question: 'Fiyatlar ne sıklıkla güncelleniyor?',
    answer: 'Takip ettiğimiz markalar günde 4 kez otomatik olarak taranıyor, fiyat değişiklikleri buna göre güncelleniyor.',
  },
  {
    question: 'Ürünü ProteinAvcısı üzerinden mi satın alıyorum?',
    answer:
      'Hayır. ProteinAvcısı bir satış sitesi değil, fiyat takip sitesidir. "Mağazaya Git" butonuna tıklayınca doğrudan ilgili markanın kendi sitesine yönlendirilirsin, satış işlemi orada gerçekleşir.',
  },
  {
    question: 'Kupon kodlarını nereden buluyorsunuz?',
    answer:
      'Kupon kodları otomatik toplanmıyor — süresi geçmiş ya da hatalı bir kod göstermemek için yalnızca elle doğruladığımız kodları yayınlıyoruz.',
  },
  {
    question: 'Neden bazı ürünlerde "servis başı fiyat" gösterilmiyor?',
    answer:
      'Bu bilgiyi yalnızca markanın gerçek besin değeri tablosuna ulaşabildiğimiz ürünlerde gösteriyoruz; tahmini bir rakam paylaşmıyoruz.',
  },
];

@Component({
  selector: 'app-deals-list',
  imports: [DecimalPipe, FormsModule, PreferredProducts, ProductCardSparkline, ProductModal, RouterLink],
  templateUrl: './deals-list.html',
})
export class DealsList implements OnInit {
  // Template'te (H1, kart başlıkları) ALL CAPS ürün isimlerini okunabilir
  // Title Case'e çeviren saf fonksiyon — component metodu değil, doğrudan
  // referans veriliyor.
  protected readonly displayName = displayName;
  private readonly dealsService = inject(DealsService);
  private readonly couponsService = inject(CouponsService);
  private readonly articlesService = inject(ArticlesService);
  private readonly favoritesService = inject(FavoritesService);
  // Kart üzerindeki karşılaştırma butonu için — seçim servis seviyesinde
  // paylaşılıyor, alt çubuk ve diğer sayfalar aynı signal'i okuyor.
  protected readonly comparison = inject(ComparisonService);
  private readonly priceHistoryService = inject(PriceHistoryService);
  private readonly subscribeService = inject(SubscribeService);
  protected readonly pwaInstall = inject(PwaInstallService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  // Bu oturumda uygulama içinde kaç kez gezinildi. Modalı kapatırken geri mi
  // gideceğimizi buna bakarak seçiyoruz (bkz. closeDeal).
  //
  // Bayrakla ("openDeal çağrıldı mı") yapılamıyor: ürün kartı routerLink
  // kullanıyor, openDeal hiç çalışmıyor. Bu, ilk denemede yazılan düzeltmenin
  // sessizce işe yaramamasına yol açtı — tarayıcıda geçmiş uzunluğu ölçülünce
  // görüldü.
  private uygulamaIciGezinme = 0;
  /** ngOnInit'teki ilk queryParamMap emisyonunu ayırt etmek için. */
  private initialLoadDone = false;
  private readonly destroyRef = inject(DestroyRef);
  private readonly pageMeta = inject(PageMetaService);
  private readonly document = inject(DOCUMENT);
  // SSR sırasında gerçek HTTP status kodunu değiştirmek için — bkz.
  // productLoadError üzerindeki yorum. Sadece platform-server'da dolu
  // gelir, tarayıcıda `null` — optional injection bu yüzden gerekli.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly isServer = isPlatformServer(inject(PLATFORM_ID));
  protected readonly theme = inject(ThemeService);
  private readonly searchInput = viewChild<{ nativeElement: HTMLInputElement }>('searchInput');
  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  private structuredDataEl: HTMLScriptElement | null = null;
  private faqStructuredDataEl: HTMLScriptElement | null = null;
  private breadcrumbEl: HTMLScriptElement | null = null;

  protected readonly shortcutLabel = isMac ? '⌘K' : 'Ctrl+K';

  protected readonly deals = signal<Deal[]>([]);
  // Ürün kartlarındaki mini sparkline'lar — sayfa her yüklendiğinde tek bir
  // toplu istekle dolduruluyor (bkz. loadSparklines), kart başına ayrı
  // istek değil.
  protected readonly sparklines = signal<Map<number, PricePoint[]>>(new Map());
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  // Varsayılan sekme bilinçli olarak "store": "İndirimdekiler" (kendi
  // doğruladığımız indirim) şu aşamada çoğunlukla boş geliyor — yeni bir
  // ziyaretçinin ilk gördüğü şey boş bir sayfa olunca güven kırıcı oluyor
  // (kullanıcı testinde gerçek geri bildirim). "Mağaza Kampanyaları" hep
  // dolu, dürüstçe "doğrulanmamış" etiketli ama boş görünmüyor.
  protected readonly viewMode = signal<ViewMode>('store');
  protected readonly selectedDeal = signal<Deal | null>(null);
  // Gerçek Search Console bulgusu (2026-08-17): /urun/:id yüklenirken
  // OLUŞAN HERHANGİ bir hata (gerçek 404 de, backend'e geçici
  // ulaşılamama da) aynı şekilde ana sayfaya 302 yönlendiriyordu — bir
  // önceki Cloudflare Bot Fight Mode olayında backend'e giden SSR
  // istekleri geçici engellenince bu, Googlebot'a "bu ürün artık yok,
  // kalıcı olarak taşındı" yanlış sinyalini vermiş, Google 10 ürün
  // sayfasını "Yönlendirmeli sayfa" diye işaretleyip indexlemeyi
  // bırakmıştı. Artık sadece backend'in GERÇEKTEN 404 dönmesi ana
  // sayfaya yönlendiriyor; ağ hatası/5xx gibi geçici sorunlarda bu
  // sinyal set ediliyor — sayfa yönlendirmiyor, HTTP durumu 503
  // (geçici, tekrar dene) oluyor, "kalıcı olarak yok" (302) DEMİYOR.
  protected readonly productLoadError = signal(false);

  // Gerçek SEO içerik denetimi bulgusu (2026-08-17): /urun/:id, route
  // reuse sayesinde ana sayfayla aynı bileşeni paylaşıyor — modal
  // açıldığında arka plandaki TÜM ana sayfa (hero, 24 ürünlük grid,
  // kuponlar, rehber tanıtımı, SSS) SSR HTML'inden hiç çıkarılmıyordu.
  // 20 ürün sayfası ikili karşılaştırıldığında ortalama %91.7 (maks
  // %97) içerik benzerliği bulundu — muhtemelen Google'ın 559 ürün
  // sayfasının çoğunu "keşfedildi ama indexlenmedi" bırakmasının asıl
  // nedeni. Çözüm: SADECE SSR'da, bir ürün seçiliyken, arka plan
  // içeriğini render'dan çıkarıyoruz — CSR'da (tarayıcıda) davranış
  // HİÇ değişmiyor (modal zaten tüm ekranı kapladığı için kullanıcı
  // arka planı görmüyor), sadece bot/SSR çıktısı artık sadece o ürüne
  // özel içeriği taşıyor. H1 bu kuralın DIŞINDA tutuluyor (her zaman
  // render edilmeli).
  protected readonly showFullHomepageContent = computed(() => !this.isServer || !this.selectedDeal());

  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly currentPage = signal(1);

  protected readonly searchQuery = signal('');
  protected readonly selectedBrands = signal<Set<string>>(new Set());
  // Satıcı filtresi: aynı ürün hem markanın kendi sitesinden hem bir bayiden
  // gelebiliyor (barkod olmadığı için eşleştirilmiyorlar). Kullanıcı hangisini
  // istediğini seçebilmeli.
  protected readonly selectedSellers = signal<Set<string>>(new Set());
  protected readonly availableSellers = signal<string[]>([]);
  protected readonly selectedCategories = signal<Set<string>>(new Set());
  protected readonly priceMin = signal<number | null>(null);
  protected readonly priceMax = signal<number | null>(null);
  protected readonly sortBy = signal<string>('');

  // Kutuda gösterilecek değer: filtre yoksa placeholder ("Tüm markalar"),
  // varsa durum seçeneği. Boş dizeye dönmek = "tümü" = o boyutu temizle.
  protected readonly brandSelectValue = computed(() => filterSelectValue(this.selectedBrands().size));
  protected readonly categorySelectValue = computed(() => filterSelectValue(this.selectedCategories().size));
  protected readonly sellerSelectValue = computed(() => filterSelectValue(this.selectedSellers().size));

  // Kutuda ne yazacağı. TEK seçimde sayı değil ADIN KENDİSİ gösteriliyor:
  // "Markanın kendi sitesi" seçiliyken "1 satıcı seçili" yazması kullanıcıyı
  // şaşırtıyordu (bildirildi) — seçtiği şey zaten tek ve adı kutuya sığıyor.
  // Birden çoğunda ad listelemek kutuyu taşırdığı için sayıya düşülüyor.
  protected readonly brandSelectLabel = computed(() =>
    tekSecimEtiketi(this.selectedBrands(), (ad) => ad, (n) => `${n} marka seçili`));
  protected readonly categorySelectLabel = computed(() =>
    tekSecimEtiketi(this.selectedCategories(), (ad) => this.categoryLabel(ad), (n) => `${n} kategori seçili`));
  protected readonly sellerSelectLabel = computed(() =>
    tekSecimEtiketi(this.selectedSellers(), (ad) => ad, (n) => `${n} satıcı seçili`));

  protected readonly availableBrands = signal<string[]>([]);
  protected readonly availableCategories = signal<string[]>([]);

  // Nav'daki "Kategoriler" açılır menüsü — kullanıcı geri bildirimi:
  // footer'dan başka erişimi olmayan kategori sayfaları neredeyse hiç
  // görünmüyordu. site-header.ts'teki aynı desen (bu sayfa kendi özel
  // nav'ını koruyor, SiteHeader'ı kullanmıyor, bu yüzden burada ayrıca var).
  protected readonly categoriesOpen = signal(false);

  protected readonly hasActiveFilters = signal(false);

  protected readonly coupons = signal<Coupon[]>([]);

  // Gerçek tercih metriği backend'e eklenene kadar bu bant mevcut ürün
  // kataloğundan deterministik olarak karıştırılmış bir aday havuzu kullanır.
  // Ayrı signal olması, liste sekmeleri/filtreleri değişirken keşif bandının
  // içeriğinin zıplamamasını sağlar.
  protected readonly preferredProductCandidates = signal<Deal[]>([]);

  // Sayfa aşağı kaydırılınca sağ altta çıkan "yukarı çık" butonu için.
  protected readonly showScrollTop = signal(false);

  // Ana sayfadaki gerçek istatistik şeridi için — filtreden bağımsız,
  // tüm katalog sayısı (mevcut sekme/filtreye göre değişen totalCount()'tan
  // ayrı). Uydurma bir rakam değil, /api/products'tan gelen gerçek toplam.
  protected readonly siteProductCount = signal(0);
  protected readonly faqItems = FAQ_ITEMS;

  // Canlı tarama şeridi — /api/stats'tan, sayfa yüklenince bir kez.
  protected readonly stats = signal<HomepageStats | null>(null);
  protected readonly lastScanLabel = computed(() => {
    const lastScanAt = this.stats()?.lastScanAt;
    if (!lastScanAt) return null;
    const d = new Date(lastScanAt);
    return `${SCAN_TIME_FORMATTER.format(d)} · ${SCAN_DATE_FORMATTER.format(d)}`;
  });

  // Nav'daki "Takip listem" rozeti — servisteki paylaşılan signal'e
  // doğrudan referans, favori eklenince/çıkarılınca (bu sayfadan ya da
  // başka bir sayfadan) otomatik güncellenir.
  protected readonly favoritesCount = this.favoritesService.count;

  // Rehber teaser — ilk 3 yazı.
  protected readonly articles = signal<ArticleSummary[]>([]);

  // "Fiyat düşünce ilk sen bil" bandı — footer'daki NewsletterSignup
  // component'iyle AYNI SubscribeService'i kullanıyor, kendi (Nocturne
  // görünümlü) formu var; footer'daki form Faz 1'de dokunulmadığı için
  // ikisi birlikte kalıyor (küçük bir tekrar, zararsız).
  protected readonly alarmEmail = signal('');
  protected readonly alarmSubmitting = signal(false);
  protected readonly alarmStatusMessage = signal<string | null>(null);

  // Hero — "Günün en sert düşüşü": view mode/filtrelerden bağımsız, en
  // yüksek gerçek indirimli ürün (hiç yoksa en yüksek mağaza kampanyasına
  // düşer — "store sekmesi hep dolu" mantığıyla aynı, hero boş kalmasın diye).
  protected readonly heroDeal = signal<Deal | null>(null);
  protected readonly heroPoints = signal<PricePoint[]>([]);
  protected readonly heroCoordinates = computed(() => {
    const points = this.heroPoints();
    if (points.length === 0) return [];
    const prices = points.map((p) => p.price);
    return toCoordinates(points, Math.min(...prices), Math.max(...prices), HERO_CHART);
  });
  protected readonly heroLinePath = computed(() => buildLinePath(this.heroCoordinates()));
  protected readonly heroAreaPath = computed(() => buildAreaPath(this.heroCoordinates(), HERO_CHART.height));

  constructor() {
    // Ürün modalı açıkken title/description/Open Graph o ürüne özel oluyor
    // (SSR ile birleşince /urun/:id linki paylaşılınca veya Google'da
    // gerçek ürün bilgisiyle görünüyor); kapanınca site geneline dönüyor.
    effect(() => {
      const deal = this.selectedDeal();

      if (!deal) {
        this.pageMeta.set({
          title: DEFAULT_TITLE,
          description: DEFAULT_DESCRIPTION,
          canonicalPath: '/',
        });
        this.structuredDataEl?.remove();
        this.structuredDataEl = null;
        this.breadcrumbEl?.remove();
        this.breadcrumbEl = null;
        // Gerçek SEO içerik denetimi bulgusu (2026-08-17): bu FAQPage
        // JSON-LD'si eskiden ngOnInit'te KOŞULSUZ bir kez ekleniyordu —
        // /urun/:id ilk yüklenen sayfa olduğunda bile SSR HTML'ine
        // giriyordu (görünür SSS metni showFullHomepageContent() ile
        // gizlense de, bu structured data ondan bağımsız bir mekanizmaydı).
        // Artık selectedDeal()'e reaktif: sadece ana sayfa durumunda var.
        this.faqStructuredDataEl = upsertJsonLdScript(this.document, this.faqStructuredDataEl, {
          '@context': 'https://schema.org',
          '@type': 'FAQPage',
          mainEntity: FAQ_ITEMS.map((item) => ({
            '@type': 'Question',
            name: item.question,
            acceptedAnswer: { '@type': 'Answer', text: item.answer },
          })),
        });
        return;
      }

      this.faqStructuredDataEl?.remove();
      this.faqStructuredDataEl = null;

      const displayedName = displayName(deal.productName);
      const priceText = `${deal.currentPrice.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`;
      // Title'da fiyat BİLİNÇLİ OLARAK yok — fiyat günde 4 kez değişebiliyor,
      // her değişimde title'ı yeniden yazmak Google'ın snippet'i sürekli
      // güncellemesine/tarama bütçesini boşa harcamasına yol açıyordu (dış
      // kod incelemesinde bulunan bir madde, kodla doğrulandı). Fiyat artık
      // sadece description'da ve JSON-LD Offer.price'ta kalıyor. Paylaşım
      // kartında (WhatsApp/Twitter) fiyatlı görünmesi hâlâ isteniyor —
      // ogTitle ayrı tutuluyor.
      // Uzun ürün adlarında başlık 118 karaktere kadar çıkıyordu; Google o
      // uzunlukta başlığı tamamen kendi yeniden yazıyor (bkz. buildPageTitle).
      const title = buildPageTitle(displayedName, 'Fiyatı ve Fiyat Geçmişi', deal.brandName);
      const ogTitle = `${displayedName} Fiyatı: ${priceText} | ${deal.brandName} — ProteinAvcısı`;
      // Açıklama artık markanın kendi ürün metninden besleniyor (bkz.
      // core/meta-description.ts) — arama sonucunda ürünün ne olduğunu
      // söyleyen tek şey burasıydı ve yalnızca fiyat cümlesi taşıyordu.
      const description = buildProductDescription({
        displayName: displayedName,
        brandName: deal.brandName,
        priceText,
        discountPercent: deal.discountPercent,
        description: deal.description,
      });

      // Kopya bir kayıtsak canonical ASIL sayfayı göstermeli. Slug aynı —
      // zaten aynı ürün adını taşıdıkları için kopya sayılıyorlar — yalnızca
      // Id değişiyor. (bkz. DealDto.CanonicalProductId)
      const canonicalId = deal.canonicalProductId ?? deal.productId;
      const canonicalProductPath = `/urun/${canonicalId}/${slugify(deal.productName)}`;

      this.pageMeta.set({
        title,
        ogTitle,
        description,
        canonicalPath: canonicalProductPath,
        // og:type 'product' resmi bir Open Graph tipi değil (Facebook'un
        // katalog entegrasyonu için ayrı ek alanlar gerektiriyor, biz onları
        // hiç doldurmuyoruz) — asıl ürün sinyali zaten aşağıdaki JSON-LD
        // Product/Offer'da veriliyor.
        ogType: 'website',
        ogImage: deal.imageUrl ?? undefined,
        // Markanın taramada artık döndürmediği ve yerine geçen güncel bir
        // kaydı da bulunmayan ürün: sayfa çalışmaya devam ediyor (biriktirdiği
        // fiyat geçmişi hâlâ değerli ve paylaşılmış linkler bozulmuyor) ama
        // dizine girmemesi gerekiyor — site içinde hiçbir listede
        // görünmediği için oraya giden hiçbir bağlantı yok.
        noIndex: deal.isStale === true,
      });

      // schema.org Product/Offer — Google'ın arama sonucunda fiyat gösterme
      // ihtimali için. "availability" bilinçli olarak yok: 4 markanın
      // hepsinde güvenilir stok bilgisi çekmiyoruz (SSN/Hardline stok
      // durumunu hiç kontrol etmiyor), olmayan veriyi "InStock" diye
      // iddia etmektense alanı hiç eklememeyi tercih ettik.
      const jsonLd = {
        '@context': 'https://schema.org',
        '@type': 'Product',
        name: displayedName,
        sku: String(deal.productId),
        ...(deal.imageUrl ? { image: deal.imageUrl } : {}),
        brand: { '@type': 'Brand', name: deal.brandName },
        // GSC "description alanı eksik" diyordu. Metin markanın tanıtım
        // yazısından değil kendi ölçümlerimizden üretiliyor.
        description: buildProductJsonLdDescription(deal),
        offers: {
          '@type': 'Offer',
          url: `${canonicalOrigin(this.document)}${canonicalProductPath}`,
          priceCurrency: 'TRY',
          price: deal.currentPrice.toFixed(2),
        },
        // Markanın kendi sitesindeki müşteri puanı. YALNIZCA veri gerçekten
        // varsa ekleniyor ve sayfada da görünür durumda (bilgi listesinde,
        // markanın adıyla etiketli) — Google, işaretlemedeki puanın sayfada
        // gösterilmesini şart koşuyor.
        ...(deal.ratingValue !== null && deal.ratingCount !== null
          ? {
              aggregateRating: {
                '@type': 'AggregateRating',
                ratingValue: deal.ratingValue,
                reviewCount: deal.ratingCount,
                bestRating: 5,
              },
            }
          : {}),
      };

      this.structuredDataEl = upsertJsonLdScript(this.document, this.structuredDataEl, jsonLd);

      const categoryLabel = deal.category ? (CATEGORY_LABELS[deal.category] ?? deal.category) : null;
      this.breadcrumbEl = upsertJsonLdScript(
        this.document,
        this.breadcrumbEl,
        buildBreadcrumbJsonLd(this.document, [
          { name: 'Ana Sayfa', path: '/' },
          ...(categoryLabel && deal.category ? [{ name: categoryLabel, path: `/kategori/${deal.category}` }] : []),
          { name: deal.productName, path: canonicalProductPath },
        ]),
      );
    });
  }

  ngOnInit(): void {
    this.dealsService.getFilterOptions().subscribe((options) => {
      this.availableBrands.set(options.brands);
      this.availableCategories.set(options.categories);
      this.availableSellers.set(options.sellers ?? []);
    });
    this.couponsService.getCoupons().subscribe((coupons) => this.coupons.set(coupons));
    this.dealsService.getPreferredProducts(60).subscribe({
      next: (products) => this.preferredProductCandidates.set(products),
      error: () => this.preferredProductCandidates.set([]),
    });
    // pageSize:1 — sadece toplam sayıyı okumak için, tüm ürünleri çekmeye gerek yok.
    this.dealsService.getAllProducts({ pageSize: 1 }).subscribe((result) => this.siteProductCount.set(result.totalCount));
    this.dealsService.getStats().subscribe((stats) => this.stats.set(stats));
    this.favoritesService.ensureCount();
    this.articlesService.getArticles().subscribe((articles) => this.articles.set(articles.slice(0, 3)));
    this.loadHeroDeal();
    // İlk yükleme bilinçli olarak BURADA DEĞİL, aşağıdaki queryParamMap
    // aboneliğinin içinde: abonelik kurulur kurulmaz senkron bir kez
    // tetikleniyor, yani ?search= / ?page= varsa daha ilk istekte
    // uygulanıyor. Burada ayrıca load() çağırmak iki paralel istek
    // başlatıyordu ve filtresiz olan sonra dönüp filtreli sonucu eziyordu.

    // Sayfa numarası URL'de ?page= olarak tutuluyor — tarayıcının geri/ileri
    // (mouse yan tuşları dahil) butonlarının sayfalama geçmişinde doğru
    // gezinebilmesi için. goToPage() zaten currentPage'i set edip load()
    // çağırdığından, bu abonelik asıl olarak URL DIŞARIDAN değiştiğinde
    // (geri/ileri tuşu, paylaşılan link) devreye giriyor — sayfa zaten
    // eşleşiyorsa tekrar yüklemiyor.
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      // Diğer sayfalardaki üst menüden yapılan arama buraya ?search= ile
      // geliyor (o menüde ürün listesi olmadığı için arama ana sayfada
      // sonuçlanıyor). Önceden üstteki kutu yalnızca ana sayfaya giden bir
      // bağlantıydı: tıklayan kişi arama yaptığını sanıp ana sayfaya
      // düşüyordu.
      const search = (params.get('search') ?? '').trim();
      // İlk emisyon her zaman yüklemeli — o an değerler zaten eşit olduğu
      // için aşağıdaki karşılaştırmalar false döner, liste hiç dolmazdı.
      let needsLoad = !this.initialLoadDone;
      this.initialLoadDone = true;

      if (search !== this.searchQuery()) {
        this.searchQuery.set(search);
        needsLoad = true;
        // Dışarıdan arama ile gelindiğinde "Tümü" sekmesine geçiyoruz.
        // Varsayılan sekme mağaza kampanyaları; aranan ürünün o an bir
        // kampanyası yoksa (ör. magnezyum) kişi sonuç varken boş ekran
        // görürdü. Kullanıcının kendi seçtiği sekme korunuyor: bu satır
        // yalnızca URL'den yeni bir arama geldiğinde çalışıyor.
        if (search) this.viewMode.set('all');
      }

      // Filtreler de adresten geri yükleniyor. Bu, GERİ tuşunun çalışması
      // için şart: kullanıcı arama/filtre yapıp başka bir sayfaya (ürün,
      // karşılaştırma) gidip geri döndüğünde bileşen yeniden kuruluyor ve
      // signal'ler varsayılana düşüyordu — durum yalnızca adreste yaşıyorsa
      // kurtarılabiliyor.
      const kume = (ad: string) => {
        const ham = params.get(ad);
        return new Set((ham ?? '').split(',').map((x) => x.trim()).filter(Boolean));
      };
      const sayi = (ad: string) => {
        const ham = params.get(ad);
        if (ham === null || ham.trim() === '') return null;
        const deger = Number(ham);
        return Number.isFinite(deger) ? deger : null;
      };

      const kumeFarkli = (a: Set<string>, b: Set<string>) =>
        a.size !== b.size || [...a].some((x) => !b.has(x));

      const markalar = kume('brands');
      if (kumeFarkli(markalar, this.selectedBrands())) {
        this.selectedBrands.set(markalar);
        needsLoad = true;
      }

      const kategoriler = kume('categories');
      if (kumeFarkli(kategoriler, this.selectedCategories())) {
        this.selectedCategories.set(kategoriler);
        needsLoad = true;
      }

      const saticilar = kume('sellers');
      if (kumeFarkli(saticilar, this.selectedSellers())) {
        this.selectedSellers.set(saticilar);
        needsLoad = true;
      }

      const enAz = sayi('min');
      if (enAz !== this.priceMin()) {
        this.priceMin.set(enAz);
        needsLoad = true;
      }

      const enCok = sayi('max');
      if (enCok !== this.priceMax()) {
        this.priceMax.set(enCok);
        needsLoad = true;
      }

      const siralama = params.get('sort') ?? '';
      if (siralama !== this.sortBy()) {
        this.sortBy.set(siralama);
        needsLoad = true;
      }

      // Görünüm sekmesi listeyi değiştirdiği için adresten geri geliyor; ama
      // yukarıdaki "dışarıdan arama gelince Tümü'ne geç" kuralını EZMEMELİ,
      // o yüzden yalnızca adreste açıkça yazılıysa uygulanıyor.
      const gorunum = params.get('view') as ViewMode | null;
      if (gorunum && gorunum !== this.viewMode()) {
        this.viewMode.set(gorunum);
        needsLoad = true;
      }

      const page = Math.max(1, Number(params.get('page')) || 1);
      if (page !== this.currentPage()) {
        this.currentPage.set(page);
        needsLoad = true;
      }

      if (needsLoad) this.load();
    });

    // Ürün modalı artık URL'e bağlı (/urun/:id) — bileşen '' ve 'urun/:id'
    // arasında yeniden kurulmadan yaşadığı için (bkz. DealsRouteReuseStrategy)
    // parametre değişikliklerine burada tek seferlik abone oluyoruz.
    this.router.events
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event instanceof NavigationEnd) this.uygulamaIciGezinme++;
      });

    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const idParam = params.get('id');
      if (!idParam) {
        this.selectedDeal.set(null);
        return;
      }

      const id = Number(idParam);
      const slugParam = params.get('slug');

      const alreadyLoaded = this.deals().find((d) => d.productId === id);
      if (alreadyLoaded) {
        this.selectedDeal.set(alreadyLoaded);
        this.ensureCanonicalSlug(alreadyLoaded, slugParam);
        return;
      }

      this.productLoadError.set(false);
      this.dealsService.getProductById(id).subscribe({
        next: (deal) => {
          this.selectedDeal.set(deal);
          this.ensureCanonicalSlug(deal, slugParam);
        },
        error: (err: HttpErrorResponse) => {
          if (err.status === 404) {
            showNotFound(this.router);
            return;
          }
          // Geçici sorun (ağ hatası, backend 5xx/erişilemez) — "artık yok"
          // sinyali (302) vermiyoruz, "şu an geçici olarak yüklenemedi"
          // diyoruz. SSR'da bu gerçek bir HTTP 503 olarak dönüyor.
          this.productLoadError.set(true);
          if (this.responseInit) this.responseInit.status = 503;
        },
      });
    });
  }

  // URL'deki slug segmenti eksikse (eski /urun/:id linkleri, elle yazılan
  // adresler) ya da ürün adı değiştiği için eskiyse, kanonik slug'a
  // replaceUrl ile yönlendiriyor. SSR'da bu, /urun/:id ↔ / arası geçersiz-ID
  // yönlendirmesiyle AYNI mekanizmayla (Angular Universal'ın render sırasında
  // yakalanan navigate() çağrısını gerçek bir HTTP 302'ye çevirmesi) gerçek
  // bir yönlendirmeye dönüşüyor — Google'ın zaten indexlediği çıplak /urun/:id
  // linklerinin ranking sinyalini kanonik (slug'lı) URL'e taşıması için.
  // Slug zaten doğruysa hiçbir şey yapmıyor (sonsuz döngü riski yok).
  private ensureCanonicalSlug(deal: Deal, slugParam: string | null): void {
    // Marka bu kaydın adresini değiştirmiş ve aynı ürünün güncel bir kaydı
    // var — iki sayfanın arama sonuçlarında birbiriyle çakışmaması için eski
    // adres güncel kayda taşınıyor. Aynı yönlendirme mekanizması (SSR'da
    // gerçek bir HTTP yönlendirmesine dönüşüyor) slug düzeltmesinde de
    // kullanılıyor.
    if (deal.replacementProductId) {
      this.router.navigate(['/urun', deal.replacementProductId, slugify(deal.productName)], {
        replaceUrl: true,
        queryParamsHandling: 'preserve',
      });
      return;
    }

    const canonicalSlug = slugify(deal.productName);
    if (slugParam === canonicalSlug) return;
    this.router.navigate(['/urun', deal.productId, canonicalSlug], {
      replaceUrl: true,
      queryParamsHandling: 'preserve',
    });
  }

  protected setViewMode(mode: ViewMode): void {
    if (this.viewMode() === mode) return;
    this.viewMode.set(mode);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected onSearchChange(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounceHandle) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.currentPage.set(1);
      this.load();
      this.syncUrlState(1, false);
    }, SEARCH_DEBOUNCE_MS);
  }

  protected onSortChange(value: string): void {
    this.sortBy.set(value);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected toggleSeller(seller: string): void {
    const current = new Set(this.selectedSellers());
    if (current.has(seller)) current.delete(seller);
    else current.add(seller);
    this.selectedSellers.set(current);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected onSellerSelect(value: string): void {
    const selection = readFilterSelection(value);
    if (selection.kind === 'toggle') this.toggleSeller(selection.value);
    else if (selection.kind === 'clear') this.clearSellers();
  }

  protected toggleBrand(brand: string): void {
    const current = new Set(this.selectedBrands());
    current.has(brand) ? current.delete(brand) : current.add(brand);
    this.selectedBrands.set(current);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected toggleCategory(category: string): void {
    const current = new Set(this.selectedCategories());
    current.has(category) ? current.delete(category) : current.add(category);
    this.selectedCategories.set(current);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected onPriceMinChange(value: number | null): void {
    this.priceMin.set(value);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected onPriceMaxChange(value: number | null): void {
    this.priceMax.set(value);
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected onBrandSelect(value: string): void {
    const selection = readFilterSelection(value);
    if (selection.kind === 'toggle') this.toggleBrand(selection.value);
    else if (selection.kind === 'clear') this.clearBrands();
  }

  protected onCategorySelect(value: string): void {
    const selection = readFilterSelection(value);
    if (selection.kind === 'toggle') this.toggleCategory(selection.value);
    else if (selection.kind === 'clear') this.clearCategories();
  }

  private clearBrands(): void {
    if (this.selectedBrands().size === 0) return;
    this.selectedBrands.set(new Set());
    this.afterFilterChange();
  }

  private clearCategories(): void {
    if (this.selectedCategories().size === 0) return;
    this.selectedCategories.set(new Set());
    this.afterFilterChange();
  }

  private clearSellers(): void {
    if (this.selectedSellers().size === 0) return;
    this.selectedSellers.set(new Set());
    this.afterFilterChange();
  }

  private afterFilterChange(): void {
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected clearFilters(): void {
    this.selectedBrands.set(new Set());
    this.selectedSellers.set(new Set());
    this.selectedCategories.set(new Set());
    this.priceMin.set(null);
    this.priceMax.set(null);
    this.searchQuery.set('');
    // Sıralama teknik olarak bir filtre değil (hasActiveFilters'a da dahil
    // değil), ama kullanıcı "temizle" dediğinde listenin tümüyle varsayılan
    // görünüme dönmesini bekliyor — sıralama kutusu eski seçimde kalırsa
    // liste temizlenmemiş gibi duruyor.
    this.sortBy.set('');
    this.currentPage.set(1);
    this.load();
    this.syncUrlState(1, false);
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.load();
    // Yeni bir tarayıcı geçmişi kaydı oluştur (push) — geri/ileri tuşlarıyla
    // sayfalar arasında doğru gezinilsin diye. Filtre/arama/sıralama
    // değişiklikleri bunun aksine replaceUrl kullanıyor (bkz. syncPageQueryParam
    // çağrıları yukarıda) — her filtre tıklaması ayrı bir "geri" durağı
    // olmasın diye.
    this.syncUrlState(page, true);
    // Sayfa değişince en üste dön, kullanıcı grid'in ortasında kalmasın.
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /**
   * Liste durumunun TAMAMINI adrese yazar.
   *
   * Eskiden adreste yalnızca `page` vardı; arama ve filtreler saf signal'di.
   * Sonuç: kullanıcı "magnezyum" arayıp bir ürüne/karşılaştırmaya gidip GERİ
   * döndüğünde her şey sıfırlanmış boş bir ana sayfa buluyordu (kullanıcı
   * mobilde bildirdi). Durum adreste olduğu için artık tarayıcının kendi
   * geri/ileri mantığı yeterli — ayrıca liste paylaşılabilir ve yenilemeye
   * dayanıklı hale geliyor.
   *
   * Varsayılan değerler adrese YAZILMIYOR (null veriliyor, Angular parametreyi
   * düşürüyor): filtresiz ana sayfanın adresi sade kalsın ve kanonik adres
   * bozulmasın diye.
   */
  private syncUrlState(page: number, push: boolean): void {
    const dizi = (deger: Set<string>) => (deger.size > 0 ? [...deger].join(',') : null);
    const arama = this.searchQuery().trim();

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        page: page > 1 ? page : null,
        search: arama || null,
        brands: dizi(this.selectedBrands()),
        categories: dizi(this.selectedCategories()),
        sellers: dizi(this.selectedSellers()),
        min: this.priceMin(),
        max: this.priceMax(),
        sort: this.sortBy() || null,
        view: this.viewMode() === DEFAULT_VIEW_MODE ? null : this.viewMode(),
      },
      queryParamsHandling: 'merge',
      replaceUrl: !push,
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    const query: DealsQuery = {
      brands: [...this.selectedBrands()],
      sellers: [...this.selectedSellers()],
      categories: [...this.selectedCategories()],
      search: this.searchQuery().trim() || undefined,
      minPrice: this.priceMin(),
      maxPrice: this.priceMax(),
      sortBy: this.sortBy() || undefined,
      page: this.currentPage(),
      pageSize: PAGE_SIZE,
    };

    this.hasActiveFilters.set(
      query.brands!.length > 0 ||
        query.sellers!.length > 0 ||
        query.categories!.length > 0 ||
        this.priceMin() !== null ||
        this.priceMax() !== null ||
        !!query.search,
    );

    const request$ =
      this.viewMode() === 'deals'
        ? this.dealsService.getDeals(query)
        : this.viewMode() === 'store'
          ? this.dealsService.getStoreDeals(query)
          : this.dealsService.getAllProducts(query);

    request$.subscribe({
      next: (result) => {
        this.deals.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages);
        this.loading.set(false);
        this.loadSparklines(result.items);
      },
      error: () => {
        this.error.set('Veriler yüklenemedi. API çalışıyor mu kontrol et.');
        this.loading.set(false);
      },
    });
  }

  private loadSparklines(deals: Deal[]): void {
    this.sparklines.set(new Map());
    const ids = deals.map((d) => d.productId);
    this.dealsService.getSparklines(ids).subscribe((result) => {
      this.sparklines.set(new Map(result.map((s) => [s.productId, s.points])));
    });
  }

  protected sparklineFor(productId: number): PricePoint[] {
    return this.sparklines().get(productId) ?? [];
  }

  protected discountBadge(deal: Deal): string {
    return `-%${deal.discountPercent}`;
  }

  protected storeDiscountBadge(deal: Deal): string {
    return `Mağaza -%${deal.storeDiscountPercent}`;
  }

  // Hero kartı: gerçek indirim varsa onu, yoksa mağaza kampanyasını gösterir.
  protected heroBadgeText(deal: Deal): string {
    return deal.discountPercent > 0 ? this.discountBadge(deal) : this.storeDiscountBadge(deal);
  }

  protected goToStoreUrl(deal: Deal): string {
    return this.priceHistoryService.goToStoreUrl(deal.productId, deal.storeUrl);
  }

  /** Mağaza tıklamasını sayar; bağlantı doğrudan mağazaya gittiği için
   *  sayacı artık /go/{id} artıramıyor (bkz. PriceHistoryService). */
  protected magazaTiklamasi(productId: number): void {
    this.priceHistoryService.trackStoreClick(productId);
  }

  private loadHeroDeal(): void {
    this.dealsService.getDeals({ pageSize: 1 }).subscribe({
      next: (result) => {
        if (result.items.length > 0) {
          this.setHeroDeal(result.items[0]);
          return;
        }
        // Hiç gerçek indirim yoksa (fiyat geçmişi henüz yeniyken sık
        // rastlanan bir durum) mağaza kampanyalarının en yükseğine düş.
        this.dealsService.getStoreDeals({ pageSize: 1 }).subscribe((storeResult) => {
          if (storeResult.items.length > 0) this.setHeroDeal(storeResult.items[0]);
        });
      },
    });
  }

  private setHeroDeal(deal: Deal): void {
    this.heroDeal.set(deal);
    this.priceHistoryService.get(deal.productId, 30).subscribe((history) => this.heroPoints.set(history.points));
  }

  protected onAlarmSubmit(): void {
    const value = this.alarmEmail().trim();
    if (!value) return;

    this.alarmSubmitting.set(true);
    this.subscribeService.subscribe(value).subscribe({
      next: (result) => {
        this.alarmStatusMessage.set(result.message);
        this.alarmEmail.set('');
        this.alarmSubmitting.set(false);
      },
      error: () => {
        this.alarmStatusMessage.set('Bir şeyler ters gitti, birazdan tekrar dener misin?');
        this.alarmSubmitting.set(false);
      },
    });
  }

  protected lastCheckedText(deal: Deal): string {
    return formatRelativeTime(deal.scrapedAt);
  }

  // CATEGORY_LABELS'ta (footer/kategori sayfası ile paylaşılan tek kaynak)
  // tanımlı doğru Türkçe etiket varsa onu kullan; yoksa (beklenmeyen bir
  // slug gelirse) eski basit tire→boşluk dönüşümüne düş.
  protected categoryLabel(category: string): string {
    return (
      CATEGORY_LABELS[category] ??
      category
        .split('-')
        .map((word) => word.charAt(0).toLocaleUpperCase('tr') + word.slice(1))
        .join(' ')
    );
  }

  // Kategoriler dropdown'ındaki ikon — kullanıcı geri bildirimi: liste
  // sadece düz metindi (bkz. site-header.ts'teki aynı desen).
  protected categoryIconPath(category: string): string {
    return CATEGORY_ICON_PATHS[category] ?? DEFAULT_CATEGORY_ICON;
  }

  protected categoryPhosphorIcon(category: string): string {
    return categoryPhosphorIcon(category);
  }

  protected setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
  }

  protected toggleCategories(): void {
    this.categoriesOpen.update((open) => !open);
  }

  protected closeCategories(): void {
    this.categoriesOpen.set(false);
  }

  // Kartlardaki bağlantılar RouterLink ile kuruluyor (bkz. core/product-link.ts):
  // gerçek bir <a href> üretiyor — arama motorları takip edebiliyor, orta tık ve
  // "yeni sekmede aç" çalışıyor — ama tıklandığında yine SPA gezinmesi yapıyor,
  // yani aşağıdaki openDeal ile birebir aynı sonucu veriyor.
  protected productPath(deal: Deal): string {
    return productPath(deal);
  }

  protected openDeal(deal: Deal): void {
    this.router.navigate(['/urun', deal.productId, slugify(deal.productName)], { queryParamsHandling: 'preserve' });
  }

  /**
   * Modalı kapat.
   *
   * Uygulama içinden girildiyse GERİ gidiyoruz, '/' adresine yeni bir
   * navigasyon YAPMIYORUZ. Eskiden navigate(['/']) çağrılıyordu ve bu
   * geçmişe ÜÇÜNCÜ bir kayıt ekliyordu: [/] -> [/urun/123] -> [/]. Sonuç,
   * telefonun geri tuşunun tersine çalışması: kullanıcı modalı kapattıktan
   * sonra geri tuşuna basınca kapattığı modal yeniden açılıyordu. Aynı
   * kalıp birkaç ürün gezildiğinde geçmişi tamamen şişiriyor ve "geri"
   * kullanıcıyı gezdiği ürünlerin arasında ileri geri dolaştırıyordu.
   *
   * Doğrudan /urun/:id ile girildiyse (arama motorundan gelen ziyaretçi,
   * paylaşılan bağlantı) geride bu sitenin bir sayfası YOK — orada
   * location.back() kullanıcıyı siteden tamamen çıkarırdı, bu yüzden ana
   * sayfaya gerçek bir navigasyon yapılıyor. Ayrımı `uygulamaIciGezinme`
   * sayacı veriyor. Bileşen İLK navigasyon sırasında oluştuğu için o
   * navigasyonun NavigationEnd'ini KAÇIRIYOR; dolayısıyla sayaç 0 ise
   * kullanıcı doğrudan /urun/:id ile gelmiş, 1+ ise uygulama içinde en az
   * bir kez gezinmiş demektir. (İlk denemede eşik 1 yazılmıştı ve düzeltme
   * sessizce çalışmadı — tarayıcıda geçmiş uzunluğu ölçülerek bulundu.)
   */
  protected closeDeal(): void {
    if (this.uygulamaIciGezinme > 0) {
      this.location.back();
      return;
    }

    this.router.navigate(['/'], { queryParamsHandling: 'preserve' });
  }

  // Gerçek besin değeri verisi olan ürünlerde (şimdilik sadece HIQ) servis
  // başı fiyat gösteriyoruz. Sadece paket boyutu gram cinsindeyse hesaplıyoruz
  // — "adet/kapsül" gibi birimlerde gram varsayımı yanlış olur, o yüzden
  // uydurmak yerine null dönüp göstermiyoruz.
  protected pricePerServing(deal: Deal): number | null {
    if (!deal.servingSizeGrams || deal.servingSizeGrams <= 0 || !deal.size) return null;

    const match = /^(\d+(?:[.,]\d+)?)\s*Gr$/i.exec(deal.size.trim());
    if (!match) return null;

    const packageGrams = Number(match[1].replace(',', '.'));
    if (!packageGrams) return null;

    const servings = packageGrams / deal.servingSizeGrams;
    if (!servings) return null;

    return deal.currentPrice / servings;
  }

  @HostListener('document:keydown', ['$event'])
  protected onGlobalKeydown(event: KeyboardEvent): void {
    const isShortcut = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
    if (!isShortcut) return;

    event.preventDefault();
    this.searchInput()?.nativeElement.focus();
  }

  @HostListener('window:scroll')
  protected onWindowScroll(): void {
    this.showScrollTop.set(window.scrollY > 400);
  }

  protected scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
