using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Infrastructure.Data;
using ParkEasy.Infrastructure.Repositories;
using Xunit;

namespace ParkEasy.Tests;

public class ParkingSessionRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ParkingDbContext _context;
    private readonly ParkingSessionRepository _repository;

    public ParkingSessionRepositoryTests()
    {
        // SQLite em memória: a conexão precisa ficar aberta durante todo o teste,
        // senão o banco "some" entre as operações.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ParkingDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ParkingDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new ParkingSessionRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetMostRecentByPlateAsync_MultipleSessionsForSamePlate_ReturnsLatestByEntryDateTime()
    {
        var now = DateTime.Now;

        // Inseridos fora de ordem cronológica de propósito: prova que a busca ordena
        // por EntryDateTime, e não pela ordem de inserção (Id).
        await SeedSessionAsync("ABC1D23", now.AddDays(-5), "Cliente do meio", ticketNumber: "000002");
        await SeedSessionAsync("ABC1D23", now.AddDays(-10), "Cliente mais antigo", ticketNumber: "000001");
        await SeedSessionAsync("ABC1D23", now.AddDays(-1), "Cliente mais recente", ticketNumber: "000003");

        var result = await _repository.GetMostRecentByPlateAsync("ABC1D23");

        Assert.NotNull(result);
        Assert.Equal("Cliente mais recente", result!.CustomerName);
        Assert.Equal("000003", result.TicketNumber);
    }

    [Fact]
    public async Task GetMostRecentByPlateAsync_NoSessionsForPlate_ReturnsNull()
    {
        var result = await _repository.GetMostRecentByPlateAsync("ZZZ9999");

        Assert.Null(result);
    }

    private async Task SeedSessionAsync(string plate, DateTime entryDateTime, string customerName, string ticketNumber)
    {
        _context.ParkingSessions.Add(new ParkingSession
        {
            TicketNumber = ticketNumber,
            Plate = plate,
            VehicleType = VehicleType.Carro,
            CustomerName = customerName,
            EntryDateTime = entryDateTime,
            ExitDateTime = entryDateTime.AddHours(1),
            Status = ParkingSessionStatus.Completed,
            FinalAmount = 10.00m,
            CreatedAt = entryDateTime,
            UpdatedAt = entryDateTime
        });

        await _context.SaveChangesAsync();
    }
}
