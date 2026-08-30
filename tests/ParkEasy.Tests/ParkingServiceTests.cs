using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkEasy.Application;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;
using Xunit;

namespace ParkEasy.Tests;

public class ParkingServiceTests
{
    private readonly Mock<IParkingSessionRepository> _repoMock;
    private readonly Mock<ISheetsSyncService> _sheetsSyncServiceMock;
    private readonly ParkingFeeCalculator _feeCalculator;
    private readonly CurrentUserContext _currentUserContext;
    private readonly ParkingService _parkingService;

    public ParkingServiceTests()
    {
        _repoMock = new Mock<IParkingSessionRepository>();
        _sheetsSyncServiceMock = new Mock<ISheetsSyncService>();
        _sheetsSyncServiceMock.Setup(s => s.SyncPendingAsync()).Returns(Task.CompletedTask);

        var pricingOptions = Options.Create(new PricingSettings());
        _feeCalculator = new ParkingFeeCalculator(pricingOptions);

        var parkingOptions = Options.Create(new ParkingSettings { TotalSpaces = 50 });
        var washPricingOptions = Options.Create(new WashPricingSettings
        {
            ["Lav. Completa"] = new() { Price = 35.00m, AverageMinutes = 40 },
            ["Lav. Simples"] = new() { Price = 20.00m, AverageMinutes = 15 },
            ["Ducha Simples"] = new() { Price = 15.00m, AverageMinutes = 15 },
            ["Lav. Detalhada"] = new() { Price = 35.00m, AverageMinutes = 60 }
        });
        var washQueueOptions = Options.Create(new WashQueueSettings { Enabled = true });
        var logger = NullLogger<ParkingService>.Instance;

        _currentUserContext = new CurrentUserContext();
        _currentUserContext.SignIn(1, "testuser", UserRole.Colaborador);

        _parkingService = new ParkingService(
            _repoMock.Object, _feeCalculator, _currentUserContext, _sheetsSyncServiceMock.Object,
            parkingOptions, washPricingOptions, washQueueOptions, logger);
    }

