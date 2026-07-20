using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Gdpr.Retention;

public sealed record PolicyRetentionVista(
    string Categoria,
    string Titolo,
    string Finalita,
    string Decorrenza,
    string Azione,
    string Avvertenza,
    int Versione,
    int? GiorniConservazione,
    bool Attiva,
    bool Approvata,
    string? BaseGiuridica,
    string? RiferimentoDecisione,
    string? IstruzioniBackup,
    DateTime? CreataUtc);

public sealed record ModificaPolicyRetention(
    int VersioneAttesa,
    int? GiorniConservazione,
    bool Attiva,
    bool Approvata,
    string? BaseGiuridica,
    string? RiferimentoDecisione,
    string? IstruzioniBackup);

public sealed record AnteprimaRetentionVista(
    Guid Id,
    string Categoria,
    int VersionePolicy,
    DateTime SogliaUtc,
    int Candidati,
    int Bloccati,
    int Elaborabili,
    DateTime CreataUtc,
    DateTime ScadeUtc,
    string CodiceConferma,
    string Azione,
    string Nota);

public sealed record BloccoConservazioneVista(
    Guid Id,
    string Categoria,
    string MotivoCodice,
    DateTime CreatoUtc,
    DateTime? RevocatoUtc);

public sealed record RicevutaRetentionVista(
    Guid Id,
    string Categoria,
    int VersionePolicy,
    DateTime SogliaUtc,
    int CandidatiAnteprima,
    int Bloccati,
    int Elaborati,
    int RestantiStimati,
    string Esito,
    DateTime IniziataUtc,
    DateTime CompletataUtc,
    string? CorrelationId,
    string Digest);

/// <summary>
/// Motore di retention esplicito e tenant-safe. Nessuna durata è predefinita e soltanto le
/// categorie nell'allowlist possono essere cancellate. Cedolini, anagrafiche e documenti fiscali
/// non sono raggiungibili da questo servizio.
/// </summary>
public sealed class RetentionService
{
    private const int GiorniMassimiTecnici = 36_500;
    private const int LottoMassimo = 500;
    private static readonly TimeSpan DurataAnteprima = TimeSpan.FromMinutes(10);

    private readonly PagheDbContext _db;
    private readonly TimeProvider _orologio;

