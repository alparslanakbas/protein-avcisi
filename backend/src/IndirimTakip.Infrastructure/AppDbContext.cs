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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.BaseUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Product>(p =>
        {
            p.Property(x => x.Name).HasMaxLength(500);
            p.Property(x => x.Url).HasMaxLength(1000);
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
            c.HasOne(x => x.Brand)
                .WithMany()
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
