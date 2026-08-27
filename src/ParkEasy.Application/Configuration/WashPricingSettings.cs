namespace ParkEasy.Application.Configuration;

public class WashTypeConfig
{
    public decimal Price { get; set; }
    public int AverageMinutes { get; set; }
}

/// <summary>
/// Tipos de lavagem e seus valores/tempo médio sugeridos, definidos livremente em
/// appsettings.json (seção "WashPricing") — sem lista fixa no código, o operador cadastra
/// os nomes que quiser.
/// </summary>
public class WashPricingSettings : Dictionary<string, WashTypeConfig>
{
    public const string SectionName = "WashPricing";
}
