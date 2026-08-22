using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;

namespace Paghe.Persistence.Ai;

/// <summary>Chiave di firma dei token di conferma delle proposte (derivata dalla chiave JWT).</summary>
public sealed record AiPropostaOptions(byte[] ChiaveToken);

/// <summary>Una riga del diff mostrato all'utente prima della conferma.</summary>
public sealed record RigaDiffAurora(string Etichetta, string? Da, string? A);

public sealed record EsitoConfermaAurora(string Stato, string? Messaggio, string? EsitoJson);

public sealed record NuovaPropostaAurora(
    Guid TenantId,
    Guid UtenteId,
    Guid GrantId,
    Guid? RunId,
    string Strumento,
    string VersioneStrumento,
    string Risorsa,
    string Operazione,
    Guid AziendaId,
    string Periodo,
    string ComandoCanonicoJson,
    string UndoJson,
    string PrecondizioneHash,
    string PostcondizioneHash,
    string Titolo,
    IReadOnlyList<RigaDiffAurora> Diff,
    bool Confermabile);

public sealed record MetadatiPropostaAurora(
    Guid PropostaId,
    Guid GrantId,
    string Strumento,
    string VersioneStrumento,
    string Risorsa,
    string Operazione,
    Guid AziendaId,
    string Periodo);

public sealed record EsitoOperazionePropostaAurora(
    Guid? PropostaId,
    string Stato,
    string Messaggio,
    string? EsitoJson,
    string? TokenUndo = null);

public sealed record StatoPropostaAurora(
    Guid Id,
    string Stato,
    string Strumento,
    string Titolo,
    IReadOnlyList<RigaDiffAurora> Diff,
    DateTime ScadeUtc,
    DateTime? ApplicataUtc,
    DateTime? RipristinataUtc,
    bool PuoAnnullare,
    bool PuoRipristinare,
    string? EsitoJson,
    string? TokenUndo);

public sealed class ConflittoPropostaAuroraException(string messaggio)
    : InvalidOperationException(messaggio);

public sealed class PropostaAuroraNegataException(string messaggio)
    : InvalidOperationException(messaggio);

/// <summary>
/// SessionLog e protocollo delle proposte Aurora. La conferma e l'undo non committano mai
/// stati intermedi: proposta, quota, audit ed effetto di dominio condividono la stessa
/// transazione serializable. I payload restano cifrati e il token firma l'intero intento.
/// </summary>
public sealed class AiRuntimeStore(PagheDbContext db, AiPropostaOptions opzioni, TimeProvider orologio)
{
    public static readonly TimeSpan DurataProposta = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DurataUndo = TimeSpan.FromHours(24);
    private const string SlotAttivo = "ATTIVA";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- SessionLog ----------

    public async Task<AiRunEntity> ApriRunAsync(
        Guid tenantId, Guid utenteId, Guid? grantId, string modello, string? sezioneUi,
        CancellationToken ct = default)
    {
        var run = new AiRunEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UtenteId = utenteId,
            GrantId = grantId,
            SezioneUi = sezioneUi,
            Modello = modello,
            InizioUtc = orologio.GetUtcNow().UtcDateTime,
            Esito = "InCorso",
        };
        db.AiRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task ChiudiRunAsync(
        AiRunEntity run, string esito, string? motivo, int giri, int? tokenTotali,
        string digestMessaggi, CancellationToken ct = default)
    {
        run.FineUtc = orologio.GetUtcNow().UtcDateTime;
        run.Esito = esito;
        run.Motivo = motivo is { Length: > 300 } ? motivo[..300] : motivo;
        run.Giri = giri;
        run.TokenTotali = tokenTotali;
        run.DigestMessaggi = digestMessaggi;
        await db.SaveChangesAsync(ct);
    }

