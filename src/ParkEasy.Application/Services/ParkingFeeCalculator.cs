using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Services;

public class ParkingFeeCalculator : IParkingFeeCalculator
{
    private readonly PricingSettings _pricing;

    public ParkingFeeCalculator(IOptions<PricingSettings> pricingOptions)
    {
        _pricing = pricingOptions.Value;
    }

    public decimal CalculateFee(DateTime entryDateTime, DateTime exitDateTime, VehicleType vehicleType, bool hasWash)
    {
        if (exitDateTime <= entryDateTime)
            return 0m;

        if (hasWash && _pricing.ExemptWashFromParkingFee)
            return 0m;

        var pricing = GetPricingFor(vehicleType);
        var totalMinutes = (exitDateTime - entryDateTime).TotalMinutes;

        // Grace period — free
        if (totalMinutes <= _pricing.GracePeriodMinutes)
            return 0m;

        // First hour
        decimal fee = pricing.FirstHour;

        if (totalMinutes <= 60)
            return Math.Min(fee, _pricing.DailyMaximum);

        // Additional hours (each started hour counts as full)
        var minutesBeyondFirstHour = totalMinutes - 60;
        var additionalHours = (int)Math.Ceiling(minutesBeyondFirstHour / 60.0);
        fee += additionalHours * pricing.AdditionalHour;

        // Daily maximum cap
        return Math.Min(fee, _pricing.DailyMaximum);
    }

    public decimal CalculateCurrentFee(DateTime entryDateTime, VehicleType vehicleType, bool hasWash)
    {
        return CalculateFee(entryDateTime, DateTime.Now, vehicleType, hasWash);
    }

    private VehicleTypePricing GetPricingFor(VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.Moto => _pricing.Moto,
        VehicleType.Carro => _pricing.Carro,
        VehicleType.VagaDupla => _pricing.VagaDupla,
        _ => throw new ArgumentOutOfRangeException(nameof(vehicleType), vehicleType, "Tipo de veículo desconhecido.")
    };
}
