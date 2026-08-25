namespace ParkEasy.Application.Interfaces;

public interface IParkingFeeCalculator
{
    decimal CalculateFee(DateTime entryDateTime, DateTime exitDateTime);
    decimal CalculateCurrentFee(DateTime entryDateTime);
}
