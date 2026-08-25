using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Carica i parametri normativi e i CCNL dal database, versionati per anno, e li trasforma
/// negli oggetti di dominio usati dal motore di calcolo. Espone anche l'inizializzazione
/// del database a partire dai dati di esempio (<see cref="Seed"/>).
///
/// Così le aliquote, gli scaglioni e i minimi CCNL non sono più cablati nel codice ma
/// risiedono su tabelle aggiornabili per anno senza ricompilare.
/// </summary>
public sealed class ParametriNormativiService
{
    private readonly PagheDbContext _db;

    public ParametriNormativiService(PagheDbContext db) => _db = db;

    /// <summary>Popola il database con i parametri di esempio se non è già stato fatto.</summary>
    public async Task ImportaSeedSeVuotoAsync(CancellationToken ct = default)
    {
        // Le tabelle agricole hanno una guardia propria: sono arrivate dopo, e un database già
        // inizializzato non le riceverebbe mai se dipendessero dal controllo qui sotto.
        await ImportaSeedAgricoloSeVuotoAsync(ct);

        if (await _db.ParametriAnno.AnyAsync(ct))
            return;

        var p = Seed.Parametri2026();

        _db.ParametriAnno.Add(new ParametriAnnoEntity
        {
            Anno = p.Anno,
            DivisoreOrario = p.DivisoreOrario,
            MaggiorazioneStraordinario = p.MaggiorazioneStraordinario,
            MaggiorazioneStraordinarioNotturno = p.MaggiorazioneStraordinarioNotturno,
            MaggiorazioneStraordinarioFestivo = p.MaggiorazioneStraordinarioFestivo,
            TfrDivisore = p.TfrDivisore,
            TfrContributoFondo = p.TfrContributoFondo,
            TrasfertaItaliaEsenteGiorno = p.TrasfertaItaliaEsenteGiorno,
            TrasfertaEsteroEsenteGiorno = p.TrasfertaEsteroEsenteGiorno,
            FringeBenefitSogliaAnnua = p.FringeBenefitSogliaAnnua,
            FringeBenefitSogliaAnnuaConFigli = p.FringeBenefitSogliaAnnuaConFigli,
            BuonoPastoElettronicoEsenteGiorno = p.BuonoPastoElettronicoEsenteGiorno,
            BuonoPastoCartaceoEsenteGiorno = p.BuonoPastoCartaceoEsenteGiorno,
            PremioRisultatoLimiteAnnuo = p.PremioRisultatoLimiteAnnuo,
            PremioRisultatoAliquota = p.PremioRisultatoAliquota,
            PremioRisultatoRedditoMassimo = p.PremioRisultatoRedditoMassimo,
            CuneoUlterioreDetrazioneAnnua = p.CuneoUlterioreDetrazioneAnnua,
            CuneoRedditoOltre = p.CuneoRedditoOltre,
            CuneoRedditoFinoA = p.CuneoRedditoFinoA,
            CuneoRedditoAzzeramento = p.CuneoRedditoAzzeramento,
            SolidarietaFondiAliquota = p.SolidarietaFondiAliquota,
        });

        foreach (var (qualifica, aliquota) in p.AliquoteInps)
            _db.InpsAliquote.Add(new InpsAliquotaEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno, Qualifica = qualifica.ToString(),
                Dipendente = aliquota.Dipendente, Datore = aliquota.Datore,
            });

