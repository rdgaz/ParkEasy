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

        return services.BuildServiceProvider();
    }
}