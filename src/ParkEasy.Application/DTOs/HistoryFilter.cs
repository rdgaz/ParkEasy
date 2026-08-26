using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.DTOs;

public class HistoryFilter
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Plate { get; set; }
    public string? TicketNumber { get; set; }
    public string? CustomerName { get; set; }
    public VehicleType? VehicleType { get; set; }
}
