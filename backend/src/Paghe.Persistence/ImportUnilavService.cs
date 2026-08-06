using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Un dato che l'UNILAV non contiene (o che va controllato) prima di poter elaborare la busta paga.
/// Serve a guidare l'utente: <b>cosa</b> manca, <b>dove</b> si completa e <b>dove trovarlo</b>.
/// </summary>
/// <param name="Campo">Nome del dato in parole semplici.</param>
/// <param name="Dove">Sezione dove si completa: azienda, dipendente o rapporto.</param>
/// <param name="RiferimentoId">Id dell'azienda o del rapporto da aprire.</param>
/// <param name="Perche">A cosa serve il dato.</param>
/// <param name="DoveTrovarlo">Dove l'utente può recuperarlo.</param>
public sealed record CampoDaCompletare(
    string Campo,
    string Dove,
    string? RiferimentoId,
    string Perche,
    string DoveTrovarlo);

/// <summary>Esito dell'import UNILAV: azienda (datore) e rapporto creati, più le avvertenze.</summary>
public sealed record EsitoImportUnilav(
    AziendaEntity Azienda,
    bool AziendaCreata,
    RapportoLavoroEntity Rapporto,
    bool LavoratoreEsistente,
    IReadOnlyList<string> Avvertenze,
    IReadOnlyList<CampoDaCompletare> DaCompletare);

/// <summary>
/// Da una Comunicazione Obbligatoria UNILAV (testo estratto dal PDF o incollato) registra
/// <b>tutto il necessario</b> in modo isolato per tenant: trova o crea il <b>datore di lavoro</b>
/// (azienda), poi crea il <b>lavoratore</b> e il <b>rapporto</b>. Se il lavoratore (stesso codice
/// fiscale) esiste già, gli aggiunge un nuovo rapporto invece di duplicarlo (indice unico
/// TenantId+CodiceFiscale). Alla fine elenca i dati che l'UNILAV non contiene e che servono
/// comunque per la busta paga.
/// </summary>
public sealed class ImportUnilavService
{
    /// <summary>Ore settimanali di riferimento del tempo pieno, per dedurre la percentuale di part-time.</summary>
    private const decimal OreSettimanaliTempoPieno = 40m;

    private readonly PagheDbContext _db;

    public ImportUnilavService(PagheDbContext db) => _db = db;

