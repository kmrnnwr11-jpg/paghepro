namespace Paghe.Persistence.Entities;

/// <summary>
/// DURC (Documento Unico di Regolarità Contributiva) di un'azienda. Serve a tracciarne la
/// scadenza per generare in automatico un promemoria di rinnovo. Isolato per tenant.
/// </summary>
public class DurcEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Azienda a cui si riferisce il DURC.</summary>
    public Guid AziendaId { get; set; }

    /// <summary>Numero di protocollo della richiesta/verifica DURC.</summary>
    public string Numero { get; set; } = "";

    public DateOnly DataEmissione { get; set; }

    /// <summary>Scadenza di validità (di norma 120 giorni dall'emissione).</summary>
    public DateOnly DataScadenza { get; set; }

    /// <summary>Esito: regolare (true) o irregolare (false).</summary>
    public bool Regolare { get; set; } = true;

    public string? Note { get; set; }

    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Promemoria/scadenza tracciata dal sistema. Può essere generato in automatico da una fonte
/// (DURC in scadenza, F24 da versare) oppure inserito a mano. Il testo del messaggio è
/// precompilato. Isolato per tenant.
/// </summary>
public class PromemoriaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Azienda collegata (può mancare per promemoria generici).</summary>
    public Guid? AziendaId { get; set; }

    /// <summary>Tipo del promemoria: vedi <see cref="TipoPromemoria"/>.</summary>
    public string Tipo { get; set; } = TipoPromemoria.Manuale;

    /// <summary>
    /// Id della fonte da cui è stato generato (es. <c>DurcEntity.Id</c> o <c>PraticaF24Entity.Id</c>)
    /// come stringa. Usato per evitare duplicati in rigenerazione. Nullo per i promemoria manuali.
    /// </summary>
    public string? RiferimentoId { get; set; }

    public string Titolo { get; set; } = "";
    public string Descrizione { get; set; } = "";

    public DateOnly DataScadenza { get; set; }

    /// <summary>Giorni di preavviso: il promemoria entra "in scadenza" da DataScadenza - GiorniPreavviso.</summary>
    public int GiorniPreavviso { get; set; } = 7;

    /// <summary>Stato: vedi <see cref="StatoPromemoria"/>.</summary>
    public string Stato { get; set; } = StatoPromemoria.Aperto;

    /// <summary>Testo del messaggio precompilato (sollecito/avviso) pronto da inviare/copiare.</summary>
    public string Messaggio { get; set; } = "";

    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
    public DateTime UltimoAggiornamento { get; set; } = DateTime.UtcNow;

    /// <summary>Quando il promemoria è stato segnato come inviato (se mai).</summary>
    public DateTime? InviatoIl { get; set; }
}

/// <summary>Tipi di promemoria gestiti.</summary>
public static class TipoPromemoria
{
    public const string Durc = "Durc";
    public const string F24 = "F24";
    public const string Manuale = "Manuale";
    /// <summary>Generato automaticamente dal calendario scadenze fiscali (IRPEF, IVA, INAIL, ecc.).</summary>
    public const string Calendario = "Calendario";

    public static readonly string[] Tutti = { Durc, F24, Manuale, Calendario };
}

/// <summary>Stati di un promemoria.</summary>
public static class StatoPromemoria
{
    public const string Aperto = "Aperto";
    public const string Inviato = "Inviato";
    public const string Completato = "Completato";
    public const string Annullato = "Annullato";

    public static readonly string[] Tutti = { Aperto, Inviato, Completato, Annullato };

    /// <summary>Stati finali: non si può tornare indietro.</summary>
    public static bool Finale(string stato) => stato is Completato or Annullato;
}
