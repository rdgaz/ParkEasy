using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Interfaces;

public interface IParkingFeeCalculator
{
    decimal CalculateFee(DateTime entryDateTime, DateTime exitDateTime, VehicleType vehicleType, bool hasWash);
    decimal CalculateCurrentFee(DateTime entryDateTime, VehicleType vehicleType, bool hasWash);
}
