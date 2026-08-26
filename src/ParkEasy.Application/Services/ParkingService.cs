using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;

namespace ParkEasy.Application.Services;

public class ParkingService : IParkingService
{
    private readonly IParkingSessionRepository _repository;
    private readonly IParkingFeeCalculator _feeCalculator;
    private readonly ILogger<ParkingService> _logger;
    private readonly ParkingSettings _parkingSettings;

    public ParkingService(
        IParkingSessionRepository repository,
        IParkingFeeCalculator feeCalculator,
        IOptions<ParkingSettings> parkingOptions,
        ILogger<ParkingService> logger)
    {
        _repository = repository;
        _feeCalculator = feeCalculator;
        _parkingSettings = parkingOptions.Value;
        _logger = logger;
    }

    public async Task<ParkingSession> RegisterEntryAsync(
        string plate, VehicleType vehicleType, string? vehicleModel, string? customerName, string? customerPhone)
    {
        var normalizedPlate = PlateNormalizer.Normalize(plate);

        if (string.IsNullOrWhiteSpace(normalizedPlate))
            throw new ArgumentException("Informe a placa do veículo.");

        if (!PlateNormalizer.IsValid(normalizedPlate))
            throw new ArgumentException("A placa informada não é válida. Use o formato ABC1234 ou ABC1D23.");

        // Check for active duplicate
        var existing = await _repository.GetActiveByPlateAsync(normalizedPlate);
        if (existing is not null)
            throw new InvalidOperationException("Este veículo já possui um estacionamento ativo.");

        // Generate ticket number
        var sequence = await _repository.GetNextTicketSequenceAsync();
        var ticketNumber = sequence.ToString("D6");

        var now = DateTime.Now;
        var session = new ParkingSession
        {
            TicketNumber = ticketNumber,
            Plate = normalizedPlate,
            VehicleType = vehicleType,
            VehicleModel = string.IsNullOrWhiteSpace(vehicleModel) ? null : vehicleModel.Trim(),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim(),
            EntryDateTime = now,
            ExitDateTime = null,
            Status = ParkingSessionStatus.Active,
            FinalAmount = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(session);

        _logger.LogInformation(
            "Entrada registrada: Ticket={TicketNumber}, Placa={Plate}, Entrada={EntryDateTime}",
            session.TicketNumber, session.Plate, session.EntryDateTime);

        return session;
    }

    public async Task<ParkingSession> FinalizeSessionAsync(long sessionId)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.Status != ParkingSessionStatus.Active)
            throw new InvalidOperationException("Esta sessão já foi finalizada.");

        var exitDateTime = DateTime.Now;
        var finalAmount = _feeCalculator.CalculateFee(session.EntryDateTime, exitDateTime, session.VehicleType);

        session.ExitDateTime = exitDateTime;
        session.FinalAmount = finalAmount;
        session.Status = ParkingSessionStatus.Completed;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        _logger.LogInformation(
            "Estacionamento finalizado: Ticket={TicketNumber}, Placa={Plate}, Valor={FinalAmount:C}, Tempo={Duration}",
            session.TicketNumber, session.Plate, session.FinalAmount,
            session.ExitDateTime.Value - session.EntryDateTime);

        return session;
    }

    public async Task<ParkingSession> AddOrUpdateWashServiceAsync(long sessionId, WashType washType, decimal amount, string? notes)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.Status != ParkingSessionStatus.Active)
            throw new InvalidOperationException("Não é possível adicionar lavagem a uma sessão já finalizada.");

        if (amount <= 0)
            throw new ArgumentException("Informe um valor de lavagem maior que zero.");

        session.WashType = washType;
        session.WashAmount = amount;
        session.WashNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        _logger.LogInformation(
            "Lavagem registrada: Ticket={TicketNumber}, Tipo={WashType}, Valor={WashAmount:C}",
            session.TicketNumber, session.WashType, session.WashAmount);

        return session;
    }

    public async Task<ParkingSession> RemoveWashServiceAsync(long sessionId)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.Status != ParkingSessionStatus.Active)
            throw new InvalidOperationException("Não é possível remover lavagem de uma sessão já finalizada.");

        session.WashType = null;
        session.WashAmount = null;
        session.WashNotes = null;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        return session;
    }

    public async Task<List<ParkingSession>> GetActiveSessionsAsync()
    {
        return await _repository.GetActiveSessionsAsync();
    }

    public async Task<List<ParkingSession>> SearchActiveSessionsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await _repository.GetActiveSessionsAsync();

        return await _repository.SearchActiveSessionsAsync(searchTerm.Trim());
    }

    public async Task<List<ParkingSession>> GetHistoryAsync(HistoryFilter filter)
    {
        return await _repository.GetCompletedSessionsAsync(
            filter.StartDate, filter.EndDate,
            filter.Plate, filter.TicketNumber, filter.CustomerName,
            filter.VehicleType);
    }

    public async Task<DashboardData> GetDashboardDataAsync()
    {
        var activeCount = await _repository.GetActiveCountAsync();
        var occupiedSpaces = await _repository.GetOccupiedSpacesAsync();
        var todayRevenue = await _repository.GetTodayRevenueAsync();

        return new DashboardData
        {
            ActiveVehicles = activeCount,
            OccupiedSpaces = occupiedSpaces,
            TotalSpaces = _parkingSettings.TotalSpaces,
            TodayRevenue = todayRevenue
        };
    }

    public async Task<(decimal totalRevenue, int totalVehicles)> GetHistorySummaryAsync(HistoryFilter filter)
    {
        var sessions = await GetHistoryAsync(filter);
        var totalRevenue = sessions.Sum(s => (s.FinalAmount ?? 0) + (s.WashAmount ?? 0));
        var totalVehicles = sessions.Count;
        return (totalRevenue, totalVehicles);
    }

    public Task<ParkingTicket> BuildTicketAsync(ParkingSession session)
    {
        var ticket = new ParkingTicket
        {
            TicketNumber = session.TicketNumber,
            Plate = session.Plate,
            VehicleType = session.VehicleType,
            VehicleModel = session.VehicleModel,
            CustomerName = session.CustomerName,
            EntryDateTime = session.EntryDateTime
        };
        return Task.FromResult(ticket);
    }

    public Task<ParkingReceipt> BuildReceiptAsync(ParkingSession session)
    {
        if (session.ExitDateTime is null || session.FinalAmount is null)
            throw new InvalidOperationException("A sessão ainda não foi finalizada.");

        var receipt = new ParkingReceipt
        {
            TicketNumber = session.TicketNumber,
            Plate = session.Plate,
            VehicleType = session.VehicleType,
            VehicleModel = session.VehicleModel,
            CustomerName = session.CustomerName,
            EntryDateTime = session.EntryDateTime,
            ExitDateTime = session.ExitDateTime.Value,
            Duration = session.ExitDateTime.Value - session.EntryDateTime,
            FinalAmount = session.FinalAmount.Value,
            WashType = session.WashType,
            WashAmount = session.WashAmount,
            WashNotes = session.WashNotes,
            TotalAmount = session.FinalAmount.Value + (session.WashAmount ?? 0)
        };
        return Task.FromResult(receipt);
    }
}
