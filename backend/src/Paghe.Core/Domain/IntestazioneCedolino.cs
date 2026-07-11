namespace Paghe.Core.Domain;

/// <summary>
/// Dati anagrafici per l'intestazione della busta paga (PDF): ditta, posizioni
/// contributive e inquadramento del dipendente. Tutti i campi sono facoltativi
/// tranne i nomi: il PDF mostra solo quelli valorizzati.
/// </summary>
public sealed record IntestazioneCedolino
{
    public string Azienda { get; init; } = "Azienda";
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
    /// <summary>Data assunzione già formattata (gg/mm/aaaa).</summary>
    public string? DataAssunzione { get; init; }
    public string? Qualifica { get; init; }
    public string? Livello { get; init; }
    public string? Ccnl { get; init; }
    public int? Mensilita { get; init; }
    /// <summary>Es. "40 ore/sett." oppure "Part-time 50%".</summary>
    public string? Orario { get; init; }
}
