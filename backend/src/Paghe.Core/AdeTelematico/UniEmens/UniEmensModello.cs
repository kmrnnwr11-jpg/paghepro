namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Modello del flusso UniEmens: la denuncia mensile che il datore/intermediario trasmette
/// all'INPS con le retribuzioni e i contributi dei lavoratori dipendenti.
///
/// ATTENZIONE: struttura allineata allo schema pubblico UniEmens ma <b>semplificata</b> e
/// <b>non ancora validata</b> con il software di controllo ufficiale INPS. È una bozza
/// tecnica: NON trasmettere senza la validazione ufficiale. Diversi elementi del tracciato
/// reale (es. &lt;Settimana&gt;, &lt;DenunciaAziendale&gt; completa, gestioni speciali) qui
/// sono ridotti all'essenziale e andranno completati nelle iterazioni successive.
/// </summary>
public sealed record FlussoUniEmens(
    DatiMittenteUniEmens Mittente,
    DenunciaAziendaleUniEmens Azienda,
    IReadOnlyList<DenunciaIndividualeUniEmens> Lavoratori);

/// <summary>Chi trasmette il flusso (datore di lavoro o intermediario abilitato).</summary>
public sealed record DatiMittenteUniEmens(
    string CodiceFiscale,
    string Denominazione);

/// <summary>
/// Posizione contributiva dell'azienda (testata della denuncia aziendale).
/// La <see cref="MatricolaInps"/> è la matricola INPS della posizione contributiva.
/// </summary>
public sealed record DenunciaAziendaleUniEmens(
    string MatricolaInps,
    string CodiceFiscaleAzienda,
    int Anno,
    int Mese);

/// <summary>
/// Denuncia individuale di un lavoratore nel mese: anagrafica minima e dati retributivi
/// (imponibile previdenziale e contributo). Importi in euro (<c>decimal</c>).
/// </summary>
public sealed record DenunciaIndividualeUniEmens(
    string CodiceFiscale,
    string Cognome,
    string Nome,
    string Qualifica,
    decimal Imponibile,
    decimal Contributo,
    int GiorniRetribuiti,
    int SettimaneUtili = 0,
    string TipoLavoratore = "");
