using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application;
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
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ISheetsSyncService _sheetsSyncService;
    private readonly ILogger<ParkingService> _logger;
    private readonly ParkingSettings _parkingSettings;
    private readonly WashPricingSettings _washPricing;
    private readonly WashQueueSettings _washQueueSettings;

    public ParkingService(
        IParkingSessionRepository repository,
        IParkingFeeCalculator feeCalculator,
        ICurrentUserContext currentUserContext,
        ISheetsSyncService sheetsSyncService,
        IOptions<ParkingSettings> parkingOptions,
        IOptions<WashPricingSettings> washPricingOptions,
        IOptions<WashQueueSettings> washQueueOptions,
        ILogger<ParkingService> logger)
    {
        _repository = repository;
        _feeCalculator = feeCalculator;
        _currentUserContext = currentUserContext;
        _sheetsSyncService = sheetsSyncService;
        _parkingSettings = parkingOptions.Value;
        _washPricing = washPricingOptions.Value;
        _washQueueSettings = washQueueOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Dispara a sincronização com a planilha em segundo plano — nunca espera a rede,
    /// nunca bloqueia quem chamou. SyncPendingAsync() garante internamente que não lança.
    /// </summary>
    private void TriggerBackgroundSync()
    {
        _ = _sheetsSyncService.SyncPendingAsync();
    }

    public async Task<ParkingSession> RegisterEntryAsync(
        string plate, VehicleType vehicleType, string? vehicleModel, string? customerName, string? customerPhone,
        string serviceType, decimal? serviceAmount, string? serviceNotes)
    {
        var normalizedPlate = PlateNormalizer.Normalize(plate);

        if (string.IsNullOrWhiteSpace(normalizedPlate))
            throw new ArgumentException("Informe a placa do veículo.");

        if (!PlateNormalizer.IsValid(normalizedPlate))
            throw new ArgumentException("A placa informada não é válida. Use o formato ABC1234 ou ABC1D23.");

        if (string.IsNullOrWhiteSpace(serviceType))
            throw new ArgumentException("Informe o tipo de serviço.");

        var isHora = serviceType == ServiceTypeNames.Hora;

        if (!isHora && (serviceAmount is null || serviceAmount <= 0))
            throw new ArgumentException("Informe um valor de serviço maior que zero.");

        // Check for active duplicate
        var existing = await _repository.GetActiveByPlateAsync(normalizedPlate);
        if (existing is not null)
            throw new InvalidOperationException("Este veículo já possui um estacionamento ativo.");

        // Generate ticket number
        var sequence = await _repository.GetNextTicketSequenceAsync();
        var ticketNumber = sequence.ToString("D6");

        var now = DateTime.Now;
        var isWash = _washPricing.ContainsKey(serviceType) && _washQueueSettings.Enabled;

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
            EntryUsername = _currentUserContext.Username,
            ServiceType = serviceType,
            ServiceAmount = isHora ? null : serviceAmount,
            ServiceNotes = string.IsNullOrWhiteSpace(serviceNotes) ? null : serviceNotes.Trim(),
            ServiceStatus = isWash ? WashStatus.Pendente : null,
            ServiceRequestedAt = isWash ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(session);

        _logger.LogInformation(
            "Entrada registrada: Ticket={TicketNumber}, Placa={Plate}, Entrada={EntryDateTime}",
            session.TicketNumber, session.Plate, session.EntryDateTime);

        TriggerBackgroundSync();

        return session;
    }

    public async Task<ParkingSession?> FindMostRecentByPlateAsync(string plate)
    {
        var normalizedPlate = PlateNormalizer.Normalize(plate);

        if (!PlateNormalizer.IsValid(normalizedPlate))
            return null;

        return await _repository.GetMostRecentByPlateAsync(normalizedPlate);
    }

    public async Task<ParkingSession?> GetActiveSessionByPlateAsync(string plate)
    {
        var normalizedPlate = PlateNormalizer.Normalize(plate);

        if (!PlateNormalizer.IsValid(normalizedPlate))
            return null;

        return await _repository.GetActiveByPlateAsync(normalizedPlate);
    }

    public async Task<ParkingSession> FinalizeSessionAsync(long sessionId)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.Status != ParkingSessionStatus.Active)
            throw new InvalidOperationException("Esta sessão já foi finalizada.");

        var exitDateTime = DateTime.Now;
        var finalAmount = session.ServiceType == ServiceTypeNames.Hora
            ? _feeCalculator.CalculateFee(session.EntryDateTime, exitDateTime, session.VehicleType)
            : session.ServiceAmount ?? 0m;

        session.ExitDateTime = exitDateTime;
        session.FinalAmount = finalAmount;
        session.Status = ParkingSessionStatus.Completed;
        session.CheckoutUsername = _currentUserContext.Username;
        session.SyncedToSheets = false;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        _logger.LogInformation(
            "Estacionamento finalizado: Ticket={TicketNumber}, Placa={Plate}, Valor={FinalAmount:C}, Tempo={Duration}",
            session.TicketNumber, session.Plate, session.FinalAmount,
            session.ExitDateTime.Value - session.EntryDateTime);

        TriggerBackgroundSync();

        return session;
    }

    public async Task<ParkingSession> AddOrUpdateWashServiceAsync(long sessionId, string washTypeName, decimal amount, string? notes)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.Status != ParkingSessionStatus.Active)
            throw new InvalidOperationException("Não é possível adicionar lavagem a uma sessão já finalizada.");

        if (string.IsNullOrWhiteSpace(washTypeName))
            throw new ArgumentException("Informe o tipo de lavagem.");

        if (amount <= 0)
            throw new ArgumentException("Informe um valor de lavagem maior que zero.");

        var isNewWash = session.ServiceStatus is null;

        session.ServiceType = washTypeName.Trim();
        session.ServiceAmount = amount;
        session.ServiceNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (isNewWash)
        {
            session.ServiceStatus = WashStatus.Pendente;
            session.ServiceRequestedAt = DateTime.Now;
        }

        session.SyncedToSheets = false;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        _logger.LogInformation(
            "Lavagem registrada: Ticket={TicketNumber}, Tipo={ServiceType}, Valor={ServiceAmount:C}",
            session.TicketNumber, session.ServiceType, session.ServiceAmount);

        TriggerBackgroundSync();

        return session;
    }

    public async Task<ParkingSession> RemoveWashServiceAsync(long sessionId)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.Status != ParkingSessionStatus.Active)
            throw new InvalidOperationException("Não é possível remover lavagem de uma sessão já finalizada.");

        session.ServiceType = null;
        session.ServiceAmount = null;
        session.ServiceNotes = null;
        session.ServiceStatus = null;
        session.ServiceRequestedAt = null;
        session.SyncedToSheets = false;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        TriggerBackgroundSync();

        return session;
    }

    public async Task<List<ParkingSession>> GetActiveWashesAsync()
    {
        var activeSessions = await _repository.GetActiveSessionsAsync();
        return activeSessions.Where(s => s.ServiceStatus is not null).ToList();
    }

    public async Task<ParkingSession> StartWashingAsync(long sessionId)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.ServiceStatus != WashStatus.Pendente)
            throw new InvalidOperationException("Esta lavagem não está pendente.");

        session.ServiceStatus = WashStatus.Lavando;
        session.SyncedToSheets = false;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        TriggerBackgroundSync();

        return session;
    }

    public async Task<ParkingSession> CompleteWashingAsync(long sessionId)
    {
        var session = await _repository.GetByIdAsync(sessionId);

        if (session is null)
            throw new InvalidOperationException("Sessão de estacionamento não encontrada.");

        if (session.ServiceStatus != WashStatus.Lavando)
            throw new InvalidOperationException("Esta lavagem não está em andamento.");

        session.ServiceStatus = WashStatus.Concluida;
        session.SyncedToSheets = false;
        session.UpdatedAt = DateTime.Now;

        await _repository.UpdateAsync(session);

        TriggerBackgroundSync();

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
        var totalRevenue = sessions.Sum(s => s.FinalAmount ?? 0);
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
            ServiceType = session.ServiceType,
            ServiceNotes = session.ServiceNotes,
            TotalAmount = session.FinalAmount.Value
        };
        return Task.FromResult(receipt);
    }
}
