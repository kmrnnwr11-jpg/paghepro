namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Dati per la compilazione della Certificazione Unica (modello sintetico AdE) di un
/// lavoratore dipendente: anagrafica sostituto/percipiente, parte B dati fiscali e
/// sezione previdenziale INPS. Prima versione limitata al lavoro dipendente standard
/// (niente assistenza fiscale 730, TFR a tassazione separata, lavoro autonomo).
/// </summary>
public sealed record CertificazioneUnica
{
    /// <summary>Anno d'imposta certificato (l'anno dei cedolini).</summary>
    public required int AnnoRedditi { get; init; }

    public required SostitutoCu Sostituto { get; init; }
    public required PercipienteCu Percipiente { get; init; }
    public required DatiFiscaliCu DatiFiscali { get; init; }
    public required PrevidenzaCu Previdenza { get; init; }

    /// <summary>Righe libere stampate nella pagina ANNOTAZIONI del modello.</summary>
    public IReadOnlyList<string> Annotazioni { get; init; } = [];

    /// <summary>Controlli interni minimi prima della stampa. Vuoto = compilabile.</summary>
    public List<string> Valida()
    {
        var errori = new List<string>();
        if (AnnoRedditi is < 2000 or > 2100)
            errori.Add("Anno dei redditi non valido.");
        if (string.IsNullOrWhiteSpace(Sostituto.CodiceFiscale))
            errori.Add("Codice fiscale del sostituto (azienda) mancante.");
        if (string.IsNullOrWhiteSpace(Sostituto.Denominazione))
            errori.Add("Denominazione del sostituto (azienda) mancante.");
        if (string.IsNullOrWhiteSpace(Percipiente.CodiceFiscale))
            errori.Add("Codice fiscale del dipendente mancante.");
        if (string.IsNullOrWhiteSpace(Percipiente.Cognome))
            errori.Add("Cognome del dipendente mancante.");
        if (DatiFiscali.RedditoTempoIndeterminato < 0m || DatiFiscali.RedditoTempoDeterminato < 0m)
            errori.Add("Reddito negativo: verificare i cedolini dell'anno.");
        if (DatiFiscali.GiorniDetrazione is < 0 or > 366)
            errori.Add("Numero di giorni per detrazioni fuori intervallo (0-366).");
        return errori;
    }
}

/// <summary>Datore di lavoro / sostituto d'imposta (riquadro DATI ANAGRAFICI).</summary>
public sealed record SostitutoCu
{
    public required string CodiceFiscale { get; init; }
    public required string Denominazione { get; init; }
    public string? Comune { get; init; }
    public string? Provincia { get; init; }
    public string? Cap { get; init; }
    public string? Indirizzo { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    /// <summary>Codice attività ATECO.</summary>
    public string? CodiceAttivita { get; init; }
}

/// <summary>Dipendente / percettore delle somme (riquadro DATI ANAGRAFICI).</summary>
public sealed record PercipienteCu
{
    public required string CodiceFiscale { get; init; }
    public required string Cognome { get; init; }
    public string? Nome { get; init; }
    /// <summary>"M" o "F" (ricavabile dal codice fiscale).</summary>
    public string? Sesso { get; init; }
    public DateOnly? DataNascita { get; init; }
    public string? ComuneNascita { get; init; }
    public string? ProvinciaNascita { get; init; }
    /// <summary>Domicilio fiscale all'1/1 dell'anno certificato (per le addizionali).</summary>
    public string? DomicilioComune { get; init; }
    public string? DomicilioProvincia { get; init; }
    public string? DomicilioCodiceComune { get; init; }
}

/// <summary>Parte B — dati fiscali (punti del modello tra parentesi).</summary>
public sealed record DatiFiscaliCu
{
    /// <summary>Punto 1: redditi con contratto a tempo indeterminato.</summary>
    public decimal RedditoTempoIndeterminato { get; init; }
    /// <summary>Punto 2: redditi con contratto a tempo determinato.</summary>
    public decimal RedditoTempoDeterminato { get; init; }
    /// <summary>Punto 6: numero giorni per i quali spettano le detrazioni.</summary>
    public int GiorniDetrazione { get; init; }
    /// <summary>Punto 8: data inizio del rapporto di lavoro.</summary>
    public DateOnly? DataInizioRapporto { get; init; }
    /// <summary>Punto 9: data di cessazione del rapporto (se cessato).</summary>
    public DateOnly? DataCessazione { get; init; }
    /// <summary>Punto 10: rapporto ancora in forza al 31/12 dell'anno certificato.</summary>
    public bool InForzaAl31Dicembre { get; init; }
    /// <summary>Punto 21: ritenute IRPEF operate.</summary>
    public decimal RitenuteIrpef { get; init; }
    /// <summary>Punto 22: addizionale regionale all'IRPEF trattenuta.</summary>
    public decimal AddizionaleRegionale { get; init; }
    /// <summary>Punto 27: saldo addizionale comunale all'IRPEF trattenuta.</summary>
    public decimal AddizionaleComunale { get; init; }
    /// <summary>Punto 361: imposta lorda annua.</summary>
    public decimal ImpostaLorda { get; init; }
    /// <summary>Punto 367: detrazioni per lavoro dipendente.</summary>
    public decimal DetrazioniLavoroDipendente { get; init; }
    /// <summary>Punto 374: totale detrazioni.</summary>
    public decimal TotaleDetrazioni { get; init; }
    /// <summary>Punto 375: imposta netta annua.</summary>
    public decimal ImpostaNetta { get; init; }
}

/// <summary>Sezione 1 dati previdenziali INPS lavoratori subordinati.</summary>
public sealed record PrevidenzaCu
{
    /// <summary>Punto 1: matricola azienda INPS.</summary>
    public string? MatricolaAzienda { get; init; }
    /// <summary>Punto 4: imponibile previdenziale annuo.</summary>
    public decimal ImponibilePrevidenziale { get; init; }
    /// <summary>Punto 6: contributi a carico del lavoratore trattenuti.</summary>
    public decimal ContributiLavoratore { get; init; }
    /// <summary>Mesi (1-12) con retribuzione: barrano le caselle della denuncia UniEmens.</summary>
    public IReadOnlyList<int> MesiRetribuiti { get; init; } = [];
}

/// <summary>
/// Ricava sesso, data di nascita e codice catastale del comune di nascita dal codice
/// fiscale di una persona fisica (gestendo le sostituzioni per omocodia). Restituisce
/// null sui codici non standard (16 caratteri attesi).
/// </summary>
public static class AnagraficaCodiceFiscale
{
    private const string LettereMese = "ABCDEHLMPRST"; // A=gennaio ... T=dicembre

