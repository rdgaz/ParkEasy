using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Services;
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
            FirstHour = 10.00m,
            AdditionalHour = 5.00m,
            GracePeriodMinutes = 10,
            DailyMaximum = 50.00m
        };

        var options = Options.Create(_pricingSettings);
        _calculator = new ParkingFeeCalculator(options);
    }

    [Fact]
    public void CalculateFee_ZeroMinutes_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry;

        var fee = _calculator.CalculateFee(entry, exit);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_WithinGracePeriod_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(9);

        var fee = _calculator.CalculateFee(entry, exit);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_ExactlyGracePeriodLimit_ReturnsZero()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(10);

        var fee = _calculator.CalculateFee(entry, exit);

        Assert.Equal(0m, fee);
    }

    [Fact]
    public void CalculateFee_JustOverGracePeriod_ReturnsFirstHourFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddMinutes(11);

        var fee = _calculator.CalculateFee(entry, exit);

        Assert.Equal(10.00m, fee);
    }

    [Fact]
    public void CalculateFee_ExactlyOneHour_ReturnsFirstHourFee()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1);

        var fee = _calculator.CalculateFee(entry, exit);

        Assert.Equal(10.00m, fee);
    }

    [Fact]
    public void CalculateFee_OneHourOneMinute_ReturnsFirstHourPlusOneAdditionalHour()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(1).AddMinutes(1);

        var fee = _calculator.CalculateFee(entry, exit);

        // First hour (10.00) + 1 additional hour started (5.00) = 15.00
        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_TwoHours_ReturnsFirstHourPlusOneAdditionalHour()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(2);

        var fee = _calculator.CalculateFee(entry, exit);

        Assert.Equal(15.00m, fee);
    }

    [Fact]
    public void CalculateFee_FiveHours_ReturnsThirty()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(5);

        var fee = _calculator.CalculateFee(entry, exit);

        // 10.00 + (4 * 5.00) = 30.00
        Assert.Equal(30.00m, fee);
    }

    [Fact]
    public void CalculateFee_MultipleHours_DoesNotExceedDailyMaximum()
    {
        var entry = DateTime.Now;
        var exit = entry.AddHours(15);

        var fee = _calculator.CalculateFee(entry, exit);

        // Calculated: 10 + (14 * 5) = 80, but capped at DailyMaximum 50.00
        Assert.Equal(50.00m, fee);
    }
}
