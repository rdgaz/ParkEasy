namespace ParkEasy.Application.Configuration;

public class BusinessSettings
{
    public const string SectionName = "Business";

    public string Name { get; set; } = "ParkEasy Estacionamento";
    public string Cnpj { get; set; } = string.Empty;
}
