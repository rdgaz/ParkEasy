namespace ParkEasy.Application.Configuration;

public class VehicleTypePricing
{
    public decimal FirstHour { get; set; }
    public decimal AdditionalHour { get; set; }
}

public class PricingSettings
{
    public const string SectionName = "Pricing";

    public int GracePeriodMinutes { get; set; } = 10;
    public decimal DailyMaximum { get; set; } = 50.00m;

    public VehicleTypePricing Moto { get; set; } = new() { FirstHour = 5.00m, AdditionalHour = 3.00m };
    public VehicleTypePricing Carro { get; set; } = new() { FirstHour = 10.00m, AdditionalHour = 5.00m };
    public VehicleTypePricing VagaDupla { get; set; } = new() { FirstHour = 15.00m, AdditionalHour = 8.00m };
}
