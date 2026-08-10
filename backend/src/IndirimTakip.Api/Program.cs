using IndirimTakip.Core.Scraping;
using IndirimTakip.Infrastructure;
using IndirimTakip.Infrastructure.Scraping;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

const string AngularDevCorsPolicy = "AngularDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(AngularDevCorsPolicy);

// Geçici tetikleme endpoint'i: gerçek zamanlanmış worker (roadmap'teki
// BackgroundService) eklenene kadar taramayı elle tetiklemek için.
app.MapPost("/api/dev/ingest/{brand}", async (string brand, IEnumerable<IBrandScraper> scrapers, ScrapeIngestionService ingestion, CancellationToken ct) =>
{
    var scraper = scrapers.FirstOrDefault(s => s.BrandName.Equals(brand, StringComparison.OrdinalIgnoreCase));
    if (scraper is null)
        return Results.NotFound($"'{brand}' için scraper bulunamadı.");

    var count = await ingestion.IngestAsync(scraper, ct);
    return Results.Ok(new { brand = scraper.BrandName, scrapedCount = count });
});

app.Run();
