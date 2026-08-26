namespace ParkEasy.Domain.Enums;

public static class WashTypeExtensions
{
    public static string ToDisplayName(this WashType washType) => washType switch
    {
        WashType.Expressa => "Expressa",
        WashType.Completa => "Completa",
        WashType.Interna => "Interna",
        WashType.Personalizada => "Personalizada",
        _ => washType.ToString()
    };
}
