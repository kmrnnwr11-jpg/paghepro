using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Paghe.Core.AdeTelematico.UniEmens;
using Paghe.Core.Domain;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Un dato che il tracciato INPS prevede ma che l'archivio non ha: si dichiara, non si inventa.
/// La UI lo mostra come fa già con i campi da completare dell'import UNILAV.
/// </summary>
public sealed record CampoUniEmensMancante(
    string Campo, string Lavoratore, string Perche, string Dove);

/// <summary>
/// Esito della generazione: il flusso, il suo XML, l'esito della validazione contro lo schema
/// ufficiale e l'elenco dei dati ancora da raccogliere.
/// </summary>
public sealed record EsitoUniEmens(
    FlussoUniEmens Flusso,
    string Xml,
    IReadOnlyList<string> ErroriSchema,
    IReadOnlyList<CampoUniEmensMancante> DaCompletare);

/// <summary>
/// Costruisce il flusso UniEmens (denuncia mensile INPS) per un'azienda raccogliendo i
/// cedolini archiviati del periodo, l'anagrafica dei lavoratori e il calendario presenze.
/// Il file prodotto viene validato contro lo schema ufficiale INPS; l'invio telematico resta
/// sui canali ufficiali (cassetto previdenziale o intermediario). Isolato per tenant.
/// </summary>
public sealed class UniEmensService
{
    /// <summary>
    /// Giorni retribuiti di un mese pieno per un lavoratore mensilizzato: la settimana
    /// contributiva INPS va da lunedì a sabato, la domenica non è una giornata retribuita.
    /// </summary>
    private static int GiorniLunediSabato(int anno, int mese) =>
        Enumerable.Range(1, DateTime.DaysInMonth(anno, mese))
            .Count(g => new DateOnly(anno, mese, g).DayOfWeek != DayOfWeek.Sunday);

    private readonly PagheDbContext _db;

    public UniEmensService(PagheDbContext db) => _db = db;

    public async Task<EsitoUniEmens> GeneraDelMeseAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese,
        string matricolaInps, CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        AziendeService.VerificaProntaPerAdempimentiContributivi(azienda);

        var cedolini = await _db.Cedolini.UtilizzabiliAValle()
            .Include(c => c.Voci)
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException(
                $"Nessun cedolino chiuso o storico pre-workflow per il periodo {mese:00}/{anno}.");