        foreach (var s in p.ScaglioniIrpef)
            _db.IrpefScaglioni.Add(new IrpefScaglioneEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno,
                DaImporto = s.DaImporto, AImporto = s.AImporto, Aliquota = s.Aliquota,
            });

        foreach (var (regione, aliquota) in p.AddizionaleRegionale)
            _db.AddizionaliRegionali.Add(new AddizionaleRegionaleEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno, Regione = regione, Aliquota = aliquota,
            });

        foreach (var (codice, aliquota) in p.AddizionaleComunale)
            _db.AddizionaliComunali.Add(new AddizionaleComunaleEntity
            {
                Id = Guid.NewGuid(), Anno = p.Anno, CodiceCatastale = codice, Aliquota = aliquota,
            });

        foreach (var ccnl in Seed.TuttiICcnl2026())
        {
            _db.Ccnl.Add(new CcnlEntity { Codice = ccnl.Codice, Descrizione = ccnl.Descrizione });
            _db.CcnlVersioni.Add(new CcnlVersioneEntity
            {
                Id = Guid.NewGuid(), CcnlCodice = ccnl.Codice, Anno = p.Anno,
                CodiceCnel = ccnl.CodiceCnel, Fonte = ccnl.Fonte,
            });
            foreach (var liv in ccnl.Livelli)
                _db.CcnlLivelli.Add(new CcnlLivelloEntity
                {
                    Id = Guid.NewGuid(), CcnlCodice = ccnl.Codice, Anno = p.Anno,
                    Livello = liv.Codice, Descrizione = liv.Descrizione,
                    MinimoTabellare = liv.MinimoTabellare,
                    Edr = liv.Edr,
                    ImportoScatto = liv.ImportoScatto,
                    Contingenza = liv.Contingenza,
                });

            foreach (var (provincia, importo) in ccnl.TerzoElementoProvinciale)
                _db.CcnlTerzoElemento.Add(new CcnlTerzoElementoEntity
                {
                    Id = Guid.NewGuid(),
                    CcnlCodice = ccnl.Codice,
                    Anno = p.Anno,
                    Provincia = provincia,
                    Importo = importo,
                });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Costruisce i parametri normativi di dominio per l'anno indicato.</summary>
    public async Task<ParametriNormativi> CaricaParametriAsync(int anno, CancellationToken ct = default)
    {
        var pa = await _db.ParametriAnno.FirstOrDefaultAsync(x => x.Anno == anno, ct)
            ?? throw new ArgumentException($"Parametri normativi non presenti per l'anno {anno}.");

        var aliquote = await _db.InpsAliquote.Where(x => x.Anno == anno).ToListAsync(ct);
        var scaglioni = await _db.IrpefScaglioni.Where(x => x.Anno == anno)
            .OrderBy(x => x.DaImporto).ToListAsync(ct);
        var regionali = await _db.AddizionaliRegionali.Where(x => x.Anno == anno).ToListAsync(ct);
        var comunali = await _db.AddizionaliComunali.Where(x => x.Anno == anno).ToListAsync(ct);

        // Regimi a scaglioni + esenzione dai file ufficiali MEF (se disponibili per l'anno):
        // prevalgono sull'aliquota piatta di DB, che resta come fallback.
        var regimiMef = CaricaRegimiMef.PerAnno(anno);

        var regoleCcnl = await _db.RegoleCcnl.Where(x => x.Anno == anno).ToListAsync(ct);

        // Valori di riferimento delle soglie agevolate, usati quando la riga dell'anno
        // non le contiene ancora.
        var predefiniti = new ParametriNormativi
        {
            Anno = anno,
            ScaglioniIrpef = [],
            AliquoteInps = new Dictionary<Qualifica, AliquotaInps>(),
            AddizionaleRegionale = new Dictionary<string, decimal>(),
            AddizionaleComunale = new Dictionary<string, decimal>(),
        };

        return new ParametriNormativi
        {
            Anno = anno,
            ScaglioniIrpef = scaglioni
                .Select(s => new ScaglioneIrpef(s.DaImporto, s.AImporto, s.Aliquota)).ToList(),
            AliquoteInps = aliquote.ToDictionary(
                a => Enum.Parse<Qualifica>(a.Qualifica),
                a => new AliquotaInps(a.Dipendente, a.Datore)),
            AddizionaleRegionale = regionali.ToDictionary(
                r => r.Regione, r => r.Aliquota, StringComparer.OrdinalIgnoreCase),
            AddizionaleComunale = comunali.ToDictionary(
                c => c.CodiceCatastale, c => c.Aliquota, StringComparer.OrdinalIgnoreCase),
            RegimiAddizionaleRegionale = regimiMef.Regionali,
            RegimiAddizionaleComunale = regimiMef.Comunali,
            DivisoreOrario = pa.DivisoreOrario,
            MaggiorazioneStraordinario = pa.MaggiorazioneStraordinario,
            MaggiorazioneStraordinarioNotturno = pa.MaggiorazioneStraordinarioNotturno,
            MaggiorazioneStraordinarioFestivo = pa.MaggiorazioneStraordinarioFestivo,

            // Regole per contratto: dove il dato del CCNL manca vale il default dell'anno,
            // così un contratto senza tabella non prende di nascosto quelle del Commercio.
            RegoleCcnl = regoleCcnl.ToDictionary(
                r => r.CcnlCodice,
                r => new RegoleCcnl(
                    r.DivisoreOrario,
                    r.DivisoreGiornaliero ?? predefiniti.DivisoreGiornaliero,
                    r.MaggiorazioneStraordinario,
                    r.MaggiorazioneStraordinarioNotturno,
                    r.MaggiorazioneStraordinarioFestivo,
                    r.GiorniFerieMese ?? predefiniti.GiorniFerieMese,
                    r.OrePermessiMese ?? predefiniti.OrePermessiMese,
                    r.OrePermessiMeseOltre15,
                    r.ScattiOgniMesi,
                    r.ScattiMassimo,
                    r.MeseErogazioneTredicesima,
                    r.MeseErogazioneQuattordicesima,
                    r.AssistenzaSanitariaLavoratoreMese,
                    r.AssistenzaSanitariaDatoreMese,
                    r.EnteBilateraleAliquotaLavoratore,
                    r.EnteBilateraleAliquotaDatore,
                    r.OreRolAnno,
                    r.OreRolAnnoOltre15,
                    r.OreExFestivitaAnno,
                    r.MesiAnzianitaSenzaRol,
                    r.MesiAnzianitaRolRidotto,
                    r.PercentualeRolRidotta),
                StringComparer.OrdinalIgnoreCase),
            TfrDivisore = pa.TfrDivisore,
            TfrContributoFondo = pa.TfrContributoFondo,
            TfrVariazioneIstat = pa.VariazioneIstatTfr,

            // Limiti degli elementi agevolati: se la riga dell'anno non li ha (database
            // creato prima di queste colonne) restano i default di ParametriNormativi.
            TrasfertaItaliaEsenteGiorno = pa.TrasfertaItaliaEsenteGiorno ?? predefiniti.TrasfertaItaliaEsenteGiorno,
            TrasfertaEsteroEsenteGiorno = pa.TrasfertaEsteroEsenteGiorno ?? predefiniti.TrasfertaEsteroEsenteGiorno,
            FringeBenefitSogliaAnnua = pa.FringeBenefitSogliaAnnua ?? predefiniti.FringeBenefitSogliaAnnua,
            FringeBenefitSogliaAnnuaConFigli = pa.FringeBenefitSogliaAnnuaConFigli ?? predefiniti.FringeBenefitSogliaAnnuaConFigli,
            BuonoPastoElettronicoEsenteGiorno = pa.BuonoPastoElettronicoEsenteGiorno ?? predefiniti.BuonoPastoElettronicoEsenteGiorno,
            BuonoPastoCartaceoEsenteGiorno = pa.BuonoPastoCartaceoEsenteGiorno ?? predefiniti.BuonoPastoCartaceoEsenteGiorno,
            PremioRisultatoLimiteAnnuo = pa.PremioRisultatoLimiteAnnuo ?? predefiniti.PremioRisultatoLimiteAnnuo,
            PremioRisultatoAliquota = pa.PremioRisultatoAliquota ?? predefiniti.PremioRisultatoAliquota,
            PremioRisultatoRedditoMassimo = pa.PremioRisultatoRedditoMassimo ?? predefiniti.PremioRisultatoRedditoMassimo,
            CuneoUlterioreDetrazioneAnnua = pa.CuneoUlterioreDetrazioneAnnua ?? predefiniti.CuneoUlterioreDetrazioneAnnua,
            CuneoRedditoOltre = pa.CuneoRedditoOltre ?? predefiniti.CuneoRedditoOltre,
            CuneoRedditoFinoA = pa.CuneoRedditoFinoA ?? predefiniti.CuneoRedditoFinoA,
            CuneoRedditoAzzeramento = pa.CuneoRedditoAzzeramento ?? predefiniti.CuneoRedditoAzzeramento,
            SolidarietaFondiAliquota = pa.SolidarietaFondiAliquota ?? predefiniti.SolidarietaFondiAliquota,
        };
    }

    /// <summary>Costruisce il CCNL di dominio per il codice e l'anno indicati.</summary>
    public async Task<Ccnl> CaricaCcnlAsync(string codice, int anno, CancellationToken ct = default)
    {
        var ccnl = await _db.Ccnl.FirstOrDefaultAsync(c => c.Codice == codice, ct)
            ?? throw new ArgumentException($"CCNL '{codice}' non presente.");

        var livelli = await _db.CcnlLivelli
            .Where(l => l.CcnlCodice == codice && l.Anno == anno).ToListAsync(ct);
        if (livelli.Count == 0)
            throw new ArgumentException($"Nessun livello per il CCNL '{codice}' nell'anno {anno}.");

        var versione = await _db.CcnlVersioni
            .FirstOrDefaultAsync(v => v.CcnlCodice == codice && v.Anno == anno, ct);

        var terzoElemento = await _db.CcnlTerzoElemento
            .Where(t => t.CcnlCodice == codice && t.Anno == anno).ToListAsync(ct);

        return new Ccnl(ccnl.Codice, ccnl.Descrizione,
            livelli.Select(l => new LivelloCcnl(
                l.Livello, l.Descrizione, l.MinimoTabellare, l.Edr, l.ImportoScatto,
                l.Contingenza ?? 0m)),
            versione?.CodiceCnel,
            versione?.Fonte,
            terzoElemento.ToDictionary(
                t => t.Provincia, t => t.Importo, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Carica le tabelle agricole di partenza (Cuneo 2025, lette da buste reali) se non c'è
    /// ancora nulla. Non sovrascrive: appena il consulente carica il CPL vero, questo tace.
    /// </summary>
    private async Task ImportaSeedAgricoloSeVuotoAsync(CancellationToken ct)
    {
        if (await _db.ParametriAgricoli.AnyAsync(ct))
            return;

        await ImportaParametriAgricoliAsync(SeedAgricoltura.Cuneo2025(), ct);
    }

    /// <summary>
    /// Parametri agricoli di un anno e di una provincia. Fallisce se la provincia non è
    /// caricata: applicare la tabella di un'altra provincia produrrebbe una busta sbagliata
    /// che sembra giusta, ed è il modo peggiore di sbagliare in questo mestiere.
    /// </summary>
    public async Task<ParametriAgricoli> CaricaParametriAgricoliAsync(
        int anno, string provincia, CancellationToken ct = default)
    {
        provincia = (provincia ?? "").Trim().ToUpperInvariant();

        var testata = await _db.ParametriAgricoli
            .FirstOrDefaultAsync(p => p.Anno == anno && p.Provincia == provincia, ct)
            ?? throw new ArgumentException(
                $"Tabelle agricole non presenti per la provincia {provincia} nell'anno {anno}: "
                + "vanno caricate dal contratto provinciale prima di elaborare le buste.");

        var tariffe = await _db.TariffeAgricole
            .Where(t => t.Anno == anno && t.Provincia == provincia)
            .OrderBy(t => t.Livello).ToListAsync(ct);
        if (tariffe.Count == 0)
            throw new ArgumentException(
                $"Nessuna tariffa salariale per la provincia {provincia} nell'anno {anno}.");

        var contributi = await _db.ContributiAgricoli
            .Where(c => c.Anno == anno && c.Provincia == provincia)
            .OrderBy(c => c.Ordine).ToListAsync(ct);

        return new ParametriAgricoli
        {
            Anno = anno,
            Provincia = provincia,
            Fonte = testata.Fonte,
            AliquotaContributiDeducibili = testata.AliquotaContributiDeducibili,
            Tariffe = tariffe
                .Select(t => new TariffaAgricola(
                    t.Livello, t.Descrizione, t.SalarioProvincialeOrario, t.TerzoElementoOrario))
                .ToList(),
            Contributi = contributi
                .Select(c => new ContributoAgricolo(
                    c.Codice, c.Descrizione, c.Aliquota, c.ACaricoDipendente))
                .ToList(),
        };
    }

    /// <summary>
    /// Carica (o sostituisce) le tabelle agricole di un anno e di una provincia. È un upsert
    /// per anno+provincia, come l'import dei CCNL: si ricarica la tabella aggiornata senza
    /// toccare il codice e senza sporcare gli anni già usati per buste emesse.
    /// </summary>
    public async Task<(int tariffe, int contributi)> ImportaParametriAgricoliAsync(
        ParametriAgricoli parametri, CancellationToken ct = default)
    {
        var provincia = parametri.Provincia.Trim().ToUpperInvariant();

        var testata = await _db.ParametriAgricoli
            .FirstOrDefaultAsync(p => p.Anno == parametri.Anno && p.Provincia == provincia, ct);
        if (testata is null)
        {
            testata = new ParametriAgricoliEntity
            {
                Id = Guid.NewGuid(), Anno = parametri.Anno, Provincia = provincia,
            };
            _db.ParametriAgricoli.Add(testata);
        }
        testata.AliquotaContributiDeducibili = parametri.AliquotaContributiDeducibili;
        testata.Fonte = parametri.Fonte;

        _db.TariffeAgricole.RemoveRange(await _db.TariffeAgricole
            .Where(t => t.Anno == parametri.Anno && t.Provincia == provincia).ToListAsync(ct));
        _db.ContributiAgricoli.RemoveRange(await _db.ContributiAgricoli
            .Where(c => c.Anno == parametri.Anno && c.Provincia == provincia).ToListAsync(ct));

        foreach (var t in parametri.Tariffe)
            _db.TariffeAgricole.Add(new TariffaAgricolaEntity
            {
                Id = Guid.NewGuid(), Anno = parametri.Anno, Provincia = provincia,
                Livello = t.Livello, Descrizione = t.Descrizione,
                SalarioProvincialeOrario = t.SalarioProvincialeOrario,
                TerzoElementoOrario = t.TerzoElementoOrario,
            });

        int ordine = 0;
        foreach (var c in parametri.Contributi)
            _db.ContributiAgricoli.Add(new ContributoAgricoloEntity
            {
                Id = Guid.NewGuid(), Anno = parametri.Anno, Provincia = provincia,
                Codice = c.Codice, Descrizione = c.Descrizione, Aliquota = c.Aliquota,
                ACaricoDipendente = c.ACaricoDipendente, Ordine = ordine++,
            });

        await _db.SaveChangesAsync(ct);
        return (parametri.Tariffe.Count, parametri.Contributi.Count);
    }
}
