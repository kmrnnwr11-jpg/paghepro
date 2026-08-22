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
    int? MaxLetture = null,
    int? MaxProposte = null,
    int? MaxEffetti = null,
    IReadOnlyList<RichiestaPermessoAi>? Permessi = null);

/// <summary>Coppia esplicita richiesta dal pannello: niente prodotto cartesiano implicito.</summary>
public sealed record RichiestaPermessoAi(string Risorsa, string Operazione);

public sealed record ManifestGrant(
    Guid GrantId,
    string PurposeCode,
    string Modalita,
    string ScopeLevel,
    Guid? AziendaId,
    string? Periodo,
    string[] Risorse,
    string[] Operazioni,
    string[] Permessi,
    string[] Strumenti,
    string AutonomiaMax,
    DateTime ScadeUtc,
    int MaxLetture,
    int LettureUsate,
    int MaxProposte,
    int ProposteUsate,
    int MaxEffetti,
    int EffettiUsati,
    string Stato,
    string Descrizione);

public sealed record EsitoPreflight(
    bool Consentito,
    string? Motivo,
    string? Descrizione,
    int? DurataMinuti = null,
    int? MaxLetture = null,
    int? MaxProposte = null,
    int? MaxEffetti = null);

public sealed record GrantEmesso(ManifestGrant Manifest, string Lease);

public sealed record EsitoAccessoAi(
    bool Consentito,
    string? Motivo,
    int LettureRimanenti,
    int ProposteRimanenti = 0,
    int EffettiRimanenti = 0);

public sealed record EsitoCatena(bool Integra, int Righe, long? PrimaSequenzaAnomala);

/// <summary>
/// Usage Governor (G2): unica autorità che emette, verifica e revoca i mandati AI e ne tiene
/// l'audit concatenato. Regole del piano §6: il grant nasce da UI e policy (mai dal prompt),
/// ogni accesso ricontrolla firma, tenant, finalità, scope, operazione, quota, scadenza e
/// revoca; un diniego prevale; i contatori sono atomici (token di concorrenza + retry).
/// Nel verticale G2 corrente non esistono scritture: solo read/search su classi D0-D2.
/// </summary>
public sealed class AiGrantService
{
    private readonly PagheDbContext db;
    private readonly AiGovernorOptions opzioni;
    private readonly TimeProvider orologio;
    private readonly AiRolloutPolicy rollout;

    public AiGrantService(
        PagheDbContext db,
        AiGovernorOptions opzioni,
        TimeProvider orologio,
        AiRolloutPolicy? rollout = null)
    {
        this.db = db;
        this.opzioni = opzioni;
        this.orologio = orologio;
        this.rollout = rollout ?? AiRolloutPolicy.SoloA2;
    }
    private const int TentativiConcorrenza = 5;

    // La sequenza audit è contesa da ogni evento del tenant: sotto concorrenza servono
    // più tentativi del contatore del singolo grant.
    private const int TentativiAudit = 50;

    // ---------- Preflight ed emissione ----------

    public async Task<EsitoPreflight> PreflightAsync(
        Guid tenantId, Guid utenteId, RichiestaGrant richiesta, CancellationToken ct = default,
        string? ruolo = null)
    {
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            return new EsitoPreflight(false, "Tenant e utente sono obbligatori.", null);

        if (RichiedeA3(richiesta)
            && (!rollout.ProposteAbilitate(tenantId, utenteId) || !RuoloScrittura(ruolo)))
        {
            const string motivo = "Il collaudo A3 non è abilitato per questa identità e ruolo.";
            await AppendiAuditAsync(tenantId, null, utenteId, "Preflight", Riassunto(richiesta), motivo, ct);
            return new EsitoPreflight(false, motivo, null);
        }
        if (RichiedeEffetti(richiesta) && !rollout.EffettiAbilitati(tenantId, utenteId))
        {
            const string motivo = "Il rollout corrente consente soltanto proposte non confermabili.";
            await AppendiAuditAsync(tenantId, null, utenteId, "Preflight", Riassunto(richiesta), motivo, ct);
            return new EsitoPreflight(false, motivo, null);
        }

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
        return new EsitoPreflight(
            true,
            null,
            Descrizione(valida.Value),
            valida.Value.DurataMinuti,
            valida.Value.MaxLetture,
            valida.Value.MaxProposte,
            valida.Value.MaxEffetti);
    }