    public async Task RegistraToolCallAsync(
        AiRunEntity run, int ordine, string strumento, string argomentiHash, string esito,
        string? motivo, int durataMs, int righe, CancellationToken ct = default)
    {
        db.AiToolCalls.Add(new AiToolCallEntity
        {
            Id = Guid.NewGuid(),
            TenantId = run.TenantId,
            RunId = run.Id,
            Ordine = ordine,
            Strumento = strumento.Length <= 100 ? strumento : strumento[..100],
            ArgomentiHash = argomentiHash,
            Esito = esito,
            Motivo = motivo is { Length: > 300 } ? motivo[..300] : motivo,
            DurataMs = durataMs,
            Righe = righe,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Creazione proposta ----------

    public async Task<(AiPropostaEntity Proposta, string? Token)> CreaPropostaGovernataAsync(
        NuovaPropostaAurora richiesta,
        Func<CancellationToken, Task<EsitoAccessoAi>> prenotaQuotaProposta,
        CancellationToken ct = default)
    {
        ValidaNuova(richiesta);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("La creazione proposta richiede un confine transazionale proprio.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var adesso = NormalizzaUtc(orologio.GetUtcNow().UtcDateTime);
            var scadute = await db.AiProposte.Where(p =>
                p.TenantId == richiesta.TenantId && p.GrantId == richiesta.GrantId
                && p.SlotAttivo == SlotAttivo && p.ScadeUtc <= adesso).ToListAsync(ct);
            foreach (var scaduta in scadute)
            {
                scaduta.Stato = "Scaduta";
                scaduta.SlotAttivo = null;
                scaduta.Versione++;
            }
            if (scadute.Count > 0) await db.SaveChangesAsync(ct);

            if (richiesta.Confermabile && await db.AiProposte.AsNoTracking().AnyAsync(p =>
                p.TenantId == richiesta.TenantId && p.GrantId == richiesta.GrantId
                && p.SlotAttivo == SlotAttivo, ct))
                throw new ConflittoPropostaAuroraException(
                    "Esiste già una proposta in attesa per questo mandato.");

            var quota = await prenotaQuotaProposta(ct);
            if (!quota.Consentito)
            {
                // Il Governor ha già scritto il diniego nella stessa transazione. Lo
                // conserviamo, senza proposta né quota, e distinguiamo il motivo dal conflitto slot.
                await tx.CommitAsync(ct);
                throw new PropostaAuroraNegataException(
                    quota.Motivo ?? "Quota proposte non disponibile.");
            }

            var scade = NormalizzaUtc(adesso.Add(DurataProposta));
            string diffJson = JsonSerializer.Serialize(richiesta.Diff, Json);
            var proposta = new AiPropostaEntity
            {
                Id = Guid.NewGuid(),
                TenantId = richiesta.TenantId,
                UtenteId = richiesta.UtenteId,
                GrantId = richiesta.GrantId,
                RunId = richiesta.RunId,
                Strumento = richiesta.Strumento,
                VersioneStrumento = richiesta.VersioneStrumento,
                Risorsa = richiesta.Risorsa,
                Operazione = richiesta.Operazione,
                AziendaId = richiesta.AziendaId,
                Periodo = richiesta.Periodo,
                ArgomentiHash = Hash(richiesta.ComandoCanonicoJson),
                PrecondizioneHash = richiesta.PrecondizioneHash,
                PostcondizioneHash = richiesta.PostcondizioneHash,
                CreataUtc = adesso,
                ScadeUtc = scade,
                Stato = richiesta.Confermabile ? "InAttesa" : "Shadow",
                SlotAttivo = richiesta.Confermabile ? SlotAttivo : null,
                Confermabile = richiesta.Confermabile,
            };
            proposta.ArgomentiProtetti = Proteggi(
                richiesta.ComandoCanonicoJson,
                ContestoDatiProtetti.ArgomentiProposta(proposta.TenantId, proposta.Id));
            proposta.UndoProtetto = Proteggi(
                richiesta.UndoJson,
                ContestoDatiProtetti.UndoProposta(proposta.TenantId, proposta.Id));
            proposta.Titolo = Proteggi(
                richiesta.Titolo,
                ContestoDatiProtetti.TitoloProposta(proposta.TenantId, proposta.Id));
            proposta.DiffJson = Proteggi(
                diffJson,
                ContestoDatiProtetti.DiffProposta(proposta.TenantId, proposta.Id));
            proposta.IntentoHash = CalcolaIntento(
                proposta, richiesta.ComandoCanonicoJson, richiesta.UndoJson,
                richiesta.Titolo, diffJson);

            db.AiProposte.Add(proposta);
            await db.SaveChangesAsync(ct);
            string? token = richiesta.Confermabile
                ? AiPropostaCodec.CreaConferma(
                    proposta.Id, proposta.IntentoHash, proposta.ScadeUtc, opzioni.ChiaveToken)
                : null;
            await tx.CommitAsync(ct);
            return (proposta, token);
        }
        catch (PropostaAuroraNegataException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            if (await db.AiProposte.AsNoTracking().AnyAsync(p =>
                p.TenantId == richiesta.TenantId && p.GrantId == richiesta.GrantId
                && p.SlotAttivo == SlotAttivo, CancellationToken.None))
                throw new ConflittoPropostaAuroraException(
                    "Esiste già una proposta in attesa per questo mandato.");
            throw;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    // ---------- Conferma atomica ----------

    public async Task<EsitoOperazionePropostaAurora> ConfermaGovernataAsync(
        Guid tenantId,
        Guid utenteId,
        string? token,
        Func<MetadatiPropostaAurora, (bool Consentita, string? Motivo)> autorizzaStrumento,
        Func<MetadatiPropostaAurora, CancellationToken, Task<EsitoAccessoAi>> prenotaEffetto,
        Func<MetadatiPropostaAurora, string, CancellationToken,
            Task<(bool Riuscita, string EsitoJson)>> applica,
        Func<MetadatiPropostaAurora, CancellationToken, Task> registraAudit,
        CancellationToken ct = default)
    {
        var decodificato = AiPropostaCodec.ProvaConferma(token, opzioni.ChiaveToken);
        if (decodificato is null)
            return new(null, "NonValida", "Token della conferma assente o alterato.", null);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var proposta = await db.AiProposte.FirstOrDefaultAsync(p =>
                p.Id == decodificato.PropostaId && p.TenantId == tenantId
                && p.UtenteId == utenteId, ct);
            if (proposta is null)
                return await RollbackRisultatoAsync(tx,
                    new(null, "NonTrovata", "Proposta non trovata.", null));
            var chiari = SvelaEVerifica(proposta, decodificato, tipoUndo: false);
            if (chiari is null)
                return await RollbackRisultatoAsync(tx,
                    new(proposta.Id, "NonValida", "Il token non corrisponde all'intento registrato.", null));

            if (proposta.Stato == "Applicata")
                return await RollbackRisultatoAsync(tx, EsitoApplicata(proposta, "GiaApplicata"));
            if (proposta.Stato == "Ripristinata")
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, "Ripristinata", "L'operazione è già stata ripristinata.",
                    SvelaEsitoUndo(proposta)));
            if (proposta.Stato is "Annullata" or "Scaduta" or "Conflitto" or "Shadow")
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, proposta.Stato,
                    "La proposta non è più confermabile.", SvelaEsito(proposta)));
            if (!proposta.Confermabile || proposta.Stato != "InAttesa")
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, "InCorso", "La proposta è già in elaborazione.", null));

