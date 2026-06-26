namespace Paghe.Persistence.Entities;

/// <summary>Azienda (datore di lavoro / soggetto emittente) persistita su database.</summary>
public class AziendaEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant proprietario del dato (isolamento multi-tenant).</summary>
    public Guid TenantId { get; set; }

    public string RagioneSociale { get; set; } = "";
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }

    public string Indirizzo { get; set; } = "";
    public string Cap { get; set; } = "";
    public string Comune { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Nazione { get; set; } = "IT";
    public string RegimeFiscale { get; set; } = "RF01";

    public List<FatturaEntity> Fatture { get; set; } = new();
}
