using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Servizio per la gestione delle anagrafiche aziende, isolato per tenant.</summary>
public sealed class AziendeService
{
    private readonly PagheDbContext _db;

    public AziendeService(PagheDbContext db) => _db = db;

    public sealed record AziendaSintesi(
        AziendaEntity Azienda,
        int NumeroDipendenti,
        IReadOnlyList<string> DatiMancanti)
    {
        public bool DatiContributiviCompleti => !DatiMancanti.Any(d =>
            d is "settore" or "codiceAteco" or "matricolaInps" or "codiceSedeInps"
                or "cscInps" or "codiciAutorizzazioneInps" or "fondoIntegrazione"
                or "agevolazioneContributiva" or "aliquotaInpsDipendente"
                or "aliquotaInpsDatore" or "fonteProfiloContributivo"
                or "patInail" or "ccnlDefault" or "livelloDefault");
    }

    /// <summary>
    /// Blocca gli adempimenti contributivi finché l'anagrafica non contiene identificativi
    /// ufficiali completi. La modalità prova può salvare la bozza, ma non trasformarla in un
    /// F24 o UniEmens apparentemente utilizzabile.
    /// </summary>
    public static void VerificaProntaPerAdempimentiContributivi(AziendaEntity azienda)
    {
        var mancanti = DatiContributiviMancanti(azienda);
        if (mancanti.Count > 0)
            throw new ArgumentException(
                $"Azienda con dati contributivi provvisori: completa {string.Join(", ", mancanti.Select(DescriviCampoContributivo))} prima di generare F24 o UniEmens.");
    }

    public async Task<AziendaEntity> CreaAsync(Guid tenantId, AziendaEntity azienda, CancellationToken ct = default)
    {
        Normalizza(azienda);
        ValidaIdentificativi(azienda);
        var piva = azienda.PartitaIva;
        var cf = azienda.CodiceFiscale;
        if (!string.IsNullOrEmpty(piva))
        {
            var esiste = await _db.Aziende.AnyAsync(
                a => a.TenantId == tenantId && a.PartitaIva == piva, ct);
            if (esiste)
                throw new InvalidOperationException($"Esiste già un'azienda con Partita IVA {piva}.");
        }
        if (!string.IsNullOrEmpty(cf))
        {
            var esiste = await _db.Aziende.AnyAsync(
                a => a.TenantId == tenantId && a.CodiceFiscale == cf, ct);
            if (esiste)
                throw new InvalidOperationException($"Esiste già un'azienda con Codice Fiscale {cf}.");
        }
        azienda.Id = azienda.Id == Guid.Empty ? Guid.NewGuid() : azienda.Id;
        azienda.TenantId = tenantId;
        _db.Aziende.Add(azienda);
        await _db.SaveChangesAsync(ct);
        return azienda;
    }

    public async Task<bool> EliminaAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var a = await _db.Aziende.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (a is null) return false;
        await ScollegaPromemoriaAsync(tenantId, [id], ct);
        _db.Aziende.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> EliminaBulkAsync(Guid tenantId, IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var lista = ids.ToList();
        var aziende = await _db.Aziende
            .Where(a => a.TenantId == tenantId && lista.Contains(a.Id))
            .ToListAsync(ct);
        await ScollegaPromemoriaAsync(tenantId, aziende.Select(a => a.Id).ToList(), ct);
        _db.Aziende.RemoveRange(aziende);
        await _db.SaveChangesAsync(ct);
        return aziende.Count;
    }

    public Task<AziendaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Aziende.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

    private async Task ScollegaPromemoriaAsync(
        Guid tenantId, IReadOnlyCollection<Guid> aziendeIds, CancellationToken ct)
    {
        if (aziendeIds.Count == 0) return;
        var promemoria = await _db.Promemoria
            .Where(p => p.TenantId == tenantId
                && p.AziendaId.HasValue
                && aziendeIds.Contains(p.AziendaId.Value))
            .ToListAsync(ct);
        foreach (var voce in promemoria) voce.AziendaId = null;
    }

