namespace IndirimTakip.Infrastructure.Deals;

public record FilterOptionsDto(IReadOnlyList<string> Brands, IReadOnlyList<string> Categories);
