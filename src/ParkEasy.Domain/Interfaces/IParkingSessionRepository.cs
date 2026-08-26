using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.Domain.Interfaces;

public interface IParkingSessionRepository
{
    Task<List<ParkingSession>> GetActiveSessionsAsync();
    Task<List<ParkingSession>> SearchActiveSessionsAsync(string searchTerm);
    Task<ParkingSession?> GetByIdAsync(long id);
    Task<ParkingSession?> GetByTicketNumberAsync(string ticketNumber);
    Task<ParkingSession?> GetActiveByPlateAsync(string normalizedPlate);
    Task<ParkingSession?> GetMostRecentByPlateAsync(string normalizedPlate);
    Task<List<ParkingSession>> GetCompletedSessionsAsync(
        DateTime? startDate, DateTime? endDate,
        string? plate, string? ticketNumber, string? customerName,
        VehicleType? vehicleType);
    Task AddAsync(ParkingSession session);
    Task UpdateAsync(ParkingSession session);
    Task<int> GetNextTicketSequenceAsync();
    Task<decimal> GetTodayRevenueAsync();
    Task<int> GetActiveCountAsync();
    Task<int> GetOccupiedSpacesAsync();
}
