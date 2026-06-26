namespace Paghe.Persistence.Entities;

/// <summary>Fattura emessa, persistita su database (con righe e totali calcolati).</summary>
public class FatturaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public AziendaEntity? Azienda { get; set; }

    public string Numero { get; set; } = "";
    public int Anno { get; set; }
    public DateOnly Data { get; set; }
    public string TipoDocumento { get; set; } = "TD01";
    public string Divisa { get; set; } = "EUR";

    // Cliente (cessionario/committente)
    public string CessionarioDenominazione { get; set; } = "";
    public string? CessionarioPartitaIva { get; set; }
    public string? CessionarioCodiceFiscale { get; set; }
    public string CessionarioIndirizzo { get; set; } = "";
    public string CessionarioCap { get; set; } = "";
    public string CessionarioComune { get; set; } = "";
    public string CessionarioProvincia { get; set; } = "";
    public string CessionarioNazione { get; set; } = "IT";
    public string CodiceDestinatario { get; set; } = "0000000";

    // Totali calcolati al momento del salvataggio
    public decimal TotaleImponibile { get; set; }
    public decimal TotaleImposta { get; set; }
    public decimal TotaleDocumento { get; set; }

    public List<RigaFatturaEntity> Righe { get; set; } = new();
}

/// <summary>Riga di dettaglio di una fattura persistita.</summary>
public class RigaFatturaEntity
{
    public Guid Id { get; set; }
    public Guid FatturaId { get; set; }

    public string Descrizione { get; set; } = "";
    public decimal Quantita { get; set; } = 1m;
    public decimal PrezzoUnitario { get; set; }
    public decimal AliquotaIva { get; set; }
    public string? NaturaIva { get; set; }
}
