using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.DTOs;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Enums;

namespace ParkEasy.Infrastructure.Printing;

/// <summary>
/// Bematech MP-4200 TH thermal printer implementation using ESC/POS commands via raw Windows spooler.
/// </summary>
public class BematechMp4200PrinterService : IPrinterService
{
    private readonly PrinterSettings _settings;
    private readonly BusinessSettings _business;
    private readonly ILogger<BematechMp4200PrinterService> _logger;

    // ESC/POS command constants
    private static readonly byte[] CMD_INIT = [0x1B, 0x40]; // Initialize printer
    private static readonly byte[] CMD_CENTER = [0x1B, 0x61, 0x01]; // Center align
    private static readonly byte[] CMD_LEFT = [0x1B, 0x61, 0x00]; // Left align
    private static readonly byte[] CMD_BOLD_ON = [0x1B, 0x45, 0x01]; // Bold on
    private static readonly byte[] CMD_BOLD_OFF = [0x1B, 0x45, 0x00]; // Bold off
    private static readonly byte[] CMD_DOUBLE_SIZE = [0x1D, 0x21, 0x11]; // Double width + height
    private static readonly byte[] CMD_NORMAL_SIZE = [0x1D, 0x21, 0x00]; // Normal size
    private static readonly byte[] CMD_CUT = [0x1D, 0x56, 0x42, 0x03]; // Partial cut with feed
    private static readonly byte[] CMD_FEED = [0x0A]; // Line feed

    private readonly Encoding _encoding;

    public BematechMp4200PrinterService(
        IOptions<PrinterSettings> settings,
        IOptions<BusinessSettings> businessOptions,
        ILogger<BematechMp4200PrinterService> logger)
    {
        _settings = settings.Value;
        _business = businessOptions.Value;
        _logger = logger;

        // Register code page provider for Brazilian character support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _encoding = Encoding.GetEncoding(850); // CP850 — Latin-1 for Portuguese
    }

