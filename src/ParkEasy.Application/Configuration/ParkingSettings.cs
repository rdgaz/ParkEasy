namespace ParkEasy.Application.Configuration;

public class ParkingSettings
{
    public const string SectionName = "Parking";

    public int TotalSpaces { get; set; } = 50;
}
