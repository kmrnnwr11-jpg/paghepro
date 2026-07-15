using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;

namespace Paghe.Persistence;

/// <summary>
/// Bozza di Certificazione Unica (CU) per un rapporto di lavoro e un anno: aggrega i cedolini
/// archiviati dell'anno nei totali certificati. NON è il file telematico ufficiale dell'Agenzia
/// Entrate (quello richiede le specifiche annuali AE e l'invio via Entratel — vedi PIANO-DICHIARAZIONI.md).
/// </summary>
public sealed record CuBozza(
    string CodiceFiscale,
    string Nominativo,
    int Anno,
    int NumeroCedolini,
    decimal TotaleImponibileIrpef,
    decimal TotaleRitenuteIrpef,
    decimal TotaleAddizionaleRegionale,
    decimal TotaleAddizionaleComunale,
    decimal TotaleImponibileInps,
    decimal TotaleNetto);

public sealed class CertificazioneUnicaService
{
    private readonly PagheDbContext _db;
    public CertificazioneUnicaService(PagheDbContext db) => _db = db;

    /// <summary>Costruisce la bozza CU per il rapporto e l'anno indicati. Null se il rapporto non esiste per il tenant.</summary>
    public async Task<CuBozza?> CostruisciAsync(
        Guid tenantId, Guid rapportoId, int anno, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno)
            .ToListAsync(ct);

