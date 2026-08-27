using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Enums;
using Xunit;

namespace ParkEasy.Tests;

public class ParkingFeeCalculatorTests
{
    private readonly ParkingFeeCalculator _calculator;
    private readonly PricingSettings _pricingSettings;

    public ParkingFeeCalculatorTests()
    {
        _pricingSettings = new PricingSettings
        {
            GracePeriodMinutes = 10,
            DailyMaximum = 50.00m,
            Moto = new VehicleTypePricing { FirstHour = 6.00m, AdditionalHour = 3.00m },
            Carro = new VehicleTypePricing { FirstHour = 10.00m, AdditionalHour = 5.00m },
            VagaDupla = new VehicleTypePricing { FirstHour = 18.00m, AdditionalHour = 9.00m }
        };

        var options = Options.Create(_pricingSettings);
        _calculator = new ParkingFeeCalculator(options);
    }

    [Fact]
    public void CalculateFee_ZeroMinutes_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry;

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_WithinGracePeriod_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(9);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_ExactlyGracePeriodLimit_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(10);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_JustOverGracePeriod_ReturnsFirstHourFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(11);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(10.00m, fee);
    }

    [Fact]
    public void CalculateFee_ExactlyOneHour_ReturnsFirstHourFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(10.00m, fee);
    }

    [Fact]
    public void CalculateFee_OneHourOneMinute_ReturnsFirstHourPlusOneAdditionalHour()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1).AddMinutes(1);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        // First hour (10.00) + 1 additional hour started (5.00) = 15.00
        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_TwoHours_ReturnsFirstHourPlusOneAdditionalHour()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_FiveHours_ReturnsThirty()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(5);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        // 10.00 + (4 * 5.00) = 30.00
        Assert.Equal(30.00m, fee);
    }

    [Fact]
    public void CalculateFee_MultipleHours_DoesNotExceedDailyMaximum()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(15);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        // Calculated: 10 + (14 * 5) = 80, but capped at DailyMaximum 50.00
        Assert.Equal(50.00m, fee);
    }

    [Fact]
    public void CalculateFee_Moto_UsesMotoRates()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Moto, hasWash: false);

        // First hour (6.00) + 1 additional hour started (3.00) = 9.00
        Assert.Equal(9.00m, fee);
    }

    [Fact]
    public void CalculateFee_VagaDupla_UsesVagaDuplaRates()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.VagaDupla, hasWash: false);

        // First hour (18.00) + 1 additional hour started (9.00) = 27.00
        Assert.Equal(27.00m, fee);
    }

    [Fact]
    public void CalculateFee_DifferentVehicleTypes_SameDurationDifferentFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1);

        var motoFee = _calculator.CalculateFee(entry, exit, VehicleType.Moto, hasWash: false);
        var carroFee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);
        var vagaDuplaFee = _calculator.CalculateFee(entry, exit, VehicleType.VagaDupla, hasWash: false);

        Assert.Equal(6.00m, motoFee);
        Assert.Equal(10.00m, carroFee);
        Assert.Equal(18.00m, vagaDuplaFee);
    }

    [Fact]
    public void CalculateFee_HasWashButExemptionDisabled_StillChargesParkingFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: true);

        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_HasWashAndExemptionEnabled_ReturnsZero()
    {
        _pricingSettings.ExemptWashFromParkingFee = true;

        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: true);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_NoWashButExemptionEnabled_StillChargesParkingFee()
    {
        _pricingSettings.ExemptWashFromParkingFee = true;

        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro, hasWash: false);

        Assert.Equal(15.00m, fee);
    }
}
