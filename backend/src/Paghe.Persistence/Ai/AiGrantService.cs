using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Ai;

/// <summary>Chiave di firma delle lease del Governor (derivata dalla chiave JWT in Program).</summary>
public sealed record AiGovernorOptions(byte[] ChiaveLease);

public sealed record RichiestaGrant(
    string PurposeCode,
    string Modalita,
    string ScopeLevel,
    Guid? AziendaId,
    string? Periodo,
    IReadOnlyList<string> Risorse,
    IReadOnlyList<string> Operazioni,
    int? DurataMinuti = null,
    int? MaxLetture = null);

public sealed record ManifestGrant(
    Guid GrantId,
    string PurposeCode,
    string Modalita,
    string ScopeLevel,
    Guid? AziendaId,
    string? Periodo,
    string[] Risorse,
    string[] Operazioni,
    string AutonomiaMax,
    DateTime ScadeUtc,
    int MaxLetture,
    int LettureUsate,
    string Stato,
    string Descrizione);

public sealed record EsitoPreflight(bool Consentito, string? Motivo, string? Descrizione);

public sealed record GrantEmesso(ManifestGrant Manifest, string Lease);

public sealed record EsitoAccessoAi(bool Consentito, string? Motivo, int LettureRimanenti);

public sealed record EsitoCatena(bool Integra, int Righe, long? PrimaSequenzaAnomala);

/// <summary>
/// Usage Governor (G2): unica autorità che emette, verifica e revoca i mandati AI e ne tiene
/// l'audit concatenato. Regole del piano §6: il grant nasce da UI e policy (mai dal prompt),
/// ogni accesso ricontrolla firma, tenant, finalità, scope, operazione, quota, scadenza e
/// revoca; un diniego prevale; i contatori sono atomici (token di concorrenza + retry).
/// In G2 non esistono scritture: solo read/search/draft su classi D0-D2.
/// </summary>
public sealed class AiGrantService(PagheDbContext db, AiGovernorOptions opzioni, TimeProvider orologio)
{
    private const int TentativiConcorrenza = 5;

    // La sequenza audit è contesa da ogni evento del tenant: sotto concorrenza servono
    // più tentativi del contatore del singolo grant.
    private const int TentativiAudit = 50;

    // ---------- Preflight ed emissione ----------

    public async Task<EsitoPreflight> PreflightAsync(
        Guid tenantId, Guid utenteId, RichiestaGrant richiesta, CancellationToken ct = default)
    {
        var (valida, errore) = Valida(richiesta);
        if (valida is null)
        {
            await AppendiAuditAsync(tenantId, null, utenteId, "Preflight", Riassunto(richiesta), errore, ct);
            return new EsitoPreflight(false, errore, null);
        }

        await AppendiAuditAsync(tenantId, null, utenteId, "Preflight", Riassunto(richiesta), null, ct);
        return new EsitoPreflight(true, null, Descrizione(valida.Value));
    }

    public async Task<(GrantEmesso? Grant, string? Errore)> EmettiAsync(
        Guid tenantId, Guid utenteId, RichiestaGrant richiesta, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            return (null, "Tenant e utente sono obbligatori.");

        var (valida, errore) = Valida(richiesta);
        if (valida is null)
        {
            await AppendiAuditAsync(tenantId, null, utenteId, "Diniego", Riassunto(richiesta), errore, ct);
            return (null, errore);
        }

        var v = valida.Value;
        var adesso = orologio.GetUtcNow().UtcDateTime;
        var grant = new AiScopeGrantEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UtenteId = utenteId,
            PurposeCode = v.Policy.PurposeCode,
            Modalita = v.Modalita,
            ScopeLevel = richiesta.ScopeLevel,
            AziendaId = richiesta.AziendaId,
            Periodo = richiesta.Periodo,
            RisorseAmmesse = string.Join(",", v.Risorse),
            OperazioniAmmesse = string.Join(",", v.Operazioni),
            AutonomiaMax = v.Operazioni.Contains("draft") ? "A2" : "A1",
            PolicyVersione = AiUsagePolicyStore.Versione,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            CreatoUtc = adesso,
            ScadeUtc = adesso.AddMinutes(v.DurataMinuti),
            TimeoutInattivitaMinuti = v.Modalita == "Stretta" ? 10 : 30,
            UltimoUsoUtc = adesso,
            MaxLetture = v.MaxLetture,
        };
        db.AiScopeGrants.Add(grant);
        await db.SaveChangesAsync(ct);

