using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.DTOs;

public class ParkingReceipt
{
    public string TicketNumber { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string? VehicleModel { get; set; }
    public string? CustomerName { get; set; }
    public DateTime EntryDateTime { get; set; }
    public DateTime ExitDateTime { get; set; }
    public TimeSpan Duration { get; set; }
    public decimal FinalAmount { get; set; }
}