    public async Task<(GrantEmesso? Grant, string? Errore)> EmettiAsync(
        Guid tenantId, Guid utenteId, RichiestaGrant richiesta, CancellationToken ct = default,
        string? ruolo = null)
    {
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            return (null, "Tenant e utente sono obbligatori.");

        if (RichiedeA3(richiesta)
            && (!rollout.ProposteAbilitate(tenantId, utenteId) || !RuoloScrittura(ruolo)))
        {
            const string motivo = "Il collaudo A3 non è abilitato per questa identità e ruolo.";
            await AppendiAuditAsync(tenantId, null, utenteId, "Diniego", Riassunto(richiesta), motivo, ct);
            return (null, motivo);
        }
        if (RichiedeEffetti(richiesta) && !rollout.EffettiAbilitati(tenantId, utenteId))
        {
            const string motivo = "Il rollout corrente consente soltanto proposte non confermabili.";
            await AppendiAuditAsync(tenantId, null, utenteId, "Diniego", Riassunto(richiesta), motivo, ct);
            return (null, motivo);
        }

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
        if (RichiedeA3(richiesta))
        {
            var precedenti = await db.AiScopeGrants.Where(g =>
                g.TenantId == tenantId && g.UtenteId == utenteId
                && g.SlotA3Attivo == "ATTIVA").ToListAsync(ct);
            foreach (var precedente in precedenti)
            {
                bool nonSpendibile = precedente.Stato != "Attivo"
                    || adesso >= precedente.ScadeUtc
                    || adesso - precedente.UltimoUsoUtc
                        > TimeSpan.FromMinutes(precedente.TimeoutInattivitaMinuti);
                if (!nonSpendibile) continue;
                precedente.Stato = precedente.Stato == "Attivo" ? "Scaduto" : precedente.Stato;
                precedente.SlotA3Attivo = null;
                precedente.Versione++;
            }
            if (precedenti.Any(g => g.Stato == "Attivo" && g.SlotA3Attivo == "ATTIVA"))
            {
                const string motivo = "Esiste già un mandato A3 attivo: revocalo prima di emetterne un altro.";
                await AppendiAuditAsync(tenantId, null, utenteId, "Diniego", Riassunto(richiesta), motivo, ct);
                return (null, motivo);
            }
            if (precedenti.Any(g => db.Entry(g).State == EntityState.Modified))
                await db.SaveChangesAsync(ct);
        }
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
            PermessiAmmessi = string.Join(",", v.Permessi),
            StrumentiAmmessi = RichiedeA3(richiesta)
                ? $"compila_presenze@{CompilaPresenzeAuroraService.VersioneStrumento}"
                : "",
            SlotA3Attivo = RichiedeA3(richiesta) ? "ATTIVA" : null,
            AutonomiaMax = v.Operazioni.Contains("apply_reversible")
                ? "A3"
                : v.Operazioni.Contains("draft") ? "A2" : "A1",
            PolicyVersione = AiUsagePolicyStore.Versione,
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            CreatoUtc = adesso,
            ScadeUtc = adesso.AddMinutes(v.DurataMinuti),
            TimeoutInattivitaMinuti = v.Modalita == "Stretta" ? 10 : 30,
            UltimoUsoUtc = adesso,
            MaxLetture = v.MaxLetture,
            MaxProposte = v.MaxProposte,
            MaxEffetti = v.MaxEffetti,
        };
        db.AiScopeGrants.Add(grant);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (RichiedeA3(richiesta))
        {
            db.Entry(grant).State = EntityState.Detached;
            if (await db.AiScopeGrants.AsNoTracking().AnyAsync(g =>
                g.TenantId == tenantId && g.UtenteId == utenteId
                && g.SlotA3Attivo == "ATTIVA", ct))
                return (null,
                    "Un altro mandato A3 è appena stato emesso: usa quello già attivo.");
            throw;
        }

