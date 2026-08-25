using ParkEasy.Application.DTOs;

namespace ParkEasy.Application.Interfaces;

public interface IPrinterService
{
    Task PrintEntryTicketAsync(ParkingTicket ticket);
    Task PrintExitReceiptAsync(ParkingReceipt receipt);
    Task TestPrinterAsync();
}
