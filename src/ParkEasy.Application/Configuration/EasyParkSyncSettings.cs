namespace ParkEasy.Application.Configuration;

public class EasyParkSyncSettings
{
    public const string SectionName = "EasyParkSync";

    public string SpreadsheetId { get; set; } = string.Empty;
    public string CredentialsFilePath { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
}
