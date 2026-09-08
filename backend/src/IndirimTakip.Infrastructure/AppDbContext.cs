using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<ProductWatch> ProductWatches => Set<ProductWatch>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ProductFavorite> ProductFavorites => Set<ProductFavorite>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<BackgroundJobRun> BackgroundJobRuns => Set<BackgroundJobRun>();
    public DbSet<AdminOperationFailure> AdminOperationFailures => Set<AdminOperationFailure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.BaseUrl).HasMaxLength(500);
        });

        // GIZLENEN URUNLER HER SORGUDAN OTOMATIK DUSUYOR.
        //
        // Neden global filtre: urun sorgulari 12 dosyaya yayilmis ve tek
        // basina DealsQueryService'te 20 tane var (liste, kategori, marka,
        // arama, karsilastirma, sitemap, urun sayfasi...). Her birine elle
        // kosul eklemek, birini atlayip gizlenen urunu sitemap'te birakmanin
        // en kolay yoluydu. Filtre tek yerde duruyor ve unutulamiyor.
        //
        // GORMESI GEREKENLER IgnoreQueryFilters() KULLANIYOR:
        //   - ScrapeIngestionService: gizli urunu bulamazsa KOPYASINI olusturur
        //   - Yonetim paneli: gizli urunu listeleyip geri acabilmeli
        modelBuilder.Entity<Product>().HasQueryFilter(p => p.IsActive);

        // URUNE BAGLI KAYITLARA DA ESLESEN FILTRE (EF bunu acikca uyariyor).
        //
        // Bunlar urune ZORUNLU bagli: filtre yalnizca Product'ta olsaydi,
        // gizlenmis bir urunun takip/favori/fiyat kaydi sorguya girip
        // navigasyonu bos donerdi - "unexpected results" dedigi tam olarak bu.
        //
        // Davranis dogru olan: gizlenen urun kullanicinin takip listesinden de
        // dusuyor. Satirlar SILINMIYOR; urun geri acilinca kayitlar da geri
        // geliyor.
        modelBuilder.Entity<ProductWatch>().HasQueryFilter(w => w.Product!.IsActive);
        modelBuilder.Entity<ProductFavorite>().HasQueryFilter(f => f.Product!.IsActive);
        modelBuilder.Entity<PriceHistory>().HasQueryFilter(ph => ph.Product!.IsActive);

        modelBuilder.Entity<Product>(p =>
        {
            p.Property(x => x.Name).HasMaxLength(500);
            p.Property(x => x.Url).HasMaxLength(1000);
            // Yalnizca dosya adi tutuluyor (24 hex + ".webp" = 29 karakter);
            // dizin ve genel adres yapilandirmadan geliyor, veriye gomulmuyor.
            p.Property(x => x.LocalImagePath).HasMaxLength(64);
            p.HasOne(x => x.Brand)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PriceHistory>(ph =>
        {
            ph.Property(x => x.Price).HasPrecision(10, 2);
            ph.Property(x => x.StoreOldPrice).HasPrecision(10, 2);
            ph.HasOne(x => x.Product)
                .WithMany(x => x.PriceHistories)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            ph.HasIndex(x => new { x.ProductId, x.ScrapedAt });
        });

        modelBuilder.Entity<Coupon>(c =>
        {
            c.Property(x => x.Code).HasMaxLength(100);
            c.Property(x => x.Description).HasMaxLength(500);
            c.Property(x => x.Seller).HasMaxLength(200);
            c.HasOne(x => x.Brand)
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
            c.ToTable(t => t.HasCheckConstraint(
                "CK_Coupons_ExactlyOneTarget",
                "(\"BrandId\" IS NULL) <> (\"Seller\" IS NULL)"));
        });

        modelBuilder.Entity<Subscriber>(s =>
        {
            s.Property(x => x.Email).HasMaxLength(320);
            s.Property(x => x.Token).HasMaxLength(64);
            s.HasIndex(x => x.Email).IsUnique();
            s.HasIndex(x => x.Token).IsUnique();
        });

        modelBuilder.Entity<ProductWatch>(w =>
        {
            w.HasOne(x => x.Subscriber)
                .WithMany()
                .HasForeignKey(x => x.SubscriberId)
                .OnDelete(DeleteBehavior.Cascade);
            w.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            w.HasIndex(x => new { x.SubscriberId, x.ProductId }).IsUnique();
        });

        modelBuilder.Entity<Article>(a =>
        {
            a.Property(x => x.Title).HasMaxLength(200);
            a.Property(x => x.Slug).HasMaxLength(200);
            a.Property(x => x.Summary).HasMaxLength(500);
            a.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<ProductFavorite>(f =>
        {
            f.HasOne(x => x.Subscriber)
                .WithMany()
                .HasForeignKey(x => x.SubscriberId)
                .OnDelete(DeleteBehavior.Cascade);
            f.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            f.HasIndex(x => new { x.SubscriberId, x.ProductId }).IsUnique();
        });

        modelBuilder.Entity<SecurityEvent>(e =>
        {
            e.Property(x => x.Ip).HasMaxLength(45);          // IPv6 tam uzunluk
            e.Property(x => x.Kind).HasMaxLength(30);
            e.Property(x => x.Method).HasMaxLength(10);
            e.Property(x => x.Path).HasMaxLength(500);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.Country).HasMaxLength(2);

            // Panelde varsayilan gorunum "en yeniden eskiye".
            e.HasIndex(x => x.OccurredAt).IsDescending();

            // ASIL SORGU BU: "su adres neler yapmis". Bir suc duyurusunda
            // tek bir IP'nin butun gecmisini cikarmak gerekiyor; tarihe gore
            // index bunu karsilamaz.
            e.HasIndex(x => new { x.Ip, x.OccurredAt });

            e.HasIndex(x => x.Kind);
        });

        modelBuilder.Entity<AdminOperationFailure>(e =>
        {
            e.Property(x => x.Method).HasMaxLength(10);
            e.Property(x => x.Path).HasMaxLength(500);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.Property(x => x.Reason).HasMaxLength(2000);

            // Tek gorunum "en yeniden eskiye"; baska bir sorgu sekli yok.
            e.HasIndex(x => x.OccurredAt).IsDescending();
        });

        modelBuilder.Entity<BackgroundJobRun>(j =>
        {
            j.Property(x => x.JobName).HasMaxLength(60);
            // Is basina TEK satir olmali: iki satir olusursa "sirasi geldi mi"
            // sorusu hangisine bakildigina gore farkli cevap verirdi.
            j.HasIndex(x => x.JobName).IsUnique();
        });
    }
}
