namespace ParkEasy.Domain.Enums;

public static class WashStatusExtensions
{
    public static string ToDisplayName(this WashStatus status) => status switch
    {
        WashStatus.Pendente => "Pendente",
        WashStatus.Lavando => "Lavando",
        WashStatus.Concluida => "Concluída",
        _ => status.ToString()
    };
}
