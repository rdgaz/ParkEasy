using System.Globalization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;

namespace ParkEasy.Infrastructure.Sync;

/// <summary>
/// Sincroniza sessões pendentes com uma planilha do Google Sheets (upsert por número de
/// ticket — a coluna A). Nunca lança para o chamador: falha de rede/credencial só fica
/// logada, e a sessão continua com SyncedToSheets=false para ser tentada na próxima chamada.
/// </summary>
public class GoogleSheetsSyncService : ISheetsSyncService
{
    private static readonly string[] Headers =
    [
        "Ticket", "Placa", "Tipo Veículo", "Modelo", "Cliente", "Telefone",
        "Entrada", "Saída", "Status", "Tipo Serviço", "Valor",
        "Atendente (Entrada)", "Caixa (Pagamento)", "Última Atualização"
    ];

    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IServiceProvider _serviceProvider;
    private readonly EasyParkSyncSettings _settings;
    private readonly ILogger<GoogleSheetsSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private SheetsService? _sheetsService;

    public GoogleSheetsSyncService(
        IServiceProvider serviceProvider,
        IOptions<EasyParkSyncSettings> settingsOptions,
        ILogger<GoogleSheetsSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settingsOptions.Value;
        _logger = logger;
    }

    public async Task SyncPendingAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.SpreadsheetId) || string.IsNullOrWhiteSpace(_settings.CredentialsFilePath))
            return; // Sincronização não configurada — não é erro, só não faz nada.

        // Evita duas sincronizações rodando em paralelo (ex: duas ações em sequência rápida).
        if (!await _syncLock.WaitAsync(0))
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();

            var pending = await repository.GetUnsyncedSessionsAsync();
            if (pending.Count == 0)
                return;

            var sheetsService = GetOrCreateSheetsService();
            await EnsureHeaderRowAsync(sheetsService);
            var ticketToRow = await BuildTicketRowMapAsync(sheetsService);

            foreach (var session in pending)
            {
                try
                {
                    await UpsertRowAsync(sheetsService, ticketToRow, session);

                    session.SyncedToSheets = true;
                    await repository.UpdateAsync(session);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Falha ao sincronizar ticket {TicketNumber} com a planilha — será tentado de novo na próxima operação.",
                        session.TicketNumber);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha geral ao sincronizar com o Google Sheets.");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private SheetsService GetOrCreateSheetsService()
    {
        if (_sheetsService is not null)
            return _sheetsService;

        var credentialsPath = Path.IsPathRooted(_settings.CredentialsFilePath)
            ? _settings.CredentialsFilePath
            : Path.Combine(AppContext.BaseDirectory, _settings.CredentialsFilePath);

        var credential = GoogleCredential.FromFile(credentialsPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        _sheetsService = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ParkEasy"
        });

        return _sheetsService;
    }

    private async Task EnsureHeaderRowAsync(SheetsService sheetsService)
    {
        var headerRange = $"{_settings.SheetName}!A1:A1";
        var getRequest = sheetsService.Spreadsheets.Values.Get(_settings.SpreadsheetId, headerRange);
        var getResponse = await getRequest.ExecuteAsync();

        var hasHeader = getResponse.Values is { Count: > 0 } row
            && row.Count > 0
            && !string.IsNullOrWhiteSpace(row[0]?.ToString());

        if (hasHeader)
            return;

        var valueRange = new ValueRange { Values = [Headers.Cast<object>().ToList()] };
        var updateRange = $"{_settings.SheetName}!A1:N1";

        var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, _settings.SpreadsheetId, updateRange);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await updateRequest.ExecuteAsync();
    }

    private async Task<Dictionary<string, int>> BuildTicketRowMapAsync(SheetsService sheetsService)
    {
        var range = $"{_settings.SheetName}!A2:A";
        var request = sheetsService.Spreadsheets.Values.Get(_settings.SpreadsheetId, range);
        var response = await request.ExecuteAsync();

        var map = new Dictionary<string, int>();

        if (response.Values is null)
            return map;

        for (var i = 0; i < response.Values.Count; i++)
        {
            var ticket = response.Values[i].Count > 0 ? response.Values[i][0]?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(ticket))
                map[ticket] = i + 2; // +2: linha 1 é cabeçalho, índice de lista começa em 0
        }

        return map;
    }

    private async Task UpsertRowAsync(SheetsService sheetsService, Dictionary<string, int> ticketToRow, ParkingSession session)
    {
        var row = new List<object>
        {
            session.TicketNumber,
            session.Plate,
            session.VehicleType.ToDisplayName(),
            session.VehicleModel ?? "",
            session.CustomerName ?? "",
            session.CustomerPhone ?? "",
            session.EntryDateTime.ToString("dd/MM/yyyy HH:mm:ss"),
            session.ExitDateTime?.ToString("dd/MM/yyyy HH:mm:ss") ?? "",
            session.Status.ToString(),
            session.ServiceType ?? "",
            (session.FinalAmount ?? 0).ToString("F2", BrCulture),
            session.EntryUsername ?? "",
            session.CheckoutUsername ?? "",
            session.UpdatedAt.ToString("dd/MM/yyyy HH:mm:ss")
        };

        var valueRange = new ValueRange { Values = [row] };

        if (ticketToRow.TryGetValue(session.TicketNumber, out var rowNumber))
        {
            var range = $"{_settings.SheetName}!A{rowNumber}:N{rowNumber}";
            var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, _settings.SpreadsheetId, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateRequest.ExecuteAsync();
        }
        else
        {
            var appendRange = $"{_settings.SheetName}!A1";
            var appendRequest = sheetsService.Spreadsheets.Values.Append(valueRange, _settings.SpreadsheetId, appendRange);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;
            appendRequest.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
            await appendRequest.ExecuteAsync();
        }
    }
}
