using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkEasy.Application.Configuration;
using ParkEasy.Application.Interfaces;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Interfaces;
using ParkEasy.Infrastructure.Data;
using ParkEasy.Infrastructure.Printing;
using ParkEasy.Infrastructure.Repositories;
using ParkEasy.UI.Forms;

namespace ParkEasy.UI;

internal static class Program
{
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
            EnsureColumnExists(db, "WashType", "INTEGER NULL");
            EnsureColumnExists(db, "WashAmount", "REAL NULL");
            EnsureColumnExists(db, "WashNotes", "TEXT NULL");
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
        services.Configure<ParkingSettings>(configuration.GetSection(ParkingSettings.SectionName));
        services.Configure<PricingSettings>(configuration.GetSection(PricingSettings.SectionName));
        services.Configure<WashPricingSettings>(configuration.GetSection(WashPricingSettings.SectionName));
        services.Configure<PrinterSettings>(configuration.GetSection(PrinterSettings.SectionName));

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

        // Services
        services.AddScoped<IParkingFeeCalculator, ParkingFeeCalculator>();
        services.AddScoped<IParkingService, ParkingService>();

        // Printer — choose implementation based on config
        var printerType = configuration.GetSection("Printer:Type").Value ?? "Mock";
        if (printerType.Equals("BematechMP4200TH", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPrinterService, BematechMp4200PrinterService>();
        }
        else
        {
            services.AddSingleton<IPrinterService, MockPrinterService>();
        }

        // Forms
        services.AddTransient<MainForm>();
        services.AddTransient<EntryForm>();
        services.AddTransient<CheckoutForm>();
        services.AddTransient<HistoryForm>();
        services.AddTransient<WashForm>();

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
}