using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

public class TurkishPriceParserTests
{
    [Theory]
    [InlineData("2.899,00TL", 2899.00)]
    [InlineData("2.899,00 TL", 2899.00)]
    [InlineData("69,00TL", 69.00)]
    [InlineData("1.049,30 TL", 1049.30)]
    public void Parse_turkce_fiyat_formatini_dogru_cozumluyor(string text, decimal expected)
    {
        var result = TurkishPriceParser.Parse(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParsePricePair_tek_fiyat_varsa_eski_fiyat_null_donuyor()
    {
        // Hardline'da indirim yoksa hiç span'siz düz metin tek fiyat geliyordu.
        var (current, storeOld) = TurkishPriceParser.ParsePricePair("6.369,00 TL");

        Assert.Equal(6369.00m, current);
        Assert.Null(storeOld);
    }

    [Fact]
    public void ParsePricePair_iki_fiyat_varsa_sonuncusu_guncel_fiyat()
    {
        // Hardline'da hem eski hem yeni fiyat aynı konteynerde geliyordu,
        // güncel fiyat metinde her zaman en sonda çıkıyor.
        var (current, storeOld) = TurkishPriceParser.ParsePricePair("2.675,00 TL 2.279,00 TL");

        Assert.Equal(2279.00m, current);
        Assert.Equal(2675.00m, storeOld);
    }

    [Fact]
    public void ParsePricePair_fiyat_deseni_yoksa_hata_firlatiyor()
    {
        Assert.Throws<FormatException>(() => TurkishPriceParser.ParsePricePair("Stokta yok"));
    }
}
