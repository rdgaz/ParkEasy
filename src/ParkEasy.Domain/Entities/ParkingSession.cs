using ParkEasy.Domain.Enums;

namespace ParkEasy.Domain.Entities;

public class ParkingSession
{
    public long Id { get; set; }

    public string TicketNumber { get; set; } = string.Empty;

    public string Plate { get; set; } = string.Empty;

    public VehicleType VehicleType { get; set; }

    public string? VehicleModel { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public DateTime EntryDateTime { get; set; }

    public DateTime? ExitDateTime { get; set; }

    public ParkingSessionStatus Status { get; set; }

    public decimal? FinalAmount { get; set; }

    /// <summary>Usuário logado que registrou a entrada do veículo.</summary>
    public string? EntryUsername { get; set; }

    /// <summary>Usuário logado que confirmou o pagamento no checkout.</summary>
    public string? CheckoutUsername { get; set; }

    /// <summary>"Hora", "Diária", "Mensal", ou um dos tipos de lavagem cadastrados em WashPricing.</summary>
    public string? ServiceType { get; set; }

    /// <summary>Valor fixo do serviço — só usado quando ServiceType não é "Hora" (essa é calculada ao vivo).</summary>
    public decimal? ServiceAmount { get; set; }

    public string? ServiceNotes { get; set; }

    /// <summary>Só usado quando ServiceType é um tipo de lavagem — controla a fila Pendente/Lavando/Concluída.</summary>
    public WashStatus? ServiceStatus { get; set; }

    public DateTime? ServiceRequestedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Falso sempre que a sessão é criada ou alterada — a sincronização com a planilha
    /// (EasyParkSync) só marca como true depois de escrever a linha com sucesso.
    /// </summary>
    public bool SyncedToSheets { get; set; }
}