    /// <summary>Campi correggibili dell'anagrafica azienda (es. dopo una visura letta male). Null = non cambiare.</summary>
    /// <summary>Campi correggibili dell'anagrafica azienda (es. dopo una visura letta male). Null = non cambiare.</summary>
    public sealed record CorrezioneAzienda(
        string? RagioneSociale = null, string? PartitaIva = null, string? CodiceFiscale = null,
        string? Indirizzo = null, string? Cap = null, string? Comune = null, string? Provincia = null,
        string? Telefono = null, string? Email = null, string? CanalePredefinito = null,
        bool? HaDipendenti = null, string? TipoContribuente = null, bool? AssoggettataIva = null,
        string? Pec = null, string? CodiceDestinatarioSdi = null, string? CodiceCatastale = null,
        string? Settore = null, string? CodiceAteco = null, string? MatricolaInps = null, string? CodiceSedeInps = null,
        string? CscInps = null, string? PatInail = null, string? CcnlDefault = null,
        string? LivelloDefault = null, string? Iban = null,
        string? LegaleRappresentanteNome = null, string? LegaleRappresentanteCognome = null,
        string? LegaleRappresentanteCodiceFiscale = null,
        bool? OltreQuindiciDipendenti = null, decimal? ContributiFondiSettore = null,
        decimal? AliquotaInpsDatore = null, bool? AderisceFondiContrattuali = null,
        string? CodiciAutorizzazioneInps = null,
        string? CodiceFondoIntegrazioneSalariale = null,
        string? CodiceAgevolazioneContributiva = null,
        decimal? AliquotaInpsDipendente = null,
        string? FonteProfiloContributivo = null,
        string? RiferimentoVisuraCamerale = null,
        string? StatoAlboArtigiani = null,
        string? RiferimentoVerificaApplicabilitaCcnl = null,
        string? RiferimentoTerzoElementoTerritoriale = null);

