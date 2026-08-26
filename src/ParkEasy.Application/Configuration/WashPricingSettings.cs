namespace ParkEasy.Application.Configuration;

/// <summary>
/// Tipos de lavagem e seus valores sugeridos, definidos livremente em appsettings.json
/// (seção "WashPricing") — sem lista fixa no código, o operador cadastra os nomes que quiser.
/// </summary>
public class WashPricingSettings : Dictionary<string, decimal>
{
    public const string SectionName = "WashPricing";
}
