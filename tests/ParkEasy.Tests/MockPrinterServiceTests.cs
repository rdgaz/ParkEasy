using Microsoft.Extensions.Logging.Abstractions;
using ParkEasy.Application.DTOs;
using ParkEasy.Infrastructure.Printing;
using Xunit;

namespace ParkEasy.Tests;

public class MockPrinterServiceTests
{
    private readonly MockPrinterService _printer;

    public MockPrinterServiceTests()
    {
        _printer = new MockPrinterService(NullLogger<MockPrinterService>.Instance);
    }

    [Fact]
    public async Task PrintEntryTicketAsync_TracksPrintedTicket()
    {
        var ticket = new ParkingTicket
        {
            TicketNumber = "000001",
            Plate = "ABC1D23",
            EntryDateTime = DateTime.Now
        };

        await _printer.PrintEntryTicketAsync(ticket);

        Assert.Single(_printer.PrintedTickets);
        Assert.Equal("000001", _printer.PrintedTickets[0].TicketNumber);
    }

    [Fact]
    public async Task PrintExitReceiptAsync_TracksPrintedReceipt()
    {
        var receipt = new ParkingReceipt
        {
            TicketNumber = "000001",
            Plate = "ABC1D23",
            EntryDateTime = DateTime.Now.AddHours(-1),
            ExitDateTime = DateTime.Now,
            FinalAmount = 10.00m
        };

        await _printer.PrintExitReceiptAsync(receipt);

        Assert.Single(_printer.PrintedReceipts);
        Assert.Equal("000001", _printer.PrintedReceipts[0].TicketNumber);
        Assert.Equal(10.00m, _printer.PrintedReceipts[0].FinalAmount);
    }

    [Fact]
    public async Task TestPrinterAsync_IncrementsTestCount()
    {
        await _printer.TestPrinterAsync();

        Assert.Equal(1, _printer.TestPrintCount);
    }
}