    /// <summary>
    /// Corregge l'anagrafica di un'azienda (isolata per tenant): aggiorna solo i campi indicati.
    /// Pensata per rivedere ciò che l'import automatico (visura/OCR) ha letto male.
    /// Restituisce l'azienda aggiornata, null se non esiste per il tenant.
    /// </summary>
    public async Task<AziendaEntity?> CorreggiAsync(
        Guid tenantId, Guid id, CorrezioneAzienda c, CancellationToken ct = default)
    {
        var a = await GetAsync(tenantId, id, ct);
        if (a is null) return null;

        if (c.RagioneSociale is not null) a.RagioneSociale = c.RagioneSociale.Trim();
        if (c.PartitaIva is not null) a.PartitaIva = VuotoNull(c.PartitaIva);
        if (c.CodiceFiscale is not null) a.CodiceFiscale = VuotoNull(c.CodiceFiscale)?.ToUpperInvariant();
        if (c.Indirizzo is not null) a.Indirizzo = c.Indirizzo.Trim();
        if (c.Cap is not null) a.Cap = c.Cap.Trim();
        if (c.Comune is not null) a.Comune = c.Comune.Trim();
        if (c.Provincia is not null) a.Provincia = c.Provincia.Trim().ToUpperInvariant();
        if (c.Telefono is not null) a.Telefono = VuotoNull(c.Telefono);
        if (c.Email is not null) a.Email = VuotoNull(c.Email)?.ToLowerInvariant();
        if (c.CanalePredefinito is not null) a.CanalePredefinito = VuotoNull(c.CanalePredefinito);
        if (c.HaDipendenti is not null) a.HaDipendenti = c.HaDipendenti.Value;
        if (c.TipoContribuente is not null) a.TipoContribuente = VuotoNull(c.TipoContribuente);
        if (c.AssoggettataIva is not null) a.AssoggettataIva = c.AssoggettataIva.Value;
        if (c.Pec is not null) a.Pec = VuotoNull(c.Pec)?.ToLowerInvariant();
        if (c.CodiceDestinatarioSdi is not null) a.CodiceDestinatarioSdi = VuotoNull(c.CodiceDestinatarioSdi)?.ToUpperInvariant();
        if (c.CodiceCatastale is not null) a.CodiceCatastale = VuotoNull(c.CodiceCatastale)?.ToUpperInvariant();
        if (c.Settore is not null) a.Settore = VuotoNull(c.Settore);
        if (c.CodiceAteco is not null) a.CodiceAteco = VuotoNull(c.CodiceAteco);
        if (c.MatricolaInps is not null) a.MatricolaInps = VuotoNull(c.MatricolaInps);
        if (c.CodiceSedeInps is not null) a.CodiceSedeInps = VuotoNull(c.CodiceSedeInps);
        if (c.CscInps is not null) a.CscInps = VuotoNull(c.CscInps);
        if (c.PatInail is not null) a.PatInail = VuotoNull(c.PatInail);
        if (c.CcnlDefault is not null) a.CcnlDefault = VuotoNull(c.CcnlDefault);
        if (c.LivelloDefault is not null) a.LivelloDefault = VuotoNull(c.LivelloDefault);
        if (c.Iban is not null) a.Iban = VuotoNull(c.Iban)?.Replace(" ", "").ToUpperInvariant();
        if (c.LegaleRappresentanteNome is not null) a.LegaleRappresentanteNome = VuotoNull(c.LegaleRappresentanteNome);
        if (c.LegaleRappresentanteCognome is not null) a.LegaleRappresentanteCognome = VuotoNull(c.LegaleRappresentanteCognome);
        if (c.LegaleRappresentanteCodiceFiscale is not null) a.LegaleRappresentanteCodiceFiscale = VuotoNull(c.LegaleRappresentanteCodiceFiscale)?.ToUpperInvariant();
        if (c.OltreQuindiciDipendenti is not null) a.OltreQuindiciDipendenti = c.OltreQuindiciDipendenti.Value;
        if (c.ContributiFondiSettore is not null) a.ContributiFondiSettore = c.ContributiFondiSettore.Value;
        // Zero o negativo = torna alla tabella per qualifica (rimozione del profilo).
        if (c.AliquotaInpsDatore is not null)
            a.AliquotaInpsDatore = c.AliquotaInpsDatore.Value > 0m ? c.AliquotaInpsDatore.Value : null;
        if (c.AderisceFondiContrattuali is not null)
            a.AderisceFondiContrattuali = c.AderisceFondiContrattuali.Value;
        if (c.CodiciAutorizzazioneInps is not null)
            a.CodiciAutorizzazioneInps = VuotoNull(c.CodiciAutorizzazioneInps)?.ToUpperInvariant();
        if (c.CodiceFondoIntegrazioneSalariale is not null)
            a.CodiceFondoIntegrazioneSalariale = VuotoNull(c.CodiceFondoIntegrazioneSalariale)?.ToUpperInvariant();
        if (c.CodiceAgevolazioneContributiva is not null)
            a.CodiceAgevolazioneContributiva = VuotoNull(c.CodiceAgevolazioneContributiva)?.ToUpperInvariant();
        if (c.AliquotaInpsDipendente is not null)
            a.AliquotaInpsDipendente = c.AliquotaInpsDipendente.Value > 0m
                ? c.AliquotaInpsDipendente.Value : null;
        if (c.FonteProfiloContributivo is not null)
            a.FonteProfiloContributivo = VuotoNull(c.FonteProfiloContributivo);
        if (c.RiferimentoVisuraCamerale is not null)
            a.RiferimentoVisuraCamerale = VuotoNull(c.RiferimentoVisuraCamerale);
        if (c.StatoAlboArtigiani is not null)
            a.StatoAlboArtigiani = StatoAlbo(c.StatoAlboArtigiani);
        if (c.RiferimentoVerificaApplicabilitaCcnl is not null)
            a.RiferimentoVerificaApplicabilitaCcnl = VuotoNull(c.RiferimentoVerificaApplicabilitaCcnl);
        if (c.RiferimentoTerzoElementoTerritoriale is not null)
            a.RiferimentoTerzoElementoTerritoriale = VuotoNull(c.RiferimentoTerzoElementoTerritoriale);

        await VerificaDuplicatiAsync(tenantId, a, ct);
        Normalizza(a);
        ValidaIdentificativi(a);

        await _db.SaveChangesAsync(ct);
        return a;
    }

