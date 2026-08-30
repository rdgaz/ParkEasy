namespace ParkEasy.Application.Interfaces;

/// <summary>
/// Sincroniza sessões pendentes (SyncedToSheets = false) com a planilha do Google Sheets.
/// Nunca deve lançar exceção para o chamador — falhas de rede/credencial ficam só logadas,
/// e a linha continua marcada como não sincronizada para ser tentada na próxima chamada.
/// </summary>
public interface ISheetsSyncService
{
    Task SyncPendingAsync();
}
