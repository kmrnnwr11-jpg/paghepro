namespace Paghe.Persistence.Entities;

/// <summary>
/// Trasmissione di una fattura elettronica verso lo SdI (tramite provider o canale manuale),
/// con tracciamento dello stato e delle ricevute. Isolata per tenant.
/// </summary>
public class TrasmissioneFatturaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public Guid FatturaId { get; set; }
    public FatturaEntity? Fattura { get; set; }

    /// <summary>Canale usato (nome del gateway).</summary>
    public string Provider { get; set; } = "";

    /// <summary>Nome file XML/.p7m inviato, secondo le specifiche SdI.</summary>
    public string NomeFileXml { get; set; } = "";

    /// <summary>Vero se il file è stato firmato prima dell'invio.</summary>
    public bool Firmato { get; set; }

    /// <summary>Stato corrente (valore di <c>StatoTrasmissioneSdi</c> serializzato come stringa).</summary>
    public string Stato { get; set; } = "DaInviare";

    /// <summary>Identificativo assegnato dallo SdI/provider (per interrogare gli esiti).</summary>
    public string? IdentificativoSdi { get; set; }

    public string? CodiceErrore { get; set; }
    public string? MessaggioErrore { get; set; }

    public DateTimeOffset CreataIl { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? InviataIl { get; set; }
    public DateTimeOffset UltimoAggiornamento { get; set; } = DateTimeOffset.UtcNow;

    public List<RicevutaSdiEntity> Ricevute { get; set; } = new();
}

/// <summary>Ricevuta/notifica restituita dallo SdI per una trasmissione (RC, MC, NS, NE, DT).</summary>
public class RicevutaSdiEntity
{
    public Guid Id { get; set; }
    public Guid TrasmissioneFatturaId { get; set; }

    /// <summary>Tipo notifica SdI: RC, MC, NS, NE, DT.</summary>
    public string Tipo { get; set; } = "";

    /// <summary>Codice esito (es. EC01 accettazione, EC02 rifiuto; codici di scarto 00xxx).</summary>
    public string? CodiceEsito { get; set; }

    public string? Descrizione { get; set; }

    /// <summary>Contenuto grezzo della notifica (XML SdI), per conservazione/audit.</summary>
    public string? ContenutoGrezzo { get; set; }

    public DateTimeOffset RicevutaIl { get; set; } = DateTimeOffset.UtcNow;
}
