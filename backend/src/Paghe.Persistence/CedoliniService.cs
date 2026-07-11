using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

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

    public CedoliniService(PagheDbContext db, ParametriNormativiService parametri)
    {
        _db = db;
        _parametri = parametri;
    }

    public async Task<CedolinoEntity> CalcolaEArchiviaAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Rapporto di lavoro non trovato per il tenant indicato.");

        var dominio = ToDominio(rapporto);
        var parametri = await _parametri.CaricaParametriAsync(cartellino.Anno, ct);
        var ccnl = await _parametri.CaricaCcnlAsync(rapporto.CodiceCcnl, cartellino.Anno, ct);
        var cedolino = new MotoreCalcoloCedolino(parametri, ccnl).Calcola(dominio, cartellino);

        // Dicembre: conguaglio fiscale di fine anno applicato in busta (IRPEF annua effettiva vs
        // presuntiva trattenuta nei mesi 1-11 archiviati + dicembre corrente). Riarchiviare dicembre
        // è idempotente; se si rifà un mese precedente DOPO dicembre, va riarchiviato anche dicembre.
        decimal conguaglioDicembre = 0m;
        if (cartellino.Mese == 12)
        {
            var precedenti = await _db.Cedolini
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
        });

        var entity = new CedolinoEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AziendaId = rapporto.AziendaId,
            RapportoLavoroId = rapportoId, Anno = cedolino.Anno, Mese = cedolino.Mese,
            TotaleCompetenze = cedolino.TotaleCompetenze,
            TotaleTrattenute = cedolino.TotaleTrattenute,
            ImponibileInps = cedolino.ImponibileInps,
            ContributiDipendente = cedolino.ContributiDipendente,
            ContributiDatore = cedolino.ContributiDatore,
            ImponibileIrpef = cedolino.ImponibileIrpef,
            IrpefNetta = cedolino.IrpefNetta,
            AddizionaleRegionale = cedolino.AddizionaleRegionale,
            AddizionaleComunale = cedolino.AddizionaleComunale,
            NettoInBusta = cedolino.NettoInBusta,
            QuotaTfr = cedolino.QuotaTfr,
            CostoAzienda = cedolino.CostoAzienda,
            FerieMaturate = cedolino.FerieMaturate,
            FerieGodute = cedolino.FerieGodute,
            FerieResidue = cedolino.FerieResidue,
            PermessiMaturati = cedolino.PermessiMaturati,
            PermessiGoduti = cedolino.PermessiGoduti,
            PermessiResidui = cedolino.PermessiResidui,
            ConguaglioIrpef = conguaglioDicembre,
            CalcolatoIl = DateTime.UtcNow,
            Voci = cedolino.Righe.Select(r => new CedolinoVoceEntity
            {
                Id = Guid.NewGuid(), Codice = r.Codice, Descrizione = r.Descrizione,
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

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException($"Nessun cedolino archiviato per il periodo {mese:00}/{anno}.");

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
            azienda.RagioneSociale, matricolaInps, codiceSedeInps);

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

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException($"Nessun cedolino archiviato per l'anno {anno}.");

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

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno <= anno)
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
    /// Ricalcola (senza persistere) un cedolino già archiviato a partire dal rapporto e dal
    /// cartellino salvati, e ne restituisce il dominio con i nomi di azienda e dipendente,
    /// pronto per generare il PDF. Il ricalcolo garantisce l'ordine deterministico delle voci.
    /// Null se il cedolino, il rapporto o il cartellino non esistono per il tenant.
    /// </summary>
    public async Task<CedolinoPerPdf?> RicalcolaPerPdfAsync(
        Guid tenantId, Guid cedolinoId, CancellationToken ct = default)
    {
        var archiviato = await _db.Cedolini
            .FirstOrDefaultAsync(c => c.Id == cedolinoId && c.TenantId == tenantId, ct);
        if (archiviato is null) return null;

        var rapporto = await _db.Rapporti
            .FirstOrDefaultAsync(r => r.Id == archiviato.RapportoLavoroId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var cartellino = await _db.Cartellini.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.RapportoLavoroId == archiviato.RapportoLavoroId
                 && c.Anno == archiviato.Anno && c.Mese == archiviato.Mese, ct);
        if (cartellino is null) return null;

        var parametri = await _parametri.CaricaParametriAsync(archiviato.Anno, ct);
        var ccnl = await _parametri.CaricaCcnlAsync(rapporto.CodiceCcnl, archiviato.Anno, ct);
        var cedolino = new MotoreCalcoloCedolino(parametri, ccnl)
            .Calcola(ToDominio(rapporto), ToDominio(cartellino));

        // Riapplica il conguaglio archiviato (dicembre): il PDF deve coincidere con l'archivio.
        if (archiviato.ConguaglioIrpef != 0m)
            ConguaglioFiscale.ApplicaACedolino(cedolino, archiviato.ConguaglioIrpef);

        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == archiviato.AziendaId && a.TenantId == tenantId, ct);

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
            AziendaIndirizzo = azienda?.Indirizzo,
            AziendaComune = string.IsNullOrWhiteSpace(comuneRiga) ? null : comuneRiga.Trim(),
            PartitaIva = azienda?.PartitaIva,
            CodiceFiscaleAzienda = azienda?.CodiceFiscale,
            MatricolaInps = azienda?.MatricolaInps,
            PatInail = azienda?.PatInail,
            Dipendente = dipendenteNome,
            CodiceFiscaleDipendente = dipendente?.CodiceFiscale,
            Matricola = rapporto.Matricola,
            DataAssunzione = rapporto.DataAssunzione == default
                ? null
                : rapporto.DataAssunzione.ToString("dd/MM/yyyy"),
            Qualifica = rapporto.Qualifica,
            Livello = rapporto.Livello,
            Ccnl = ccnl.Descrizione,
            Mensilita = rapporto.NumeroMensilita,
            Orario = rapporto.PartTime
                ? $"Part-time {rapporto.PercentualePartTime:0.#}%"
                : $"{rapporto.OreSettimanali:0.#} ore/sett.",
        };

        return new CedolinoPerPdf(cedolino, intestazione);
    }

    private async Task RimuoviEsistenti(Guid tenantId, Guid rapportoId, int anno, int mese, CancellationToken ct)
    {
        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        _db.Cedolini.RemoveRange(cedolini);

        var cartellini = await _db.Cartellini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        _db.Cartellini.RemoveRange(cartellini);

        if (cedolini.Count > 0 || cartellini.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private static RapportoLavoro ToDominio(RapportoLavoroEntity e) => new()
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
    };
}

/// <summary>Cedolino ricalcolato e pronto per il PDF, con l'anagrafica già risolta.</summary>
public sealed record CedolinoPerPdf(Cedolino Cedolino, IntestazioneCedolino Intestazione);

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
