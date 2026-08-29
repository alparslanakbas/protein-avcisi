using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

// Örnekler gerçek ürün sayfalarından alındı (2026-08-29). Markaların
// altyapıları farklı ama hepsi aynı schema.org alanlarını kullanıyor;
// bu testler o varsayımın sessizce bozulmasını engelliyor.
public class AggregateRatingParserTests
{
    [Fact]
    public void HiqSayfasindakiPuaniOkur()
    {
        const string html = """
            <script type="application/ld+json">
            {"@type":"Product","name":"HIQ Beta Alanine 300g Unflavored",
             "aggregateRating":{"@type":"AggregateRating","ratingValue":"4.77","reviewCount":31},
             "review":[{"@type":"Review","reviewRating":{"@type":"Rating","ratingValue":5,"bestRating":5}}]}
            </script>
            """;

        var (value, count) = AggregateRatingParser.Parse(html);

        Assert.Equal(4.77m, value);
        Assert.Equal(31, count);
    }

    [Fact]
    public void YesilmarkaSayfasindakiPuaniOkur()
    {
        const string html = """{"aggregateRating":{"@type":"AggregateRating","ratingValue":4.77,"reviewCount":30}}""";

        var (value, count) = AggregateRatingParser.Parse(html);

        Assert.Equal(4.77m, value);
        Assert.Equal(30, count);
    }

    [Fact]
    public void TekTekYorumlarinPuaniniOrtalamaSanmaz()
    {
        // Sayfada aggregateRating'ten ÖNCE tek bir yorumun kendi bloğu
        // geliyor. Ortalama olmayan bu değeri almamalı.
        const string html = """
            {"review":{"@type":"Review","reviewRating":{"ratingValue":5,"bestRating":5}},
             "aggregateRating":{"@type":"AggregateRating","ratingValue":"4.12","reviewCount":9}}
            """;

        var (value, count) = AggregateRatingParser.Parse(html);

        Assert.Equal(4.12m, value);
        Assert.Equal(9, count);
    }

    [Fact]
    public void PuanBloguYoksaNullDoner()
    {
        var (value, count) = AggregateRatingParser.Parse("""<div class="rating no-rating">0 yorum</div>""");

        Assert.Null(value);
        Assert.Null(count);
    }

    [Theory]
    [InlineData("""{"aggregateRating":{"ratingValue":9.4,"reviewCount":12}}""")]   // 5'lik olmayan ölçek
    [InlineData("""{"aggregateRating":{"ratingValue":4.5,"reviewCount":0}}""")]    // puanlayan yok
    public void GecersizDegerleriEler(string html)
    {
        var (value, count) = AggregateRatingParser.Parse(html);

        Assert.Null(value);
        Assert.Null(count);
    }

    [Fact]
    public void OndalikAyraciniKulturdenBagimsizOkur()
    {
        // Makinenin yerel ayarı Türkçe olsa bile "4.88" 488 olarak
        // okunmamalı — JSON her zaman nokta kullanır.
        var (value, _) = AggregateRatingParser.Parse("""{"aggregateRating":{"ratingValue":4.88,"reviewCount":278}}""");

        Assert.Equal(4.88m, value);
    }
}
