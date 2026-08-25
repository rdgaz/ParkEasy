using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;

namespace ParkEasy.Application.Services;

public class ParkingFeeCalculator : IParkingFeeCalculator
{
    private readonly PricingSettings _pricing;

    public ParkingFeeCalculator(IOptions<PricingSettings> pricingOptions)
    {
        _pricing = pricingOptions.Value;
    }

    public decimal CalculateFee(DateTime entryDateTime, DateTime exitDateTime)
    {
        if (exitDateTime <= entryDateTime)
            return 0m;

        var totalMinutes = (exitDateTime - entryDateTime).TotalMinutes;

        // Grace period — free
        if (totalMinutes <= _pricing.GracePeriodMinutes)
            return 0m;

        // First hour
        decimal fee = _pricing.FirstHour;

        if (totalMinutes <= 60)
            return Math.Min(fee, _pricing.DailyMaximum);

        // Additional hours (each started hour counts as full)
        var minutesBeyondFirstHour = totalMinutes - 60;
        var additionalHours = (int)Math.Ceiling(minutesBeyondFirstHour / 60.0);
        fee += additionalHours * _pricing.AdditionalHour;

        // Daily maximum cap
        return Math.Min(fee, _pricing.DailyMaximum);
    }

    public decimal CalculateCurrentFee(DateTime entryDateTime)
    {
        return CalculateFee(entryDateTime, DateTime.Now);
    }
}
