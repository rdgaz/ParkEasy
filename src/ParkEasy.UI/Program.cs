using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;
using ParkEasy.Infrastructure.Data;
using ParkEasy.Infrastructure.Printing;
using ParkEasy.Infrastructure.Repositories;
using ParkEasy.Infrastructure.Sync;
using ParkEasy.UI.Forms;

namespace ParkEasy.UI;

internal static class Program
{
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "855683";

    [STAThread]
    static void Main()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);

        var services = ConfigureServices();

        // Ensure database is created and migrated
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            db.Database.EnsureCreated();
            EnsureColumnExists(db, "VehicleType", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumnRenamed(db, "WashTypeName", "ServiceType", "TEXT NULL");
            EnsureColumnRenamed(db, "WashAmount", "ServiceAmount", "REAL NULL");
            EnsureColumnRenamed(db, "WashNotes", "ServiceNotes", "TEXT NULL");
            EnsureColumnRenamed(db, "WashStatus", "ServiceStatus", "INTEGER NULL");
            EnsureColumnRenamed(db, "WashRequestedAt", "ServiceRequestedAt", "TEXT NULL");
            EnsureColumnExists(db, "EntryUsername", "TEXT NULL");
            EnsureColumnExists(db, "CheckoutUsername", "TEXT NULL");
            EnsureColumnExists(db, "SyncedToSheets", "INTEGER NOT NULL DEFAULT 0");
            EnsureUsersTable(db);

            SeedDefaultAdminIfNeeded(scope.ServiceProvider);
        }

        // Login gate — nada abre sem autenticação
        using (var loginScope = services.CreateScope())
        {
            var loginForm = loginScope.ServiceProvider.GetRequiredService<LoginForm>();
            if (loginForm.ShowDialog() != DialogResult.OK)
            {
                return;
            }
        }

        var mainForm = services.GetRequiredService<MainForm>();
        System.Windows.Forms.Application.Run(mainForm);
    }

    private static ServiceProvider ConfigureServices()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<BusinessSettings>(configuration.GetSection(BusinessSettings.SectionName));
        services.Configure<ParkingSettings>(configuration.GetSection(ParkingSettings.SectionName));
        services.Configure<PricingSettings>(configuration.GetSection(PricingSettings.SectionName));
        services.Configure<WashPricingSettings>(configuration.GetSection(WashPricingSettings.SectionName));
        services.Configure<WashQueueSettings>(configuration.GetSection(WashQueueSettings.SectionName));
        services.Configure<PrinterSettings>(configuration.GetSection(PrinterSettings.SectionName));
        services.Configure<EasyParkSyncSettings>(configuration.GetSection(EasyParkSyncSettings.SectionName));

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
            builder.AddDebug();
        });

        // Database
        services.AddDbContext<ParkingDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Services
        services.AddScoped<IParkingFeeCalculator, ParkingFeeCalculator>();
        services.AddScoped<IParkingService, ParkingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
        services.AddSingleton<ISheetsSyncService, GoogleSheetsSyncService>();

        // Printer — choose implementation based on config
        var printerType = configuration.GetSection("Printer:Type").Value ?? "Mock";
        if (printerType.Equals("Bematech MP-4200 TH", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPrinterService, BematechMp4200PrinterService>();
        }
        else
        {
            services.AddSingleton<IPrinterService, MockPrinterService>();
        }

        // Forms
        services.AddTransient<LoginForm>();
        services.AddTransient<ChangePasswordForm>();
        services.AddTransient<ManageUsersForm>();
        services.AddTransient<MainForm>();
        services.AddTransient<EntryForm>();
        services.AddTransient<CheckoutForm>();
        services.AddTransient<HistoryForm>();
        services.AddTransient<ServiceForm>();
        services.AddTransient<WashQueueForm>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Lightweight schema upgrade for pre-existing SQLite databases created before a given
    /// column existed. EnsureCreated() only creates new databases, so older files need
    /// columns added manually as the model evolves.
    /// </summary>
    private static void EnsureColumnExists(ParkingDbContext db, string columnName, string columnDefinition)
    {
        var hasColumn = db.Database
            .SqlQueryRaw<string>(
                $"SELECT name FROM pragma_table_info('ParkingSessions') WHERE name = '{columnName}'")
            .AsEnumerable()
            .Any();

        if (!hasColumn)
        {
            db.Database.ExecuteSqlRaw(
                $"ALTER TABLE ParkingSessions ADD COLUMN {columnName} {columnDefinition}");
        }
    }

    /// <summary>
    /// Renomeia uma coluna existente preservando os dados (ex: campos antigos de "Wash..."
    /// virando "Service..." na unificação do Tipo Serviço). Se nem a coluna antiga nem a
    /// nova existirem (banco muito antigo), cria a nova do zero como fallback.
    /// </summary>
    private static void EnsureColumnRenamed(ParkingDbContext db, string oldName, string newName, string fallbackColumnDefinition)
    {
        bool ColumnExists(string columnName) => db.Database
            .SqlQueryRaw<string>($"SELECT name FROM pragma_table_info('ParkingSessions') WHERE name = '{columnName}'")
            .AsEnumerable()
            .Any();

        if (ColumnExists(newName))
            return;

        if (ColumnExists(oldName))
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE ParkingSessions RENAME COLUMN {oldName} TO {newName}");
        }
        else
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE ParkingSessions ADD COLUMN {newName} {fallbackColumnDefinition}");
        }
    }

    /// <summary>
    /// EnsureCreated() só cria tabelas novas em bancos que ainda não existem — bancos
    /// já criados antes da tabela Users existir precisam dela adicionada manualmente.
    /// </summary>
    private static void EnsureUsersTable(ParkingDbContext db)
    {
        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                Role INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            """);

        db.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username ON Users (Username);");

        // Bancos criados antes do cargo (Role) existir na tabela.
        var hasRoleColumn = db.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Users') WHERE name = 'Role'")
            .AsEnumerable()
            .Any();

        if (!hasRoleColumn)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Role INTEGER NOT NULL DEFAULT 0");
        }
    }

    private static void SeedDefaultAdminIfNeeded(IServiceProvider serviceProvider)
    {
        var userRepository = serviceProvider.GetRequiredService<IUserRepository>();

        if (userRepository.AnyUsersExistAsync().GetAwaiter().GetResult())
            return;

        var (hash, salt) = PasswordHasher.Hash(DefaultAdminPassword);

        userRepository.AddAsync(new User
        {
            Username = DefaultAdminUsername,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Desenvolvedor,
            CreatedAt = DateTime.Now
        }).GetAwaiter().GetResult();
    }
}
