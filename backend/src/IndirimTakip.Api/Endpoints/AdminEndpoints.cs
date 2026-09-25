using IndirimTakip.Core.Caching;
using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Articles;
using IndirimTakip.Infrastructure.Catalog;
using IndirimTakip.Infrastructure.Coupons;
using IndirimTakip.Infrastructure.Deals;
using IndirimTakip.Infrastructure.Scraping;
using IndirimTakip.Infrastructure.Subscribers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IndirimTakip.Api.Endpoints;

// Yönetim uçları (/api/dev/*). HEPSİ X-Admin-Key ile korunuyor —
// 2026-08-15'te bu uçlar korumasızdı ve herkes tarama tetikleyip sahte
// kupon ekleyebiliyordu.
internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app, string? adminApiKey)
    {
        app.MapAdminKatalogEndpoints(adminApiKey);
        app.MapAdminAboneEndpoints(adminApiKey);
        app.MapAdminGozlemEndpoints(adminApiKey);
    }
}

internal record GorunurlukIstegi(bool IsActive);

/// <summary>Kategori kodu; null ya da boş = otomatik kategoriye dön.</summary>
internal record KategoriIstegi(string? Kategori);
