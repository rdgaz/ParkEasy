using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Interfaces;

public interface IParkingFeeCalculator
{
    decimal CalculateFee(DateTime entryDateTime, DateTime exitDateTime, VehicleType vehicleType);
    decimal CalculateCurrentFee(DateTime entryDateTime, VehicleType vehicleType);
    decimal GetFlatRate(VehicleType vehicleType, string serviceType);
}
