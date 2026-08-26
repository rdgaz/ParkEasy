using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkEasy.Application.Configuration;
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
}
