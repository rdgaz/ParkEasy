namespace ParkEasy.Application;

/// <summary>
/// Nomes fixos de Tipo Serviço que não vêm do cadastro de lavagens (WashPricing) —
/// esses três sempre existem; os demais itens da lista vêm das chaves de WashPricing,
/// mais a opção livre "Personalizada".
/// </summary>
public static class ServiceTypeNames
{
    public const string Hora = "Hora";
    public const string Diaria = "Diária";
    public const string Mensal = "Mensal";
    public const string Personalizada = "Personalizada";
}
