using Microsoft.Extensions.Logging;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;

namespace ParkEasy.Infrastructure.Printing;

/// <summary>
/// Mock printer service for development and testing.
/// Logs print operations instead of sending to a physical printer.
/// </summary>
public class MockPrinterService : IPrinterService
{
    private readonly ILogger<MockPrinterService> _logger;

    // Track calls for testing
    public List<ParkingTicket> PrintedTickets { get; } = [];
    public List<ParkingReceipt> PrintedReceipts { get; } = [];
    public int TestPrintCount { get; private set; }

    public MockPrinterService(ILogger<MockPrinterService> logger)
    {
        _logger = logger;
    }

    public Task PrintEntryTicketAsync(ParkingTicket ticket)
    {
        _logger.LogInformation(
            "[MOCK PRINTER] Ticket de entrada impresso: Ticket={TicketNumber}, Placa={Plate}, Entrada={EntryDateTime}",
            ticket.TicketNumber, ticket.Plate, ticket.EntryDateTime);

        PrintedTickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task PrintExitReceiptAsync(ParkingReceipt receipt)
    {
        _logger.LogInformation(
            "[MOCK PRINTER] Comprovante de saída impresso: Ticket={TicketNumber}, Placa={Plate}, Valor={FinalAmount:C}",
            receipt.TicketNumber, receipt.Plate, receipt.FinalAmount);

        PrintedReceipts.Add(receipt);
        return Task.CompletedTask;
    }

    public Task TestPrinterAsync()
    {
        TestPrintCount++;
        _logger.LogInformation("[MOCK PRINTER] Teste de impressão executado com sucesso. Total de testes: {Count}", TestPrintCount);
        return Task.CompletedTask;
    }
}