        var rapportoIds = cedolini.Select(c => c.RapportoLavoroId).Distinct().ToList();
        var rapporti = await _db.Rapporti
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && rapportoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);
        var cartellini = await _db.Cartellini
            .Where(c => c.TenantId == tenantId && rapportoIds.Contains(c.RapportoLavoroId)
                && c.Anno == anno && c.Mese == mese)
            .ToDictionaryAsync(c => c.RapportoLavoroId, ct);

        var daCompletare = new List<CampoUniEmensMancante>();
        var righe = cedolini
            .Select(c =>
            {
                rapporti.TryGetValue(c.RapportoLavoroId, out var rapporto);
                cartellini.TryGetValue(c.RapportoLavoroId, out var cartellino);
                return Riga(c, rapporto, cartellino, anno, mese, daCompletare);
            })
            .ToList();

        string codiceFiscaleAzienda = azienda.CodiceFiscale ?? azienda.PartitaIva ?? "";
        var mittente = new MittenteUniEmens(
            CfPersonaMittente: azienda.LegaleRappresentanteCodiceFiscale ?? "",
            RagioneSociale: azienda.RagioneSociale,
            CfMittente: codiceFiscaleAzienda,
            CfSoftwarehouse: codiceFiscaleAzienda,
            SedeInps: azienda.CodiceSedeInps);
        if (string.IsNullOrWhiteSpace(mittente.CfPersonaMittente))
            daCompletare.Add(new CampoUniEmensMancante(
                "Codice fiscale del legale rappresentante", "(azienda)",
                "Il tracciato INPS chiede la persona fisica che firma la denuncia.",
                "Aziende → Correggi azienda → Legale rappresentante"));

        var testata = new TestataAziendaUniEmens(
            anno, mese, codiceFiscaleAzienda, azienda.RagioneSociale, matricolaInps);

        var flusso = UniEmensDaCedolini.Costruisci(mittente, testata, righe);
        string xml = UniEmensXmlWriter.GeneraStringa(flusso);
        return new EsitoUniEmens(flusso, xml, UniEmensValidator.Valida(xml), daCompletare);
    }

    private static RigaUniEmens Riga(
        CedolinoEntity cedolino, RapportoLavoroEntity? rapporto, CartellinoEntity? cartellino,
        int anno, int mese, List<CampoUniEmensMancante> daCompletare)
    {
        var dipendente = rapporto?.Dipendente;
        string nominativo = $"{dipendente?.Cognome} {dipendente?.Nome}".Trim();
        if (nominativo.Length == 0) nominativo = "(lavoratore senza anagrafica)";

        var calendario = PreparaCalendarioUniEmens(
            cedolino, cartellino, anno, mese, nominativo);
        int giorniNonRetribuiti = calendario.GiornateReali.Count(
            g => g.Causale == CausaleGiornata.AssenzaNonRetribuita);

        string? qualifica1 = Qualifica1(rapporto?.Qualifica);
        if (qualifica1 is null)
            daCompletare.Add(new CampoUniEmensMancante(
                "Qualifica INPS (Qualifica1)", nominativo,
                $"La qualifica «{rapporto?.Qualifica}» non corrisponde a un codice INPS noto.",
                "Dipendenti → scheda del lavoratore → Rapporto"));

        // Tempo pieno = "F"; per il part-time serve sapere se è orizzontale, verticale o misto.
        string? qualifica2 = rapporto is null
            ? null
            : rapporto.PartTime ? rapporto.TipoPartTimeInps : "F";
        if (qualifica2 is null && rapporto is not null)
            daCompletare.Add(new CampoUniEmensMancante(
                "Tipo di part-time", nominativo,
                "L'INPS distingue part-time orizzontale, verticale e misto: la percentuale da sola non basta.",
                "Dipendenti → scheda del lavoratore → Rapporto"));

        string? qualifica3 = Qualifica3(rapporto?.TipoContratto);
        if (qualifica3 is null)
            daCompletare.Add(new CampoUniEmensMancante(
                "Tipo di rapporto (Qualifica3)", nominativo,
                "Serve sapere se il contratto è a tempo indeterminato o determinato.",
                "Dipendenti → scheda del lavoratore → Rapporto"));

        // La cittadinanza è un elemento obbligatorio del tracciato: senza, la denuncia viene
        // scartata. Usa la stessa codifica della Comunicazione Obbligatoria (UNILAV).
        if (string.IsNullOrWhiteSpace(rapporto?.CittadinanzaInps))
            daCompletare.Add(new CampoUniEmensMancante(
                "Cittadinanza", nominativo,
                "È obbligatoria nel tracciato INPS, nella codifica delle Comunicazioni Obbligatorie.",
                "Dipendenti → scheda del lavoratore → Rapporto"));

        string? codiceContratto = string.IsNullOrWhiteSpace(cedolino.CodiceCnel)
            ? null
            : cedolino.CodiceCnel.Trim();
        if (codiceContratto is null)
            daCompletare.Add(new CampoUniEmensMancante(
                "Codice contratto CNEL", nominativo,
                "Il controllo INPS DI00025 lo pretende su ogni denuncia individuale.",
                "Aziende → CCNL applicato, oppure scheda del lavoratore"));

        return new RigaUniEmens(
            CodiceFiscale: dipendente?.CodiceFiscale ?? "",
            Cognome: dipendente?.Cognome ?? "",
            Nome: dipendente?.Nome ?? "",
            Qualifica1: qualifica1 ?? "",
            ImponibileInps: cedolino.ImponibileInps,
            ContributiDipendente: cedolino.ContributiDipendente,
            ContributiDatore: cedolino.ContributiDatore,
            GiorniRetribuiti: Math.Max(0, GiorniLunediSabato(anno, mese) - giorniNonRetribuiti))
        {
            Qualifica2 = qualifica2,
            Qualifica3 = qualifica3,
            CodiceComune = CodiceComune(rapporto?.CodiceCatastaleComune),
            CodiceContratto = codiceContratto,
            Cittadinanza = rapporto?.CittadinanzaInps,
            TipoContribuzione = rapporto?.TipoContribuzioneInps,
            QualProf = rapporto?.QualificaProfessionaleIstat,
            // Obbligatoria dal 2008 (controllo DR00070): è la retribuzione contrattuale del
            // mese, cioè le sole voci fisse della busta, non il lordo con gli accessori.
            RetribTeorica = RetribuzioneTeorica(cedolino),
            // Il motore ordinario retribuisce a mese: "M" nella tabella INPS dei tipi di paga.
            TipoPaga = "M",
            OrarioContrattuale = rapporto is null ? null : Centesimi(rapporto.OreSettimanali),
            NumMensilita = rapporto is null ? null : rapporto.NumeroMensilita * 1000,
            PercPartTime = rapporto is { PartTime: true }
                ? Centesimi(rapporto.PercentualePartTime)
                : null,
            PercPartTimeMese = rapporto is { PartTime: true }
                ? Centesimi(rapporto.PercentualePartTime)
                : null,
            Calendario = calendario.Giorni,
            CopertureSettimanali = calendario.Settimane,
            DifferenzeAccredito = calendario.DifferenzeAccredito,
        };
    }

    private sealed record CalendarioPreparato(
        IReadOnlyList<GiornataCartellino> GiornateReali,
        IReadOnlyList<GiornoUniEmens> Giorni,
        IReadOnlyList<SettimanaUniEmens> Settimane,
        IReadOnlyList<DifferenzaAccreditoUniEmens> DifferenzeAccredito);

    private sealed record EventoSanitario(
        CausaleGiornata Causale,
        string Identificativo,
        DateOnly DataInizio,
        IReadOnlyList<GiornataCartellino> Giornate);

    private sealed record IdentificativoMalattia(string Tipo, string Valore);

    /// <summary>
    /// Il tracciato giornaliero non è ricostruibile dai totali mensili: perciò il servizio
    /// accetta soltanto un cartellino persistito, completo e riferito esattamente al periodo.
    /// </summary>
    private static CalendarioPreparato PreparaCalendarioUniEmens(
        CedolinoEntity cedolino,
        CartellinoEntity? cartellino,
        int anno,
        int mese,
        string nominativo)
    {
        if (cartellino is null)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: manca il cartellino reale del "
                + $"periodo {mese:00}/{anno}.");

        int numeroGiorni = DateTime.DaysInMonth(anno, mese);
        var giornate = cartellino.Giornate.OrderBy(g => g.Data).ToList();
        var dateAttese = Enumerable.Range(1, numeroGiorni)
            .Select(g => new DateOnly(anno, mese, g));
        if (giornate.Count != numeroGiorni
            || !giornate.Select(g => g.Data).SequenceEqual(dateAttese))
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: il cartellino deve contenere una sola "
                + $"riga reale per ciascuno dei {numeroGiorni} giorni di {mese:00}/{anno}.");

        int giorniMalattia = giornate.Count(g => g.Causale == CausaleGiornata.Malattia);
        int giorniInfortunio = giornate.Count(g => g.Causale == CausaleGiornata.Infortunio);
        if (cartellino.GiorniMalattia != giorniMalattia
            || cartellino.GiorniInfortunio != giorniInfortunio)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: i totali sanitari del cartellino non "
                + "coincidono con il dettaglio giornaliero reale di malattia/infortunio.");

        var evento = VerificaEventoSanitario(giornate, anno, mese, nominativo);
        if (evento?.Causale == CausaleGiornata.Malattia)
            return PreparaMalattia(cedolino, giornate, evento, anno, mese, nominativo);
        if (evento?.Causale == CausaleGiornata.Infortunio)
            return PreparaInfortunio(cedolino, giornate, evento, anno, mese, nominativo);

        return new CalendarioPreparato(
            giornate,
            giornate.Select(GiornoOrdinario).ToList(),
            [],
            []);
    }

    private static EventoSanitario? VerificaEventoSanitario(
        IReadOnlyList<GiornataCartellino> giornate,
        int anno,
        int mese,
        string nominativo)
    {
        var sanitarie = giornate
            .Where(g => g.Causale is CausaleGiornata.Malattia or CausaleGiornata.Infortunio)
            .OrderBy(g => g.Data)
            .ToList();
        if (giornate.Except(sanitarie).Any(g =>
                g.IdentificativoEvento is not null
                || g.DataInizioEvento is not null
                || g.RetribuzioneMediaGiornalieraPrecedente is not null
                || g.RateiGiornalieri is not null
                || g.CoefficienteLordizzazione is not null))
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: il cartellino contiene metadati "
                + "sanitari su una giornata che non è malattia o infortunio.");
        if (sanitarie.Count == 0) return null;

        foreach (var g in sanitarie)
        {
            var mancanti = new List<string>();
            if (string.IsNullOrWhiteSpace(g.IdentificativoEvento)) mancanti.Add("numero/protocollo");
            if (g.DataInizioEvento is null) mancanti.Add("data di inizio");
            if (g.RetribuzioneMediaGiornalieraPrecedente is null) mancanti.Add("RMG precedente");
            if (g.RateiGiornalieri is null) mancanti.Add("ratei");
            if (g.CoefficienteLordizzazione is null) mancanti.Add("lordizzazione");
            if (mancanti.Count > 0)
                throw new InvalidOperationException(
                    $"UniEmens bloccato per {nominativo}: il {g.Data:dd/MM/yyyy} mancano "
                    + $"{string.Join(", ", mancanti)} dell'evento sanitario.");
            if (g.DataInizioEvento > g.Data
                || g.RetribuzioneMediaGiornalieraPrecedente <= 0m
                || g.RateiGiornalieri < 0m
                || g.CoefficienteLordizzazione <= 0m)
                throw new InvalidOperationException(
                    $"UniEmens bloccato per {nominativo}: i dati di calcolo dell'evento "
                    + $"sanitario del {g.Data:dd/MM/yyyy} non sono validi.");
        }

        var prima = sanitarie[0];
        string identificativo = prima.IdentificativoEvento!.Trim();
        bool coerente = sanitarie.All(g =>
            g.Causale == prima.Causale
            && string.Equals(g.IdentificativoEvento!.Trim(), identificativo,
                StringComparison.Ordinal)
            && g.DataInizioEvento == prima.DataInizioEvento
            && g.RetribuzioneMediaGiornalieraPrecedente
                == prima.RetribuzioneMediaGiornalieraPrecedente
            && g.RateiGiornalieri == prima.RateiGiornalieri
            && g.CoefficienteLordizzazione == prima.CoefficienteLordizzazione);
        if (!coerente)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: il modello corrente può rappresentare "
                + "un solo evento sanitario coerente per mese.");

        DateOnly dataInizio = prima.DataInizioEvento!.Value;
        DateOnly primaAttesa = dataInizio < new DateOnly(anno, mese, 1)
            ? new DateOnly(anno, mese, 1)
            : dataInizio;
        var perData = sanitarie.ToDictionary(g => g.Data);
        for (var data = primaAttesa; data <= sanitarie[^1].Data; data = data.AddDays(1))
        {
            if (!perData.ContainsKey(data))
                throw new InvalidOperationException(
                    $"UniEmens bloccato per {nominativo}: l'evento '{identificativo}' non è "
                    + $"continuo; manca il dettaglio del {data:dd/MM/yyyy}.");
        }

        return new EventoSanitario(prima.Causale, identificativo, dataInizio, sanitarie);
    }

    private static CalendarioPreparato PreparaMalattia(
        CedolinoEntity cedolino,
        IReadOnlyList<GiornataCartellino> giornate,
        EventoSanitario evento,
        int anno,
        int mese,
        string nominativo)
    {
        var identificativo = LeggiIdentificativoMalattia(evento, nominativo);
        int durataNota = evento.Giornate[^1].Data.DayNumber - evento.DataInizio.DayNumber + 1;
        bool terminaNelMese = evento.Giornate[^1].Data.Day < DateTime.DaysInMonth(anno, mese);
        if (durataNota < 7 && !terminaNelMese)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: l'evento di malattia attraversa la "
                + "fine del mese ma non ha una data di fine; non si può stabilire se duri meno "
                + "di sette giorni senza inventare il dato.");

        bool integrazioneDatore = cedolino.Voci.Any(v =>
            v.Codice.StartsWith("MAL_INTEGR", StringComparison.Ordinal)
            && v.Importo > 0m);
        var giorniEvento = evento.Giornate.ToDictionary(g => g.Data);
        var giorni = giornate.Select(g =>
        {
            if (!giorniEvento.ContainsKey(g.Data)) return GiornoOrdinario(g);

            int progressivoEvento = g.Data.DayNumber - evento.DataInizio.DayNumber + 1;
            string copertura = durataNota < 7
                || progressivoEvento <= 3
                || g.Data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                    ? "0"
                    : integrazioneDatore ? "2" : "1";
            return new GiornoUniEmens(g.Data.Day, Lavorato: false)
            {
                TipoCopertura = copertura,
                Eventi =
                [
                    new EventoGiornalieroUniEmens("MAL")
                    {
                        TipoInformazioneAggiuntiva = identificativo.Tipo,
                        InformazioneAggiuntiva = identificativo.Valore,
                    },
                ],
            };
        }).ToList();

        var settimane = CopertureMalattia(giornate, giorni, anno, mese);
        var differenze = DifferenzeEvento(
            cedolino, "MAL", identificativo, settimane.Count > 0, nominativo);
        return new CalendarioPreparato(giornate, giorni, settimane, differenze);
    }

    private static CalendarioPreparato PreparaInfortunio(
        CedolinoEntity cedolino,
        IReadOnlyList<GiornataCartellino> giornate,
        EventoSanitario evento,
        int anno,
        int mese,
        string nominativo)
    {
        // Per il lavoro dipendente ordinario lo schema consente EventoGiorn, ma le istruzioni
        // applicabili non autorizzano a inventare un dettaglio giornaliero INF. Restano il
        // calendario reale, il codice settimanale INF e la differenza di accredito documentata.
        var giorniEvento = evento.Giornate.Select(g => g.Data).ToHashSet();
        var giorni = giornate.Select(g => giorniEvento.Contains(g.Data)
            ? new GiornoUniEmens(g.Data.Day, Lavorato: false)
            : GiornoOrdinario(g)).ToList();

        bool quotaDatore = cedolino.Voci.Any(v =>
            (v.Codice.StartsWith("INF_EVENTO", StringComparison.Ordinal)
                || v.Codice.StartsWith("INF_CARENZA", StringComparison.Ordinal)
                || v.Codice.StartsWith("INF_INTEGR", StringComparison.Ordinal))
            && v.Importo > 0m);
        var settimane = evento.Giornate
            .GroupBy(g => SettimaneInps.IdSettimana(g.Data))
            .Select(gruppo =>
            {
                bool altraGiornataRetribuita = giornate.Any(g =>
                    SettimaneInps.IdSettimana(g.Data) == gruppo.Key
                    && !giorniEvento.Contains(g.Data)
                    && ÈGiornataRetribuita(g));
                return new SettimanaUniEmens(
                    gruppo.Key, quotaDatore || altraGiornataRetribuita ? "2" : "1")
                {
                    CodiciEvento = ["INF"],
                };
            })
            .ToList();

        var differenze = DifferenzeEvento(
            cedolino, "INF", identificativo: null, settimane.Count > 0, nominativo);
        return new CalendarioPreparato(giornate, giorni, settimane, differenze);
    }

    private static IReadOnlyList<SettimanaUniEmens> CopertureMalattia(
        IReadOnlyList<GiornataCartellino> giornate,
        IReadOnlyList<GiornoUniEmens> giorni,
        int anno,
        int mese)
    {
        var perGiorno = giornate.ToDictionary(g => g.Data.Day);
        return giorni
            .Where(g => g.Eventi.Any(e => e.CodiceEvento == "MAL")
                && g.TipoCopertura is "1" or "2")
            .GroupBy(g => SettimaneInps.IdSettimana(new DateOnly(anno, mese, g.Giorno)))
            .Select(gruppo =>
            {
                bool parziale = gruppo.Any(g => g.TipoCopertura == "2")
                    || perGiorno.Values.Any(g =>
                        SettimaneInps.IdSettimana(g.Data) == gruppo.Key
                        && g.Causale != CausaleGiornata.Malattia
                        && ÈGiornataRetribuita(g));
                return new SettimanaUniEmens(gruppo.Key, parziale ? "2" : "1")
                {
                    CodiciEvento = ["MAL"],
                };
            })
            .ToList();
    }

    private static IReadOnlyList<DifferenzaAccreditoUniEmens> DifferenzeEvento(
        CedolinoEntity cedolino,
        string codice,
        IdentificativoMalattia? identificativo,
        bool richiesta,
        string nominativo)
    {
        if (!richiesta) return [];

        decimal storno = Math.Abs(cedolino.Voci
            .Where(v => v.Codice.StartsWith($"{codice}_STORNO", StringComparison.Ordinal))
            .Sum(v => v.Importo));
        decimal retribuzioneDatore = cedolino.Voci
            .Where(v => v.Importo > 0m && (codice == "MAL"
                ? v.Codice.StartsWith("MAL_CARENZA", StringComparison.Ordinal)
                    || v.Codice.StartsWith("MAL_INTEGR", StringComparison.Ordinal)
                : v.Codice.StartsWith("INF_EVENTO", StringComparison.Ordinal)
                    || v.Codice.StartsWith("INF_CARENZA", StringComparison.Ordinal)
                    || v.Codice.StartsWith("INF_INTEGR", StringComparison.Ordinal)))
            .Sum(v => v.Importo);

        // La differenza nasce dalle voci effettivamente archiviate: storno della paga piena
        // meno le quote retributive del datore. Le prestazioni INPS/INAIL non sono retribuzione
        // imponibile e quindi non riducono la retribuzione figurativa persa.
        decimal importo = Math.Round(
            storno - retribuzioneDatore, MidpointRounding.AwayFromZero);
        if (importo < 1m)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: le voci reali del cedolino non "
                + $"determinano una differenza di accredito {codice} positiva.");

        var differenza = new DifferenzaAccreditoUniEmens(codice, importo);
        if (identificativo is not null)
            differenza = differenza with
            {
                QuoteEvento =
                [
                    new QuotaDifferenzaAccreditoUniEmens(
                        identificativo.Valore, identificativo.Tipo, importo),
                ],
            };
        return [differenza];
    }

    private static IdentificativoMalattia LeggiIdentificativoMalattia(
        EventoSanitario evento, string nominativo)
    {
        int separatore = evento.Identificativo.IndexOf(':');
        if (separatore <= 0 || separatore == evento.Identificativo.Length - 1)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: l'identificativo di malattia deve "
                + "indicare esplicitamente CM:<PUC>, PR:<protocollo> oppure DT:<data>.");

        string prefisso = evento.Identificativo[..separatore].Trim().ToUpperInvariant();
        string valore = evento.Identificativo[(separatore + 1)..].Trim();
        string tipo = prefisso switch
        {
            "CM" or "PUC" => "CM",
            "PR" or "PROTOCOLLO" => "PR",
            "DT" => "DT",
            _ => "",
        };
        if (tipo.Length == 0 || valore.Length is 0 or > 50)
            throw new InvalidOperationException(
                $"UniEmens bloccato per {nominativo}: tipo o valore dell'identificativo di "
                + "malattia non valido per InfoAggEvento.");

        if (tipo == "DT")
        {
            string[] formati = ["yyyy-MM-dd", "dd/MM/yyyy"];
            if (!DateOnly.TryParseExact(
                    valore, formati, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dataCertificato)
                || dataCertificato != evento.DataInizio)
                throw new InvalidOperationException(
                    $"UniEmens bloccato per {nominativo}: DT deve contenere la data di "
                    + $"inizio evento {evento.DataInizio:yyyy-MM-dd}.");
            valore = evento.DataInizio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return new IdentificativoMalattia(tipo, valore);
    }

    private static GiornoUniEmens GiornoOrdinario(GiornataCartellino g) =>
        new(g.Data.Day, g.ÈLavorata);

    private static bool ÈGiornataRetribuita(GiornataCartellino g) =>
        g.Causale is not CausaleGiornata.AssenzaNonRetribuita
            and not CausaleGiornata.RiposoSettimanale
            and not CausaleGiornata.Malattia
            and not CausaleGiornata.Infortunio;

    /// <summary>
    /// Voci fisse che compongono la retribuzione contrattuale del mese: sono quelle scritte
    /// dal motore di calcolo, escluse straordinari, indennità e altri elementi variabili.
    /// </summary>
    private static readonly string[] VociRetribuzioneTeorica =
        ["PAGA_BASE", "CONTINGENZA", "TERZO_EL", "EDR", "SCATTI", "SUPERMIN"];

    private static decimal RetribuzioneTeorica(CedolinoEntity cedolino) =>
        cedolino.Voci
            .Where(v => VociRetribuzioneTeorica.Contains(v.Codice))
            .Sum(v => v.Importo);

    /// <summary>Percentuali e orari viaggiano in centesimi nel tracciato (40 ore → 4000).</summary>
    private static int Centesimi(decimal valore) =>
        (int)Math.Round(valore * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Tabella ufficiale Qualifica1 (allegato tecnico, appendice B).</summary>
    private static string? Qualifica1(string? qualifica) => (qualifica ?? "").ToLowerInvariant() switch
    {
        var q when q.StartsWith("operai") => "1",
        var q when q.StartsWith("impiegat") => "2",
        var q when q.StartsWith("dirigent") => "3",
        var q when q.StartsWith("apprendist") => "5",
        var q when q.StartsWith("quadro") || q.StartsWith("quadri") => "Q",
        _ => null,
    };

    /// <summary>Tabella ufficiale Qualifica3: indeterminato "I", determinato "D".</summary>
    private static string? Qualifica3(string? tipoContratto)
    {
        string t = (tipoContratto ?? "").ToLowerInvariant();
        if (t.Contains("indeterminato")) return "I";
        if (t.Contains("determinato")) return "D";
        return null;
    }

    /// <summary>Il tracciato vuole il codice catastale (una lettera e tre cifre), non il nome.</summary>
    private static string? CodiceComune(string? codiceCatastale)
    {
        string c = (codiceCatastale ?? "").Trim().ToUpperInvariant();
        return c.Length == 4 && char.IsAsciiLetterUpper(c[0]) && c[1..].All(char.IsAsciiDigit)
            ? c
            : null;
    }
}