    public Task<List<AziendaEntity>> ElencoAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Aziende.Where(a => a.TenantId == tenantId).OrderBy(a => a.RagioneSociale).ToListAsync(ct);

    /// <summary>Salva (o sostituisce) il logo dell'azienda. False se l'azienda non esiste per il tenant.</summary>
    public async Task<bool> SalvaLogoAsync(Guid tenantId, Guid id, byte[]? logo, CancellationToken ct = default)
    {
        var a = await GetAsync(tenantId, id, ct);
        if (a is null) return false;
        a.Logo = logo;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Salva l'immagine autentica di firma/sigla/timbro del datore. La proprietà viene cifrata
    /// tenant-bound dall'interceptor; il file grezzo non viene esposto da alcun endpoint GET.
    /// </summary>
    public async Task<bool> SalvaFirmaSiglaTimbroAsync(
        Guid tenantId, Guid id, byte[]? immagine, CancellationToken ct = default)
    {
        var a = await GetAsync(tenantId, id, ct);
        if (a is null) return false;
        a.FirmaSiglaTimbro = immagine;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<AziendaSintesi>> ElencoSintesiAsync(Guid tenantId, CancellationToken ct = default)
    {
        var aziende = await ElencoAsync(tenantId, ct);
        var conteggi = await _db.Rapporti
            .Where(r => r.TenantId == tenantId)
            .GroupBy(r => r.AziendaId)
            .Select(g => new { AziendaId = g.Key, Numero = g.Count() })
            .ToDictionaryAsync(x => x.AziendaId, x => x.Numero, ct);
        return aziende
            .Select(a => new AziendaSintesi(a, conteggi.GetValueOrDefault(a.Id), DatiMancanti(a)))
            .ToList();
    }

    public async Task<AziendaSintesi?> GetSintesiAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var azienda = await GetAsync(tenantId, id, ct);
        if (azienda is null) return null;
        var numero = await _db.Rapporti.CountAsync(r => r.TenantId == tenantId && r.AziendaId == id, ct);
        return new AziendaSintesi(azienda, numero, DatiMancanti(azienda));
    }

    private async Task VerificaDuplicatiAsync(Guid tenantId, AziendaEntity azienda, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(azienda.PartitaIva))
        {
            var esiste = await _db.Aziende.AnyAsync(
                a => a.TenantId == tenantId && a.Id != azienda.Id && a.PartitaIva == azienda.PartitaIva, ct);
            if (esiste)
                throw new InvalidOperationException($"Esiste già un'azienda con Partita IVA {azienda.PartitaIva}.");
        }
        if (!string.IsNullOrWhiteSpace(azienda.CodiceFiscale))
        {
            var esiste = await _db.Aziende.AnyAsync(
                a => a.TenantId == tenantId && a.Id != azienda.Id && a.CodiceFiscale == azienda.CodiceFiscale, ct);
            if (esiste)
                throw new InvalidOperationException($"Esiste già un'azienda con Codice Fiscale {azienda.CodiceFiscale}.");
        }
    }

    private static IReadOnlyList<string> DatiMancanti(AziendaEntity a)
    {
        var mancanti = new List<string>();
        if (string.IsNullOrWhiteSpace(a.PartitaIva) && string.IsNullOrWhiteSpace(a.CodiceFiscale)) mancanti.Add("partitaIvaOCodiceFiscale");
        mancanti.AddRange(DatiContributiviMancanti(a));
        if (string.IsNullOrWhiteSpace(a.Indirizzo) || string.IsNullOrWhiteSpace(a.Cap) || string.IsNullOrWhiteSpace(a.Comune)) mancanti.Add("sedeLegale");
        return mancanti;
    }

