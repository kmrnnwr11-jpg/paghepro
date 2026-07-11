namespace Paghe.Persistence.Entities;

/// <summary>Pratica preparatoria per apertura partita IVA / Comunicazione Unica.</summary>
public class PraticaAperturaPartitaIvaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AziendaId { get; set; }
    public AziendaEntity? Azienda { get; set; }

    public string TipoPratica { get; set; } = "NuovaImpresa";
    public string FormaGiuridica { get; set; } = "";
    public string RegimeContabile { get; set; } = "";
    public DateOnly DataInizioAttivita { get; set; }

    public string Denominazione { get; set; } = "";
    public string CodiceFiscale { get; set; } = "";
    public string? PartitaIva { get; set; }
    public string CodiceAteco { get; set; } = "";
    public string DescrizioneAttivita { get; set; } = "";

    public string IndirizzoSede { get; set; } = "";
    public string Cap { get; set; } = "";
    public string Comune { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string? Pec { get; set; }

    public bool AttivitaCommercio { get; set; }
    public bool AttivitaArtigiana { get; set; }
    public bool ImpresaAgricola { get; set; }
    public bool HaDipendentiPrevisti { get; set; }
    public bool SoggettaInail { get; set; }

    public string ModelloAgenziaEntrate { get; set; } = "";
    public bool RichiedeComunicazioneUnica { get; set; }
    public bool RichiedeRegistroImprese { get; set; }
    public bool RichiedeCameraCommercio { get; set; }
    public string Stato { get; set; } = "Bozza";
    public DateTime CreataIl { get; set; } = DateTime.UtcNow;

    public List<AdempimentoPraticaEntity> Adempimenti { get; set; } = new();
}

/// <summary>Adempimento operativo collegato a una pratica.</summary>
public class AdempimentoPraticaEntity
{
    public Guid Id { get; set; }
    public Guid PraticaAperturaPartitaIvaId { get; set; }

    public string Codice { get; set; } = "";
    public string Ente { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public bool Obbligatorio { get; set; }
    public string Stato { get; set; } = "";
    public string? Note { get; set; }
}
