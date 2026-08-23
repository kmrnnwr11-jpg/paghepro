using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;

namespace Paghe.Persistence;

/// <summary>
/// Calcola e archivia i cedolini. Carica il rapporto dal DB, ricostruisce il dominio,
/// costruisce il motore con i parametri normativi dell'anno e persiste il risultato.
/// Un nuovo calcolo per lo stesso periodo sostituisce quello precedente (ricalcolo).
/// </summary>
public sealed class CedoliniService
{
    private readonly PagheDbContext _db;
    private readonly ParametriNormativiService _parametri;
    private readonly TimeProvider _clock;

    public CedoliniService(PagheDbContext db, ParametriNormativiService parametri, TimeProvider? clock = null)
    {
        _db = db;
        _parametri = parametri;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Risultato del solo calcolo, senza alcuna scrittura: per anteprime e proposte.</summary>
    public sealed record AnteprimaCedolino(Cedolino Cedolino, Cartellino Cartellino);

    /// <summary>
    /// Esegue l'intero calcolo del mese (motore, progressivi ferie/permessi, conguaglio di
    /// dicembre) senza persistere nulla. È lo stesso percorso dell'archiviazione: un'anteprima
    /// confermata produce per costruzione gli stessi importi.
    /// </summary>
    public async Task<AnteprimaCedolino> CalcolaAnteprimaAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct = default)
    {
        var calcolo = await CalcolaInternoAsync(tenantId, rapportoId, cartellino, ct, forzaRicalcolo: false);
        return new(calcolo.Cedolino, calcolo.Cartellino);
    }

    private sealed record CalcoloInterno(
        RapportoLavoroEntity Rapporto,
        AziendaEntity? Azienda,
        ParametriNormativi Parametri,
        Ccnl Ccnl,
        Cartellino Cartellino,
        Cedolino Cedolino,
        decimal ConguaglioDicembre);