        await AppendiAuditAsync(
            tenantId, grant.Id, utenteId, "EmissioneGrant",
            $"{grant.PurposeCode} {grant.ScopeLevel}/{grant.Modalita} risorse=[{grant.RisorseAmmesse}] " +
            $"op=[{grant.OperazioniAmmesse}] quota={grant.MaxLetture} scade={grant.ScadeUtc:O}",
            null, ct);

        var lease = AiLeaseCodec.Crea(grant.Id, grant.Nonce, grant.CreatoUtc, grant.ScadeUtc, opzioni.ChiaveLease);
        return (new GrantEmesso(Manifest(grant), lease), null);
    }

    // ---------- Enforcement ----------

    /// <summary>
    /// Punto di passaggio obbligato di ogni retrieval/tool AI: senza una lease valida che
    /// risolve a un mandato attivo, l'accesso è negato e il diniego è auditato.
    /// </summary>
    public async Task<EsitoAccessoAi> AutorizzaAccessoAsync(
        Guid tenantId, Guid utenteId, string? lease, string risorsa, string operazione,
        int quantita = 1, CancellationToken ct = default) =>
        (await AutorizzaAccessoConManifestAsync(tenantId, utenteId, lease, risorsa, operazione, quantita, ct)).Esito;

    /// <summary>
    /// Come <see cref="AutorizzaAccessoAsync"/>, ma restituisce anche il manifest del mandato:
    /// il broker ci legge lo scope autorevole (azienda, periodo), mai dai parametri del client.
    /// </summary>
    public async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)> AutorizzaAccessoConManifestAsync(
        Guid tenantId, Guid utenteId, string? lease, string risorsa, string operazione,
        int quantita = 1, CancellationToken ct = default)
    {
        var adesso = orologio.GetUtcNow().UtcDateTime;

        var decodificata = AiLeaseCodec.Prova(lease, opzioni.ChiaveLease);
        if (decodificata is null)
            return (await NegaAsync(tenantId, null, utenteId, risorsa, operazione, "Lease assente o firma non valida.", ct), null);
        if (adesso >= decodificata.ExpiresAtUtc)
            return (await NegaAsync(tenantId, decodificata.GrantId, utenteId, risorsa, operazione, "Lease scaduta.", ct), null);

        var grant = await db.AiScopeGrants.FirstOrDefaultAsync(g => g.Id == decodificata.GrantId, ct);
        if (grant is null)
            return (await NegaAsync(tenantId, decodificata.GrantId, utenteId, risorsa, operazione, "Mandato inesistente.", ct), null);
        if (grant.TenantId != tenantId)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Tenant non corrispondente al mandato.", ct), null);
        if (!string.Equals(grant.Nonce, decodificata.Nonce, StringComparison.Ordinal))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Nonce non valido (possibile replay).", ct), null);
        if (grant.Stato != "Attivo")
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, $"Mandato {grant.Stato.ToLowerInvariant()}.", ct), null);
        if (adesso >= grant.ScadeUtc)
        {
            grant.Stato = "Scaduto";
            grant.Versione++;
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Mandato scaduto.", ct), null);
        }
        if (adesso - grant.UltimoUsoUtc > TimeSpan.FromMinutes(grant.TimeoutInattivitaMinuti))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Mandato inattivo oltre il limite.", ct), null);

        var operazioneNorm = (operazione ?? "").Trim().ToLowerInvariant();
        if (!Elenco(grant.OperazioniAmmesse).Contains(operazioneNorm))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, $"Operazione '{operazione}' fuori dal mandato.", ct), null);

        var risorsaNorm = (risorsa ?? "").Trim();
        if (!Elenco(grant.RisorseAmmesse).Contains(risorsaNorm, StringComparer.OrdinalIgnoreCase))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, $"Risorsa '{risorsa}' fuori dal mandato.", ct), null);

        // Difesa in profondità: la risorsa deve restare a catalogo e concedibile anche ORA
        // (una riclassificazione dopo l'emissione prevale sul mandato).
        var catalogata = ProfessionalResourceCatalog.Classifica(risorsaNorm);
        if (catalogata is null || !ProfessionalResourceCatalog.ConcedibileInG2(catalogata.Classe))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, $"Risorsa '{risorsa}' non concedibile ({catalogata?.Classe.ToString() ?? "non a catalogo"}).", ct), null);

        if (quantita < 1)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Quantità non valida.", ct), null);

        // Contatore atomico: token di concorrenza + retry; la quota non si aggira in parallelo.
        for (var tentativo = 0; tentativo < TentativiConcorrenza; tentativo++)
        {
            if (grant.LettureUsate + quantita > grant.MaxLetture)
                return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Quota del mandato esaurita: checkpoint, serve un nuovo grant.", ct), null);

            grant.LettureUsate += quantita;
            grant.UltimoUsoUtc = adesso;
            grant.Versione++;
            try
            {
                await db.SaveChangesAsync(ct);
                await AppendiAuditAsync(tenantId, grant.Id, utenteId, "Accesso", $"{risorsaNorm}:{operazioneNorm} x{quantita}", null, ct);
                return (new EsitoAccessoAi(true, null, grant.MaxLetture - grant.LettureUsate), Manifest(grant));
            }
            catch (DbUpdateConcurrencyException)
            {
                await db.Entry(grant).ReloadAsync(ct);
                if (grant.Stato != "Attivo")
                    return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Mandato revocato durante l'accesso.", ct), null);
            }
        }
        return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Conflitto di concorrenza persistente.", ct), null);
    }

    // ---------- Revoca ----------

    public async Task<bool> RevocaAsync(Guid tenantId, Guid utenteId, Guid grantId, CancellationToken ct = default)
    {
        var grant = await db.AiScopeGrants
            .FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId, ct);
        if (grant is null) return false;

        if (grant.Stato == "Attivo")
        {
            grant.Stato = "Revocato";
            grant.RevocatoUtc = orologio.GetUtcNow().UtcDateTime;
            grant.Versione++;
            await db.SaveChangesAsync(ct);
            await AppendiAuditAsync(tenantId, grant.Id, utenteId, "Revoca", grant.PurposeCode, null, ct);
        }
        return true;
    }

    /// <summary>«Arresta AI»: revoca immediata di tutti i mandati attivi dell'utente sul tenant.</summary>
    public async Task<int> ArrestaTuttoAsync(Guid tenantId, Guid utenteId, CancellationToken ct = default)
    {
        var attivi = await db.AiScopeGrants
            .Where(g => g.TenantId == tenantId && g.UtenteId == utenteId && g.Stato == "Attivo")
            .ToListAsync(ct);
        var adesso = orologio.GetUtcNow().UtcDateTime;
        foreach (var grant in attivi)
        {
            grant.Stato = "Revocato";
            grant.RevocatoUtc = adesso;
            grant.Versione++;
        }
        if (attivi.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            await AppendiAuditAsync(tenantId, null, utenteId, "ArrestoTotale", $"revocati={attivi.Count}", null, ct);
        }
        return attivi.Count;
    }

    // ---------- Trasparenza («Cosa vede l'AI») ----------

    public async Task<IReadOnlyList<ManifestGrant>> GrantsAsync(
        Guid tenantId, Guid utenteId, CancellationToken ct = default) =>
        (await db.AiScopeGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.UtenteId == utenteId)
            .OrderByDescending(g => g.CreatoUtc)
            .Take(50)
            .ToListAsync(ct))
        .Select(Manifest)
        .ToArray();

    public async Task<(ManifestGrant Manifest, IReadOnlyList<AiAuditEntity> Eventi)?> CosaVedeAsync(
        Guid tenantId, Guid grantId, CancellationToken ct = default)
    {
        var grant = await db.AiScopeGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId, ct);
        if (grant is null) return null;

        var eventi = await db.AiAudit.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.GrantId == grantId)
            .OrderByDescending(a => a.Sequenza)
            .Take(100)
            .ToListAsync(ct);
        return (Manifest(grant), eventi);
    }

    /// <summary>Riverifica l'intera catena hash dell'audit del tenant (tamper-evident).</summary>
    public async Task<EsitoCatena> VerificaCatenaAsync(Guid tenantId, CancellationToken ct = default)
    {
        var righe = await db.AiAudit.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.Sequenza)
            .ToListAsync(ct);

        var attesoPrecedente = "GENESI";
        var attesaSequenza = 1L;
        foreach (var riga in righe)
        {
            if (riga.Sequenza != attesaSequenza
                || riga.HashPrecedente != attesoPrecedente
                || riga.Hash != CalcolaHash(riga))
                return new EsitoCatena(false, righe.Count, riga.Sequenza);
            attesoPrecedente = riga.Hash;
            attesaSequenza++;
        }
        return new EsitoCatena(true, righe.Count, null);
    }

    // ---------- Validazione contro policy e catalogo ----------

    private readonly record struct RichiestaValidata(
        AiUsagePolicy Policy, string Modalita, string[] Risorse, string[] Operazioni,
        int DurataMinuti, int MaxLetture, Guid? AziendaId, string? Periodo, string ScopeLevel);

    private (RichiestaValidata? Valida, string? Errore) Valida(RichiestaGrant r)
    {
        var policy = AiUsagePolicyStore.Trova(r.PurposeCode);
        if (policy is null)
            return (null, $"Finalità '{r.PurposeCode}' non ammessa dalla policy.");

        var modalita = (r.Modalita ?? "").Trim();
        if (modalita is not ("Stretta" or "Guidata"))
            return (null, "Modalità ammesse in G2: Stretta o Guidata.");

        if (!AiUsagePolicyStore.ScopeConsentito(r.ScopeLevel, policy.ScopeMassimo))
            return (null, $"Scope '{r.ScopeLevel}' oltre il massimo '{policy.ScopeMassimo}' della finalità.");
        if (modalita == "Stretta" && !AiUsagePolicyStore.ScopeConsentito(r.ScopeLevel, "S1"))
            return (null, "La modalità Stretta ammette al massimo lo scope S1.");
        if (r.ScopeLevel is "S1" or "S2" && r.AziendaId is null)
            return (null, "Scope S1/S2 richiede l'azienda del mandato.");
        if (r.ScopeLevel == "S2" && (string.IsNullOrWhiteSpace(r.Periodo)
            || !System.Text.RegularExpressions.Regex.IsMatch(r.Periodo!, @"^\d{4}-(0[1-9]|1[0-2])$")))
            return (null, "Scope S2 richiede il periodo nel formato AAAA-MM.");

        if (r.Risorse is null || r.Risorse.Count == 0)
            return (null, "Il mandato deve elencare almeno una risorsa.");
        var risorse = r.Risorse.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var nome in risorse)
        {
            var voce = ProfessionalResourceCatalog.Classifica(nome);
            if (voce is null)
                return (null, $"Risorsa '{nome}' non a catalogo: negata per costruzione.");
            if (!ProfessionalResourceCatalog.ConcedibileInG2(voce.Classe))
                return (null, $"Risorsa '{nome}' di classe {voce.Classe}: non concedibile in G2.");
        }

        if (r.Operazioni is null || r.Operazioni.Count == 0)
            return (null, "Il mandato deve elencare almeno un'operazione.");
        var operazioni = r.Operazioni.Select(x => (x ?? "").Trim().ToLowerInvariant())
            .Where(x => x.Length > 0).Distinct().ToArray();
        foreach (var op in operazioni)
            if (!policy.OperazioniAmmesse.Contains(op))
                return (null, $"Operazione '{op}' non ammessa dalla finalità '{policy.PurposeCode}'.");

        var durata = r.DurataMinuti ?? policy.TtlMassimoMinuti;
        if (durata < 1) return (null, "Durata non valida.");
        durata = Math.Min(durata, policy.TtlMassimoMinuti);
        if (modalita == "Stretta") durata = Math.Min(durata, 30);

        var maxLetture = r.MaxLetture ?? policy.QuotaLettureDefault;
        if (maxLetture < 1) return (null, "Quota letture non valida.");
        maxLetture = Math.Min(maxLetture, policy.QuotaLettureMassima);

        return (new RichiestaValidata(
            policy, modalita, risorse, operazioni, durata, maxLetture,
            r.AziendaId, r.Periodo, r.ScopeLevel), null);
    }

    // ---------- Audit concatenato ----------

    private async Task<EsitoAccessoAi> NegaAsync(
        Guid tenantId, Guid? grantId, Guid utenteId, string? risorsa, string? operazione,
        string motivo, CancellationToken ct)
    {
        await AppendiAuditAsync(tenantId, grantId, utenteId, "Diniego",
            $"{(risorsa ?? "?").Trim()}:{(operazione ?? "?").Trim()}", motivo, ct);
        return new EsitoAccessoAi(false, motivo, 0);
    }

    private async Task AppendiAuditAsync(
        Guid tenantId, Guid? grantId, Guid? utenteId, string evento, string dettaglio,
        string? motivo, CancellationToken ct)
    {
        for (var tentativo = 0; tentativo < TentativiAudit; tentativo++)
        {
            var ultima = await db.AiAudit.AsNoTracking()
                .Where(a => a.TenantId == tenantId)
                .OrderByDescending(a => a.Sequenza)
                .FirstOrDefaultAsync(ct);

            var riga = new AiAuditEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sequenza = (ultima?.Sequenza ?? 0) + 1,
                GrantId = grantId,
                UtenteId = utenteId,
                Evento = evento,
                Dettaglio = dettaglio.Length <= 500 ? dettaglio : dettaglio[..500],
                Motivo = motivo is { Length: > 300 } ? motivo[..300] : motivo,
                // Troncato al millisecondo: SQLite e PostgreSQL lo restituiscono identico,
                // così la verifica della catena ricalcola lo stesso hash.
                QuandoUtc = TruncaAlMillisecondo(orologio.GetUtcNow().UtcDateTime),
                HashPrecedente = ultima?.Hash ?? "GENESI",
            };
            riga.Hash = CalcolaHash(riga);
            db.AiAudit.Add(riga);
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException)
            {
                // Corsa sulla stessa sequenza: si stacca la riga e si rilegge la coda.
                db.Entry(riga).State = EntityState.Detached;
            }
        }
        throw new InvalidOperationException("Audit AI non scrivibile: troppe corse sulla sequenza.");
    }

    internal static string CalcolaHash(AiAuditEntity riga)
    {
        var canonico = string.Join('|',
            riga.Sequenza,
            riga.TenantId.ToString("N"),
            riga.GrantId?.ToString("N") ?? "",
            riga.UtenteId?.ToString("N") ?? "",
            riga.Evento,
            riga.Dettaglio,
            riga.Motivo ?? "",
            DateTime.SpecifyKind(riga.QuandoUtc, DateTimeKind.Utc).ToString("O"),
            riga.HashPrecedente);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    private static DateTime TruncaAlMillisecondo(DateTime valore) =>
        new(valore.Ticks - valore.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

    // ---------- Presentazione ----------

    private static string[] Elenco(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static ManifestGrant Manifest(AiScopeGrantEntity g) => new(
        g.Id, g.PurposeCode, g.Modalita, g.ScopeLevel, g.AziendaId, g.Periodo,
        Elenco(g.RisorseAmmesse), Elenco(g.OperazioniAmmesse), g.AutonomiaMax,
        g.ScadeUtc, g.MaxLetture, g.LettureUsate, g.Stato,
        DescriviGrant(Elenco(g.RisorseAmmesse), Elenco(g.OperazioniAmmesse), g.AziendaId, g.Periodo));

    private static string Descrizione(RichiestaValidata v) =>
        DescriviGrant(v.Risorse, v.Operazioni, v.AziendaId, v.Periodo);

    private static string DescriviGrant(string[] risorse, string[] operazioni, Guid? aziendaId, string? periodo)
    {
        var ambito = aziendaId is null ? "elemento selezionato"
            : periodo is null ? "azienda del mandato"
            : $"azienda del mandato, periodo {periodo}";
        var fara = string.Join(", ", operazioni.Select(o => o switch
        {
            "read" => "lettura",
            "search" => "ricerca",
            "draft" => "bozze",
            _ => o,
        }));
        return $"Leggerò: {string.Join(", ", risorse)} ({ambito}). Farò: {fara}. " +
               "Non farò: invii, pagamenti o modifiche.";
    }

    private static string Riassunto(RichiestaGrant r) =>
        $"{r.PurposeCode} {r.ScopeLevel}/{r.Modalita} risorse=[{string.Join(",", r.Risorse ?? [])}] " +
        $"op=[{string.Join(",", r.Operazioni ?? [])}]";
}
