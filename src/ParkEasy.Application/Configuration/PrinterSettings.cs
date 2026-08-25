namespace ParkEasy.Application.Configuration;

public class PrinterSettings
{
    public const string SectionName = "Printer";

    public string Type { get; set; } = "Mock";
    public string WindowsPrinterName { get; set; } = "Bematech MP-4200 TH";
}