    /// <param name="forzaRicalcolo">true = l'utente ha confermato il ricalcolo di un mese con
    /// F24 già inviato (correzione di un errore); le altre regole restano attive.</param>
    public async Task<CedolinoEntity> CalcolaEArchiviaAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct = default,
        bool forzaRicalcolo = false)
    {
        var calcolo = await CalcolaInternoAsync(tenantId, rapportoId, cartellino, ct, forzaRicalcolo);
        return await ArchiviaAsync(tenantId, rapportoId, calcolo, ct);
    }

    private async Task<CalcoloInterno> CalcolaInternoAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct,
        bool forzaRicalcolo)
    {
        RegoleCartellino.Valida(cartellino);
        RegoleCartellino.BloccaPeriodoFuturo(cartellino, _clock);

        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Rapporto di lavoro non trovato per il tenant indicato.");

        // Quando arriva il calendario giorno per giorno è lui la verità: i totali del mese li
        // ricalcola il server dalle giornate, non li prende dal client. Così cedolino, Libro
        // Unico e denunce non possono raccontare mesi diversi, e i controlli sul calendario
        // (giorni scoperti, causali senza ore, malattia con straordinari) fermano qui l'elaborazione.
        if (cartellino.Giornate.Count > 0)
            cartellino = CalendarioCartellino.Compila(
                cartellino, cartellino.Giornate, rapporto.OreSettimanali / 5m);

        // Il calendario può derivare totali (incluse le giornate di malattia) diversi dai
        // campi mensili ricevuti: le invarianti che dipendono da quei totali vanno rieseguite.
        RegoleCartellino.Valida(cartellino);

        RegoleCartellino.VerificaPeriodoRapporto(rapporto, cartellino);

        // Regola: un mese il cui F24 risulta già inviato o quietanzato non si ricalcola —
        // cambierebbe ritenute e contributi già versati. Se il commercialista deve correggere
        // un errore può forzare il ricalcolo (conferma esplicita); l'F24 andrà rigenerato.
        // Le pratiche in bozza/generata/pronta non bloccano; una Scartata nemmeno.
        if (!forzaRicalcolo)
        {
            bool f24Versato = await _db.PraticheF24.AnyAsync(p =>
                p.TenantId == tenantId && p.AziendaId == rapporto.AziendaId
                && p.Anno == cartellino.Anno && p.Mese == cartellino.Mese
                && (p.Stato == nameof(StatoPraticaTelematica.InviataManualmente)
                    || p.Stato == nameof(StatoPraticaTelematica.RicevutaAcquisita)), ct);
            if (f24Versato)
                throw new ArgumentException(
                    $"Il periodo {cartellino.Mese:00}/{cartellino.Anno} ha un F24 già inviato o "
                    + "quietanzato: ricalcolare la busta paga cambierebbe importi già versati. "
                    + "Se devi correggere un errore, conferma il ricalcolo forzato: "
                    + "l'F24 del mese andrà poi rigenerato.");
        }

        var aziendaDelRapporto = await _db.Aziende.FirstOrDefaultAsync(
            a => a.Id == rapporto.AziendaId && a.TenantId == tenantId, ct);
        var dominio = ToDominio(rapporto, aziendaDelRapporto);
        var parametri = await _parametri.CaricaParametriAsync(cartellino.Anno, ct);
        var ccnl = await _parametri.CaricaCcnlAsync(rapporto.CodiceCcnl, cartellino.Anno, ct);
        var cedolino = new MotoreCalcoloCedolino(parametri, ccnl).Calcola(dominio, cartellino);

        // Il motore calcola la maturazione teorica da gennaio. Per ottenere il saldo realmente
        // leggibile dal dipendente aggiungiamo i riporti iniziali e tutti gli utilizzi dei soli
        // mesi precedenti chiusi o storici pre-workflow; le versioni in lavorazione non alterano
        // progressivi che finiranno sul prospetto paga.
        var mesiPrecedentiUtilizzabili = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
                && c.Anno == cartellino.Anno && c.Mese < cartellino.Mese)
            .Select(c => c.Mese)
            .ToListAsync(ct);
        var cartelliniPrecedenti = await _db.Cartellini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
                && c.Anno == cartellino.Anno && mesiPrecedentiUtilizzabili.Contains(c.Mese))
            .ToListAsync(ct);
        cedolino.FerieGodute = cartelliniPrecedenti.Sum(c => c.GiorniFerie) + cartellino.GiorniFerie;
        cedolino.FerieResidue = rapporto.FerieResidueIniziali
            + cedolino.FerieMaturate - cedolino.FerieGodute;
        cedolino.PermessiGoduti = cartelliniPrecedenti.Sum(c => c.OrePermessi + c.OreExFestivita)
            + cartellino.OrePermessi + cartellino.OreExFestivita;
        cedolino.PermessiResidui = rapporto.PermessiResiduiIniziali
            + cedolino.PermessiMaturati - cedolino.PermessiGoduti;

        // Dicembre: conguaglio fiscale di fine anno applicato in busta (IRPEF annua effettiva vs
        // presuntiva trattenuta nei mesi 1-11 archiviati + dicembre corrente). Riarchiviare dicembre
        // è idempotente; se si rifà un mese precedente DOPO dicembre, va riarchiviato anche dicembre.
        decimal conguaglioDicembre = 0m;
        if (cartellino.Mese == 12)
        {
            var precedenti = await _db.Cedolini.UtilizzabiliAValle()
                .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
                         && c.Anno == cartellino.Anno && c.Mese < 12)
                .ToListAsync(ct);
            decimal imponibileAnnuo = precedenti.Sum(c => c.ImponibileIrpef) + cedolino.ImponibileIrpef;
            decimal irpefTrattenuta = precedenti.Sum(c => c.IrpefNetta) + cedolino.IrpefNetta;
            decimal detrazioni = rapporto.DetrazioniLavoroDipendente
                ? CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(imponibileAnnuo, rapporto.GiorniDetrazioneAnno)
                : 0m;
            var esito = ConguaglioFiscale.Calcola(
                imponibileAnnuo, irpefTrattenuta, detrazioni, parametri.ScaglioniIrpef);
            ConguaglioFiscale.ApplicaACedolino(cedolino, esito.Conguaglio);
            conguaglioDicembre = esito.Conguaglio;
        }

        return new(rapporto, aziendaDelRapporto, parametri, ccnl, cartellino, cedolino, conguaglioDicembre);
    }

    private async Task<CedolinoEntity> ArchiviaAsync(
        Guid tenantId, Guid rapportoId, CalcoloInterno calcolo, CancellationToken ct)
    {
        var (rapporto, aziendaDelRapporto, parametri, ccnl, cartellino, cedolino, conguaglioDicembre) = calcolo;

        // Ricalcolo: rimuove cartellino e cedolino esistenti per lo stesso periodo.
        await RimuoviEsistenti(tenantId, rapportoId, cartellino.Anno, cartellino.Mese, ct);

        _db.Cartellini.Add(new CartellinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RapportoLavoroId = rapportoId,
            Anno = cartellino.Anno, Mese = cartellino.Mese,
            OreStraordinario = cartellino.OreStraordinario,
            OreStraordinarioNotturno = cartellino.OreStraordinarioNotturno,
            OreStraordinarioFestivo = cartellino.OreStraordinarioFestivo,
            GiorniFerie = cartellino.GiorniFerie, OrePermessi = cartellino.OrePermessi,
            OreExFestivita = cartellino.OreExFestivita,
            GiorniMalattia = cartellino.GiorniMalattia,
            NumeroEventoMalattiaAnno = cartellino.NumeroEventoMalattiaAnno,
            GiorniInfortunio = cartellino.GiorniInfortunio,
            OreAssenzaNonRetribuita = cartellino.OreAssenzaNonRetribuita,
            StatoAssegniFamiliari = cartellino.StatoAssegniFamiliari.ToString(),
            ImportoAssegniFamiliari = cartellino.ImportoAssegniFamiliari,
            GiorniTrasfertaItalia = cartellino.GiorniTrasfertaItalia,
            GiorniTrasfertaEstero = cartellino.GiorniTrasfertaEstero,
            IndennitaTrasfertaGiornaliera = cartellino.IndennitaTrasfertaGiornaliera,
            RimborsoTrasferta = cartellino.RimborsoTrasferta.ToString(),
            RimborsiSpeseDocumentate = cartellino.RimborsiSpeseDocumentate,
            FringeBenefit = cartellino.FringeBenefit,
            FringeBenefitGiaErogatoAnno = cartellino.FringeBenefitGiaErogatoAnno,
            FigliACarico = cartellino.FigliACarico,
            GiorniBuonoPasto = cartellino.GiorniBuonoPasto,
            ValoreBuonoPasto = cartellino.ValoreBuonoPasto,
            BuonoPastoElettronico = cartellino.BuonoPastoElettronico,
            PremioRisultato = cartellino.PremioRisultato,
            RedditoAnnoPrecedente = cartellino.RedditoAnnoPrecedente,
            Giornate = cartellino.Giornate,
        });

        var cedolinoId = Guid.NewGuid();
        var parametriJson = CedolinoSnapshotCodec.SerializzaParametri(
            parametri, ccnl, rapporto, aziendaDelRapporto);
        var entity = new CedolinoEntity
        {
            Id = cedolinoId, TenantId = tenantId, AziendaId = rapporto.AziendaId,
            RapportoLavoroId = rapportoId, Anno = cedolino.Anno, Mese = cedolino.Mese,
            TotaleCompetenze = cedolino.TotaleCompetenze,
            TotaleTrattenute = cedolino.TotaleTrattenute,
            ImponibileInps = cedolino.ImponibileInps,
            ContributiDipendente = cedolino.ContributiDipendente,
            ContributiDatore = cedolino.ContributiDatore,
            ImponibileIrpef = cedolino.ImponibileIrpef,
            IrpefLorda = cedolino.IrpefLorda,
            DetrazioniLavoroDipendente = cedolino.DetrazioniLavoroDipendente,
            IrpefNetta = cedolino.IrpefNetta,
            AddizionaleRegionale = cedolino.AddizionaleRegionale,
            AddizionaleComunale = cedolino.AddizionaleComunale,
            NettoInBusta = cedolino.NettoInBusta,
            TotaleEsenti = cedolino.TotaleEsenti,
            PremioRisultatoAgevolato = cedolino.PremioRisultatoAgevolato,
            ImpostaSostitutivaPremio = cedolino.ImpostaSostitutivaPremio,
            QuotaTfr = cedolino.QuotaTfr,
            CostoAzienda = cedolino.CostoAzienda,
            FerieMaturate = cedolino.FerieMaturate,
            FerieGodute = cedolino.FerieGodute,
            FerieResidue = cedolino.FerieResidue,
            PermessiMaturati = cedolino.PermessiMaturati,
            PermessiGoduti = cedolino.PermessiGoduti,
            PermessiResidui = cedolino.PermessiResidui,
            ConguaglioIrpef = conguaglioDicembre,
            CodiceCnel = ccnl.CodiceCnel,
            DescrizioneCcnl = ccnl.Descrizione,
            FonteCcnl = ccnl.Fonte,
            Stato = nameof(StatoCedolino.Bozza),
            Versione = 1,
            ParametriApplicatiProtetti = _db.Protettore.ProteggiConContesto(
                parametriJson,
                ContestoDatiProtetti.ParametriCedolino(tenantId, cedolinoId))!,
            HashParametriSha256 = CedolinoSnapshotCodec.HashTesto(parametriJson),
            CalcolatoIl = _clock.GetUtcNow().UtcDateTime,
            Voci = cedolino.Righe.Select((r, i) => new CedolinoVoceEntity
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Ordine = i,
                Codice = r.Codice, Descrizione = r.Descrizione,
                Tipo = r.Tipo.ToString(), Quantita = r.Quantita, Base = r.Base, Importo = r.Importo,
            }).ToList(),
        };

        _db.Cedolini.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Costruisce l'F24 da versare per un'azienda raccogliendo automaticamente i cedolini
    /// archiviati del periodo (ritenute IRPEF, addizionali regionale/comunale, contributi INPS).
    /// Il territorio delle addizionali è preso dal rapporto di lavoro di ciascun cedolino.
    /// </summary>
    public async Task<ModelloF24> GeneraF24DelMeseAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese,
        string matricolaInps, string codiceSedeInps = "", CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        AziendeService.VerificaProntaPerAdempimentiContributivi(azienda);

        var cedolini = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException(
                $"Nessun cedolino chiuso o storico pre-workflow per il periodo {mese:00}/{anno}.");

        var rapportoIds = cedolini.Select(c => c.RapportoLavoroId).Distinct().ToList();
        var rapporti = await _db.Rapporti
            .Where(r => r.TenantId == tenantId && rapportoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var righe = cedolini.Select(c =>
        {
            rapporti.TryGetValue(c.RapportoLavoroId, out var rap);
            return new RigaPagaPerF24(
                c.IrpefNetta, c.AddizionaleRegionale, c.AddizionaleComunale,
                c.ContributiDipendente, c.ContributiDatore,
                rap?.Regione ?? "", rap?.CodiceCatastaleComune ?? "");
        }).ToList();

        var sostituto = new DatiSostitutoImposta(
            azienda.CodiceFiscale ?? azienda.PartitaIva ?? "",
            azienda.RagioneSociale, azienda.MatricolaInps!, azienda.CodiceSedeInps!);

        return F24DaCedolini.Costruisci(sostituto, righe, anno, mese);
    }

    /// <summary>
    /// Conguaglio fiscale IRPEF di fine anno per un rapporto: aggrega i cedolini archiviati
    /// dell'anno (imponibile e IRPEF già trattenuta), calcola la detrazione da lavoro dipendente
    /// spettante e ricalcola l'IRPEF netta dovuta sull'anno, restituendo la differenza a debito o a
    /// credito. Gli scaglioni sono quelli versionati dell'anno. Lancia se non ci sono cedolini.
    /// </summary>
    public async Task<RisultatoConguaglio> CalcolaConguaglioAnnualeAsync(
        Guid tenantId, Guid rapportoId, int anno, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Rapporto di lavoro non trovato per il tenant indicato.");

        var cedolini = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
                && c.Anno == anno)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException(
                $"Nessun cedolino chiuso o storico pre-workflow per l'anno {anno}.");

        decimal imponibileAnnuo = cedolini.Sum(c => c.ImponibileIrpef);
        decimal irpefTrattenuta = cedolini.Sum(c => c.IrpefNetta);
        decimal detrazioni = rapporto.DetrazioniLavoroDipendente
            ? CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(imponibileAnnuo, rapporto.GiorniDetrazioneAnno)
            : 0m;

        var parametri = await _parametri.CaricaParametriAsync(anno, ct);
        return ConguaglioFiscale.Calcola(imponibileAnnuo, irpefTrattenuta, detrazioni, parametri.ScaglioniIrpef);
    }

    /// <summary>
    /// Rivalutazione annua del fondo TFR di un rapporto: fondo al 31/12 dell'anno precedente
    /// (saldo iniziale del rapporto + quote dei cedolini archiviati degli anni prima) rivalutato
    /// con il coefficiente 1,5% + 75% della variazione ISTAT. La variazione arriva dal parametro
    /// esplicito o, in mancanza, dai parametri dell'anno su DB — mai cablata nel codice.
    /// </summary>
    public async Task<RivalutazioneTfrAnnuale> CalcolaRivalutazioneTfrAsync(
        Guid tenantId, Guid rapportoId, int anno, decimal? variazioneIstat = null,
        CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Rapporto di lavoro non trovato per il tenant indicato.");

        decimal variazione = variazioneIstat
            ?? (await _db.ParametriAnno.FirstOrDefaultAsync(p => p.Anno == anno, ct))?.VariazioneIstatTfr
            ?? throw new ArgumentException(
                $"Variazione ISTAT non disponibile per il {anno}: passarla nella richiesta "
                + "o inserirla nei parametri dell'anno.");

        var cedolini = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
                && c.Anno <= anno)
            .ToListAsync(ct);
        decimal fondo = (rapporto.FondoTfrIniziale ?? 0m)
            + cedolini.Where(c => c.Anno < anno).Sum(c => c.QuotaTfr);
        decimal quoteAnno = cedolini.Where(c => c.Anno == anno).Sum(c => c.QuotaTfr);

        var r = CalcoloTfr.Rivalutazione(fondo, variazione);
        return new RivalutazioneTfrAnnuale(
            anno, fondo, variazione, r.Coefficiente, r.RivalutazioneLorda, r.ImpostaSostitutiva,
            r.RivalutazioneNetta, r.FondoRivalutato, quoteAnno,
            Arrotondamento.Euro(r.FondoRivalutato + quoteAnno));
    }

    public Task<CedolinoEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Cedolini.Include(c => c.Voci)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);

    public Task<List<CedolinoEntity>> ElencoPerRapportoAsync(
        Guid tenantId, Guid rapportoId, CancellationToken ct = default) =>
        _db.Cedolini.Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId)
            .OrderBy(c => c.Anno).ThenBy(c => c.Mese).ToListAsync(ct);

    /// <summary>
    /// Rilegge dall'archivio un cedolino già elaborato e lo prepara per il PDF. <b>Non ricalcola
    /// nulla</b>: importi e voci sono quelli archiviati il giorno dell'elaborazione, nell'ordine
    /// in cui sono stati calcolati. È la fotografia della busta: se domani cambiano un minimo
    /// tabellare, un'aliquota o le regole del CCNL, la busta già consegnata resta quella.
    /// Per Bozza usa l'anagrafica corrente; dal controllo in avanti il workflow congela anche
    /// l'intestazione e il download usa lo snapshot, senza tornare a queste anagrafiche.
    /// Null se il cedolino o il rapporto non esistono per il tenant.
    /// </summary>
    public async Task<CedolinoPerPdf?> LeggiPerPdfAsync(
        Guid tenantId, Guid cedolinoId, CancellationToken ct = default)
    {
        var archiviato = await _db.Cedolini.Include(c => c.Voci)
            .FirstOrDefaultAsync(c => c.Id == cedolinoId && c.TenantId == tenantId, ct);
        if (archiviato is null) return null;

        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == archiviato.RapportoLavoroId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var cartellino = await _db.Cartellini.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.RapportoLavoroId == archiviato.RapportoLavoroId
                 && c.Anno == archiviato.Anno && c.Mese == archiviato.Mese, ct);

        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == archiviato.AziendaId && a.TenantId == tenantId, ct);
        var cedolino = DaArchivio(archiviato);

        var dipendente = await _db.Dipendenti
            .FirstOrDefaultAsync(d => d.Id == rapporto.DipendenteId && d.TenantId == tenantId, ct);
        var dipendenteNome = dipendente is null ? "Dipendente" : $"{dipendente.Cognome} {dipendente.Nome}".Trim();

        string? comuneRiga = azienda is null
            ? null
            : string.Join(" ", new[] { azienda.Cap, azienda.Comune }.Where(s => !string.IsNullOrWhiteSpace(s)))
              + (string.IsNullOrWhiteSpace(azienda.Provincia) ? "" : $" ({azienda.Provincia})");

        var intestazione = new IntestazioneCedolino
        {
            Azienda = string.IsNullOrWhiteSpace(azienda?.RagioneSociale) ? "Azienda" : azienda!.RagioneSociale,
            Logo = azienda?.Logo,
            AziendaIndirizzo = azienda?.Indirizzo,
            AziendaComune = string.IsNullOrWhiteSpace(comuneRiga) ? null : comuneRiga.Trim(),
            PartitaIva = azienda?.PartitaIva,
            CodiceFiscaleAzienda = azienda?.CodiceFiscale,
            MatricolaInps = azienda?.MatricolaInps,
            PatInail = azienda?.PatInail,
            Dipendente = dipendenteNome,
            CodiceFiscaleDipendente = dipendente?.CodiceFiscale,
            Matricola = rapporto.Matricola,
            IbanDipendente = dipendente?.Iban,
            DataAssunzione = rapporto.DataAssunzione == default
                ? null
                : rapporto.DataAssunzione.ToString("dd/MM/yyyy"),
            DataCessazione = rapporto.DataCessazione?.ToString("dd/MM/yyyy"),
            DataNascita = Paghe.Core.AdeTelematico.AnagraficaCodiceFiscale
                .Decodifica(dipendente?.CodiceFiscale, archiviato.Anno)?.DataNascita.ToString("dd/MM/yyyy"),
            ComuneResidenza = Paghe.Core.Parametri.ComuniCatastali
                .Trova(rapporto.CodiceCatastaleComune) is { } residenza
                ? $"{residenza.Nome} ({residenza.Provincia})"
                : null,
            Qualifica = rapporto.Qualifica,
            Livello = rapporto.Livello,
            Ccnl = string.IsNullOrWhiteSpace(archiviato.DescrizioneCcnl)
                ? rapporto.CodiceCcnl
                : archiviato.DescrizioneCcnl,
            CodiceCnel = archiviato.CodiceCnel,
            TipoContratto = rapporto.TipoContratto,
            Mensilita = rapporto.NumeroMensilita,
            Orario = rapporto.PartTime
                ? $"Part-time {rapporto.PercentualePartTime:0.#}%"
                : $"{rapporto.OreSettimanali:0.#} ore/sett.",
        };

        return new CedolinoPerPdf(
            cedolino,
            intestazione,
            cartellino is null
                ? new Cartellino { Anno = archiviato.Anno, Mese = archiviato.Mese }
                : ToDominio(cartellino));
    }

    /// <summary>Alias storico: il metodo non ricalcola più alcun importo.</summary>
    [Obsolete("Usare LeggiPerPdfAsync: il PDF viene ricostruito dai valori archiviati.")]
    public Task<CedolinoPerPdf?> RicalcolaPerPdfAsync(
        Guid tenantId, Guid cedolinoId, CancellationToken ct = default) =>
        LeggiPerPdfAsync(tenantId, cedolinoId, ct);

    /// <summary>
    /// Ricostruisce il cedolino di dominio dai dati archiviati: totali e voci sono quelli del
    /// giorno dell'elaborazione, nell'ordine in cui il motore li aveva prodotti.
    /// </summary>
    private static Cedolino DaArchivio(CedolinoEntity e)
    {
        var cedolino = new Cedolino
        {
            Anno = e.Anno,
            Mese = e.Mese,
            TotaleCompetenze = e.TotaleCompetenze,
            TotaleTrattenute = e.TotaleTrattenute,
            ImponibileInps = e.ImponibileInps,
            ContributiDipendente = e.ContributiDipendente,
            ContributiDatore = e.ContributiDatore,
            ImponibileIrpef = e.ImponibileIrpef,
            IrpefLorda = e.IrpefLorda,
            DetrazioniLavoroDipendente = e.DetrazioniLavoroDipendente,
            IrpefNetta = e.IrpefNetta,
            AddizionaleRegionale = e.AddizionaleRegionale,
            AddizionaleComunale = e.AddizionaleComunale,
            NettoInBusta = e.NettoInBusta,
            TotaleEsenti = e.TotaleEsenti,
            PremioRisultatoAgevolato = e.PremioRisultatoAgevolato,
            ImpostaSostitutivaPremio = e.ImpostaSostitutivaPremio,
            QuotaTfr = e.QuotaTfr,
            CostoAzienda = e.CostoAzienda,
            FerieMaturate = e.FerieMaturate,
            FerieGodute = e.FerieGodute,
            FerieResidue = e.FerieResidue,
            PermessiMaturati = e.PermessiMaturati,
            PermessiGoduti = e.PermessiGoduti,
            PermessiResidui = e.PermessiResidui,
        };

        foreach (var v in e.Voci.OrderBy(v => v.Ordine))
            cedolino.Righe.Add(new CedolinoRiga
            {
                Codice = v.Codice,
                Descrizione = v.Descrizione,
                Tipo = Enum.TryParse<TipoVoce>(v.Tipo, out var tipo) ? tipo : TipoVoce.Competenza,
                Quantita = v.Quantita,
                Base = v.Base,
                Importo = v.Importo,
            });

        return cedolino;
    }

    private async Task RimuoviEsistenti(Guid tenantId, Guid rapportoId, int anno, int mese, CancellationToken ct)
    {
        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        var nonBozza = cedolini.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.Stato)
            && !string.Equals(c.Stato, nameof(StatoCedolino.Bozza), StringComparison.OrdinalIgnoreCase));
        if (nonBozza is not null)
            throw new ArgumentException(
                $"Il cedolino {mese:00}/{anno} è '{nonBozza.Stato}' e non può essere sostituito. " +
                "Per correggerlo serve una rettifica separata.");
        _db.Cedolini.RemoveRange(cedolini);

        var cartellini = await _db.Cartellini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        _db.Cartellini.RemoveRange(cartellini);

        // Nessun SaveChanges intermedio: cancellazione della vecchia bozza e inserimento della
        // nuova vengono confermati insieme dal chiamante. Se il nuovo salvataggio fallisce, il
        // database conserva la versione precedente invece di lasciare il mese vuoto.
    }

    /// <param name="azienda">
    /// Serve per i due dati contributivi che stanno sull'azienda e non sul rapporto: la soglia
    /// dei 15 dipendenti (0,30% CIG) e i fondi di settore. Senza, la quota INPS a carico del
    /// dipendente uscirebbe sottostimata.
    /// </param>
    private static RapportoLavoro ToDominio(RapportoLavoroEntity e, AziendaEntity? azienda) => new()
    {
        CodiceCcnl = e.CodiceCcnl,
        Livello = e.Livello,
        Qualifica = Enum.Parse<Qualifica>(e.Qualifica),
        NumeroMensilita = e.NumeroMensilita,
        PartTime = e.PartTime,
        PercentualePartTime = e.PercentualePartTime,
        ScattiAnzianita = e.ScattiAnzianita,
        Superminimo = e.Superminimo,
        Regione = e.Regione,
        CodiceCatastaleComune = e.CodiceCatastaleComune,
        DetrazioniLavoroDipendente = e.DetrazioniLavoroDipendente,
        GiorniDetrazioneAnno = e.GiorniDetrazioneAnno,
        AziendaOltre15Dipendenti = azienda?.OltreQuindiciDipendenti ?? false,
        ContributiFondiSettore = azienda?.ContributiFondiSettore ?? 0m,
        FerieResidueIniziali = e.FerieResidueIniziali,
        PermessiResiduiIniziali = e.PermessiResiduiIniziali,
    };

    private static Cartellino ToDominio(CartellinoEntity e) => new()
    {
        Anno = e.Anno,
        Mese = e.Mese,
        OreStraordinario = e.OreStraordinario,
        OreStraordinarioNotturno = e.OreStraordinarioNotturno,
        OreStraordinarioFestivo = e.OreStraordinarioFestivo,
        GiorniFerie = e.GiorniFerie,
        OrePermessi = e.OrePermessi,
        OreExFestivita = e.OreExFestivita,
        GiorniMalattia = e.GiorniMalattia,
        NumeroEventoMalattiaAnno = e.NumeroEventoMalattiaAnno > 0 ? e.NumeroEventoMalattiaAnno : 1,
        GiorniInfortunio = e.GiorniInfortunio,
        OreAssenzaNonRetribuita = e.OreAssenzaNonRetribuita,
        StatoAssegniFamiliari = Enum.TryParse<StatoAssegniFamiliari>(
            e.StatoAssegniFamiliari, out var statoAnf)
            ? statoAnf
            : StatoAssegniFamiliari.DaVerificare,
        ImportoAssegniFamiliari = e.ImportoAssegniFamiliari,
        GiorniTrasfertaItalia = e.GiorniTrasfertaItalia,
        GiorniTrasfertaEstero = e.GiorniTrasfertaEstero,
        IndennitaTrasfertaGiornaliera = e.IndennitaTrasfertaGiornaliera,
        // I cartellini archiviati prima di questi campi hanno la colonna vuota: vale il forfettario.
        RimborsoTrasferta = Enum.TryParse<TipoRimborsoTrasferta>(e.RimborsoTrasferta, out var tipo)
            ? tipo
            : TipoRimborsoTrasferta.Forfettario,
        RimborsiSpeseDocumentate = e.RimborsiSpeseDocumentate,
        FringeBenefit = e.FringeBenefit,
        FringeBenefitGiaErogatoAnno = e.FringeBenefitGiaErogatoAnno,
        FigliACarico = e.FigliACarico,
        GiorniBuonoPasto = e.GiorniBuonoPasto,
        ValoreBuonoPasto = e.ValoreBuonoPasto,
        BuonoPastoElettronico = e.BuonoPastoElettronico,
        PremioRisultato = e.PremioRisultato,
        RedditoAnnoPrecedente = e.RedditoAnnoPrecedente,
        Giornate = e.Giornate,
    };
}

/// <summary>Cedolino ricalcolato e pronto per il PDF, con l'anagrafica già risolta.</summary>
public sealed record CedolinoPerPdf(
    Cedolino Cedolino,
    IntestazioneCedolino Intestazione,
    Cartellino Cartellino);

/// <summary>Esito della rivalutazione annua del fondo TFR di un rapporto.</summary>
public sealed record RivalutazioneTfrAnnuale(
    int Anno,
    decimal FondoAl31DicembrePrecedente,
    decimal VariazioneIstat,
    decimal Coefficiente,
    decimal RivalutazioneLorda,
    decimal ImpostaSostitutiva,
    decimal RivalutazioneNetta,
    decimal FondoRivalutato,
    decimal QuoteMaturateAnno,
    decimal FondoFinale);
