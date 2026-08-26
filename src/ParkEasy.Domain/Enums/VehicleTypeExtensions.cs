namespace ParkEasy.Domain.Enums;

public static class VehicleTypeExtensions
{
    public static string ToDisplayName(this VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.Moto => "Moto",
        VehicleType.Carro => "Carro",
        VehicleType.VagaDupla => "Vaga Dupla",
        _ => vehicleType.ToString()
    };
}
