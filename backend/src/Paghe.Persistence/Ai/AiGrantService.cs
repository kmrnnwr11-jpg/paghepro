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
/// Nel verticale G2 corrente non esistono scritture: solo read/search su classi D0-D2.
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
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            return new EsitoPreflight(false, "Tenant e utente sono obbligatori.", null);

        var (valida, errore) = Valida(richiesta);
        if (valida is null)
        {
            await AppendiAuditAsync(tenantId, null, utenteId, "Preflight", Riassunto(richiesta), errore, ct);
            return new EsitoPreflight(false, errore, null);
        }

        if (!await AziendaNelTenantAsync(tenantId, valida.Value.AziendaId, ct))
        {
            const string motivo = "Azienda del mandato non trovata nel tenant corrente.";
            await AppendiAuditAsync(tenantId, null, utenteId, "Preflight", Riassunto(richiesta), motivo, ct);
            return new EsitoPreflight(false, motivo, null);
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
        if (!await AziendaNelTenantAsync(tenantId, v.AziendaId, ct))
        {
            const string motivo = "Azienda del mandato non trovata nel tenant corrente.";
            await AppendiAuditAsync(tenantId, null, utenteId, "Diniego", Riassunto(richiesta), motivo, ct);
            return (null, motivo);
        }
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
        => await ValidaAccessoConManifestAsync(
            tenantId, utenteId, lease, risorsa, operazione, quantita, consumaQuota: true, ct);

    /// <summary>
    /// Verifica firma, proprietario, scope e policy senza consumare quota. Il broker la usa prima
    /// della query e consuma poi esattamente il numero di record che sta per restituire.
    /// </summary>
    public async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)> VerificaAccessoConManifestAsync(
        Guid tenantId, Guid utenteId, string? lease, string risorsa, string operazione,
        CancellationToken ct = default)
        => await ValidaAccessoConManifestAsync(
            tenantId, utenteId, lease, risorsa, operazione, 1, consumaQuota: false, ct);

    private async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)> ValidaAccessoConManifestAsync(
        Guid tenantId, Guid utenteId, string? lease, string risorsa, string operazione,
        int quantita, bool consumaQuota, CancellationToken ct)
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
        if (grant.UtenteId != utenteId)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Utente non corrispondente al mandato.", ct), null);
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

        // Una lease resta valida solo entro la policy versionata della finalità. Questo impedisce
        // di usare un purpose innocuo per ottenere risorse non previste (purpose laundering).
        var policy = AiUsagePolicyStore.Trova(grant.PurposeCode);
        if (grant.PolicyVersione != AiUsagePolicyStore.Versione)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione,
                "Policy del mandato superata: serve un nuovo grant.", ct), null);
        if (policy is null || !policy.RisorseAmmesse.Contains(risorsaNorm, StringComparer.OrdinalIgnoreCase))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, $"Risorsa '{risorsa}' non ammessa dalla finalità '{grant.PurposeCode}'.", ct), null);
        if (!policy.OperazioniAmmesse.Contains(operazioneNorm, StringComparer.OrdinalIgnoreCase))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione,
                $"Operazione '{operazioneNorm}' non ammessa dalla finalità '{grant.PurposeCode}'.", ct), null);
        var capability = AiCapabilityRegistry.VerificaUsoG2(
            grant.PurposeCode, grant.Modalita, [risorsaNorm]);
        if (!capability.Consentito)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione,
                capability.Motivo ?? "Capability non disponibile.", ct), null);

        if (!consumaQuota)
            return (new EsitoAccessoAi(true, null, grant.MaxLetture - grant.LettureUsate), Manifest(grant));

        if (quantita < 0)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Quantità non valida.", ct), null);

        // Contatore atomico: token di concorrenza + retry; la quota non si aggira in parallelo.
        for (var tentativo = 0; tentativo < TentativiConcorrenza; tentativo++)
        {
            // Sottrazione invece della somma: evita l'overflow di int con quantità ostili.
            if (grant.LettureUsate < 0 || grant.LettureUsate > grant.MaxLetture
                || quantita > grant.MaxLetture - grant.LettureUsate)
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
                if (grant.UtenteId != utenteId)
                    return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Utente non corrispondente al mandato.", ct), null);
            }
        }
        return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Conflitto di concorrenza persistente.", ct), null);
    }

    /// <summary>
    /// Risolve la lease di una sessione chat nel manifest del mandato: stesse verifiche di
    /// integrità di <see cref="AutorizzaAccessoAsync"/> (firma, proprietario, nonce, stato,
    /// scadenza, inattività) ma senza risorsa/operazione, senza quota e senza righe di audit:
    /// ogni accesso reale ai dati passa comunque dal broker, che vidima e appende alla catena.
    /// </summary>
    public async Task<(ManifestGrant? Manifest, string? Motivo)> RisolviSessioneAsync(
        Guid tenantId, Guid utenteId, string? lease, CancellationToken ct = default)
    {
        var adesso = orologio.GetUtcNow().UtcDateTime;
        var decodificata = AiLeaseCodec.Prova(lease, opzioni.ChiaveLease);
        if (decodificata is null)
            return (null, "Lease assente o firma non valida.");
        if (adesso >= decodificata.ExpiresAtUtc)
            return (null, "Lease scaduta.");

        var grant = await db.AiScopeGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == decodificata.GrantId, ct);
        if (grant is null || grant.TenantId != tenantId || grant.UtenteId != utenteId)
            return (null, "Mandato non trovato per questo utente.");
        if (!string.Equals(grant.Nonce, decodificata.Nonce, StringComparison.Ordinal))
            return (null, "Nonce non valido (possibile replay).");
        if (grant.Stato != "Attivo")
            return (null, $"Mandato {grant.Stato.ToLowerInvariant()}.");
        if (adesso >= grant.ScadeUtc)
            return (null, "Mandato scaduto.");
        if (adesso - grant.UltimoUsoUtc > TimeSpan.FromMinutes(grant.TimeoutInattivitaMinuti))
            return (null, "Mandato inattivo oltre il limite.");
        if (grant.PolicyVersione != AiUsagePolicyStore.Versione)
            return (null, "Policy del mandato superata: serve un nuovo grant.");

        return (Manifest(grant), null);
    }

    // ---------- Revoca ----------

    public async Task<bool> RevocaAsync(Guid tenantId, Guid utenteId, Guid grantId, CancellationToken ct = default)
    {
        var grant = await db.AiScopeGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId && g.UtenteId == utenteId, ct);
        if (grant is null) return false;

        if (grant.Stato == "Attivo")
        {
            var aggiornati = await db.AiScopeGrants
                .Where(g => g.Id == grantId && g.TenantId == tenantId
                    && g.UtenteId == utenteId && g.Stato == "Attivo")
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(g => g.Stato, "Revocato")
                    .SetProperty(g => g.RevocatoUtc, orologio.GetUtcNow().UtcDateTime)
                    .SetProperty(g => g.Versione, g => g.Versione + 1), ct);
            StaccaGrantLocale(grantId);
            if (aggiornati > 0)
                await AppendiAuditAsync(tenantId, grant.Id, utenteId, "Revoca", grant.PurposeCode, null, ct);
        }
        return true;
    }

    /// <summary>«Arresta AI»: revoca immediata di tutti i mandati attivi dell'utente sul tenant.</summary>
    public async Task<int> ArrestaTuttoAsync(Guid tenantId, Guid utenteId, CancellationToken ct = default)
    {
        var adesso = orologio.GetUtcNow().UtcDateTime;
        var revocati = await db.AiScopeGrants
            .Where(g => g.TenantId == tenantId && g.UtenteId == utenteId && g.Stato == "Attivo")
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(g => g.Stato, "Revocato")
                .SetProperty(g => g.RevocatoUtc, adesso)
                .SetProperty(g => g.Versione, g => g.Versione + 1), ct);
        foreach (var locale in db.AiScopeGrants.Local
            .Where(g => g.TenantId == tenantId && g.UtenteId == utenteId).ToArray())
            db.Entry(locale).State = EntityState.Detached;
        if (revocati > 0)
        {
            await AppendiAuditAsync(tenantId, null, utenteId, "ArrestoTotale", $"revocati={revocati}", null, ct);
        }
        return revocati;
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
        Guid tenantId, Guid utenteId, Guid grantId, CancellationToken ct = default)
    {
        var grant = await db.AiScopeGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == grantId && g.TenantId == tenantId && g.UtenteId == utenteId, ct);
        if (grant is null) return null;

        var eventi = await db.AiAudit.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.GrantId == grantId && a.UtenteId == utenteId)
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
        if (r.ScopeLevel == "S0")
            return (null, "Scope S0 sospeso finché il mandato non include selettori di record verificabili.");
        if (modalita == "Stretta" && !AiUsagePolicyStore.ScopeConsentito(r.ScopeLevel, "S1"))
            return (null, "La modalità Stretta ammette al massimo lo scope S1.");
        if (r.ScopeLevel is "S1" or "S2" && r.AziendaId is null)
            return (null, "Scope S1/S2 richiede l'azienda del mandato.");
        if (r.ScopeLevel == "S1" && !string.IsNullOrWhiteSpace(r.Periodo))
            return (null, "Lo scope S1 non può includere un periodo: usare S2.");
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
            if (!policy.RisorseAmmesse.Contains(nome, StringComparer.OrdinalIgnoreCase))
                return (null, $"Risorsa '{nome}' non ammessa dalla finalità '{policy.PurposeCode}'.");
        }
        if (risorse.Any(nome => nome.Equals("Cartellini", StringComparison.OrdinalIgnoreCase)
                || nome.Equals("Cedolini", StringComparison.OrdinalIgnoreCase))
            && r.ScopeLevel != "S2")
            return (null, "Cartellini e cedolini richiedono scope S2 con azienda e periodo.");

        var capability = AiCapabilityRegistry.VerificaUsoG2(policy.PurposeCode, modalita, risorse);
        if (!capability.Consentito)
            return (null, capability.Motivo);

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
            $"{RisorsaPerAudit(risorsa)}:{OperazionePerAudit(operazione)}", motivo, ct);
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
        $"{(AiUsagePolicyStore.Trova(r.PurposeCode) is null ? "PURPOSE_NEGATO" : r.PurposeCode.Trim())} " +
        $"{(r.ScopeLevel is "S0" or "S1" or "S2" ? r.ScopeLevel : "SCOPE_NEGATO")}/" +
        $"{(r.Modalita is "Stretta" or "Guidata" ? r.Modalita : "MODALITA_NEGATA")} " +
        $"risorse=[{string.Join(",", (r.Risorse ?? []).Select(RisorsaPerAudit))}] " +
        $"op=[{string.Join(",", (r.Operazioni ?? []).Select(OperazionePerAudit))}]";

    private static string RisorsaPerAudit(string? risorsa)
    {
        string nome = (risorsa ?? "").Trim();
        return ProfessionalResourceCatalog.Classifica(nome)?.Nome ?? "RISORSA_NEGATA";
    }

    private static string OperazionePerAudit(string? operazione)
    {
        string nome = (operazione ?? "").Trim().ToLowerInvariant();
        return nome is "read" or "search" or "draft" ? nome : "OPERAZIONE_NEGATA";
    }

    private async Task<bool> AziendaNelTenantAsync(
        Guid tenantId, Guid? aziendaId, CancellationToken ct) =>
        aziendaId is null || await db.Aziende.AsNoTracking()
            .AnyAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);

    private void StaccaGrantLocale(Guid grantId)
    {
        var locale = db.AiScopeGrants.Local.FirstOrDefault(g => g.Id == grantId);
        if (locale is not null) db.Entry(locale).State = EntityState.Detached;
    }
}
