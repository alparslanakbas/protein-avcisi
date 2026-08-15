using IndirimTakip.Infrastructure.Scraping;

namespace IndirimTakip.Infrastructure.Tests;

public class ProductAttributeParserTests
{
    // Regresyon testi: tr-TR kültürüyle küçük harfe çevirme, büyük "I"yi
    // noktasız "ı" yapıp İngilizce anahtar kelimelerle eşleşmeyi
    // engelliyordu (bkz. CLAUDE.md, 2026-08-14 "kreatin araması" bug'ı).
    [Theory]
    [InlineData("CREATINE", "kreatin")]
    [InlineData("CREATINE CHEWS", "kreatin")]
    [InlineData("HIQ Creatine 240g", "kreatin")]
    [InlineData("SSN ... Whey Protein Tozu", "protein-tozu")]
    [InlineData("HIQ Beta Alanine 300g", "amino-asitler")]
    [InlineData("HIQ B.M.F. Extreme Thermo Burner", "yag-yakici")]
    [InlineData("Bilinmeyen bir ürün adı", null)]
    public void InferCategory_dogru_kategoriyi_donuyor(string productName, string? expectedCategory)
    {
        var result = ProductAttributeParser.InferCategory(productName);

        Assert.Equal(expectedCategory, result);
    }

    [Fact]
    public void InferCategory_ilk_eslesen_kategoriyi_donuyor()
    {
        // "protein" hem protein-tozu hem (dolaylı olarak) başka kategorilerde
        // geçebilir — CategoryKeywords sırasına göre İLK eşleşen kazanmalı.
        var result = ProductAttributeParser.InferCategory("Whey Protein Creatine Kombinasyonu");

        Assert.Equal("protein-tozu", result);
    }

    [Theory]
    [InlineData("SSN Whey 2100 Gr Protein Tozu", "2100 Gr")]
    [InlineData("HIQ Creatine 240g", "240 Gr")]
    [InlineData("Ürün 1,5 Kg Paket", "1.5 Kg")]
    [InlineData("Boyut bilgisi olmayan ürün", null)]
    public void ExtractSize_dogru_boyutu_cikariyor(string productName, string? expectedSize)
    {
        var result = ProductAttributeParser.ExtractSize(productName);

        Assert.Equal(expectedSize, result);
    }

    [Theory]
    [InlineData("SSN Whey 2100 Gr (Çikolata) Protein Tozu", "Çikolata")]
    [InlineData("Ürün (60 Ml *18 Adet)", null)] // boyut bilgisi taşıyan parantez aroma sayılmamalı
    [InlineData("Aroma parantezi olmayan ürün", null)]
    public void ExtractFlavor_boyut_parantezini_aroma_sanmiyor(string productName, string? expectedFlavor)
    {
        var result = ProductAttributeParser.ExtractFlavor(productName);

        Assert.Equal(expectedFlavor, result);
    }

    [Fact]
    public void GetSearchSynonyms_kreatin_aramasi_creatine_i_de_iceriyor()
    {
        var synonyms = ProductAttributeParser.GetSearchSynonyms("kreatin");

        Assert.Contains("creatine", synonyms);
        Assert.Contains("kreatin", synonyms);
    }

    [Fact]
    public void GetSearchSynonyms_eslesmeyen_terim_icin_bos_donuyor()
    {
        var synonyms = ProductAttributeParser.GetSearchSynonyms("alakasız-terim-xyz");

        Assert.Empty(synonyms);
    }
}
