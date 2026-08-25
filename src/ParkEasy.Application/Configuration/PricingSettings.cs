namespace ParkEasy.Application.Configuration;

public class PricingSettings
{
    public const string SectionName = "Pricing";

    public decimal FirstHour { get; set; } = 10.00m;
    public decimal AdditionalHour { get; set; } = 5.00m;
    public int GracePeriodMinutes { get; set; } = 10;
    public decimal DailyMaximum { get; set; } = 50.00m;
}