    public async Task<EsitoImportUnilav> ImportaDaTestoAsync(
        Guid tenantId, string testoUnilav,
        string? ccnlDefault = null, string? livelloDefault = null,
        Guid? aziendaIdScelto = null, CancellationToken ct = default)
    {
        var imp = ImportazioneUnilavPdf.Importa(testoUnilav);
        var avvertenze = new List<string>(imp.Avvertenze);

        string? cfLav = imp.Lavoratore.CodiceFiscale?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cfLav))
            throw new ArgumentException(
                "Impossibile registrare il lavoratore: codice fiscale non riconosciuto nel PDF UNILAV.");

        // 1) Azienda: se l'utente ne ha scelta una, ci si attacca; altrimenti si trova/crea il datore del PDF.
        AziendaEntity azienda;
        bool aziendaCreata;
        if (aziendaIdScelto is { } scelta)
        {
            azienda = await _db.Aziende.FirstOrDefaultAsync(a => a.Id == scelta && a.TenantId == tenantId, ct)
                ?? throw new ArgumentException("Azienda scelta non trovata per il tenant indicato.");
            aziendaCreata = false;
        }
        else
        {
            (azienda, aziendaCreata) = await TrovaOCreaDatoreAsync(tenantId, imp.Datore, avvertenze, ct);
        }

        // L'azienda già in archivio si completa solo se è davvero lo stesso datore dell'UNILAV
        // (stesso codice fiscale o partita IVA): mai sovrascrivere i dati di un'altra azienda.
        if (!aziendaCreata && StessoDatore(azienda, imp.Datore))
            await CompletaCampiVuotiAsync(azienda, imp.Datore, avvertenze, ct);

        // 2) Rapporto di lavoro: mappa il CCNL del portale UNILAV a un CCNL interno, così il cedolino è calcolabile.
        var mappato = MappaturaCcnl.Riconosci(imp.Rapporto.Ccnl);
        string codiceCcnl, livello;
        if (mappato is not null)
        {
            codiceCcnl = mappato.CodiceInterno;
            livello = await LivelloValidoAsync(codiceCcnl, imp.Rapporto.Livello, mappato.LivelloDefault, avvertenze, ct);
            avvertenze.Add($"CCNL «{imp.Rapporto.Ccnl}» riconosciuto come {codiceCcnl}: cedolino calcolabile (verificare livello {livello}).");
        }
        else
        {
            codiceCcnl = Primo(imp.Rapporto.Ccnl, ccnlDefault) ?? "";
            livello = Primo(imp.Rapporto.Livello, livelloDefault) ?? "";
        }

        var rapporto = new RapportoLavoroEntity
        {
            CodiceCcnl = codiceCcnl,
            Livello = livello,
            Qualifica = ImportazioneUnilav.MappaQualifica(imp.Rapporto.Qualifica).ToString(),
            PartTime = ImportazioneUnilav.EParTime(imp.Rapporto.TipoOrario),
            DataAssunzione = imp.Rapporto.DataInizio ?? DateOnly.FromDateTime(DateTime.UtcNow),
        };
        rapporto.OreSettimanali = imp.Rapporto.OreSettimanali is > 0 and <= 48
            ? imp.Rapporto.OreSettimanali.Value
            : rapporto.OreSettimanali;
        rapporto.PercentualePartTime = PercentualePartTime(imp.Rapporto, rapporto.PartTime, avvertenze);

        if (TipoContratto(imp.Rapporto.TipoContratto) is { } tipo)
        {
            rapporto.TipoContratto = tipo;
            // Tempo determinato con termine indicato: il termine chiude il rapporto, così non si
            // elaborano per sbaglio buste paga dopo la scadenza del contratto.
            if (tipo == "Tempo determinato" && imp.Rapporto.DataFine is { } fine
                && fine >= rapporto.DataAssunzione)
            {
                rapporto.DataCessazione = fine;
                avvertenze.Add($"Contratto a termine: fine rapporto impostata al {fine:dd/MM/yyyy} come da UNILAV.");
            }
        }

        avvertenze.Add("Regione e comune (catastale) del rapporto non sono nell'UNILAV: indicarli per le addizionali.");

        // 3) Lavoratore: riusa se esiste già (indice unico TenantId+CodiceFiscale), altrimenti crealo.
        var esistente = await _db.Dipendenti
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.CodiceFiscale == cfLav, ct);

        RapportoLavoroEntity salvato;
        bool lavoratoreEsistente = esistente is not null;
        if (esistente is not null)
        {
            avvertenze.Add($"Lavoratore con codice fiscale {cfLav} già presente: aggiunto un nuovo rapporto.");
            rapporto.Id = Guid.NewGuid();
            rapporto.TenantId = tenantId;
            rapporto.AziendaId = azienda.Id;
            rapporto.DipendenteId = esistente.Id;
            _db.Rapporti.Add(rapporto);
            await _db.SaveChangesAsync(ct);
            rapporto.Dipendente = esistente;
            salvato = rapporto;
        }
        else
        {
            var dipendente = new DipendenteEntity
            {
                CodiceFiscale = cfLav,
                Nome = imp.Lavoratore.Nome ?? "",
                Cognome = imp.Lavoratore.Cognome ?? "",
            };
            salvato = await new AnagraficheLavoroService(_db)
                .CreaDipendenteERapportoAsync(tenantId, azienda.Id, dipendente, rapporto, ct);
            salvato.Dipendente = dipendente;
        }

        var daCompletare = DatiDaCompletare(azienda, salvato, imp);
        return new EsitoImportUnilav(azienda, aziendaCreata, salvato, lavoratoreEsistente, avvertenze, daCompletare);
    }

    private async Task<(AziendaEntity azienda, bool creata)> TrovaOCreaDatoreAsync(
        Guid tenantId, DatoreLavoroUnilav datore, List<string> avvertenze, CancellationToken ct)
    {
        string? cf = datore.CodiceFiscale?.Trim();

        if (!string.IsNullOrWhiteSpace(cf))
        {
            var trovata = await _db.Aziende.FirstOrDefaultAsync(
                a => a.TenantId == tenantId && (a.CodiceFiscale == cf || a.PartitaIva == cf), ct);
            if (trovata is not null)
                return (trovata, false);
        }

        if (string.IsNullOrWhiteSpace(datore.Denominazione) && string.IsNullOrWhiteSpace(cf))
            throw new ArgumentException(
                "Impossibile registrare il datore di lavoro: né denominazione né codice fiscale presenti nel PDF.");

        var nuova = new AziendaEntity
        {
            RagioneSociale = string.IsNullOrWhiteSpace(datore.Denominazione)
                ? $"Datore {cf}"
                : datore.Denominazione!,
        };
        if (!string.IsNullOrWhiteSpace(cf))
        {
            if (Regex.IsMatch(cf, "^[0-9]{11}$")) nuova.PartitaIva = cf;
            else nuova.CodiceFiscale = cf;
        }
        ApplicaDatiDatore(nuova, datore);

        avvertenze.Add("Datore di lavoro creato dai dati UNILAV: controllare l'anagrafica e completare il regime fiscale.");
        var creata = await new AziendeService(_db).CreaAsync(tenantId, nuova, ct);
        return (creata, true);
    }

    /// <summary>Riempie i campi ancora vuoti dell'azienda con quanto letto nell'UNILAV (non sovrascrive nulla).</summary>
    private async Task CompletaCampiVuotiAsync(
        AziendaEntity azienda, DatoreLavoroUnilav datore, List<string> avvertenze, CancellationToken ct)
    {
        if (!ApplicaDatiDatore(azienda, datore)) return;
        await _db.SaveChangesAsync(ct);
        avvertenze.Add("Anagrafica del datore completata con i dati mancanti letti dall'UNILAV.");
    }

    /// <summary>Scrive sull'azienda i dati del datore letti dall'UNILAV, solo dove il campo è vuoto.</summary>
    private static bool ApplicaDatiDatore(AziendaEntity a, DatoreLavoroUnilav d)
    {
        bool cambiato = false;
        cambiato |= Riempi(d.Indirizzo, a.Indirizzo, v => a.Indirizzo = v);
        cambiato |= Riempi(d.Cap, a.Cap, v => a.Cap = v);
        cambiato |= Riempi(d.Comune, a.Comune, v => a.Comune = v);
        cambiato |= Riempi(d.Provincia, a.Provincia, v => a.Provincia = v);
        cambiato |= Riempi(d.Telefono, a.Telefono, v => a.Telefono = v);
        cambiato |= Riempi(d.Email, a.Email, v => a.Email = v);
        cambiato |= Riempi(d.CodiceAteco, a.CodiceAteco, v => a.CodiceAteco = v);
        cambiato |= Riempi(d.Settore, a.Settore, v => a.Settore = v);
        cambiato |= Riempi(d.MatricolaInps, a.MatricolaInps, v => a.MatricolaInps = v);
        cambiato |= Riempi(d.PatInail, a.PatInail, v => a.PatInail = v);
        cambiato |= Riempi(d.RappresentanteNome, a.LegaleRappresentanteNome, v => a.LegaleRappresentanteNome = v);
        cambiato |= Riempi(d.RappresentanteCognome, a.LegaleRappresentanteCognome, v => a.LegaleRappresentanteCognome = v);
        cambiato |= Riempi(d.RappresentanteCodiceFiscale, a.LegaleRappresentanteCodiceFiscale,
            v => a.LegaleRappresentanteCodiceFiscale = v);
        if (!a.HaDipendenti) { a.HaDipendenti = true; cambiato = true; }
        return cambiato;
    }

    private static bool Riempi(string? letto, string? attuale, Action<string> imposta)
    {
        if (string.IsNullOrWhiteSpace(letto) || !string.IsNullOrWhiteSpace(attuale)) return false;
        imposta(letto.Trim());
        return true;
    }

    private static bool StessoDatore(AziendaEntity azienda, DatoreLavoroUnilav datore)
    {
        string cf = datore.CodiceFiscale?.Trim() ?? "";
        if (cf.Length == 0) return false;
        return string.Equals(azienda.CodiceFiscale?.Trim(), cf, StringComparison.OrdinalIgnoreCase)
            || string.Equals(azienda.PartitaIva?.Trim(), cf, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Percentuale di part-time: quella dichiarata se c'è, altrimenti dedotta dalle ore settimanali
    /// sul tempo pieno di 40 ore. Senza questo il cedolino di un part-time pagherebbe l'intero.
    /// </summary>
    private static decimal PercentualePartTime(RapportoUnilav r, bool partTime, List<string> avvertenze)
    {
        if (r.PercentualePartTime is > 0 and <= 100) return r.PercentualePartTime.Value;
        if (!partTime) return 100m;
        if (r.OreSettimanali is > 0 and < OreSettimanaliTempoPieno)
        {
            decimal perc = Math.Round(r.OreSettimanali.Value / OreSettimanaliTempoPieno * 100m, 2);
            avvertenze.Add(
                $"Part-time: percentuale calcolata al {perc}% da {r.OreSettimanali} ore settimanali su 40. "
                + "Verificare l'orario pieno previsto dal CCNL.");
            return perc;
        }
        avvertenze.Add("Part-time senza percentuale né ore settimanali nell'UNILAV: indicare la percentuale prima del cedolino.");
        return 100m;
    }

    /// <summary>Vero se il testo dice davvero la qualifica contrattuale (e non solo la professione ISTAT).</summary>
    private static bool QualificaEsplicita(string? qualificaUnilav)
    {
        string q = (qualificaUnilav ?? "").ToLowerInvariant();
        return q.Contains("operai") || q.Contains("impiegat") || q.Contains("quadr")
            || q.Contains("dirigent") || q.Contains("apprendist");
    }

    private static string? TipoContratto(string? unilav)
    {
        string t = (unilav ?? "").ToLowerInvariant();
        if (t.Length == 0) return null;
        if (t.Contains("indeterminato")) return "Tempo indeterminato";
        if (t.Contains("determinato")) return "Tempo determinato";
        return null;
    }

    /// <summary>
    /// Livello UNILAV riportato al codice del CCNL interno («000008 - VII» → «7»), accettato solo
    /// se quel livello esiste davvero nel contratto; altrimenti si usa il default e lo si segnala.
    /// </summary>
    private async Task<string> LivelloValidoAsync(
        string codiceCcnl, string? livelloUnilav, string livelloDefault, List<string> avvertenze, CancellationToken ct)
    {
        string? candidato = NormalizzaLivello(livelloUnilav);
        if (candidato is not null)
        {
            bool esiste = await _db.CcnlLivelli
                .AnyAsync(l => l.CcnlCodice == codiceCcnl && l.Livello == candidato, ct);
            if (esiste) return candidato;
            avvertenze.Add(
                $"Livello «{livelloUnilav}» non presente nel CCNL {codiceCcnl}: impostato il livello {livelloDefault}, da verificare.");
        }
        return livelloDefault;
    }

    /// <summary>Estrae il livello dalla dicitura UNILAV: numero, oppure numero romano («VII» → «7»).</summary>
    private static string? NormalizzaLivello(string? livello)
    {
        if (string.IsNullOrWhiteSpace(livello)) return null;
        string t = livello.Trim();
        // «000008 - VII»: la parte dopo il trattino è la descrizione del livello.
        int sep = t.LastIndexOf('-');
        if (sep >= 0 && sep < t.Length - 1) t = t[(sep + 1)..].Trim();

        if (Regex.IsMatch(t, @"^\d+$")) return t.TrimStart('0') is { Length: > 0 } n ? n : null;
        string romano = t.ToUpperInvariant();
        return romano switch
        {
            "I" => "1", "II" => "2", "III" => "3", "IV" => "4", "V" => "5",
            "VI" => "6", "VII" => "7", "VIII" => "8", "IX" => "9", "X" => "10",
            _ => null,
        };
    }

    /// <summary>
    /// Cosa serve ancora dopo l'import: l'UNILAV non contiene tutto quello che serve a una busta paga.
    /// Ogni voce dice dove si completa e dove il dato si può recuperare.
    /// </summary>
    private static List<CampoDaCompletare> DatiDaCompletare(
        AziendaEntity a, RapportoLavoroEntity r, RisultatoImportUnilav imp)
    {
        string azienda = a.Id.ToString();
        string rapporto = r.Id.ToString();
        var elenco = new List<CampoDaCompletare>();

        void Manca(bool condizione, string campo, string dove, string? rif, string perche, string doveTrovarlo)
        {
            if (condizione) elenco.Add(new CampoDaCompletare(campo, dove, rif, perche, doveTrovarlo));
        }

        // --- Azienda ---
        Manca(string.IsNullOrWhiteSpace(a.Indirizzo) || string.IsNullOrWhiteSpace(a.Comune)
                || string.IsNullOrWhiteSpace(a.Cap),
            "Sede: indirizzo, CAP e comune", "azienda", azienda,
            "Serve sulla busta paga, sulle fatture e sui modelli ufficiali.",
            "Visura camerale o intestazione dell'UNILAV (sede legale).");
        Manca(string.IsNullOrWhiteSpace(a.Provincia), "Provincia della sede", "azienda", azienda,
            "Completa l'indirizzo su documenti e modelli.",
            "Visura camerale; si ricava dal comune della sede.");
        Manca(string.IsNullOrWhiteSpace(a.PartitaIva), "Partita IVA", "azienda", azienda,
            "Necessaria per fatture, F24 e adempimenti fiscali.",
            "Visura camerale o certificato di attribuzione dell'Agenzia delle Entrate.");
        Manca(string.IsNullOrWhiteSpace(a.MatricolaInps), "Matricola INPS", "azienda", azienda,
            "Serve per UniEmens e per la sezione INPS dell'F24.",
            "Lettera di attribuzione INPS o cassetto previdenziale dell'azienda.");
        Manca(string.IsNullOrWhiteSpace(a.PatInail), "P.A.T. INAIL", "azienda", azienda,
            "Serve per il premio INAIL e l'autoliquidazione.",
            "Certificato di assicurazione INAIL o portale INAIL dell'azienda.");
        Manca(string.IsNullOrWhiteSpace(a.CodiceCatastale), "Codice catastale del comune (azienda)", "azienda", azienda,
            "Serve per le addizionali comunali IRPEF.",
            "Tabella dei codici catastali: si ricava dal comune della sede.");
        Manca(string.IsNullOrWhiteSpace(a.LegaleRappresentanteNome)
                && string.IsNullOrWhiteSpace(a.LegaleRappresentanteCognome),
            "Legale rappresentante", "azienda", azienda,
            "Firma i modelli ufficiali e le comunicazioni.",
            "Visura camerale, sezione titolari/amministratori.");

        // --- Rapporto ---
        Manca(string.IsNullOrWhiteSpace(r.CodiceCcnl), "CCNL applicato", "rapporto", rapporto,
            "Senza CCNL il cedolino non è calcolabile (minimi tabellari).",
            "Contratto di assunzione o UNILAV, campo CCNL.");
        Manca(string.IsNullOrWhiteSpace(r.Livello), "Livello di inquadramento", "rapporto", rapporto,
            "Determina il minimo tabellare della busta paga.",
            "Contratto di assunzione o UNILAV, campo livello.");
        Manca(string.IsNullOrWhiteSpace(r.Regione), "Regione di residenza del lavoratore", "rapporto", rapporto,
            "Serve per l'addizionale regionale IRPEF.",
            "Residenza del lavoratore (carta d'identità o autocertificazione).");
        Manca(string.IsNullOrWhiteSpace(r.CodiceCatastaleComune), "Comune di residenza (codice catastale)", "rapporto", rapporto,
            "Serve per l'addizionale comunale IRPEF.",
            "Residenza del lavoratore; il codice si ricava dal comune.");
        // L'UNILAV indica la professione ISTAT («8.1.3.3.0.2 addetto al volantinaggio»), non la
        // qualifica contrattuale: non si deduce da sola perché cambia le aliquote INPS.
        Manca(!QualificaEsplicita(imp.Rapporto.Qualifica),
            $"Qualifica contrattuale (ora: {r.Qualifica})", "rapporto", rapporto,
            "Operaio, impiegato, quadro o dirigente: cambia le aliquote INPS e il calcolo.",
            "Contratto di assunzione o lettera di assunzione (inquadramento).");
        Manca(imp.Rapporto.PercentualePartTime is null && r.PartTime,
            "Percentuale di part-time", "rapporto", rapporto,
            "Riduce la retribuzione in proporzione: se sbagliata, la busta paga è sbagliata.",
            "Contratto di assunzione (ore settimanali rispetto al tempo pieno del CCNL).");

        // --- Dipendente ---
        var d = r.Dipendente;
        Manca(d is not null && (string.IsNullOrWhiteSpace(d.Nome) || string.IsNullOrWhiteSpace(d.Cognome)),
            "Nome e cognome del lavoratore", "dipendente", rapporto,
            "Compaiono su busta paga, CU e comunicazioni.",
            "UNILAV o documento d'identità del lavoratore.");
        Manca(d is not null && d.CodiceFiscale.Length != 16,
            "Codice fiscale del lavoratore", "dipendente", rapporto,
            "Il codice fiscale ha 16 caratteri: quello letto non è valido.",
            "Tessera sanitaria o codice fiscale del lavoratore.");
        Manca(imp.Lavoratore.NomeCognomeDaCampoUnico,
            "Controlla nome e cognome", "dipendente", rapporto,
            "Nel testo importato erano su un campo unico: l'ordine va confermato.",
            "UNILAV originale o documento d'identità del lavoratore.");

        return elenco;
    }

    private static string? Primo(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : (!string.IsNullOrWhiteSpace(b) ? b : null);
}
