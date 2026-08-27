using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;
using Xunit;

namespace ParkEasy.Tests;

public class ParkingServiceTests
{
    private readonly Mock<IParkingSessionRepository> _repoMock;
    private readonly ParkingFeeCalculator _feeCalculator;
    private readonly ParkingService _parkingService;

    public ParkingServiceTests()
    {
        _repoMock = new Mock<IParkingSessionRepository>();

        var pricingOptions = Options.Create(new PricingSettings());
        _feeCalculator = new ParkingFeeCalculator(pricingOptions);

        var parkingOptions = Options.Create(new ParkingSettings { TotalSpaces = 50 });
        var logger = NullLogger<ParkingService>.Instance;

        _parkingService = new ParkingService(_repoMock.Object, _feeCalculator, parkingOptions, logger);
    }

    [Fact]
    public async Task RegisterEntryAsync_ValidPlate_CreatesActiveSession()
    {
        _repoMock.Setup(r => r.GetActiveByPlateAsync("ABC1D23")).ReturnsAsync((ParkingSession?)null);
        _repoMock.Setup(r => r.GetNextTicketSequenceAsync()).ReturnsAsync(1);

        var session = await _parkingService.RegisterEntryAsync("abc-1d23", VehicleType.Carro, "Corolla", "João", "53999999999");

        Assert.NotNull(session);
        Assert.Equal("000001", session.TicketNumber);
        Assert.Equal("ABC1D23", session.Plate);
        Assert.Equal(VehicleType.Carro, session.VehicleType);
        Assert.Equal("Corolla", session.VehicleModel);
        Assert.Equal("João", session.CustomerName);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Null(session.ExitDateTime);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<ParkingSession>()), Times.Once);
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
            _parkingService.RegisterEntryAsync("ABC1D23", VehicleType.Carro, null, null, null));
    }

    [Fact]
    public async Task RegisterEntryAsync_InvalidPlateFormat_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _parkingService.RegisterEntryAsync("INVALID_PLATE", VehicleType.Carro, null, null, null));
    }

    [Fact]
    public async Task FinalizeSessionAsync_ActiveSession_CompletesSessionWithFinalAmount()
    {
        var entryTime = DateTime.Now.AddHours(-2);
        var activeSession = new ParkingSession
        {
            Id = 10,
            TicketNumber = "000010",
            Plate = "ABC1D23",
            EntryDateTime = entryTime,
            Status = ParkingSessionStatus.Active
        };

        _repoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(activeSession);

        var completed = await _parkingService.FinalizeSessionAsync(10);

        Assert.Equal(ParkingSessionStatus.Completed, completed.Status);
        Assert.NotNull(completed.ExitDateTime);
        Assert.NotNull(completed.FinalAmount);
        Assert.True(completed.FinalAmount > 0m);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ParkingSession>()), Times.Once);
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
    public async Task AddOrUpdateWashServiceAsync_ActiveSession_SetsWashFields()
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

        Assert.Equal("Lav. Completa", result.WashTypeName);
        Assert.Equal(35.00m, result.WashAmount);
        Assert.Equal("Cliente pediu cera", result.WashNotes);
        Assert.Equal(WashStatus.Pendente, result.WashStatus);
        Assert.NotNull(result.WashRequestedAt);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ParkingSession>()), Times.Once);
    }

    [Fact]
    public async Task AddOrUpdateWashServiceAsync_EditingExistingWash_KeepsStatusAndRequestedAt()
    {
        var requestedAt = DateTime.Now.AddMinutes(-10);
        var activeSession = new ParkingSession
        {
            Id = 25,
            Status = ParkingSessionStatus.Active,
            WashTypeName = "Lav. Simples",
            WashAmount = 20.00m,
            WashStatus = WashStatus.Lavando,
            WashRequestedAt = requestedAt
        };

        _repoMock.Setup(r => r.GetByIdAsync(25)).ReturnsAsync(activeSession);

        var result = await _parkingService.AddOrUpdateWashServiceAsync(25, "Lav. Completa", 35.00m, "Trocou o tipo");

        Assert.Equal("Lav. Completa", result.WashTypeName);
        Assert.Equal(WashStatus.Lavando, result.WashStatus);
        Assert.Equal(requestedAt, result.WashRequestedAt);
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
    public async Task RemoveWashServiceAsync_ActiveSessionWithWash_ClearsWashFields()
    {
        var activeSession = new ParkingSession
        {
            Id = 23,
            Status = ParkingSessionStatus.Active,
            WashTypeName = "Lav. Detalhada",
            WashAmount = 20.00m,
            WashNotes = "Aspirar bem"
        };

        _repoMock.Setup(r => r.GetByIdAsync(23)).ReturnsAsync(activeSession);

        var result = await _parkingService.RemoveWashServiceAsync(23);

        Assert.Null(result.WashTypeName);
        Assert.Null(result.WashAmount);
        Assert.Null(result.WashNotes);
        Assert.Null(result.WashStatus);
        Assert.Null(result.WashRequestedAt);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ParkingSession>()), Times.Once);
    }

    [Fact]
    public async Task StartWashingAsync_PendingWash_MovesToLavando()
    {
        var session = new ParkingSession { Id = 30, WashStatus = WashStatus.Pendente };
        _repoMock.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(session);

        var result = await _parkingService.StartWashingAsync(30);

        Assert.Equal(WashStatus.Lavando, result.WashStatus);
        _repoMock.Verify(r => r.UpdateAsync(session), Times.Once);
    }

    [Fact]
    public async Task StartWashingAsync_NotPending_ThrowsInvalidOperationException()
    {
        var session = new ParkingSession { Id = 31, WashStatus = WashStatus.Lavando };
        _repoMock.Setup(r => r.GetByIdAsync(31)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _parkingService.StartWashingAsync(31));
    }

    [Fact]
    public async Task CompleteWashingAsync_WashingWash_MovesToConcluida()
    {
        var session = new ParkingSession { Id = 32, WashStatus = WashStatus.Lavando };
        _repoMock.Setup(r => r.GetByIdAsync(32)).ReturnsAsync(session);

        var result = await _parkingService.CompleteWashingAsync(32);

        Assert.Equal(WashStatus.Concluida, result.WashStatus);
    }

    [Fact]
    public async Task CompleteWashingAsync_NotWashing_ThrowsInvalidOperationException()
    {
        var session = new ParkingSession { Id = 33, WashStatus = WashStatus.Pendente };
        _repoMock.Setup(r => r.GetByIdAsync(33)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _parkingService.CompleteWashingAsync(33));
    }

    [Fact]
    public async Task GetActiveWashesAsync_ReturnsOnlyActiveSessionsWithWashStatus()
    {
        var sessions = new List<ParkingSession>
        {
            new() { Id = 1, WashStatus = WashStatus.Pendente },
            new() { Id = 2, WashStatus = null },
            new() { Id = 3, WashStatus = WashStatus.Concluida }
        };

        _repoMock.Setup(r => r.GetActiveSessionsAsync()).ReturnsAsync(sessions);

        var result = await _parkingService.GetActiveWashesAsync();

        Assert.Equal([1L, 3L], result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetHistorySummaryAsync_SessionsWithWash_SumsFinalAmountAndWashAmount()
    {
        var sessions = new List<ParkingSession>
        {
            new() { FinalAmount = 10.00m, WashAmount = 15.00m, Status = ParkingSessionStatus.Completed },
            new() { FinalAmount = 20.00m, WashAmount = null, Status = ParkingSessionStatus.Completed }
        };

        _repoMock.Setup(r => r.GetCompletedSessionsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<VehicleType?>()))
            .ReturnsAsync(sessions);

        var (totalRevenue, totalVehicles) = await _parkingService.GetHistorySummaryAsync(new HistoryFilter());

        Assert.Equal(45.00m, totalRevenue);
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
