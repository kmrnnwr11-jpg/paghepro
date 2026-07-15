namespace Paghe.Persistence.Entities;

/// <summary>
/// Contatore mensile delle chiamate AI di uno studio (tenant). Serve al modello
/// "AI inclusa nel canone": la chiave del provider è unica e centrale (dello sviluppatore),
/// quindi ogni piano ha un limite mensile di letture AI per tenerne sotto controllo il costo.
/// </summary>
public class ConsumoAiEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }

    /// <summary>Chiamate AI effettuate nel mese.</summary>
    public int Chiamate { get; set; }

    public DateTime AggiornatoIl { get; set; }
}
