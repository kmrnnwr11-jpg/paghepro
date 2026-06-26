namespace Paghe.Persistence.Entities;

/// <summary>
/// Delega di un cliente (azienda) verso lo studio/intermediario (il tenant) per uno specifico
/// adempimento telematico: fattura elettronica, ComUnica, F24, cassetto fiscale, ecc.
/// Senza una delega attiva il software non deve trasmettere per conto del cliente.
/// </summary>
public class DelegaEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant intermediario (studio/commercialista) titolare della delega.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Azienda cliente che delega.</summary>
    public Guid AziendaId { get; set; }
    public AziendaEntity? Azienda { get; set; }

    /// <summary>Adempimento delegato (es. FatturaElettronica, ComUnica, F24, CassettoFiscale).</summary>
    public string TipoAdempimento { get; set; } = "";

    /// <summary>Stato: Attiva, Sospesa, Revocata.</summary>
    public string Stato { get; set; } = "Attiva";

    public DateOnly DataInizio { get; set; }
    public DateOnly? DataFine { get; set; }

    /// <summary>Riferimento/protocollo del modulo di delega firmato dal cliente.</summary>
    public string? Riferimento { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset CreataIl { get; set; } = DateTimeOffset.UtcNow;
}
