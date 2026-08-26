using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.DTOs;

public class ParkingTicket
{
    public string TicketNumber { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string? VehicleModel { get; set; }
    public string? CustomerName { get; set; }
    public DateTime EntryDateTime { get; set; }
}
