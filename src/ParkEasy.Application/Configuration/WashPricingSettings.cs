namespace ParkEasy.Application.Configuration;

public class WashPricingSettings
{
    public const string SectionName = "WashPricing";

    public decimal Expressa { get; set; } = 15.00m;
    public decimal Completa { get; set; } = 35.00m;
    public decimal Interna { get; set; } = 20.00m;
}