    public Task PrintEntryTicketAsync(ParkingTicket ticket)
    {
        _logger.LogInformation("Impressão de ticket iniciada: {TicketNumber}", ticket.TicketNumber);

        try
        {
            using var ms = new MemoryStream();

            Write(ms, CMD_INIT);

            // 1. Nome do estacionamento
            Write(ms, CMD_CENTER);
            Write(ms, CMD_BOLD_ON);
            Write(ms, CMD_DOUBLE_SIZE);
            WriteText(ms, _business.Name);
            Write(ms, CMD_NORMAL_SIZE);
            Write(ms, CMD_BOLD_OFF);

            // 2. CNPJ (menor que o nome)
            if (!string.IsNullOrWhiteSpace(_business.Cnpj))
            {
                WriteText(ms, $"CNPJ {_business.Cnpj}");
            }

            // 3. Duas linhas de separação
            WriteText(ms, "--------------------------------");
            WriteText(ms, "--------------------------------");
            WriteText(ms, "");

            // 4. Placa (tamanho maior)
            Write(ms, CMD_BOLD_ON);
            Write(ms, CMD_DOUBLE_SIZE);
            WriteText(ms, ticket.Plate);
            Write(ms, CMD_NORMAL_SIZE);

            // 5. Modelo do carro registrado
            if (!string.IsNullOrWhiteSpace(ticket.VehicleModel))
            {
                WriteText(ms, ticket.VehicleModel.ToUpperInvariant());
            }
            Write(ms, CMD_BOLD_OFF);
            WriteText(ms, "");

            // 6-8. Tipo, Ticket, Data e Hora
            Write(ms, CMD_LEFT);
            WriteText(ms, $"Tipo: {ticket.VehicleType.ToDisplayName()}");
            WriteText(ms, $"Ticket: {ticket.TicketNumber}");
            WriteText(ms, $"Data: {ticket.EntryDateTime:dd/MM/yyyy}");
            WriteText(ms, $"Hora: {ticket.EntryDateTime:HH:mm}");
            WriteText(ms, "");

            // Footer
            Write(ms, CMD_CENTER);
            WriteText(ms, "Indispensável a apresentação deste");
            WriteText(ms, "cupom para a retirada do veículo");
            WriteText(ms, "");
            WriteText(ms, "ParkEasy - software de gestão");
            WriteText(ms, "para estacionamentos");
            WriteText(ms, "");
            WriteText(ms, "");

            // Cut
            Write(ms, CMD_CUT);

            var data = ms.ToArray();
            var success = RawPrinterHelper.SendBytesToPrinter(_settings.WindowsPrinterName, data, "ParkEasy - Ticket Entrada");

            if (!success)
            {
                _logger.LogWarning("Falha ao enviar dados para a impressora {PrinterName}", _settings.WindowsPrinterName);
                throw new InvalidOperationException(
                    $"Não foi possível imprimir o ticket.\nVerifique a impressora '{_settings.WindowsPrinterName}'.");
            }

            _logger.LogInformation("Ticket impresso com sucesso: {TicketNumber}", ticket.TicketNumber);
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw printer errors
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao imprimir ticket: {TicketNumber}", ticket.TicketNumber);
            throw new InvalidOperationException(
                $"Erro ao imprimir o ticket.\nVerifique a Bematech MP-4200 TH.\n\nDetalhes: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task PrintExitReceiptAsync(ParkingReceipt receipt)
    {
        _logger.LogInformation("Impressão de comprovante iniciada: {TicketNumber}", receipt.TicketNumber);

        try
        {
            using var ms = new MemoryStream();

            Write(ms, CMD_INIT);

            // Header line
            Write(ms, CMD_CENTER);
            WriteText(ms, "--------------------------------");
            Write(ms, CMD_BOLD_ON);
            WriteText(ms, _business.Name);
            Write(ms, CMD_BOLD_OFF);
            WriteText(ms, "--------------------------------");
            WriteText(ms, "");

            // Ticket
            Write(ms, CMD_LEFT);
            WriteText(ms, $"Ticket: {receipt.TicketNumber}");
            WriteText(ms, "");
            WriteText(ms, $"Placa: {receipt.Plate}");
            WriteText(ms, $"Tipo: {receipt.VehicleType.ToDisplayName()}");

            if (!string.IsNullOrWhiteSpace(receipt.VehicleModel))
                WriteText(ms, $"Modelo: {receipt.VehicleModel}");

            if (!string.IsNullOrWhiteSpace(receipt.CustomerName))
                WriteText(ms, $"Cliente: {receipt.CustomerName}");

            WriteText(ms, "");
            WriteText(ms, "Entrada:");
            WriteText(ms, receipt.EntryDateTime.ToString("dd/MM/yyyy HH:mm"));
            WriteText(ms, "");
            WriteText(ms, "Saida:");
            WriteText(ms, receipt.ExitDateTime.ToString("dd/MM/yyyy HH:mm"));
            WriteText(ms, "");
            WriteText(ms, "Tempo:");
            WriteText(ms, $"{(int)receipt.Duration.TotalHours:D2}:{receipt.Duration.Minutes:D2}");
            WriteText(ms, "");

            var brCulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

            Write(ms, CMD_LEFT);
            WriteText(ms, $"Estacionamento: {receipt.FinalAmount.ToString("C2", brCulture)}");

            // Wash service (optional)
            if (receipt.WashAmount.HasValue && receipt.WashType.HasValue)
            {
                WriteText(ms, $"Lavagem ({receipt.WashType.Value.ToDisplayName()}): {receipt.WashAmount.Value.ToString("C2", brCulture)}");

                if (!string.IsNullOrWhiteSpace(receipt.WashNotes))
                    WriteText(ms, $"Obs: {receipt.WashNotes}");
            }

            WriteText(ms, "");

            // Amount
            Write(ms, CMD_CENTER);
            Write(ms, CMD_BOLD_ON);
            WriteText(ms, "VALOR PAGO:");
            Write(ms, CMD_DOUBLE_SIZE);
            WriteText(ms, receipt.TotalAmount.ToString("C2", brCulture));
            Write(ms, CMD_NORMAL_SIZE);
            Write(ms, CMD_BOLD_OFF);
            WriteText(ms, "");

            // Footer
            WriteText(ms, "--------------------------------");
            WriteText(ms, "OBRIGADO!");
            WriteText(ms, "--------------------------------");
            WriteText(ms, "");
            WriteText(ms, "");

            Write(ms, CMD_CUT);

            var data = ms.ToArray();
            var success = RawPrinterHelper.SendBytesToPrinter(_settings.WindowsPrinterName, data, "ParkEasy - Comprovante Saida");

            if (!success)
            {
                _logger.LogWarning("Falha ao enviar comprovante para a impressora {PrinterName}", _settings.WindowsPrinterName);
                throw new InvalidOperationException(
                    $"Não foi possível imprimir o comprovante.\nVerifique a impressora '{_settings.WindowsPrinterName}'.");
            }

            _logger.LogInformation("Comprovante impresso com sucesso: {TicketNumber}", receipt.TicketNumber);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao imprimir comprovante: {TicketNumber}", receipt.TicketNumber);
            throw new InvalidOperationException(
                $"Erro ao imprimir o comprovante.\nVerifique a Bematech MP-4200 TH.\n\nDetalhes: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task TestPrinterAsync()
    {
        _logger.LogInformation("Teste de impressora iniciado: {PrinterName}", _settings.WindowsPrinterName);

        using var ms = new MemoryStream();

        Write(ms, CMD_INIT);

        Write(ms, CMD_CENTER);
        Write(ms, CMD_BOLD_ON);
        Write(ms, CMD_DOUBLE_SIZE);
        WriteText(ms, "TESTE");
        Write(ms, CMD_NORMAL_SIZE);
        Write(ms, CMD_BOLD_OFF);
        WriteText(ms, "");
        WriteText(ms, "ParkEasy");
        WriteText(ms, "Impressora OK!");
        WriteText(ms, "");
        WriteText(ms, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        WriteText(ms, "");
        WriteText(ms, "");

        Write(ms, CMD_CUT);

        var data = ms.ToArray();
        var success = RawPrinterHelper.SendBytesToPrinter(_settings.WindowsPrinterName, data, "ParkEasy - Teste");

        if (!success)
        {
            throw new InvalidOperationException(
                $"Falha ao imprimir teste.\nVerifique se a impressora '{_settings.WindowsPrinterName}' está instalada e ligada.");
        }

        _logger.LogInformation("Teste de impressora concluído com sucesso.");
        return Task.CompletedTask;
    }

    private void Write(MemoryStream ms, byte[] data)
    {
        ms.Write(data, 0, data.Length);
    }

    private void WriteText(MemoryStream ms, string text)
    {
        var bytes = _encoding.GetBytes(text);
        ms.Write(bytes, 0, bytes.Length);
        ms.Write(CMD_FEED, 0, CMD_FEED.Length);
    }
}