    // Omocodia: le cifre possono essere sostituite da lettere secondo questa tabella.
    private static readonly Dictionary<char, int> CifraOmocodia = new()
    {
        ['L'] = 0, ['M'] = 1, ['N'] = 2, ['P'] = 3, ['Q'] = 4,
        ['R'] = 5, ['S'] = 6, ['T'] = 7, ['U'] = 8, ['V'] = 9,
    };

    public sealed record Anagrafica(string Sesso, DateOnly DataNascita, string CodiceCatastaleNascita);

    /// <summary>Decodifica il CF; annoRiferimento risolve il secolo delle due cifre dell'anno.</summary>
    public static Anagrafica? Decodifica(string? codiceFiscale, int annoRiferimento)
    {
        if (codiceFiscale is null) return null;
        string cf = codiceFiscale.Trim().ToUpperInvariant();
        if (cf.Length != 16) return null;

        int? anno2 = Cifra(cf[6]) is int d1 && Cifra(cf[7]) is int d2 ? d1 * 10 + d2 : null;
        int mese = LettereMese.IndexOf(cf[8]) + 1;
        int? giornoGrezzo = Cifra(cf[9]) is int g1 && Cifra(cf[10]) is int g2 ? g1 * 10 + g2 : null;
        if (anno2 is null || mese == 0 || giornoGrezzo is null) return null;

        string sesso = giornoGrezzo > 40 ? "F" : "M";
        int giorno = giornoGrezzo.Value > 40 ? giornoGrezzo.Value - 40 : giornoGrezzo.Value;

        // Secolo: la nascita non può essere successiva all'anno di riferimento.
        int anno = 2000 + anno2.Value;
        if (anno > annoRiferimento) anno -= 100;

        if (giorno < 1 || giorno > DateTime.DaysInMonth(anno, mese)) return null;

        // Comune di nascita: lettera + 3 cifre (con eventuale omocodia sulle cifre).
        char[] catastale = [cf[11], .. new char[3]];
        for (int i = 0; i < 3; i++)
        {
            int? cifra = Cifra(cf[12 + i]);
            if (cifra is null) return null;
            catastale[1 + i] = (char)('0' + cifra.Value);
        }

        return new Anagrafica(sesso, new DateOnly(anno, mese, giorno), new string(catastale));
    }

    private static int? Cifra(char c) =>
        char.IsAsciiDigit(c) ? c - '0'
        : CifraOmocodia.TryGetValue(c, out int v) ? v
        : null;
}
