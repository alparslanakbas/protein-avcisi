using IndirimTakip.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndirimTakip.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();

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
            ph.HasOne(x => x.Product)
                .WithMany(x => x.PriceHistories)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            ph.HasIndex(x => new { x.ProductId, x.ScrapedAt });
        });
    }
}