            var adesso = NormalizzaUtc(orologio.GetUtcNow().UtcDateTime);
            if (adesso >= proposta.ScadeUtc)
            {
                proposta.Stato = "Scaduta";
                proposta.SlotAttivo = null;
                proposta.Versione++;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(proposta.Id, "Scaduta",
                    "Proposta scaduta: chiedi ad Aurora di prepararla di nuovo.", null);
            }

            var meta = Metadati(proposta);
            var autorizzazione = autorizzaStrumento(meta);
            if (!autorizzazione.Consentita)
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, "Negata", autorizzazione.Motivo
                        ?? "Strumento non più autorizzato.", null));

            // InApplicazione esiste soltanto in questa transazione non committata.
            proposta.Stato = "InApplicazione";
            proposta.Versione++;
            await db.SaveChangesAsync(ct);

            var quota = await prenotaEffetto(meta, ct);
            if (!quota.Consentito)
            {
                proposta.Stato = "InAttesa";
                proposta.Versione++;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct); // conserva il diniego auditato, nessun effetto
                return new(proposta.Id, "Negata",
                    quota.Motivo ?? "Mandato non più spendibile.", null);
            }

            var (riuscita, esitoJson) = await applica(meta, chiari.ComandoJson, ct);
            if (!riuscita)
                throw new OperazioneAuroraNonConsentitaException(
                    "Il dominio ha rifiutato l'operazione proposta.");
            await registraAudit(meta, ct);

            proposta.Stato = "Applicata";
            proposta.SlotAttivo = null;
            proposta.ApplicataUtc = adesso;
            proposta.UndoScadeUtc = NormalizzaUtc(adesso.Add(DurataUndo));
            proposta.EsitoProtetto = Proteggi(esitoJson,
                ContestoDatiProtetti.EsitoProposta(proposta.TenantId, proposta.Id));
            proposta.Versione++;
            await db.SaveChangesAsync(ct);
            // Prepara tutto ciò che può fallire (decifratura e token) prima del commit.
            var risultato = EsitoApplicata(proposta, "Applicata", esitoJson);
            await tx.CommitAsync(ct);
            return risultato;
        }
        catch (OperazioneAuroraNonConsentitaException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            await MarcaConflittoAsync(
                tenantId, utenteId, decodificato.PropostaId, ex.Message);
            return new(decodificato.PropostaId, "Conflitto", ex.Message, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await EsitoDopoConcorrenzaAsync(
                tenantId, utenteId, decodificato.PropostaId, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    // ---------- Annullamento, stato e undo ----------

    public async Task<EsitoOperazionePropostaAurora> AnnullaAsync(
        Guid tenantId,
        Guid utenteId,
        string? token,
        Func<MetadatiPropostaAurora, CancellationToken, Task> registraAudit,
        CancellationToken ct = default)
    {
        var decodificato = AiPropostaCodec.ProvaConferma(token, opzioni.ChiaveToken);
        if (decodificato is null)
            return new(null, "NonValida", "Token della proposta assente o alterato.", null);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var proposta = await db.AiProposte.FirstOrDefaultAsync(p =>
                p.Id == decodificato.PropostaId && p.TenantId == tenantId
                && p.UtenteId == utenteId, ct);
            if (proposta is null || SvelaEVerifica(proposta, decodificato, tipoUndo: false) is null)
                return await RollbackRisultatoAsync(tx,
                    new(null, "NonTrovata", "Proposta non trovata.", null));
            if (proposta.Stato == "Annullata")
                return await RollbackRisultatoAsync(tx,
                    new(proposta.Id, "Annullata", "Proposta già annullata.", null));
            if (proposta.Stato != "InAttesa")
                return await RollbackRisultatoAsync(tx,
                    EsitoStatoTerminale(proposta,
                        "La proposta non può essere annullata in questo stato."));

            proposta.Stato = "Annullata";
            proposta.SlotAttivo = null;
            proposta.AnnullataUtc = NormalizzaUtc(orologio.GetUtcNow().UtcDateTime);
            proposta.Versione++;
            await registraAudit(Metadati(proposta), ct);
            await db.SaveChangesAsync(ct);
            var risultato = new EsitoOperazionePropostaAurora(
                proposta.Id, "Annullata",
                "Proposta annullata. Nessun dato è stato modificato.", null);
            await tx.CommitAsync(ct);
            return risultato;
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await EsitoDopoConcorrenzaAsync(
                tenantId, utenteId, decodificato.PropostaId, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<StatoPropostaAurora?> StatoAsync(
        Guid tenantId, Guid utenteId, Guid propostaId, CancellationToken ct = default)
    {
        var proposta = await db.AiProposte.AsNoTracking().FirstOrDefaultAsync(p =>
            p.Id == propostaId && p.TenantId == tenantId && p.UtenteId == utenteId, ct);
        if (proposta is null || SvelaEVerificaIntegrita(proposta) is not { } chiari) return null;
        RigaDiffAurora[] diff;
        try
        {
            diff = JsonSerializer.Deserialize<RigaDiffAurora[]>(chiari.DiffJson, Json) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }
        bool puoUndo = proposta.Stato == "Applicata"
            && proposta.UndoScadeUtc is { } limite
            && orologio.GetUtcNow().UtcDateTime < limite;
        return new(
            proposta.Id, proposta.Stato, proposta.Strumento, chiari.Titolo, diff,
            proposta.ScadeUtc, proposta.ApplicataUtc, proposta.RipristinataUtc,
            proposta.Stato == "InAttesa", puoUndo,
            proposta.Stato == "Ripristinata" ? SvelaEsitoUndo(proposta) : SvelaEsito(proposta),
            puoUndo ? TokenUndo(proposta) : null);
    }

    public async Task<EsitoOperazionePropostaAurora> RipristinaAsync(
        Guid tenantId,
        Guid utenteId,
        string? tokenUndo,
        Func<MetadatiPropostaAurora, (bool Consentita, string? Motivo)> autorizzaUndo,
        Func<MetadatiPropostaAurora, string, CancellationToken,
            Task<(bool Riuscita, string EsitoJson)>> ripristina,
        Func<MetadatiPropostaAurora, CancellationToken, Task> registraAudit,
        CancellationToken ct = default)
    {
        var decodificato = AiPropostaCodec.ProvaUndo(tokenUndo, opzioni.ChiaveToken);
        if (decodificato is null)
            return new(null, "NonValida", "Token di ripristino assente o alterato.", null);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var proposta = await db.AiProposte.FirstOrDefaultAsync(p =>
                p.Id == decodificato.PropostaId && p.TenantId == tenantId
                && p.UtenteId == utenteId, ct);
            if (proposta is null || SvelaEVerifica(proposta, decodificato, tipoUndo: true) is not { } chiari)
                return await RollbackRisultatoAsync(tx,
                    new(null, "NonTrovata", "Proposta non trovata.", null));
            if (proposta.Stato == "Ripristinata")
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, "GiaRipristinata", "Operazione già ripristinata.",
                    SvelaEsitoUndo(proposta)));
            if (proposta.Stato != "Applicata")
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, proposta.Stato, "L'operazione non è ripristinabile in questo stato.", null));
            if (proposta.UndoScadeUtc is not { } scadeUndo
                || orologio.GetUtcNow().UtcDateTime >= scadeUndo)
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, "Scaduta", "Il periodo di ripristino automatico è terminato.", null));

            var meta = Metadati(proposta);
            var autorizzazione = autorizzaUndo(meta);
            if (!autorizzazione.Consentita)
                return await RollbackRisultatoAsync(tx, new(
                    proposta.Id, "Negata", autorizzazione.Motivo ?? "Ripristino non autorizzato.", null));

            proposta.Stato = "InRipristino"; // mai committato
            proposta.Versione++;
            await db.SaveChangesAsync(ct);
            var (riuscita, esitoJson) = await ripristina(meta, chiari.UndoJson, ct);
            if (!riuscita)
                throw new OperazioneAuroraNonConsentitaException(
                    "Il dominio ha rifiutato il ripristino.");
            await registraAudit(meta, ct);

            proposta.Stato = "Ripristinata";
            proposta.RipristinataUtc = NormalizzaUtc(orologio.GetUtcNow().UtcDateTime);
            proposta.EsitoUndoProtetto = Proteggi(esitoJson,
                ContestoDatiProtetti.EsitoUndoProposta(proposta.TenantId, proposta.Id));
            proposta.Versione++;
            await db.SaveChangesAsync(ct);
            var risultato = new EsitoOperazionePropostaAurora(
                proposta.Id, "Ripristinata", "Operazione ripristinata.", esitoJson);
            await tx.CommitAsync(ct);
            return risultato;
        }
        catch (OperazioneAuroraNonConsentitaException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return new(decodificato.PropostaId, "Conflitto", ex.Message, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await EsitoDopoConcorrenzaAsync(
                tenantId, utenteId, decodificato.PropostaId, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    // ---------- Integrità e presentazione ----------

    private sealed record ChiariProposta(
        string ComandoJson, string UndoJson, string Titolo, string DiffJson);

    private ChiariProposta? SvelaEVerifica(
        AiPropostaEntity proposta, AiPropostaCodec.TokenDecodificato token, bool tipoUndo)
    {
        DateTime scadeToken = tipoUndo
            ? proposta.UndoScadeUtc ?? DateTime.MinValue
            : proposta.ScadeUtc;
        if (token.PropostaId != proposta.Id
            || NormalizzaUtc(token.ScadeUtc) != NormalizzaUtc(scadeToken)
            || !string.Equals(token.ArgomentiHash, proposta.IntentoHash, StringComparison.Ordinal))
            return null;
        return SvelaEVerificaIntegrita(proposta);
    }

    private ChiariProposta? SvelaEVerificaIntegrita(AiPropostaEntity proposta)
    {
        try
        {
            var comando = Svela(proposta.ArgomentiProtetti,
                ContestoDatiProtetti.ArgomentiProposta(proposta.TenantId, proposta.Id));
            var undo = proposta.UndoProtetto is null ? "" : Svela(proposta.UndoProtetto,
                ContestoDatiProtetti.UndoProposta(proposta.TenantId, proposta.Id));
            var titolo = Svela(proposta.Titolo,
                ContestoDatiProtetti.TitoloProposta(proposta.TenantId, proposta.Id));
            var diff = Svela(proposta.DiffJson,
                ContestoDatiProtetti.DiffProposta(proposta.TenantId, proposta.Id));
            var ricalcolato = CalcolaIntento(proposta, comando, undo, titolo, diff);
            return string.Equals(ricalcolato, proposta.IntentoHash, StringComparison.Ordinal)
                ? new(comando, undo, titolo, diff)
                : null;
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string CalcolaIntento(
        AiPropostaEntity p, string comando, string undo, string titolo, string diffJson)
    {
        string canonico = string.Join('\u001F',
            p.Id.ToString("N"), p.TenantId.ToString("N"), p.UtenteId.ToString("N"),
            p.GrantId?.ToString("N") ?? "", p.Strumento, p.VersioneStrumento,
            p.Risorsa, p.Operazione, p.AziendaId?.ToString("N") ?? "", p.Periodo ?? "",
            Hash(comando), p.PrecondizioneHash, p.PostcondizioneHash ?? "", Hash(undo),
            Hash(titolo), Hash(diffJson),
            NormalizzaUtc(p.ScadeUtc).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.Confermabile ? "1" : "0");
        return Hash(canonico);
    }

    private static MetadatiPropostaAurora Metadati(AiPropostaEntity p) => new(
        p.Id,
        p.GrantId ?? throw new InvalidOperationException("Grant della proposta assente."),
        p.Strumento,
        p.VersioneStrumento,
        p.Risorsa,
        p.Operazione,
        p.AziendaId ?? throw new InvalidOperationException("Azienda della proposta assente."),
        p.Periodo ?? throw new InvalidOperationException("Periodo della proposta assente."));

    private EsitoOperazionePropostaAurora EsitoApplicata(
        AiPropostaEntity proposta, string stato, string? esitoJson = null) => new(
        proposta.Id,
        stato,
        stato == "Applicata" ? "Operazione applicata." : "Operazione già applicata.",
        esitoJson ?? SvelaEsito(proposta),
        proposta.UndoScadeUtc is { } limite && orologio.GetUtcNow().UtcDateTime < limite
            ? TokenUndo(proposta)
            : null);

    private string TokenUndo(AiPropostaEntity proposta) => AiPropostaCodec.CreaUndo(
        proposta.Id,
        proposta.IntentoHash,
        proposta.UndoScadeUtc
            ?? throw new InvalidOperationException("Scadenza undo assente."),
        opzioni.ChiaveToken);

    private async Task<EsitoOperazionePropostaAurora> EsitoDopoConcorrenzaAsync(
        Guid tenantId, Guid utenteId, Guid propostaId, CancellationToken ct)
    {
        var p = await db.AiProposte.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == propostaId && x.TenantId == tenantId && x.UtenteId == utenteId, ct);
        if (p is null) return new(null, "NonTrovata", "Proposta non trovata.", null);
        return p.Stato is "InAttesa" or "InApplicazione" or "InRipristino"
            ? new(p.Id, "InCorso", "Un'altra richiesta sta elaborando la proposta.", null)
            : EsitoStatoTerminale(p, "La proposta è già arrivata a uno stato terminale.");
    }

    private async Task MarcaConflittoAsync(
        Guid tenantId, Guid utenteId, Guid propostaId, string motivo)
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ct = limite.Token;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var p = await db.AiProposte.FirstOrDefaultAsync(x =>
            x.Id == propostaId && x.TenantId == tenantId && x.UtenteId == utenteId, ct);
        if (p is not null && p.Stato == "InAttesa")
        {
            p.Stato = "Conflitto";
            p.SlotAttivo = null;
            p.EsitoProtetto = Proteggi(
                JsonSerializer.Serialize(new { errore = motivo }, Json),
                ContestoDatiProtetti.EsitoProposta(p.TenantId, p.Id));
            p.Versione++;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private EsitoOperazionePropostaAurora EsitoStatoTerminale(
        AiPropostaEntity proposta, string messaggio) => proposta.Stato switch
        {
            "Applicata" => EsitoApplicata(proposta, "GiaApplicata"),
            "Ripristinata" => new(
                proposta.Id, "GiaRipristinata", "Operazione già ripristinata.",
                SvelaEsitoUndo(proposta)),
            "Annullata" => new(proposta.Id, "Annullata", "Proposta già annullata.", null),
            "Scaduta" => new(proposta.Id, "Scaduta", "Proposta scaduta.", null),
            "Conflitto" => new(proposta.Id, "Conflitto", messaggio, SvelaEsito(proposta)),
            "Shadow" => new(proposta.Id, "Shadow",
                "Anteprima DryRun: non può essere confermata.", null),
            _ => new(proposta.Id, proposta.Stato, messaggio, null),
        };

    private static async Task<EsitoOperazionePropostaAurora> RollbackRisultatoAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        EsitoOperazionePropostaAurora risultato)
    {
        await tx.RollbackAsync(CancellationToken.None);
        return risultato;
    }

    private string Proteggi(string valore, string contesto) =>
        db.Protettore.ProteggiConContesto(valore, contesto)
        ?? throw new CryptographicException("Protezione payload Aurora fallita.");

    private string Svela(string valore, string contesto) =>
        db.Protettore.SvelaConContesto(valore, contesto)
        ?? throw new CryptographicException("Payload Aurora non leggibile.");

    private string? SvelaEsito(AiPropostaEntity proposta) =>
        proposta.EsitoProtetto is null ? null : Svela(proposta.EsitoProtetto,
            ContestoDatiProtetti.EsitoProposta(proposta.TenantId, proposta.Id));

    private string? SvelaEsitoUndo(AiPropostaEntity proposta) =>
        proposta.EsitoUndoProtetto is null ? null : Svela(proposta.EsitoUndoProtetto,
            ContestoDatiProtetti.EsitoUndoProposta(proposta.TenantId, proposta.Id));

    private static void ValidaNuova(NuovaPropostaAurora r)
    {
        if (r.TenantId == Guid.Empty || r.UtenteId == Guid.Empty || r.GrantId == Guid.Empty
            || r.AziendaId == Guid.Empty || string.IsNullOrWhiteSpace(r.Periodo)
            || string.IsNullOrWhiteSpace(r.Strumento)
            || string.IsNullOrWhiteSpace(r.VersioneStrumento)
            || string.IsNullOrWhiteSpace(r.Risorsa)
            || r.Operazione != "apply_reversible"
            || string.IsNullOrWhiteSpace(r.ComandoCanonicoJson)
            || string.IsNullOrWhiteSpace(r.UndoJson)
            || string.IsNullOrWhiteSpace(r.PrecondizioneHash)
            || string.IsNullOrWhiteSpace(r.PostcondizioneHash))
            throw new ArgumentException("Intento della proposta A3 incompleto.");
    }

    public static string Hash(string testo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testo ?? "")));

    private static DateTime NormalizzaUtc(DateTime valore)
    {
        var utc = valore.Kind switch
        {
            DateTimeKind.Utc => valore,
            DateTimeKind.Local => valore.ToUniversalTime(),
            _ => DateTime.SpecifyKind(valore, DateTimeKind.Utc),
        };
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);
    }
}