    [Fact]
    public async Task RegisterEntryAsync_ValidPlate_CreatesActiveSession()
    {
        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync((ParkingSession?)null);
        _repoMock.Setup(r => r.GetNextTicketSequenceAsync()).ReturnsAsync(1);

        var session = await _parkingService.RegisterEntryAsync(
            "abc-1d23", VehicleType.Carro, "Corolla", "João", "53999999999", ServiceTypeNames.Hora, null, null);

        Assert.NotNull(session);
        Assert.Equal("000001", session.TicketNumber);
        Assert.Equal("ABC1D23", session.Plate);
        Assert.Equal(VehicleType.Carro, session.VehicleType);
        Assert.Equal("Corolla", session.VehicleModel);
        Assert.Equal("João", session.CustomerName);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal(ServiceTypeNames.Hora, session.ServiceType);
        Assert.Null(session.ServiceAmount);
        Assert.Null(session.ServiceStatus);
        Assert.Null(session.ExitDateTime);
        Assert.Equal("testuser", session.EntryUsername);
        Assert.False(session.SyncedToSheets);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<ParkingSession>()), Times.Once);
        _sheetsSyncServiceMock.Verify(s => s.SyncPendingAsync(), Times.Once);
    }

    [Fact]
    public async Task RegisterEntryAsync_WashServiceType_SetsPendingServiceStatus()
    {
        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync((ParkingSession?)null);
        _repoMock.Setup(r => r.GetNextTicketSequenceAsync()).ReturnsAsync(1);

        var session = await _parkingService.RegisterEntryAsync(
            "abc-1d23", VehicleType.Carro, null, null, null, "Lav. Completa", 35.00m, "Cliente pediu cera");

        Assert.Equal("Lav. Completa", session.ServiceType);
        Assert.Equal(35.00m, session.ServiceAmount);
        Assert.Equal("Cliente pediu cera", session.ServiceNotes);
        Assert.Equal(WashStatus.Pendente, session.ServiceStatus);
        Assert.NotNull(session.ServiceRequestedAt);
    }

    [Fact]
    public async Task RegisterEntryAsync_DiariaServiceType_DoesNotSetServiceStatus()
    {
        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync((ParkingSession?)null);
        _repoMock.Setup(r => r.GetNextTicketSequenceAsync()).ReturnsAsync(1);

        var session = await _parkingService.RegisterEntryAsync(
            "abc-1d23", VehicleType.Carro, null, null, null, ServiceTypeNames.Diaria, 40.00m, null);

        Assert.Equal(ServiceTypeNames.Diaria, session.ServiceType);
        Assert.Equal(40.00m, session.ServiceAmount);
        Assert.Null(session.ServiceStatus);
    }

    [Fact]
    public async Task RegisterEntryAsync_NonHoraWithoutAmount_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _parkingService.RegisterEntryAsync("ABC1D23", VehicleType.Carro, null, null, null, ServiceTypeNames.Diaria, null, null));
    }

    [Fact]
    public async Task RegisterEntryAsync_DuplicateActivePlate_ThrowsInvalidOperationException()
    {
        var existingSession = new ParkingSession
        {
            Id = 1,
            Plate = "ABC1D23",
            Status = ParkingSessionStatus.Active
        };

        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync(existingSession);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _parkingService.RegisterEntryAsync("ABC1D23", VehicleType.Carro, null, null, null, ServiceTypeNames.Hora, null, null));
    }

    [Fact]
    public async Task RegisterEntryAsync_InvalidPlateFormat_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _parkingService.RegisterEntryAsync("INVALID_PLATE", VehicleType.Carro, null, null, null, ServiceTypeNames.Hora, null, null));
    }

    [Fact]
    public async Task FinalizeSessionAsync_HoraService_CompletesSessionWithLiveCalculatedAmount()
    {
        var entryTime = DateTime.Now.AddHours(-2);
        var activeSession = new ParkingSession
        {
            Id = 10,
            TicketNumber = "000010",
            Plate = "ABC1D23",
            EntryDateTime = entryTime,
            Status = ParkingSessionStatus.Active,
            ServiceType = ServiceTypeNames.Hora
        };

        _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(activeSession);

        var completed = await _parkingService.FinalizeSessionAsync(10);

        Assert.Equal(ParkingSessionStatus.Completed, completed.Status);
        Assert.NotNull(completed.ExitDateTime);
        Assert.NotNull(completed.FinalAmount);
        Assert.True(completed.FinalAmount > 0m);
        Assert.Equal("testuser", completed.CheckoutUsername);
        Assert.False(completed.SyncedToSheets);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ParkingSession>()), Times.Once);
        _sheetsSyncServiceMock.Verify(s => s.SyncPendingAsync(), Times.Once);
    }

    [Fact]
    public async Task FinalizeSessionAsync_DiariaService_UsesFixedServiceAmount()
    {
        var activeSession = new ParkingSession
        {
            Id = 11,
            TicketNumber = "000011",
            Plate = "ABC1D23",
            EntryDateTime = DateTime.Now.AddHours(-1),
            Status = ParkingSessionStatus.Active,
            ServiceType = ServiceTypeNames.Diaria,
            ServiceAmount = 40.00m
        };

        _repoMock.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(activeSession);

        var completed = await _parkingService.FinalizeSessionAsync(11);

        Assert.Equal(40.00m, completed.FinalAmount);
    }

    [Fact]
    public async Task FinalizeSessionAsync_AlreadyCompletedSession_ThrowsInvalidOperationException()
    {
        var completedSession = new ParkingSession
        {
            Id = 10,
            TicketNumber = "000010",
            Plate = "ABC1D23",
            Status = ParkingSessionStatus.Completed
        };

        _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(completedSession);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _parkingService.FinalizeSessionAsync(10));
    }

    [Fact]
    public async Task AddOrUpdateWashServiceAsync_ActiveSession_SetsServiceFields()
    {
        var activeSession = new ParkingSession
        {
            Id = 20,
            TicketNumber = "000020",
            Plate = "ABC1D23",
            Status = ParkingSessionStatus.Active
        };

        _repoMock.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(activeSession);

        var result = await _parkingService.AddOrUpdateWashServiceAsync(20, "Lav. Completa", 35.00m, "Cliente pediu cera");

        Assert.Equal("Lav. Completa", result.ServiceType);
        Assert.Equal(35.00m, result.ServiceAmount);
        Assert.Equal("Cliente pediu cera", result.ServiceNotes);
        Assert.Equal(WashStatus.Pendente, result.ServiceStatus);
        Assert.NotNull(result.ServiceRequestedAt);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ParkingSession>()), Times.Once);
        _sheetsSyncServiceMock.Verify(s => s.SyncPendingAsync(), Times.Once);
    }

    [Fact]
    public async Task AddOrUpdateWashServiceAsync_EditingExistingWash_KeepsStatusAndRequestedAt()
    {
        var requestedAt = DateTime.Now.AddMinutes(-10);
        var activeSession = new ParkingSession
        {
            Id = 25,
            Status = ParkingSessionStatus.Active,
            ServiceType = "Lav. Simples",
            ServiceAmount = 20.00m,
            ServiceStatus = WashStatus.Lavando,
            ServiceRequestedAt = requestedAt
        };

        _repoMock.Setup(r => r.GetByIdAsync(25)).ReturnsAsync(activeSession);

        var result = await _parkingService.AddOrUpdateWashServiceAsync(25, "Lav. Completa", 35.00m, "Trocou o tipo");

        Assert.Equal("Lav. Completa", result.ServiceType);
        Assert.Equal(WashStatus.Lavando, result.ServiceStatus);
        Assert.Equal(requestedAt, result.ServiceRequestedAt);
    }

    [Fact]
    public async Task AddOrUpdateWashServiceAsync_ZeroAmount_ThrowsArgumentException()
    {
        var activeSession = new ParkingSession { Id = 21, Status = ParkingSessionStatus.Active };
        _repoMock.Setup(r => r.GetByIdAsync(21)).ReturnsAsync(activeSession);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _parkingService.AddOrUpdateWashServiceAsync(21, "Ducha Simples", 0m, null));
    }

    [Fact]
    public async Task AddOrUpdateWashServiceAsync_CompletedSession_ThrowsInvalidOperationException()
    {
        var completedSession = new ParkingSession { Id = 22, Status = ParkingSessionStatus.Completed };
        _repoMock.Setup(r => r.GetByIdAsync(22)).ReturnsAsync(completedSession);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _parkingService.AddOrUpdateWashServiceAsync(22, "Ducha Simples", 15.00m, null));
    }

    [Fact]
    public async Task AddOrUpdateWashServiceAsync_BlankTypeName_ThrowsArgumentException()
    {
        var activeSession = new ParkingSession { Id = 24, Status = ParkingSessionStatus.Active };
        _repoMock.Setup(r => r.GetByIdAsync(24)).ReturnsAsync(activeSession);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _parkingService.AddOrUpdateWashServiceAsync(24, "   ", 15.00m, null));
    }

    [Fact]
    public async Task RemoveWashServiceAsync_ActiveSessionWithWash_ClearsServiceFields()
    {
        var activeSession = new ParkingSession
        {
            Id = 23,
            Status = ParkingSessionStatus.Active,
            ServiceType = "Lav. Detalhada",
            ServiceAmount = 20.00m,
            ServiceNotes = "Aspirar bem"
        };

        _repoMock.Setup(r => r.GetByIdAsync(23)).ReturnsAsync(activeSession);

        var result = await _parkingService.RemoveWashServiceAsync(23);

        Assert.Null(result.ServiceType);
        Assert.Null(result.ServiceAmount);
        Assert.Null(result.ServiceNotes);
        Assert.Null(result.ServiceStatus);
        Assert.Null(result.ServiceRequestedAt);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ParkingSession>()), Times.Once);
    }

    [Fact]
    public async Task StartWashingAsync_PendingWash_MovesToLavando()
    {
        var session = new ParkingSession { Id = 30, ServiceStatus = WashStatus.Pendente };
        _repoMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(session);

        var result = await _parkingService.StartWashingAsync(30);

        Assert.Equal(WashStatus.Lavando, result.ServiceStatus);
        _repoMock.Verify(r => r.UpdateAsync(session), Times.Once);
    }

    [Fact]
    public async Task StartWashingAsync_NotPending_ThrowsInvalidOperationException()
    {
        var session = new ParkingSession { Id = 31, ServiceStatus = WashStatus.Lavando };
        _repoMock.Setup(r => r.GetByIdAsync(31)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _parkingService.StartWashingAsync(31));
    }

    [Fact]
    public async Task CompleteWashingAsync_WashingWash_MovesToConcluida()
    {
        var session = new ParkingSession { Id = 32, ServiceStatus = WashStatus.Lavando };
        _repoMock.Setup(r => r.GetByIdAsync(32)).ReturnsAsync(session);

        var result = await _parkingService.CompleteWashingAsync(32);

        Assert.Equal(WashStatus.Concluida, result.ServiceStatus);
    }

    [Fact]
    public async Task CompleteWashingAsync_NotWashing_ThrowsInvalidOperationException()
    {
        var session = new ParkingSession { Id = 33, ServiceStatus = WashStatus.Pendente };
        _repoMock.Setup(r => r.GetByIdAsync(33)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _parkingService.CompleteWashingAsync(33));
    }

    [Fact]
    public async Task GetActiveWashesAsync_ReturnsOnlyActiveSessionsWithServiceStatus()
    {
        var sessions = new List<ParkingSession>
        {
            new() { Id = 1, ServiceStatus = WashStatus.Pendente },
            new() { Id = 2, ServiceStatus = null },
            new() { Id = 3, ServiceStatus = WashStatus.Concluida }
        };

        _repoMock.Setup(r => r.GetActiveSessionsAsync()).ReturnsAsync(sessions);

        var result = await _parkingService.GetActiveWashesAsync();

        Assert.Equal([1L, 3L], result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetHistorySummaryAsync_SumsFinalAmountOnly()
    {
        var sessions = new List<ParkingSession>
        {
            new() { FinalAmount = 10.00m, Status = ParkingSessionStatus.Completed },
            new() { FinalAmount = 20.00m, Status = ParkingSessionStatus.Completed }
        };

        _repoMock.Setup(r => r.GetCompletedSessionsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<VehicleType?>()))
            .ReturnsAsync(sessions);

        var (totalRevenue, totalVehicles) = await _parkingService.GetHistorySummaryAsync(new HistoryFilter());

        Assert.Equal(30.00m, totalRevenue);
        Assert.Equal(2, totalVehicles);
    }

    [Fact]
    public async Task FindMostRecentByPlateAsync_ValidPlate_NormalizesAndDelegatesToRepository()
    {
        var previousSession = new ParkingSession { Id = 30, Plate = "ABC1D23", CustomerName = "Maria" };
        _repoMock.Setup(r => r.GetMostRecentByPlateAsync("ABC1D23")).ReturnsAsync(previousSession);

        var result = await _parkingService.FindMostRecentByPlateAsync("abc-1d23");

        Assert.Same(previousSession, result);
        _repoMock.Verify(r => r.GetMostRecentByPlateAsync("ABC1D23"), Times.Once);
    }

    [Fact]
    public async Task GetActiveSessionByPlateAsync_ValidPlate_NormalizesAndDelegatesToRepository()
    {
        var activeSession = new ParkingSession { Id = 40, Plate = "ABC1D23", Status = ParkingSessionStatus.Active };
        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync(activeSession);

        var result = await _parkingService.GetActiveSessionByPlateAsync("abc-1d23");

        Assert.Same(activeSession, result);
    }

    [Fact]
    public async Task GetActiveSessionByPlateAsync_NoActiveSession_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync((ParkingSession?)null);

        var result = await _parkingService.GetActiveSessionByPlateAsync("ABC1D23");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindMostRecentByPlateAsync_InvalidPlateFormat_ReturnsNullWithoutQueryingRepository()
    {
        var result = await _parkingService.FindMostRecentByPlateAsync("INVALID");

        Assert.Null(result);
        _repoMock.Verify(r => r.GetMostRecentByPlateAsync(It.IsAny<string>()), Times.Never);
    }
}
