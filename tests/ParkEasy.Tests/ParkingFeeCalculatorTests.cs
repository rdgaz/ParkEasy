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
            Moto = new VehicleTypePricing { FirstHour = 6.00m, AdditionalHour = 3.00m, DailyRate = 25.00m, MonthlyRate = 200.00m },
            Carro = new VehicleTypePricing { FirstHour = 10.00m, AdditionalHour = 5.00m, DailyRate = 40.00m, MonthlyRate = 350.00m },
            VagaDupla = new VehicleTypePricing { FirstHour = 18.00m, AdditionalHour = 9.00m, DailyRate = 60.00m, MonthlyRate = 500.00m }
        };

        var options = Options.Create(_pricingSettings);
        _calculator = new ParkingFeeCalculator(options);
    }

    [Fact]
    public void CalculateFee_ZeroMinutes_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry;

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_WithinGracePeriod_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(9);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_ExactlyGracePeriodLimit_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(10);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_JustOverGracePeriod_ReturnsFirstHourFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(11);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        Assert.Equal(10.00m, fee);
    }

    [Fact]
    public void CalculateFee_ExactlyOneHour_ReturnsFirstHourFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        Assert.Equal(10.00m, fee);
    }

    [Fact]
    public void CalculateFee_OneHourOneMinute_ReturnsFirstHourPlusOneAdditionalHour()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1).AddMinutes(1);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        // First hour (10.00) + 1 additional hour started (5.00) = 15.00
        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_TwoHours_ReturnsFirstHourPlusOneAdditionalHour()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_FiveHours_ReturnsThirty()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(5);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        // 10.00 + (4 * 5.00) = 30.00
        Assert.Equal(30.00m, fee);
    }

    [Fact]
    public void CalculateFee_MultipleHours_DoesNotExceedDailyMaximum()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(15);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);

        // Calculated: 10 + (14 * 5) = 80, but capped at DailyMaximum 50.00
        Assert.Equal(50.00m, fee);
    }

    [Fact]
    public void CalculateFee_Moto_UsesMotoRates()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.Moto);

        // First hour (6.00) + 1 additional hour started (3.00) = 9.00
        Assert.Equal(9.00m, fee);
    }

    [Fact]
    public void CalculateFee_VagaDupla_UsesVagaDuplaRates()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit, VehicleType.VagaDupla);

        // First hour (18.00) + 1 additional hour started (9.00) = 27.00
        Assert.Equal(27.00m, fee);
    }

    [Fact]
    public void CalculateFee_DifferentVehicleTypes_SameDurationDifferentFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1);

        var motoFee = _calculator.CalculateFee(entry, exit, VehicleType.Moto);
        var carroFee = _calculator.CalculateFee(entry, exit, VehicleType.Carro);
        var vagaDuplaFee = _calculator.CalculateFee(entry, exit, VehicleType.VagaDupla);

        Assert.Equal(6.00m, motoFee);
        Assert.Equal(10.00m, carroFee);
        Assert.Equal(18.00m, vagaDuplaFee);
    }

    [Theory]
    [InlineData(VehicleType.Moto, "Diária", 25.00)]
    [InlineData(VehicleType.Carro, "Diária", 40.00)]
    [InlineData(VehicleType.VagaDupla, "Diária", 60.00)]
    [InlineData(VehicleType.Moto, "Mensal", 200.00)]
    [InlineData(VehicleType.Carro, "Mensal", 350.00)]
    [InlineData(VehicleType.VagaDupla, "Mensal", 500.00)]
    public void GetFlatRate_DiariaOrMensal_ReturnsConfiguredRate(VehicleType vehicleType, string serviceType, decimal expected)
    {
        var rate = _calculator.GetFlatRate(vehicleType, serviceType);

        Assert.Equal(expected, rate);
    }

    [Fact]
    public void GetFlatRate_UnknownServiceType_ReturnsZero()
    {
        var rate = _calculator.GetFlatRate(VehicleType.Carro, "Lav. Completa");

        Assert.Equal(0m, rate);
    }
}