        await AppendiAuditAsync(
            tenantId, grant.Id, utenteId, "EmissioneGrant",
            $"{grant.PurposeCode} {grant.ScopeLevel}/{grant.Modalita} risorse=[{grant.RisorseAmmesse}] " +
            $"permessi=[{grant.PermessiAmmessi}] quote=L{grant.MaxLetture}/P{grant.MaxProposte}/E{grant.MaxEffetti} "
            + $"scade={grant.ScadeUtc:O}",
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
        Guid tenantId, Guid utenteId, string? lease, string? risorsa, string? operazione,
        int quantita = 1, CancellationToken ct = default) =>
        (await AutorizzaAccessoConManifestAsync(tenantId, utenteId, lease, risorsa, operazione, quantita, ct)).Esito;

    /// <summary>
    /// Come <see cref="AutorizzaAccessoAsync"/>, ma restituisce anche il manifest del mandato:
    /// il broker ci legge lo scope autorevole (azienda, periodo), mai dai parametri del client.
    /// </summary>
    public async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)> AutorizzaAccessoConManifestAsync(
        Guid tenantId, Guid utenteId, string? lease, string? risorsa, string? operazione,
        int quantita = 1, CancellationToken ct = default)
        => await ValidaAccessoConManifestAsync(
            tenantId, utenteId, lease, risorsa, operazione, quantita, consumaQuota: true, ct);

    /// <summary>
    /// Verifica firma, proprietario, scope e policy senza consumare quota. Il broker la usa prima
    /// della query e consuma poi esattamente il numero di record che sta per restituire.
    /// </summary>
    public async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)> VerificaAccessoConManifestAsync(
        Guid tenantId, Guid utenteId, string? lease, string? risorsa, string? operazione,
        CancellationToken ct = default)
        => await ValidaAccessoConManifestAsync(
            tenantId, utenteId, lease, risorsa, operazione, 1, consumaQuota: false, ct);

    /// <summary>
    /// Vidima tutte le dipendenze di una preparazione A3 e prenota il loro costo in un solo
    /// incremento. Se anche una coppia è negata, la quota resta invariata e nessuna query di
    /// dominio deve partire.
    /// </summary>
    public async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)>
        AutorizzaPacchettoLettureAsync(
            Guid tenantId,
            Guid utenteId,
            string? lease,
            IReadOnlyCollection<string> risorse,
            CancellationToken ct = default)
    {
        var distinte = (risorse ?? [])
            .Select(r => (r ?? "").Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinte.Length == 0)
            return (await NegaAsync(tenantId, null, utenteId, null, "read",
                "Il pacchetto letture è vuoto.", ct), null);

        ManifestGrant? manifest = null;
        foreach (var risorsa in distinte)
        {
            var verifica = await VerificaAccessoConManifestAsync(
                tenantId, utenteId, lease, risorsa, "read", ct);
            if (!verifica.Esito.Consentito) return verifica;
            if (manifest is not null && verifica.Manifest?.GrantId != manifest.GrantId)
                return (await NegaAsync(tenantId, manifest.GrantId, utenteId, risorsa, "read",
                    "Il pacchetto non risolve allo stesso mandato.", ct), null);
            manifest = verifica.Manifest;
        }

        // Il grant è immutabile salvo stato e contatori: dopo aver provato tutte le coppie,
        // una sola prenotazione ricontrolla ancora lease/revoca/policy e addebita l'intero lotto.
        return await AutorizzaAccessoConManifestAsync(
            tenantId, utenteId, lease, distinte[0], "read", distinte.Length, ct);
    }

    private async Task<(EsitoAccessoAi Esito, ManifestGrant? Manifest)> ValidaAccessoConManifestAsync(
        Guid tenantId, Guid utenteId, string? lease, string? risorsa, string? operazione,
        int quantita, bool consumaQuota, CancellationToken ct)
    {
        var adesso = orologio.GetUtcNow().UtcDateTime;

        if ((operazione ?? "").Trim().ToLowerInvariant() is "draft" or "apply_reversible")
            return (await NegaAsync(tenantId, null, utenteId, risorsa, operazione,
                "Proposte ed effetti passano soltanto dal protocollo firmato A3.", ct), null);

        var decodificata = AiLeaseCodec.Prova(lease, opzioni.ChiaveLease);
        if (decodificata is null)
            return (await NegaAsync(tenantId, null, utenteId, risorsa, operazione, "Lease assente o firma non valida.", ct), null);
        if (adesso >= decodificata.ExpiresAtUtc)
            return (await NegaAsync(tenantId, decodificata.GrantId, utenteId, risorsa, operazione, "Lease scaduta.", ct), null);

        // Il confine tenant/owner deve stare nel predicato SQL: non materializziamo mai un
        // mandato appartenente a un altro contesto per poi respingerlo in memoria.
        var queryGrant = db.AiScopeGrants.Where(g =>
            g.Id == decodificata.GrantId
            && g.TenantId == tenantId
            && g.UtenteId == utenteId);
        // La vidimazione senza consumo deve interrogare sempre il database: dopo una
        // prenotazione il DbContext può avere ancora in memoria il grant Attivo, mentre una
        // richiesta concorrente lo ha già revocato. AsNoTracking chiude questa finestra.
        var grant = consumaQuota
            ? await queryGrant.FirstOrDefaultAsync(ct)
            : await queryGrant.AsNoTracking().FirstOrDefaultAsync(ct);
        if (grant is null)
            return (await NegaAsync(tenantId, decodificata.GrantId, utenteId, risorsa, operazione,
                "Mandato non trovato per questo utente.", ct), null);
        if (!string.Equals(grant.Nonce, decodificata.Nonce, StringComparison.Ordinal))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Nonce non valido (possibile replay).", ct), null);
        if (grant.Stato != "Attivo")
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, $"Mandato {grant.Stato.ToLowerInvariant()}.", ct), null);
        if (adesso >= grant.ScadeUtc)
        {
            if (consumaQuota)
            {
                grant.Stato = "Scaduto";
                grant.SlotA3Attivo = null;
                grant.Versione++;
            }
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
        var permessoEsatto = $"{risorsaNorm}|{operazioneNorm}";
        if (!Elenco(grant.PermessiAmmessi).Contains(permessoEsatto, StringComparer.OrdinalIgnoreCase))
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione,
                $"La coppia '{risorsaNorm}:{operazioneNorm}' è fuori dal mandato.", ct), null);

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
        var capability = AiCapabilityRegistry.VerificaPermesso(
            grant.PurposeCode, grant.Modalita, risorsaNorm, operazioneNorm);
        if (!capability.Consentito)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione,
                capability.Motivo ?? "Capability non disponibile.", ct), null);

        if (!consumaQuota)
            return (EsitoConsentito(grant), Manifest(grant));

        if (quantita < 0)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Quantità non valida.", ct), null);
        if (operazioneNorm is "draft" or "apply_reversible" && quantita != 1)
            return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione,
                "Proposte ed effetti consumano esattamente una unità.", ct), null);

        // Contatore atomico: token di concorrenza + retry; la quota non si aggira in parallelo.
        for (var tentativo = 0; tentativo < TentativiConcorrenza; tentativo++)
        {
            var (massimo, usate) = Quota(grant, operazioneNorm);
            // Sottrazione invece della somma: evita l'overflow di int con quantità ostili.
            if (usate < 0 || usate > massimo || quantita > massimo - usate)
                return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Quota del mandato esaurita: checkpoint, serve un nuovo grant.", ct), null);

            IncrementaQuota(grant, operazioneNorm, quantita);
            grant.UltimoUsoUtc = adesso;
            grant.Versione++;
            try
            {
                await db.SaveChangesAsync(ct);
                await AppendiAuditAsync(tenantId, grant.Id, utenteId, "Accesso", $"{risorsaNorm}:{operazioneNorm} x{quantita}", null, ct);
                return (EsitoConsentito(grant), Manifest(grant));
            }
            catch (DbUpdateConcurrencyException)
            {
                // ReloadAsync usa soltanto la PK. Stacchiamo invece l'istanza e rileggiamo
                // applicando ancora tenant e proprietario nel predicato SQL.
                db.Entry(grant).State = EntityState.Detached;
                grant = await db.AiScopeGrants.FirstOrDefaultAsync(g =>
                    g.Id == decodificata.GrantId
                    && g.TenantId == tenantId
                    && g.UtenteId == utenteId, ct);
                if (grant is null)
                    return (await NegaAsync(tenantId, decodificata.GrantId, utenteId,
                        risorsa, operazione, "Mandato non trovato per questo utente.", ct), null);
                if (grant.Stato != "Attivo")
                    return (await NegaAsync(tenantId, grant.Id, utenteId, risorsa, operazione, "Mandato revocato durante l'accesso.", ct), null);
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
            .FirstOrDefaultAsync(g => g.Id == decodificata.GrantId
                && g.TenantId == tenantId
                && g.UtenteId == utenteId, ct);
        if (grant is null)
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

    /// <summary>Prenota la quota proposta dentro la stessa transazione che la persiste.</summary>
    public Task<EsitoAccessoAi> AutorizzaPropostaAsync(
        Guid tenantId,
        Guid utenteId,
        Guid grantId,
        Guid aziendaId,
        string periodo,
        string risorsa,
        string? ruolo,
        CancellationToken ct = default) =>
        AutorizzaScritturaPropostaAsync(
            tenantId, utenteId, grantId, aziendaId, periodo, risorsa,
            "draft", ruolo, richiedeEffetto: false, ct);

    /// <summary>
    /// Riverifica e prenota un effetto dalla proposta firmata. Non accetta una lease dal
    /// browser: grant, scope e permesso sono quelli già legati alla proposta server-side.
    /// Il chiamante deve avere aperto la stessa transazione dell'effetto di dominio.
    /// </summary>
    public async Task<EsitoAccessoAi> AutorizzaEffettoPropostaAsync(
        Guid tenantId,
        Guid utenteId,
        Guid grantId,
        Guid aziendaId,
        string periodo,
        string risorsa,
        string operazione,
        string? ruolo,
        CancellationToken ct = default)
        => await AutorizzaScritturaPropostaAsync(
            tenantId, utenteId, grantId, aziendaId, periodo, risorsa,
            operazione, ruolo, richiedeEffetto: true, ct);

    private async Task<EsitoAccessoAi> AutorizzaScritturaPropostaAsync(
        Guid tenantId,
        Guid utenteId,
        Guid grantId,
        Guid aziendaId,
        string periodo,
        string risorsa,
        string operazione,
        string? ruolo,
        bool richiedeEffetto,
        CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Il protocollo A3 richiede la transazione condivisa con proposta ed effetto.");

        var op = (operazione ?? "").Trim().ToLowerInvariant();
        var resource = (risorsa ?? "").Trim();
        var operazioneAttesa = richiedeEffetto ? "apply_reversible" : "draft";
        if (op != operazioneAttesa)
            return await NegaAsync(tenantId, grantId, utenteId, resource, op,
                $"Il protocollo richiede l'operazione {operazioneAttesa}.", ct);
        if (!RuoloScrittura(ruolo))
            return await NegaAsync(tenantId, grantId, utenteId, resource, op,
                "Il ruolo corrente non può usare operazioni A3.", ct);
        if (richiedeEffetto
            ? !rollout.EffettiAbilitati(tenantId, utenteId)
            : !rollout.ProposteAbilitate(tenantId, utenteId))
            return await NegaAsync(tenantId, grantId, utenteId, resource, op,
                richiedeEffetto
                    ? "Il kill-switch A3 non consente effetti in questo momento."
                    : "Il rollout A3 non consente proposte in questo momento.", ct);

        var adesso = orologio.GetUtcNow().UtcDateTime;
        var grant = await db.AiScopeGrants.FirstOrDefaultAsync(g =>
            g.Id == grantId && g.TenantId == tenantId && g.UtenteId == utenteId, ct);
        if (grant is null)
            return await NegaAsync(tenantId, grantId, utenteId, resource, op,
                "Mandato non trovato per questo utente.", ct);
        if (grant.Stato != "Attivo")
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                $"Mandato {grant.Stato.ToLowerInvariant()}.", ct);
        if (adesso >= grant.ScadeUtc
            || adesso - grant.UltimoUsoUtc > TimeSpan.FromMinutes(grant.TimeoutInattivitaMinuti))
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                "Mandato scaduto o inattivo.", ct);
        if (grant.PolicyVersione != AiUsagePolicyStore.Versione)
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                "Policy del mandato superata: serve un nuovo grant.", ct);
        if (grant.AziendaId != aziendaId
            || !string.Equals(grant.Periodo, periodo, StringComparison.Ordinal))
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                "Scope della proposta diverso dal mandato.", ct);
        if (!Elenco(grant.PermessiAmmessi).Contains(
                $"{resource}|{op}", StringComparer.OrdinalIgnoreCase))
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                "Effetto fuori dai permessi esatti del mandato.", ct);

        var policy = AiUsagePolicyStore.Trova(grant.PurposeCode);
        var capability = AiCapabilityRegistry.VerificaPermesso(
            grant.PurposeCode, grant.Modalita, resource, op);
        if (policy is null
            || !policy.RisorseAmmesse.Contains(resource, StringComparer.OrdinalIgnoreCase)
            || !policy.OperazioniAmmesse.Contains(op, StringComparer.OrdinalIgnoreCase)
            || !capability.Consentito)
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                capability.Motivo ?? "Effetto non più ammesso dalla policy.", ct);

        var (massimo, usate) = Quota(grant, op);
        if (usate < 0 || massimo < 1 || usate >= massimo)
            return await NegaAsync(tenantId, grant.Id, utenteId, resource, op,
                $"Quota {(richiedeEffetto ? "effetti" : "proposte")} del mandato esaurita: serve un nuovo grant.", ct);

        IncrementaQuota(grant, op, 1);
        grant.UltimoUsoUtc = adesso;
        grant.Versione++;
        await db.SaveChangesAsync(ct);
        await AppendiAuditAsync(tenantId, grant.Id, utenteId,
            richiedeEffetto ? "EffettoConfermato" : "PropostaCreata",
            $"{resource}:{op} x1", null, ct);
        return EsitoConsentito(grant);
    }

    /// <summary>
    /// Registra un evento di lifecycle proposta nella transazione del chiamante. Non contiene
    /// mai comando, diff, nominativi o dati del cartellino.
    /// </summary>
    public async Task RegistraEventoPropostaAsync(
        Guid tenantId, Guid utenteId, Guid? grantId, Guid propostaId,
        string evento, string? motivo, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "L'audit della proposta deve condividere la transazione del lifecycle A3.");
        await AppendiAuditAsync(
            tenantId, grantId, utenteId, evento,
            $"proposta={propostaId:N}", motivo, ct);
    }

    /// <summary>
    /// Restituisce al mandato una prenotazione che il broker non ha potuto consegnare
    /// (cancellazione concorrente, errore o revoca prima dell'egress). Il rimborso è protetto
    /// dallo stesso token di concorrenza della quota e non può oltrepassare le letture usate.
    /// </summary>
    public async Task RimborsaLettureAsync(
        Guid tenantId, Guid utenteId, Guid grantId, int quantita,
        CancellationToken ct = default)
    {
        if (quantita <= 0) return;

        var grant = await db.AiScopeGrants.FirstOrDefaultAsync(g =>
            g.Id == grantId && g.TenantId == tenantId && g.UtenteId == utenteId, ct);
        if (grant is null) return;

        for (var tentativo = 0; tentativo < TentativiConcorrenza; tentativo++)
        {
            var rimborso = Math.Min(quantita, Math.Max(0, grant.LettureUsate));
            if (rimborso == 0) return;
            grant.LettureUsate -= rimborso;
            grant.Versione++;
            try
            {
                await db.SaveChangesAsync(ct);
                await AppendiAuditAsync(tenantId, grant.Id, utenteId, "Rimborso",
                    $"letture non consegnate x{rimborso}", null, ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                db.Entry(grant).State = EntityState.Detached;
                grant = await db.AiScopeGrants.FirstOrDefaultAsync(g =>
                    g.Id == grantId && g.TenantId == tenantId && g.UtenteId == utenteId, ct);
                if (grant is null) return;
            }
        }

        throw new InvalidOperationException(
            "Impossibile riconciliare la quota del mandato dopo conflitti concorrenti.");
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
                    .SetProperty(g => g.SlotA3Attivo, (string?)null)
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
                .SetProperty(g => g.SlotA3Attivo, (string?)null)
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
        string[] Permessi, int DurataMinuti, int MaxLetture, int MaxProposte, int MaxEffetti,
        Guid? AziendaId, string? Periodo, string ScopeLevel);

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

        if (r.Operazioni is null || r.Operazioni.Count == 0)
            return (null, "Il mandato deve elencare almeno un'operazione.");
        var operazioni = r.Operazioni.Select(x => (x ?? "").Trim().ToLowerInvariant())
            .Where(x => x.Length > 0).Distinct().ToArray();
        foreach (var op in operazioni)
            if (!policy.OperazioniAmmesse.Contains(op))
                return (null, $"Operazione '{op}' non ammessa dalla finalità '{policy.PurposeCode}'.");
        if (operazioni.Any(op => op is "draft" or "apply_reversible")
            && r.Permessi is not { Count: > 0 })
            return (null,
                "Le operazioni A3 richiedono permessi espliciti risorsa/operazione; "
                + "il prodotto cartesiano è vietato.");

        var permessi = r.Permessi is { Count: > 0 }
            ? r.Permessi.Select(p => new
                {
                    Risorsa = (p.Risorsa ?? "").Trim(),
                    Operazione = (p.Operazione ?? "").Trim().ToLowerInvariant(),
                })
                .Where(p => p.Risorsa.Length > 0 && p.Operazione.Length > 0)
                .Select(p => $"{p.Risorsa}|{p.Operazione}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : risorse.SelectMany(risorsa => operazioni.Select(op => $"{risorsa}|{op}"))
                .ToArray();
        if (permessi.Length == 0)
            return (null, "Il mandato deve contenere almeno un permesso risorsa/operazione.");
        foreach (var permesso in permessi)
        {
            var parti = permesso.Split('|', 2);
            var risorsa = parti[0];
            var operazione = parti[1];
            if (!risorse.Contains(risorsa, StringComparer.OrdinalIgnoreCase)
                || !operazioni.Contains(operazione, StringComparer.OrdinalIgnoreCase))
                return (null, $"Il permesso '{risorsa}:{operazione}' non coincide con risorse e operazioni dichiarate.");
            var capability = AiCapabilityRegistry.VerificaPermesso(
                policy.PurposeCode, modalita, risorsa, operazione);
            if (!capability.Consentito)
                return (null, capability.Motivo);
        }
        if (risorse.Any(risorsa => !permessi.Any(p =>
                p.StartsWith($"{risorsa}|", StringComparison.OrdinalIgnoreCase))))
            return (null, "Ogni risorsa dichiarata deve avere almeno un permesso esplicito.");
        if (operazioni.Any(op => !permessi.Any(p =>
                p.EndsWith($"|{op}", StringComparison.OrdinalIgnoreCase))))
            return (null, "Ogni operazione dichiarata deve avere almeno un permesso esplicito.");

        var durata = r.DurataMinuti ?? policy.TtlMassimoMinuti;
        if (durata < 1) return (null, "Durata non valida.");
        durata = Math.Min(durata, policy.TtlMassimoMinuti);
        if (modalita == "Stretta") durata = Math.Min(durata, 30);

        var maxLetture = r.MaxLetture ?? policy.QuotaLettureDefault;
        if (maxLetture < 1) return (null, "Quota letture non valida.");
        maxLetture = Math.Min(maxLetture, policy.QuotaLettureMassima);

        int maxProposte = 0;
        if (operazioni.Contains("draft"))
        {
            maxProposte = r.MaxProposte ?? policy.QuotaProposteDefault;
            if (maxProposte < 1 || policy.QuotaProposteMassima < 1)
                return (null, "Quota proposte non disponibile per questa finalità.");
            maxProposte = Math.Min(maxProposte, policy.QuotaProposteMassima);
        }

        int maxEffetti = 0;
        if (operazioni.Contains("apply_reversible"))
        {
            maxEffetti = r.MaxEffetti ?? policy.QuotaEffettiDefault;
            if (maxEffetti < 1 || policy.QuotaEffettiMassima < 1)
                return (null, "Quota effetti non disponibile per questa finalità.");
            maxEffetti = Math.Min(maxEffetti, policy.QuotaEffettiMassima);
        }

        return (new RichiestaValidata(
            policy, modalita, risorse, operazioni, permessi, durata, maxLetture,
            maxProposte, maxEffetti,
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

    private static (int Massimo, int Usate) Quota(AiScopeGrantEntity grant, string operazione) =>
        operazione switch
        {
            "draft" => (grant.MaxProposte, grant.ProposteUsate),
            "apply_reversible" => (grant.MaxEffetti, grant.EffettiUsati),
            _ => (grant.MaxLetture, grant.LettureUsate),
        };

    private static void IncrementaQuota(
        AiScopeGrantEntity grant, string operazione, int quantita)
    {
        switch (operazione)
        {
            case "draft": grant.ProposteUsate += quantita; break;
            case "apply_reversible": grant.EffettiUsati += quantita; break;
            default: grant.LettureUsate += quantita; break;
        }
    }

    private static EsitoAccessoAi EsitoConsentito(AiScopeGrantEntity grant) => new(
        true,
        null,
        Math.Max(0, grant.MaxLetture - grant.LettureUsate),
        Math.Max(0, grant.MaxProposte - grant.ProposteUsate),
        Math.Max(0, grant.MaxEffetti - grant.EffettiUsati));

    // ---------- Presentazione ----------

    private static string[] Elenco(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static ManifestGrant Manifest(AiScopeGrantEntity g) => new(
        g.Id, g.PurposeCode, g.Modalita, g.ScopeLevel, g.AziendaId, g.Periodo,
        Elenco(g.RisorseAmmesse), Elenco(g.OperazioniAmmesse), Elenco(g.PermessiAmmessi),
        Elenco(g.StrumentiAmmessi),
        g.AutonomiaMax, g.ScadeUtc, g.MaxLetture, g.LettureUsate,
        g.MaxProposte, g.ProposteUsate, g.MaxEffetti, g.EffettiUsati, g.Stato,
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
            "apply_reversible" => "modifiche reversibili solo dopo conferma",
            _ => o,
        }));
        bool modifica = operazioni.Contains("apply_reversible", StringComparer.OrdinalIgnoreCase);
        return (modifica ? "Dati coinvolti: " : "Leggerò: ")
            + $"{string.Join(", ", risorse)} ({ambito}). Farò: {fara}. "
            + (modifica
                ? "Ogni modifica richiede anteprima e conferma esplicita; non farò invii o pagamenti."
                : "Non farò: invii, pagamenti o modifiche.");
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
        return nome is "read" or "search" or "draft" or "apply_reversible"
            ? nome
            : "OPERAZIONE_NEGATA";
    }

    private static bool RichiedeA3(RichiestaGrant richiesta) =>
        (richiesta.Operazioni ?? []).Any(op =>
            string.Equals(op?.Trim(), "draft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(op?.Trim(), "apply_reversible", StringComparison.OrdinalIgnoreCase));

    private static bool RichiedeEffetti(RichiestaGrant richiesta) =>
        (richiesta.Operazioni ?? []).Any(op =>
            string.Equals(op?.Trim(), "apply_reversible", StringComparison.OrdinalIgnoreCase));

    private static bool RuoloScrittura(string? ruolo) =>
        string.Equals(ruolo, "operatore", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ruolo, "admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ruolo, "superadmin", StringComparison.OrdinalIgnoreCase);

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