    private static List<string> DatiContributiviMancanti(AziendaEntity a)
    {
        var mancanti = new List<string>();
        if (string.IsNullOrWhiteSpace(a.Settore)) mancanti.Add("settore");
        if (string.IsNullOrWhiteSpace(a.CodiceAteco)) mancanti.Add("codiceAteco");
        if (string.IsNullOrWhiteSpace(a.MatricolaInps)) mancanti.Add("matricolaInps");
        if (string.IsNullOrWhiteSpace(a.CodiceSedeInps)) mancanti.Add("codiceSedeInps");
        if (string.IsNullOrWhiteSpace(a.CscInps)) mancanti.Add("cscInps");
        if (string.IsNullOrWhiteSpace(a.CodiciAutorizzazioneInps)) mancanti.Add("codiciAutorizzazioneInps");
        if (string.IsNullOrWhiteSpace(a.CodiceFondoIntegrazioneSalariale)) mancanti.Add("fondoIntegrazione");
        if (string.IsNullOrWhiteSpace(a.CodiceAgevolazioneContributiva)) mancanti.Add("agevolazioneContributiva");
        if (a.AliquotaInpsDipendente is not > 0m) mancanti.Add("aliquotaInpsDipendente");
        if (a.AliquotaInpsDatore is not > 0m) mancanti.Add("aliquotaInpsDatore");
        if (string.IsNullOrWhiteSpace(a.FonteProfiloContributivo)) mancanti.Add("fonteProfiloContributivo");
        if (string.IsNullOrWhiteSpace(a.PatInail)) mancanti.Add("patInail");
        if (string.IsNullOrWhiteSpace(a.CcnlDefault)) mancanti.Add("ccnlDefault");
        if (string.IsNullOrWhiteSpace(a.LivelloDefault)) mancanti.Add("livelloDefault");
        return mancanti;
    }

    private static string DescriviCampoContributivo(string campo) => campo switch
    {
        "codiceAteco" => "codice ATECO",
        "matricolaInps" => "matricola INPS",
        "codiceSedeInps" => "codice sede INPS",
        "cscInps" => "CSC INPS",
        "codiciAutorizzazioneInps" => "codici autorizzazione INPS (o NESSUNO)",
        "fondoIntegrazione" => "FIS/fondo di integrazione (o NESSUNO)",
        "agevolazioneContributiva" => "agevolazione contributiva (o NESSUNA)",
        "aliquotaInpsDipendente" => "aliquota INPS c/dipendente del profilo",
        "aliquotaInpsDatore" => "aliquota INPS c/datore del profilo",
        "fonteProfiloContributivo" => "fonte del profilo contributivo",
        "patInail" => "P.A.T. INAIL",
        "ccnlDefault" => "CCNL",
        "livelloDefault" => "livello CCNL",
        _ => campo,
    };

