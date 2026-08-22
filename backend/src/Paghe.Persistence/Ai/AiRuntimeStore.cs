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

/// <summary>
/// Esito della conferma di una proposta. <c>Stato</c>: Applicata · GiaApplicata · Fallita ·
/// InCorso · Scaduta · NonTrovata · NonValida.
/// </summary>
public sealed record EsitoConfermaAurora(string Stato, string? Messaggio, string? EsitoJson);

/// <summary>
/// SessionLog e proposte del runtime Aurora (fase A2). Il log registra digest, esiti e
/// quantità — mai testo dei messaggi o payload; la catena autorevole dei dati letti resta
/// l'audit del Governor. Le proposte conservano gli argomenti cifrati lato server: la
/// conferma rimanda solo il token firmato e l'applicazione riesegue le validazioni di
/// dominio nei servizi normali.
/// </summary>
public sealed class AiRuntimeStore(PagheDbContext db, AiPropostaOptions opzioni, TimeProvider orologio)
{
    public static readonly TimeSpan DurataProposta = TimeSpan.FromMinutes(10);

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
            // Il nome può arrivare inventato dal modello: si tronca e basta, mai payload.
            Strumento = strumento.Length <= 100 ? strumento : strumento[..100],
            ArgomentiHash = argomentiHash,
            Esito = esito,
            Motivo = motivo is { Length: > 300 } ? motivo[..300] : motivo,
            DurataMs = durataMs,
            Righe = righe,
        });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Proposte firmate ----------

    public async Task<(AiPropostaEntity Proposta, string Token)> CreaPropostaAsync(
        Guid tenantId, Guid utenteId, Guid? grantId, Guid? runId, string strumento,
        string argomentiJson, string titolo, IReadOnlyList<RigaDiffAurora> diff,
        CancellationToken ct = default)
    {
        var adesso = orologio.GetUtcNow().UtcDateTime;
        var proposta = new AiPropostaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UtenteId = utenteId,
            GrantId = grantId,
            RunId = runId,
            Strumento = strumento,
            ArgomentiHash = Hash(argomentiJson),
            CreataUtc = adesso,
            ScadeUtc = adesso.Add(DurataProposta),
        };
        proposta.ArgomentiProtetti = db.Protettore.ProteggiConContesto(
            argomentiJson, ContestoDatiProtetti.ArgomentiProposta(tenantId, proposta.Id))!;
        proposta.Titolo = db.Protettore.ProteggiConContesto(
            titolo, ContestoDatiProtetti.TitoloProposta(tenantId, proposta.Id))!;
        proposta.DiffJson = db.Protettore.ProteggiConContesto(
            JsonSerializer.Serialize(diff, Json),
            ContestoDatiProtetti.DiffProposta(tenantId, proposta.Id))!;
        db.AiProposte.Add(proposta);
        await db.SaveChangesAsync(ct);

        var token = AiPropostaCodec.Crea(
            proposta.Id, proposta.ArgomentiHash, proposta.ScadeUtc, opzioni.ChiaveToken);
        return (proposta, token);
    }

    /// <summary>
    /// Conferma con token firmato: verifica firma, proprietario, hash e scadenza, reclama la
    /// proposta in modo atomico e solo allora esegue <paramref name="applica"/> (strumento,
    /// argomenti). Ripetere la stessa conferma non duplica l'effetto: restituisce l'esito già
    /// registrato.
    /// </summary>
    public async Task<EsitoConfermaAurora> ConfermaAsync(
        Guid tenantId, Guid utenteId, string? token,
        Func<string, string, CancellationToken, Task<(bool Riuscita, string Esito)>> applica,
        CancellationToken ct = default)
    {
        var decodificato = AiPropostaCodec.Prova(token, opzioni.ChiaveToken);
        if (decodificato is null)
            return new("NonValida", "Token della conferma assente o alterato.", null);

        var adesso = orologio.GetUtcNow().UtcDateTime;
        var proposta = await db.AiProposte
            .FirstOrDefaultAsync(p => p.Id == decodificato.PropostaId
                && p.TenantId == tenantId
                && p.UtenteId == utenteId, ct);
        // Risposta identica per proposta inesistente o di un altro utente/tenant: nessun indizio.
        if (proposta is null)
            return new("NonTrovata", "Proposta non trovata.", null);
        if (!string.Equals(proposta.ArgomentiHash, decodificato.ArgomentiHash, StringComparison.Ordinal))
            return new("NonValida", "Il token non corrisponde alla proposta registrata.", null);

        switch (proposta.Stato)
        {
            case "Applicata":
                return new("GiaApplicata", "Operazione già applicata.", Svela(proposta));
            case "InApplicazione":
                return new("InCorso", "Applicazione già in corso: attendi l'esito.", null);
            case "Annullata":
                return new("Fallita", "La proposta è stata annullata.", Svela(proposta));
            case "Scaduta":
                return new("Scaduta", "Proposta scaduta: chiedi ad Aurora di prepararla di nuovo.", null);
        }

        if (adesso >= proposta.ScadeUtc)
        {
            proposta.Stato = "Scaduta";
            proposta.Versione++;
            await db.SaveChangesAsync(ct);
            return new("Scaduta", "Proposta scaduta: chiedi ad Aurora di prepararla di nuovo.", null);
        }

        // Reclamo atomico: chi perde la corsa vede InApplicazione/Applicata, mai una doppia esecuzione.
        proposta.Stato = "InApplicazione";
        proposta.Versione++;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Anche il percorso di concorrenza mantiene il tenant/owner nel predicato SQL:
            // ReloadAsync per sola PK potrebbe materializzare una riga fuori contesto.
            db.Entry(proposta).State = EntityState.Detached;
            var aggiornata = await db.AiProposte.AsNoTracking().FirstOrDefaultAsync(p =>
                p.Id == decodificato.PropostaId
                && p.TenantId == tenantId
                && p.UtenteId == utenteId, ct);
            if (aggiornata is null)
                return new("NonTrovata", "Proposta non trovata.", null);
            return aggiornata.Stato == "Applicata"
                ? new("GiaApplicata", "Operazione già applicata.", Svela(aggiornata))
                : new("InCorso", "Applicazione già in corso: attendi l'esito.", null);
        }

        string argomentiJson = db.Protettore.SvelaConContesto(
            proposta.ArgomentiProtetti,
            ContestoDatiProtetti.ArgomentiProposta(proposta.TenantId, proposta.Id))!;

        bool riuscita;
        string esitoJson;
        try
        {
            (riuscita, esitoJson) = await applica(proposta.Strumento, argomentiJson, ct);
        }
        catch
        {
            // Errore imprevisto (non di dominio): la proposta torna confermabile entro la scadenza.
            proposta.Stato = "InAttesa";
            proposta.Versione++;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        proposta.Stato = riuscita ? "Applicata" : "Annullata";
        proposta.ApplicataUtc = riuscita ? adesso : null;
        proposta.EsitoProtetto = db.Protettore.ProteggiConContesto(
            esitoJson, ContestoDatiProtetti.EsitoProposta(proposta.TenantId, proposta.Id));
        proposta.Versione++;
        await db.SaveChangesAsync(ct);

        return riuscita
            ? new("Applicata", "Operazione applicata.", esitoJson)
            : new("Fallita", "Il programma ha rifiutato l'operazione.", esitoJson);
    }

    private string? Svela(AiPropostaEntity proposta) =>
        proposta.EsitoProtetto is null
            ? null
            : db.Protettore.SvelaConContesto(
                proposta.EsitoProtetto,
                ContestoDatiProtetti.EsitoProposta(proposta.TenantId, proposta.Id));

    public static string Hash(string testo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testo ?? "")));
}