        var dip = rapporto.Dipendente;
        return new CuBozza(
            CodiceFiscale: dip?.CodiceFiscale ?? "",
            Nominativo: $"{dip?.Cognome} {dip?.Nome}".Trim(),
            Anno: anno,
            NumeroCedolini: cedolini.Count,
            TotaleImponibileIrpef: cedolini.Sum(c => c.ImponibileIrpef),
            TotaleRitenuteIrpef: cedolini.Sum(c => c.IrpefNetta),
            TotaleAddizionaleRegionale: cedolini.Sum(c => c.AddizionaleRegionale),
            TotaleAddizionaleComunale: cedolini.Sum(c => c.AddizionaleComunale),
            TotaleImponibileInps: cedolini.Sum(c => c.ImponibileInps),
            TotaleNetto: cedolini.Sum(c => c.NettoInBusta));
    }

    /// <summary>
    /// Costruisce la Certificazione Unica completa (per la stampa sul modello ufficiale AdE) di un
    /// rapporto e un anno: anagrafiche di sostituto e percipiente (sesso/nascita ricavati dal codice
    /// fiscale, domicilio fiscale dal comune delle addizionali), parte B fiscale con i totali annui
    /// ricalcolati con gli scaglioni dell'anno (imposta lorda, detrazioni, imposta netta) e sezione
    /// previdenziale INPS. Null se il rapporto non esiste per il tenant; lancia se l'anno non ha
    /// cedolini archiviati.
    /// </summary>
    public async Task<CertificazioneUnica?> CostruisciModelloAsync(
        Guid tenantId, Guid rapportoId, int anno, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti
            .Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == rapporto.AziendaId && a.TenantId == tenantId, ct);

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId && c.Anno == anno)
            .ToListAsync(ct);
        if (cedolini.Count == 0)
            throw new ArgumentException($"Nessun cedolino archiviato per l'anno {anno}.");

        decimal imponibileIrpef = cedolini.Sum(c => c.ImponibileIrpef);
        decimal ritenuteIrpef = cedolini.Sum(c => c.IrpefNetta);
        decimal quotaTfrAnno = cedolini.Sum(c => c.QuotaTfr);
        decimal conguaglio = cedolini.Sum(c => c.ConguaglioIrpef);

        // Totali annui coerenti con il conguaglio di fine anno (scaglioni versionati dell'anno).
        var parametri = await new ParametriNormativiService(_db).CaricaParametriAsync(anno, ct);
        decimal detrazioniSpettanti = rapporto.DetrazioniLavoroDipendente
            ? CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(imponibileIrpef, rapporto.GiorniDetrazioneAnno)
            : 0m;
        var annuale = ConguaglioFiscale.Calcola(
            imponibileIrpef, ritenuteIrpef, detrazioniSpettanti, parametri.ScaglioniIrpef);

        var dip = rapporto.Dipendente;
        var dalCf = AnagraficaCodiceFiscale.Decodifica(dip?.CodiceFiscale, anno);
        var comuneNascita = ComuniCatastali.Trova(dalCf?.CodiceCatastaleNascita);
        var domicilio = ComuniCatastali.Trova(rapporto.CodiceCatastaleComune);

        string tipoContratto = rapporto.TipoContratto ?? "";
        bool determinato = tipoContratto.Contains("determinato", StringComparison.OrdinalIgnoreCase)
            && !tipoContratto.Contains("indeterminato", StringComparison.OrdinalIgnoreCase);

        var annotazioni = new List<string>();
        if (quotaTfrAnno > 0m)
            annotazioni.Add($"Quota TFR maturata nell'anno: euro {quotaTfrAnno.ToString("N2", It)}.");
        if (conguaglio != 0m)
            annotazioni.Add("Conguaglio IRPEF di fine anno applicato in busta: euro "
                + $"{Math.Abs(conguaglio).ToString("N2", It)} {(conguaglio > 0m ? "a debito" : "a credito")}.");
        annotazioni.Add("Addizionali regionale e comunale trattenute nell'anno con metodo mensile semplificato.");
        annotazioni.Add("Certificazione generata da PaghePro sui cedolini archiviati - "
            + "BOZZA DA VALIDARE con il Consulente del Lavoro prima della consegna.");

        return new CertificazioneUnica
        {
            AnnoRedditi = anno,
            Sostituto = new SostitutoCu
            {
                CodiceFiscale = azienda?.CodiceFiscale ?? azienda?.PartitaIva ?? "",
                Denominazione = azienda?.RagioneSociale ?? "",
                Comune = azienda?.Comune,
                Provincia = azienda?.Provincia,
                Cap = azienda?.Cap,
                Indirizzo = azienda?.Indirizzo,
                Telefono = azienda?.Telefono,
                Email = azienda?.Email ?? azienda?.Pec,
                CodiceAttivita = azienda?.CodiceAteco,
            },
            Percipiente = new PercipienteCu
            {
                CodiceFiscale = dip?.CodiceFiscale ?? "",
                Cognome = dip?.Cognome ?? "",
                Nome = dip?.Nome,
                Sesso = dalCf?.Sesso,
                DataNascita = dalCf?.DataNascita,
                ComuneNascita = comuneNascita?.Nome,
                ProvinciaNascita = comuneNascita?.Provincia,
                DomicilioComune = domicilio?.Nome,
                DomicilioProvincia = domicilio?.Provincia,
                DomicilioCodiceComune = string.IsNullOrWhiteSpace(rapporto.CodiceCatastaleComune)
                    ? null : rapporto.CodiceCatastaleComune,
            },
            DatiFiscali = new DatiFiscaliCu
            {
                RedditoTempoIndeterminato = determinato ? 0m : imponibileIrpef,
                RedditoTempoDeterminato = determinato ? imponibileIrpef : 0m,
                GiorniDetrazione = rapporto.DetrazioniLavoroDipendente ? rapporto.GiorniDetrazioneAnno : 0,
                DataInizioRapporto = rapporto.DataAssunzione == default ? null : rapporto.DataAssunzione,
                DataCessazione = rapporto.DataCessazione,
                InForzaAl31Dicembre = rapporto.DataCessazione is null
                    || rapporto.DataCessazione >= new DateOnly(anno, 12, 31),
                RitenuteIrpef = ritenuteIrpef,
                AddizionaleRegionale = cedolini.Sum(c => c.AddizionaleRegionale),
                AddizionaleComunale = cedolini.Sum(c => c.AddizionaleComunale),
                ImpostaLorda = annuale.IrpefLordaAnnua,
                DetrazioniLavoroDipendente = annuale.DetrazioniSpettanti,
                TotaleDetrazioni = annuale.DetrazioniSpettanti,
                ImpostaNetta = annuale.IrpefNettaAnnua,
            },
            Previdenza = new PrevidenzaCu
            {
                MatricolaAzienda = azienda?.MatricolaInps,
                ImponibilePrevidenziale = cedolini.Sum(c => c.ImponibileInps),
                ContributiLavoratore = cedolini.Sum(c => c.ContributiDipendente),
                MesiRetribuiti = cedolini.Select(c => c.Mese).Distinct().Order().ToList(),
            },
            Annotazioni = annotazioni,
        };
    }

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>
    /// Costruisce le bozze CU di tutti i dipendenti di un'azienda per l'anno: una CU per ciascun
    /// rapporto che ha almeno un cedolino archiviato nell'anno. Ordinate per cognome/nome.
    /// </summary>
    public async Task<IReadOnlyList<CuBozza>> CostruisciPerAziendaAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        var rapporti = await _db.Rapporti
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);

        var cedolini = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno)
            .ToListAsync(ct);
        var perRapporto = cedolini.GroupBy(c => c.RapportoLavoroId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var bozze = new List<CuBozza>();
        foreach (var r in rapporti)
        {
            if (!perRapporto.TryGetValue(r.Id, out var cs) || cs.Count == 0)
                continue; // nessun cedolino nell'anno: nessuna CU da certificare

            var dip = r.Dipendente;
            bozze.Add(new CuBozza(
                CodiceFiscale: dip?.CodiceFiscale ?? "",
                Nominativo: $"{dip?.Cognome} {dip?.Nome}".Trim(),
                Anno: anno,
                NumeroCedolini: cs.Count,
                TotaleImponibileIrpef: cs.Sum(c => c.ImponibileIrpef),
                TotaleRitenuteIrpef: cs.Sum(c => c.IrpefNetta),
                TotaleAddizionaleRegionale: cs.Sum(c => c.AddizionaleRegionale),
                TotaleAddizionaleComunale: cs.Sum(c => c.AddizionaleComunale),
                TotaleImponibileInps: cs.Sum(c => c.ImponibileInps),
                TotaleNetto: cs.Sum(c => c.NettoInBusta)));
        }

        return bozze
            .OrderBy(b => b.Nominativo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Export CSV (bozza rivedibile) delle CU: separatore ';', decimali con punto (cultura invariante).
    /// NON è il file telematico ufficiale — serve al commercialista per controllare i totali certificati
    /// prima della generazione/controllo col software ufficiale dell'Agenzia Entrate.
    /// </summary>
    public static string EsportaCsv(IReadOnlyList<CuBozza> bozze)
    {
        static string N(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine("CodiceFiscale;Nominativo;Anno;ImponibileIrpef;RitenuteIrpef;" +
                      "AddizionaleRegionale;AddizionaleComunale;ImponibileInps;Netto");
        foreach (var b in bozze)
            sb.AppendLine(string.Join(';',
                b.CodiceFiscale, b.Nominativo, b.Anno, N(b.TotaleImponibileIrpef), N(b.TotaleRitenuteIrpef),
                N(b.TotaleAddizionaleRegionale), N(b.TotaleAddizionaleComunale),
                N(b.TotaleImponibileInps), N(b.TotaleNetto)));
        return sb.ToString();
    }
}
