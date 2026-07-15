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

    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Pec { get; set; }
    public string? CodiceDestinatarioSdi { get; set; }
    public string? CodiceCatastale { get; set; }
    public string? Settore { get; set; }
    public string? CodiceAteco { get; set; }
    public string? MatricolaInps { get; set; }
    public string? CodiceSedeInps { get; set; }
    public string? CscInps { get; set; }
    public string? PatInail { get; set; }
    public string? CcnlDefault { get; set; }
    public string? LivelloDefault { get; set; }
    public string? Iban { get; set; }
    public string? LegaleRappresentanteNome { get; set; }
    public string? LegaleRappresentanteCognome { get; set; }
    public string? LegaleRappresentanteCodiceFiscale { get; set; }
    /// <summary>null = usa il default globale (Manuale). Valori: "Email", "Sms", "WhatsApp".</summary>
    public string? CanalePredefinito { get; set; }

    /// <summary>Logo aziendale (PNG o JPEG) mostrato sui documenti, es. la busta paga. Facoltativo.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[]? Logo { get; set; }

    public bool HaDipendenti { get; set; }
    /// <summary>"Artigiano", "Commerciante", "Professionista", "Societa".</summary>
    public string? TipoContribuente { get; set; }
    public bool AssoggettataIva { get; set; } = true;

    public List<FatturaEntity> Fatture { get; set; } = new();
}
