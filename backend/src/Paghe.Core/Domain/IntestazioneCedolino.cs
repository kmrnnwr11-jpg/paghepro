namespace Paghe.Core.Domain;

/// <summary>
/// Dati anagrafici per l'intestazione della busta paga (PDF): ditta, posizioni
/// contributive e inquadramento del dipendente. Tutti i campi sono facoltativi
/// tranne i nomi: il PDF mostra solo quelli valorizzati.
/// </summary>
public sealed record IntestazioneCedolino
{
    public string Azienda { get; init; } = "Azienda";
    /// <summary>Logo aziendale (PNG/JPEG) mostrato in testata; null = nessun logo.</summary>
    public byte[]? Logo { get; init; }
    public string? AziendaIndirizzo { get; init; }
    /// <summary>Riga "CAP Comune (Provincia)" già composta.</summary>
    public string? AziendaComune { get; init; }
    public string? PartitaIva { get; init; }
    public string? CodiceFiscaleAzienda { get; init; }
    public string? MatricolaInps { get; init; }
    public string? PatInail { get; init; }

    public string Dipendente { get; init; } = "Dipendente";
    public string? CodiceFiscaleDipendente { get; init; }
    public string? Matricola { get; init; }
    /// <summary>IBAN usato per l'accredito dello stipendio.</summary>
    public string? IbanDipendente { get; init; }
    /// <summary>Data assunzione già formattata (gg/mm/aaaa).</summary>
    public string? DataAssunzione { get; init; }
    /// <summary>Data di cessazione già formattata; null = rapporto ancora in forza.</summary>
    public string? DataCessazione { get; init; }
    /// <summary>Data di nascita già formattata, ricavata dal codice fiscale.</summary>
    public string? DataNascita { get; init; }
    /// <summary>Comune di residenza/domicilio fiscale, nella forma "Comune (PR)".</summary>
    public string? ComuneResidenza { get; init; }
    public string? Qualifica { get; init; }
    public string? Livello { get; init; }
    public string? Ccnl { get; init; }
    /// <summary>
    /// Codice alfanumerico unico del CCNL depositato al CNEL. Non va dedotto dal codice
    /// interno: se manca, il PDF dichiara che la bozza non è validabile.
    /// </summary>
    public string? CodiceCnel { get; init; }
    public string? TipoContratto { get; init; }
    public int? Mensilita { get; init; }
    /// <summary>Es. "40 ore/sett." oppure "Part-time 50%".</summary>
    public string? Orario { get; init; }
}