    private static void Normalizza(AziendaEntity a)
    {
        a.RagioneSociale = a.RagioneSociale.Trim();
        a.PartitaIva = VuotoNull(a.PartitaIva);
        a.CodiceFiscale = VuotoNull(a.CodiceFiscale)?.ToUpperInvariant();
        a.Indirizzo = a.Indirizzo.Trim();
        a.Cap = a.Cap.Trim();
        a.Comune = a.Comune.Trim();
        a.Provincia = a.Provincia.Trim().ToUpperInvariant();
        a.Telefono = VuotoNull(a.Telefono);
        a.Email = VuotoNull(a.Email)?.ToLowerInvariant();
        a.Pec = VuotoNull(a.Pec)?.ToLowerInvariant();
        a.CodiceDestinatarioSdi = VuotoNull(a.CodiceDestinatarioSdi)?.ToUpperInvariant();
        a.CodiceCatastale = VuotoNull(a.CodiceCatastale)?.ToUpperInvariant();
        a.Settore = VuotoNull(a.Settore);
        a.CodiceAteco = VuotoNull(a.CodiceAteco);
        a.MatricolaInps = CodiceContributivoOVuoto(a.MatricolaInps);
        a.CodiceSedeInps = CodiceContributivoOVuoto(a.CodiceSedeInps);
        a.CscInps = CodiceContributivoOVuoto(a.CscInps);
        a.CodiciAutorizzazioneInps = VuotoNull(a.CodiciAutorizzazioneInps)?.ToUpperInvariant();
        a.CodiceFondoIntegrazioneSalariale = VuotoNull(a.CodiceFondoIntegrazioneSalariale)?.ToUpperInvariant();
        a.CodiceAgevolazioneContributiva = VuotoNull(a.CodiceAgevolazioneContributiva)?.ToUpperInvariant();
        a.FonteProfiloContributivo = VuotoNull(a.FonteProfiloContributivo);
        a.RiferimentoVisuraCamerale = VuotoNull(a.RiferimentoVisuraCamerale);
        a.StatoAlboArtigiani = StatoAlbo(a.StatoAlboArtigiani);
        a.RiferimentoVerificaApplicabilitaCcnl = VuotoNull(a.RiferimentoVerificaApplicabilitaCcnl);
        a.RiferimentoTerzoElementoTerritoriale = VuotoNull(a.RiferimentoTerzoElementoTerritoriale);
        a.PatInail = CodiceContributivoOVuoto(a.PatInail);
        a.CcnlDefault = VuotoNull(a.CcnlDefault);
        a.LivelloDefault = VuotoNull(a.LivelloDefault);
        a.Iban = VuotoNull(a.Iban)?.Replace(" ", "").ToUpperInvariant();
        a.LegaleRappresentanteNome = VuotoNull(a.LegaleRappresentanteNome);
        a.LegaleRappresentanteCognome = VuotoNull(a.LegaleRappresentanteCognome);
        a.LegaleRappresentanteCodiceFiscale = VuotoNull(a.LegaleRappresentanteCodiceFiscale)?.ToUpperInvariant();
        a.CanalePredefinito = VuotoNull(a.CanalePredefinito);
        a.TipoContribuente = VuotoNull(a.TipoContribuente);
    }

    private static string? VuotoNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? CodiceContributivoOVuoto(string? valore)
    {
        var normalizzato = VuotoNull(valore);
        if (normalizzato is null) return null;
        return normalizzato.All(c => c is '0' or '/' or ' ' or '.' or '-') ? null : normalizzato;
    }

    private static void ValidaIdentificativi(AziendaEntity a)
    {
        if (!string.IsNullOrWhiteSpace(a.PartitaIva)
            && !Paghe.Core.AdeTelematico.ValidatoreFiscale.PartitaIvaValida(a.PartitaIva))
            throw new ArgumentException("Partita IVA formalmente errata: il checksum non coincide.");
        if (!string.IsNullOrWhiteSpace(a.CodiceFiscale)
            && !Paghe.Core.AdeTelematico.ValidatoreFiscale.CodiceFiscaleValido(a.CodiceFiscale))
            throw new ArgumentException("Codice fiscale aziendale formalmente errato: il checksum non coincide.");
    }

    private static string StatoAlbo(string? valore)
    {
        var normalizzato = VuotoNull(valore) ?? "DaVerificare";
        if (normalizzato.Equals("DaVerificare", StringComparison.OrdinalIgnoreCase)) return "DaVerificare";
        if (normalizzato.Equals("Iscritta", StringComparison.OrdinalIgnoreCase)) return "Iscritta";
        if (normalizzato.Equals("NonIscritta", StringComparison.OrdinalIgnoreCase)) return "NonIscritta";
        throw new ArgumentException("Stato Albo artigiani non valido: usare DaVerificare, Iscritta o NonIscritta.");
    }
}
