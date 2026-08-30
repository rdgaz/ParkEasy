namespace ParkEasy.Application.Configuration;

/// <summary>
/// Quando desativada, entradas com Tipo Serviço de lavagem são cobradas como um valor fixo
/// direto (igual Diária/Mensal), sem passar pelo fluxo Pendente/Lavando/Concluída.
/// </summary>
public class WashQueueSettings
{
    public const string SectionName = "WashQueue";

    public bool Enabled { get; set; } = true;
}