    public RetentionService(PagheDbContext db, TimeProvider? orologio = null)
    {
        _db = db;
        _orologio = orologio ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<PolicyRetentionVista>> ElencaPolicyAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, null);
        var versioni = await _db.PolicyRetention.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(ct);
        var ultime = versioni
            .GroupBy(p => p.Categoria, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Versione).First(),
                StringComparer.OrdinalIgnoreCase);

        return CatalogoRetention.Tutte
            .Select(d => VistaPolicy(d, ultime.GetValueOrDefault(d.Categoria)))
            .ToArray();
    }

    public async Task<PolicyRetentionVista> AggiornaPolicyAsync(
        Guid tenantId,
        Guid utenteId,
        string categoria,
        ModificaPolicyRetention modifica,
        CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var definizione = CatalogoRetention.Trova(categoria);
        if (modifica.GiorniConservazione is < 1 or > GiorniMassimiTecnici)
            throw new RetentionNonValidaException(
                $"La durata deve essere compresa fra 1 e {GiorniMassimiTecnici} giorni.");

        var baseGiuridica = Testo(modifica.BaseGiuridica, 200, "base giuridica");
        var riferimento = Testo(modifica.RiferimentoDecisione, 200, "riferimento della decisione");
        var istruzioniBackup = Testo(modifica.IstruzioniBackup, 300, "istruzioni sui backup");
        if (modifica.Attiva && (!modifica.Approvata
            || modifica.GiorniConservazione is null
            || baseGiuridica is null
            || riferimento is null
            || istruzioniBackup is null))
            throw new RetentionNonValidaException(
                "Per attivare la policy servono durata, approvazione, base giuridica, "
                + "riferimento della decisione e istruzioni sui backup.");

        var corrente = await UltimaPolicyAsync(tenantId, definizione.Categoria, ct);
        var versioneCorrente = corrente?.Versione ?? 0;
        if (modifica.VersioneAttesa != versioneCorrente)
            throw new RetentionConflittoException(
                "La policy è cambiata: ricaricare la pagina prima di salvare.");

        var nuova = new PolicyRetentionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Categoria = definizione.Categoria,
            Versione = versioneCorrente + 1,
            GiorniConservazione = modifica.GiorniConservazione,
            Attiva = modifica.Attiva,
            Approvata = modifica.Approvata,
            BaseGiuridica = baseGiuridica,
            RiferimentoDecisione = riferimento,
            IstruzioniBackup = istruzioniBackup,
            Finalita = definizione.Finalita,
            Decorrenza = definizione.Decorrenza,
            Azione = definizione.Azione,
            CreataUtc = OraUtc(),
            CreataDaUtenteId = utenteId,
        };
        _db.PolicyRetention.Add(nuova);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new RetentionConflittoException(
                "La policy è stata aggiornata contemporaneamente: ricaricare e riprovare.");
        }
        return VistaPolicy(definizione, nuova);
    }

    public async Task<AnteprimaRetentionVista> CreaAnteprimaAsync(
        Guid tenantId,
        Guid utenteId,
        string categoria,
        CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var definizione = CatalogoRetention.Trova(categoria);
        var policy = await PolicyEseguibileAsync(tenantId, definizione.Categoria, ct);
        var ora = OraUtc();
        var soglia = ora.AddDays(-policy.GiorniConservazione!.Value);
        var conteggio = await ContaAsync(tenantId, definizione.Categoria, soglia, ct);

        var anteprima = new AnteprimaRetentionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Categoria = definizione.Categoria,
            VersionePolicy = policy.Versione,
            SogliaUtc = soglia,
            Candidati = conteggio.Candidati,
            Bloccati = conteggio.Bloccati,
            Elaborabili = conteggio.Elaborabili,
            ImprontaCandidati = conteggio.ImprontaCandidati,
            ImprontaBlocchi = conteggio.ImprontaBlocchi,
            CreataUtc = ora,
            ScadeUtc = ora.Add(DurataAnteprima),
            CreataDaUtenteId = utenteId,
        };
        _db.AnteprimeRetention.Add(anteprima);
        await _db.SaveChangesAsync(ct);
        return VistaAnteprima(anteprima, definizione);
    }

    public async Task<IReadOnlyList<BloccoConservazioneVista>> ElencaBlocchiAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, null);
        return await _db.BlocchiConservazione.AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatoUtc)
            .Take(100)
            .Select(b => new BloccoConservazioneVista(
                b.Id, b.Categoria, b.MotivoCodice, b.CreatoUtc, b.RevocatoUtc))
            .ToListAsync(ct);
    }

    public async Task<BloccoConservazioneVista> CreaBloccoAsync(
        Guid tenantId,
        Guid utenteId,
        string categoria,
        string motivoCodice,
        CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var definizione = CatalogoRetention.Trova(categoria);
        if (!MotiviBloccoConservazione.Tutti.Contains(motivoCodice))
            throw new RetentionNonValidaException("Motivo del blocco non valido.");
        var motivo = MotiviBloccoConservazione.Tutti.First(m =>
            string.Equals(m, motivoCodice, StringComparison.OrdinalIgnoreCase));

        var esistente = await _db.BlocchiConservazione.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId
                && b.Categoria == definizione.Categoria
                && b.MotivoCodice == motivo
                && b.RevocatoUtc == null, ct);
        if (esistente is not null)
            return VistaBlocco(esistente);

        var blocco = new BloccoConservazioneEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Categoria = definizione.Categoria,
            MotivoCodice = motivo,
            CreatoUtc = OraUtc(),
            CreatoDaUtenteId = utenteId,
        };
        _db.BlocchiConservazione.Add(blocco);
        await _db.SaveChangesAsync(ct);
        return VistaBlocco(blocco);
    }

    public async Task<BloccoConservazioneVista> RevocaBloccoAsync(
        Guid tenantId,
        Guid utenteId,
        Guid bloccoId,
        CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var blocco = await _db.BlocchiConservazione
            .FirstOrDefaultAsync(b => b.Id == bloccoId && b.TenantId == tenantId, ct)
            ?? throw new RetentionNonTrovataException("Blocco di conservazione non trovato.");
        if (blocco.RevocatoUtc is null)
        {
            blocco.RevocatoUtc = OraUtc();
            blocco.RevocatoDaUtenteId = utenteId;
            await _db.SaveChangesAsync(ct);
        }
        return VistaBlocco(blocco);
    }

    public async Task<IReadOnlyList<RicevutaRetentionVista>> ElencaEsecuzioniAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, null);
        var righe = await _db.EsecuzioniRetention.AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CompletataUtc)
            .Take(50)
            .ToListAsync(ct);
        return righe.Select(VistaRicevuta).ToArray();
    }

    public async Task<RicevutaRetentionVista> EseguiAsync(
        Guid tenantId,
        Guid utenteId,
        Guid anteprimaId,
        Guid chiaveIdempotenza,
        string codiceConferma,
        int dimensioneLotto = 100,
        CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        if (anteprimaId == Guid.Empty || chiaveIdempotenza == Guid.Empty)
            throw new RetentionNonValidaException("Anteprima e chiave idempotenza sono obbligatorie.");
        if (dimensioneLotto is < 1 or > LottoMassimo)
            throw new RetentionNonValidaException(
                $"La dimensione del lotto deve essere compresa fra 1 e {LottoMassimo}.");

        var giaEseguita = await _db.EsecuzioniRetention.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId
                && e.ChiaveIdempotenza == chiaveIdempotenza, ct);
        if (giaEseguita is not null)
        {
            if (giaEseguita.AnteprimaId != anteprimaId)
                throw new RetentionConflittoException(
                    "La chiave idempotenza è già stata usata per un'altra anteprima.");
            return VistaRicevuta(giaEseguita);
        }

        await using var transazione = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var anteprima = await _db.AnteprimeRetention
            .FirstOrDefaultAsync(a => a.Id == anteprimaId && a.TenantId == tenantId, ct)
            ?? throw new RetentionNonTrovataException("Anteprima non trovata.");

        if (anteprima.ConsumataUtc is not null)
            throw new RetentionConflittoException(
                "L'anteprima è già stata utilizzata: consultare le ricevute o crearne una nuova.");
        var ora = OraUtc();
        if (anteprima.ScadeUtc <= ora)
            throw new RetentionConflittoException("L'anteprima è scaduta: ricalcolarla.");
        if (!ConfermaValida(codiceConferma, CodiceConferma(anteprima.Id)))
            throw new RetentionNonValidaException("Codice di conferma non valido.");

        var definizione = CatalogoRetention.Trova(anteprima.Categoria);
        var policy = await PolicyEseguibileAsync(tenantId, definizione.Categoria, ct);
        if (policy.Versione != anteprima.VersionePolicy)
            throw new RetentionConflittoException(
                "La policy è cambiata dopo l'anteprima: ricalcolarla.");

        var conteggio = await ContaAsync(tenantId, definizione.Categoria, anteprima.SogliaUtc, ct);
        if (conteggio.Candidati != anteprima.Candidati
            || conteggio.Bloccati != anteprima.Bloccati
            || conteggio.Elaborabili != anteprima.Elaborabili
            || conteggio.ImprontaCandidati != anteprima.ImprontaCandidati
            || conteggio.ImprontaBlocchi != anteprima.ImprontaBlocchi)
            throw new RetentionConflittoException(
                "Dati o blocchi sono cambiati dopo l'anteprima: ricalcolarla.");
        if (conteggio.Elaborabili == 0)
            throw new RetentionNonValidaException(
                conteggio.Bloccati > 0
                    ? "Tutti i candidati sono protetti da un blocco di conservazione."
                    : "Non ci sono record da elaborare.");

        var iniziata = ora;
        var elaborati = await EliminaLottoAsync(
            tenantId, definizione.Categoria, anteprima.SogliaUtc, dimensioneLotto, ct);
        if (elaborati <= 0)
            throw new RetentionConflittoException(
                "I dati sono cambiati durante l'esecuzione: creare una nuova anteprima.");

        var completata = OraUtc();
        anteprima.ConsumataUtc = completata;
        var precedente = await _db.EsecuzioniRetention.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Categoria == definizione.Categoria)
            .OrderByDescending(e => e.CompletataUtc)
            .ThenByDescending(e => e.Id)
            .Select(e => e.Digest)
            .FirstOrDefaultAsync(ct);
        var ricevuta = new EsecuzioneRetentionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AnteprimaId = anteprima.Id,
            ChiaveIdempotenza = chiaveIdempotenza,
            Categoria = definizione.Categoria,
            VersionePolicy = policy.Versione,
            SogliaUtc = anteprima.SogliaUtc,
            CandidatiAnteprima = anteprima.Candidati,
            Bloccati = anteprima.Bloccati,
            Elaborati = elaborati,
            RestantiStimati = Math.Max(0, anteprima.Elaborabili - elaborati),
            Esito = elaborati < anteprima.Elaborabili ? "Parziale" : "Completata",
            IniziataUtc = iniziata,
            CompletataUtc = completata,
            EseguitaDaUtenteId = utenteId,
            CorrelationId = AmbientContestoAudit.Corrente.CorrelationId,
            DigestPrecedente = precedente,
        };
        ricevuta.Digest = CalcolaDigestRicevuta(ricevuta);
        _db.EsecuzioniRetention.Add(ricevuta);
        try
        {
            await _db.SaveChangesAsync(ct);
            await transazione.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transazione.RollbackAsync(ct);
            throw new RetentionConflittoException(
                "Un'altra esecuzione ha già utilizzato l'anteprima o la chiave idempotenza.");
        }
        return VistaRicevuta(ricevuta);
    }

    private async Task<PolicyRetentionEntity> PolicyEseguibileAsync(
        Guid tenantId, string categoria, CancellationToken ct)
    {
        var policy = await UltimaPolicyAsync(tenantId, categoria, ct);
        if (policy is null || !policy.Attiva || !policy.Approvata
            || policy.GiorniConservazione is null
            || string.IsNullOrWhiteSpace(policy.BaseGiuridica)
            || string.IsNullOrWhiteSpace(policy.RiferimentoDecisione)
            || string.IsNullOrWhiteSpace(policy.IstruzioniBackup))
            throw new RetentionConflittoException(
                "La policy non è attiva e completa: salvarla e farne approvare i criteri prima dell'uso.");
        return policy;
    }

    private Task<PolicyRetentionEntity?> UltimaPolicyAsync(
        Guid tenantId, string categoria, CancellationToken ct) =>
        _db.PolicyRetention.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Categoria == categoria)
            .OrderByDescending(p => p.Versione)
            .FirstOrDefaultAsync(ct);

    private async Task<ConteggioRetention> ContaAsync(
        Guid tenantId, string categoria, DateTime sogliaUtc, CancellationToken ct)
    {
        var ids = await QueryCandidati(tenantId, categoria, sogliaUtc)
            .OrderBy(id => id)
            .ToListAsync(ct);
        var blocchi = await _db.BlocchiConservazione.AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && b.Categoria == categoria
                && b.RevocatoUtc == null)
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .ToListAsync(ct);
        var bloccati = blocchi.Count > 0 ? ids.Count : 0;
        return new ConteggioRetention(
            ids.Count,
            bloccati,
            ids.Count - bloccati,
            Impronta(ids),
            Impronta(blocchi));
    }

    private IQueryable<Guid> QueryCandidati(Guid tenantId, string categoria, DateTime sogliaUtc) =>
        categoria switch
        {
            CatalogoRetention.RegistroAttivita => _db.AuditLog.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.QuandoUtc < sogliaUtc)
                .Select(a => a.Id),
            CatalogoRetention.PromemoriaManualiChiusi => _db.Promemoria.AsNoTracking()
                .Where(p => p.TenantId == tenantId
                    && p.Tipo == TipoPromemoria.Manuale
                    && (p.Stato == StatoPromemoria.Completato
                        || p.Stato == StatoPromemoria.Annullato)
                    && p.UltimoAggiornamento < sogliaUtc)
                .Select(p => p.Id),
            _ => throw new RetentionNonValidaException("Categoria di conservazione non supportata."),
        };

    private async Task<int> EliminaLottoAsync(
        Guid tenantId,
        string categoria,
        DateTime sogliaUtc,
        int dimensioneLotto,
        CancellationToken ct)
    {
        if (await _db.BlocchiConservazione.AsNoTracking().AnyAsync(b =>
                b.TenantId == tenantId
                && b.Categoria == categoria
                && b.RevocatoUtc == null, ct))
            throw new RetentionConflittoException(
                "Un blocco di conservazione è stato attivato: creare una nuova anteprima.");

        List<Guid> ids;
        switch (categoria)
        {
            case CatalogoRetention.RegistroAttivita:
                ids = await _db.AuditLog.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && a.QuandoUtc < sogliaUtc)
                    .OrderBy(a => a.QuandoUtc).ThenBy(a => a.Id)
                    .Select(a => a.Id).Take(dimensioneLotto).ToListAsync(ct);
                return await _db.AuditLog
                    .Where(a => a.TenantId == tenantId && ids.Contains(a.Id))
                    .ExecuteDeleteAsync(ct);
            case CatalogoRetention.PromemoriaManualiChiusi:
                ids = await _db.Promemoria.AsNoTracking()
                    .Where(p => p.TenantId == tenantId
                        && p.Tipo == TipoPromemoria.Manuale
                        && (p.Stato == StatoPromemoria.Completato
                            || p.Stato == StatoPromemoria.Annullato)
                        && p.UltimoAggiornamento < sogliaUtc)
                    .OrderBy(p => p.UltimoAggiornamento).ThenBy(p => p.Id)
                    .Select(p => p.Id).Take(dimensioneLotto).ToListAsync(ct);
                return await _db.Promemoria
                    .Where(p => p.TenantId == tenantId && ids.Contains(p.Id))
                    .ExecuteDeleteAsync(ct);
            default:
                throw new RetentionNonValidaException("Categoria di conservazione non supportata.");
        }
    }

    private static PolicyRetentionVista VistaPolicy(
        DefinizioneRetention definizione, PolicyRetentionEntity? policy) => new(
        definizione.Categoria,
        definizione.Titolo,
        definizione.Finalita,
        definizione.Decorrenza,
        definizione.Azione,
        definizione.Avvertenza,
        policy?.Versione ?? 0,
        policy?.GiorniConservazione,
        policy?.Attiva ?? false,
        policy?.Approvata ?? false,
        policy?.BaseGiuridica,
        policy?.RiferimentoDecisione,
        policy?.IstruzioniBackup,
        policy?.CreataUtc);

    private static AnteprimaRetentionVista VistaAnteprima(
        AnteprimaRetentionEntity a, DefinizioneRetention d) => new(
        a.Id,
        a.Categoria,
        a.VersionePolicy,
        a.SogliaUtc,
        a.Candidati,
        a.Bloccati,
        a.Elaborabili,
        a.CreataUtc,
        a.ScadeUtc,
        CodiceConferma(a.Id),
        d.Azione,
        "L'anteprima non modifica i dati. L'esecuzione riguarda solo l'archivio operativo, non i backup.");

    private static BloccoConservazioneVista VistaBlocco(BloccoConservazioneEntity b) =>
        new(b.Id, b.Categoria, b.MotivoCodice, b.CreatoUtc, b.RevocatoUtc);

    private static RicevutaRetentionVista VistaRicevuta(EsecuzioneRetentionEntity e) => new(
        e.Id,
        e.Categoria,
        e.VersionePolicy,
        e.SogliaUtc,
        e.CandidatiAnteprima,
        e.Bloccati,
        e.Elaborati,
        e.RestantiStimati,
        e.Esito,
        e.IniziataUtc,
        e.CompletataUtc,
        e.CorrelationId,
        e.Digest);

    private static string CodiceConferma(Guid anteprimaId) =>
        $"CONFERMA-{anteprimaId:N}"[..17].ToUpperInvariant();

    private static bool ConfermaValida(string ricevuto, string atteso)
    {
        var a = Encoding.UTF8.GetBytes(ricevuto?.Trim().ToUpperInvariant() ?? "");
        var b = Encoding.UTF8.GetBytes(atteso);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Impronta(IEnumerable<Guid> ids) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', ids.Select(id => id.ToString("N"))))));

    private static string CalcolaDigestRicevuta(EsecuzioneRetentionEntity e)
    {
        var contenuto = string.Join('|',
            e.DigestPrecedente ?? "INIZIO",
            e.Id.ToString("N"),
            e.TenantId.ToString("N"),
            e.AnteprimaId.ToString("N"),
            e.ChiaveIdempotenza.ToString("N"),
            e.Categoria,
            e.VersionePolicy,
            e.SogliaUtc.ToUniversalTime().ToString("O"),
            e.CandidatiAnteprima,
            e.Bloccati,
            e.Elaborati,
            e.RestantiStimati,
            e.Esito,
            e.IniziataUtc.ToUniversalTime().ToString("O"),
            e.CompletataUtc.ToUniversalTime().ToString("O"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenuto)));
    }

    private DateTime OraUtc() => _orologio.GetUtcNow().UtcDateTime;

    private static void ValidaIdentita(Guid tenantId, Guid? utenteId)
    {
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            throw new RetentionNonValidaException("Tenant e utente sono obbligatori.");
    }

    private static string? Testo(string? valore, int massimo, string nome)
    {
        if (string.IsNullOrWhiteSpace(valore)) return null;
        var pulito = valore.Trim();
        if (pulito.Length > massimo)
            throw new RetentionNonValidaException(
                $"Il campo {nome} non può superare {massimo} caratteri.");
        return pulito;
    }

    private sealed record ConteggioRetention(
        int Candidati,
        int Bloccati,
        int Elaborabili,
        string ImprontaCandidati,
        string ImprontaBlocchi);
}
