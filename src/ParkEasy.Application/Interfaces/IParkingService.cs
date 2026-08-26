using ParkEasy.Application.DTOs;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Interfaces;

public interface IParkingService
{
    Task<ParkingSession> RegisterEntryAsync(string plate, VehicleType vehicleType, string? vehicleModel, string? customerName, string? customerPhone);
    Task<ParkingSession> FinalizeSessionAsync(long sessionId);
    Task<List<ParkingSession>> GetActiveSessionsAsync();
    Task<List<ParkingSession>> SearchActiveSessionsAsync(string searchTerm);
    Task<List<ParkingSession>> GetHistoryAsync(HistoryFilter filter);
    Task<DashboardData> GetDashboardDataAsync();
    Task<(decimal totalRevenue, int totalVehicles)> GetHistorySummaryAsync(HistoryFilter filter);
    Task<ParkingTicket> BuildTicketAsync(ParkingSession session);
    Task<ParkingReceipt> BuildReceiptAsync(ParkingSession session);
}
