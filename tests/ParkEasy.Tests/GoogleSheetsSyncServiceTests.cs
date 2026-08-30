using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkEasy.Application.Configuration;
using ParkEasy.Infrastructure.Sync;
using Xunit;

namespace ParkEasy.Tests;

public class GoogleSheetsSyncServiceTests
{
    [Fact]
    public async Task SyncPendingAsync_NotConfigured_NeverTouchesServiceProvider()
    {
        // MockBehavior.Strict: qualquer chamada não configurada no mock lança imediatamente.
        // Chegar ao fim sem exceção prova que o guard clause (SpreadsheetId/CredentialsFilePath
        // vazios) retorna antes de sequer abrir um scope para consultar o repositório.
        var serviceProviderMock = new Mock<IServiceProvider>(MockBehavior.Strict);
        var options = Options.Create(new EasyParkSyncSettings());
        var logger = NullLogger<GoogleSheetsSyncService>.Instance;

        var service = new GoogleSheetsSyncService(serviceProviderMock.Object, options, logger);

        await service.SyncPendingAsync();
    }
}
