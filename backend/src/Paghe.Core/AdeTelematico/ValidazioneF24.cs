namespace Paghe.Core.AdeTelematico;

/// <summary>Singolo errore rilevato dai controlli interni.</summary>
public sealed record ErroreValidazione(string Campo, string Messaggio);

/// <summary>Esito dei controlli interni: valido se non ci sono errori.</summary>
public sealed class EsitoValidazione
{
    public required IReadOnlyList<ErroreValidazione> Errori { get; init; }
    public bool Valido => Errori.Count == 0;
}

/// <summary>
/// Controlli interni del modello F24 da eseguire <b>prima</b> dei JAR ufficiali
/// (cfr. TODO §7): campi obbligatori, codice fiscale/partita IVA, importi e quadratura.
/// Non sostituisce il controllo ufficiale dell'Agenzia: lo anticipa per bloccare subito
/// gli errori più comuni.
/// </summary>
public static class ValidazioneF24
{
    public static EsitoValidazione Verifica(ModelloF24 f24)
    {
        ArgumentNullException.ThrowIfNull(f24);

        var errori = new List<ErroreValidazione>();
        int annoMax = f24.DataVersamento == default ? DateTime.UtcNow.Year + 1 : f24.DataVersamento.Year + 1;

        VerificaContribuente(f24.Contribuente, errori);

        if (f24.Intermediario is { } interm && !ValidatoreFiscale.CodiceFiscaleValido(interm.CodiceFiscale))
            errori.Add(new("Intermediario.CodiceFiscale", "Codice fiscale intermediario non valido."));

        if (f24.DataVersamento == default)
            errori.Add(new("DataVersamento", "La data di versamento è obbligatoria."));

        int numeroVoci = 0;
        foreach (var v in f24.Erario)
        {
            numeroVoci++;
            VerificaTesto("Erario.CodiceTributo", v.CodiceTributo, errori);
            VerificaAnno("Erario.AnnoRiferimento", v.AnnoRiferimento, annoMax, errori);
            VerificaImporti("Erario", v.ImportoDebito, v.ImportoCredito, errori);
        }
        foreach (var v in f24.Inps)
        {
            numeroVoci++;
            VerificaTesto("Inps.CausaleContributo", v.CausaleContributo, errori);
            VerificaTesto("Inps.MatricolaCodice", v.MatricolaCodice, errori);
            VerificaTesto("Inps.PeriodoDa", v.PeriodoDa, errori);
            VerificaImporti("Inps", v.ImportoDebito, v.ImportoCredito, errori);
        }
        foreach (var v in f24.Regioni)
        {
            numeroVoci++;
            VerificaTesto("Regioni.CodiceRegione", v.CodiceRegione, errori);
            VerificaTesto("Regioni.CodiceTributo", v.CodiceTributo, errori);
            VerificaAnno("Regioni.AnnoRiferimento", v.AnnoRiferimento, annoMax, errori);
            VerificaImporti("Regioni", v.ImportoDebito, v.ImportoCredito, errori);
        }
        foreach (var v in f24.TributiLocali)
        {
            numeroVoci++;
            VerificaTesto("TributiLocali.CodiceEnteComune", v.CodiceEnteComune, errori);
            VerificaTesto("TributiLocali.CodiceTributo", v.CodiceTributo, errori);
            VerificaAnno("TributiLocali.AnnoRiferimento", v.AnnoRiferimento, annoMax, errori);
            VerificaImporti("TributiLocali", v.ImportoDebito, v.ImportoCredito, errori);
        }
        foreach (var v in f24.AltriEnti)
        {
            numeroVoci++;
            VerificaTesto("AltriEnti.CodiceEnte", v.CodiceEnte, errori);
            VerificaTesto("AltriEnti.CausaleContributo", v.CausaleContributo, errori);
            VerificaImporti("AltriEnti", v.ImportoDebito, v.ImportoCredito, errori);
        }

        if (numeroVoci == 0)
            errori.Add(new("Voci", "Il modello F24 deve contenere almeno una riga di tributo."));

        if (f24.SaldoFinale < 0m)
            errori.Add(new("SaldoFinale",
                $"Il saldo finale non può essere negativo (crediti {f24.TotaleCrediti:0.00} > debiti {f24.TotaleDebiti:0.00})."));

        return new EsitoValidazione { Errori = errori };
    }

    private static void VerificaContribuente(ContribuenteF24 c, List<ErroreValidazione> errori)
    {
        if (string.IsNullOrWhiteSpace(c.CodiceFiscale))
        {
            errori.Add(new("Contribuente.CodiceFiscale", "Il codice fiscale del contribuente è obbligatorio."));
            return;
        }

        bool valido = c.Tipo == TipoSoggettoF24.PersonaFisica
            ? ValidatoreFiscale.CodiceFiscalePersonaFisicaValido(c.CodiceFiscale)
            : ValidatoreFiscale.PartitaIvaValida(c.CodiceFiscale) || ValidatoreFiscale.CodiceFiscaleValido(c.CodiceFiscale);

        if (!valido)
            errori.Add(new("Contribuente.CodiceFiscale",
                "Codice fiscale del contribuente non coerente con il tipo soggetto."));

        if (c.Tipo == TipoSoggettoF24.SoggettoDiverso && string.IsNullOrWhiteSpace(c.Denominazione))
            errori.Add(new("Contribuente.Denominazione",
                "La denominazione è obbligatoria per i soggetti diversi da persona fisica."));

        if (c.Tipo == TipoSoggettoF24.PersonaFisica &&
            (string.IsNullOrWhiteSpace(c.Cognome) || string.IsNullOrWhiteSpace(c.Nome)))
            errori.Add(new("Contribuente.Nominativo", "Cognome e nome sono obbligatori per le persone fisiche."));
    }

    private static void VerificaTesto(string campo, string? valore, List<ErroreValidazione> errori)
    {
        if (string.IsNullOrWhiteSpace(valore))
            errori.Add(new(campo, $"Il campo {campo} è obbligatorio."));
    }

    private static void VerificaAnno(string campo, int anno, int annoMax, List<ErroreValidazione> errori)
    {
        if (anno < 1990 || anno > annoMax)
            errori.Add(new(campo, $"Anno di riferimento {anno} fuori intervallo (1990-{annoMax})."));
    }

    private static void VerificaImporti(string sezione, decimal debito, decimal credito, List<ErroreValidazione> errori)
    {
        if (debito < 0m)
            errori.Add(new($"{sezione}.ImportoDebito", "L'importo a debito non può essere negativo."));
        if (credito < 0m)
            errori.Add(new($"{sezione}.ImportoCredito", "L'importo a credito non può essere negativo."));
        if (debito > 0m && credito > 0m)
            errori.Add(new($"{sezione}.Importi",
                "Una singola riga non può avere contemporaneamente importo a debito e a credito."));
        if (debito == 0m && credito == 0m)
            errori.Add(new($"{sezione}.Importi", "Ogni riga deve avere un importo a debito o a credito."));
        if (!MaxDueDecimali(debito) || !MaxDueDecimali(credito))
            errori.Add(new($"{sezione}.Importi", "Gli importi devono avere al massimo due decimali."));
    }

    private static bool MaxDueDecimali(decimal valore) => valore == Math.Round(valore, 2);
}
