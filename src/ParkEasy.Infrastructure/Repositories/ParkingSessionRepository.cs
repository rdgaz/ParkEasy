using Microsoft.EntityFrameworkCore;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;
using ParkEasy.Infrastructure.Data;

namespace ParkEasy.Infrastructure.Repositories;

public class ParkingSessionRepository : IParkingSessionRepository
{
    private readonly ParkingDbContext _context;

    public ParkingSessionRepository(ParkingDbContext context)
    {
        _context = context;
    }

    public async Task<List<ParkingSession>> GetActiveSessionsAsync()
    {
        return await _context.ParkingSessions
            .Where(s => s.Status == ParkingSessionStatus.Active)
            .OrderBy(s => s.EntryDateTime)
            .ToListAsync();
    }

    public async Task<List<ParkingSession>> SearchActiveSessionsAsync(string searchTerm)
    {
        var term = searchTerm.ToUpperInvariant();

        return await _context.ParkingSessions
            .Where(s => s.Status == ParkingSessionStatus.Active &&
                (s.Plate.Contains(term) ||
                 s.TicketNumber.Contains(term) ||
                 (s.CustomerName != null && s.CustomerName.ToUpper().Contains(term))))
            .OrderBy(s => s.EntryDateTime)
            .ToListAsync();
    }

    public async Task<ParkingSession?> GetByIdAsync(long id)
    {
        return await _context.ParkingSessions.FindAsync(id);
    }

    public async Task<ParkingSession?> GetByTicketNumberAsync(string ticketNumber)
    {
        return await _context.ParkingSessions
            .FirstOrDefaultAsync(s => s.TicketNumber == ticketNumber);
    }

    public async Task<ParkingSession?> GetActiveByPlateAsync(string normalizedPlate)
    {
        return await _context.ParkingSessions
            .FirstOrDefaultAsync(s => s.Plate == normalizedPlate && s.Status == ParkingSessionStatus.Active);
    }

    public async Task<List<ParkingSession>> GetCompletedSessionsAsync(
        DateTime? startDate, DateTime? endDate,
        string? plate, string? ticketNumber, string? customerName)
    {
        var query = _context.ParkingSessions
            .Where(s => s.Status == ParkingSessionStatus.Completed);

        if (startDate.HasValue)
            query = query.Where(s => s.EntryDateTime >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(s => s.EntryDateTime < endDate.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(plate))
        {
            var normalizedPlate = plate.ToUpperInvariant().Replace("-", "").Replace(" ", "");
            query = query.Where(s => s.Plate.Contains(normalizedPlate));
        }

        if (!string.IsNullOrWhiteSpace(ticketNumber))
            query = query.Where(s => s.TicketNumber.Contains(ticketNumber.Trim()));

        if (!string.IsNullOrWhiteSpace(customerName))
            query = query.Where(s => s.CustomerName != null &&
                s.CustomerName.ToUpper().Contains(customerName.Trim().ToUpperInvariant()));

        return await query
            .OrderByDescending(s => s.ExitDateTime)
            .ToListAsync();
    }

    public async Task AddAsync(ParkingSession session)
    {
        _context.ParkingSessions.Add(session);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ParkingSession session)
    {
        _context.ParkingSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetNextTicketSequenceAsync()
    {
        var maxTicket = await _context.ParkingSessions
            .MaxAsync(s => (int?)s.Id) ?? 0;
        return (int)maxTicket + 1;
    }

    public async Task<decimal> GetTodayRevenueAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        return await _context.ParkingSessions
            .Where(s => s.Status == ParkingSessionStatus.Completed &&
                        s.ExitDateTime >= today &&
                        s.ExitDateTime < tomorrow)
            .SumAsync(s => s.FinalAmount ?? 0);
    }

    public async Task<int> GetActiveCountAsync()
    {
        return await _context.ParkingSessions
            .CountAsync(s => s.Status == ParkingSessionStatus.Active);
    }
}
