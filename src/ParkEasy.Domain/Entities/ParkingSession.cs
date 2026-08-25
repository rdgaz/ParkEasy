using ParkEasy.Domain.Enums;

namespace ParkEasy.Domain.Entities;

public class ParkingSession
{
    public long Id { get; set; }

    public string TicketNumber { get; set; } = string.Empty;

    public string Plate { get; set; } = string.Empty;

    public string? VehicleModel { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public DateTime EntryDateTime { get; set; }

    public DateTime? ExitDateTime { get; set; }

    public ParkingSessionStatus Status { get; set; }

    public decimal? FinalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
