using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paghe.Persistence.Ai;

namespace Paghe.Api;

/// <summary>Esito di un turno del runtime: testo per l'utente e, se preparata, la proposta.</summary>
public sealed record EsitoTurnoAurora(string Testo, PropostaAzioneAurora? Proposta, bool Ok);

/// <summary>
/// AuroraAgentRuntime (fase A2): il loop del modello con gli strumenti governati. Ogni
/// lettura passa dal broker (lease, righe minimizzate, quota), ogni bozza di scrittura passa
/// dal Governor e diventa una proposta firmata; le guardie fermano giri eccessivi,
/// ripetizioni e strumenti lenti; revoca o quota esaurita interrompono il turno in modo
/// pulito. Tutto ciò che il modello vede e chiama finisce nel SessionLog come digest ed
/// esiti, mai come payload.
/// </summary>
public sealed class AuroraAgentRuntime(
    AiChatClient chat,
    AiToolRegistry registry,
    AiGrantService governor,
    AiRuntimeStore store,
    TimeSpan? timeoutStrumento = null)
{
    internal const int MassimoGiri = 4;
    internal const int MassimoChiamatePerGiro = 5;
    internal const int MassimoRipetizioni = 2;
    internal static readonly TimeSpan TimeoutStrumentoDefault = TimeSpan.FromSeconds(15);

    private readonly TimeSpan _timeoutStrumento = timeoutStrumento ?? TimeoutStrumentoDefault;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<EsitoTurnoAurora> TurnoAsync(
        ContestoAurora contesto, List<MessaggioChatAi> messaggi, string? sezioneUi,
        CancellationToken ct)
    {
        var manifest = contesto.Manifest
            ?? throw new ArgumentException("Il runtime richiede un mandato risolto.", nameof(contesto));
        var strumenti = registry.PerMandato(contesto.Ruolo, manifest);
        var catalogo = strumenti.Select(s => AiToolRegistry.PerModello(s.Descrittore)).ToArray();

        var run = await store.ApriRunAsync(
            contesto.TenantId, contesto.UtenteId, manifest.GrantId, chat.Modello, sezioneUi, ct);
        var eseguite = new HashSet<string>(StringComparer.Ordinal);
        int ripetizioni = 0, ordine = 0, giri = 0;
        int? tokenTotali = null;

        try
        {
            for (int giro = 1; giro <= MassimoGiri; giro++)
            {
                giri = giro;
                var risposta = await chat.ParlaAsync(messaggi, catalogo, ct);
                if (risposta.TokenTotali is { } spesi)
                    tokenTotali = (tokenTotali ?? 0) + spesi;

                if (risposta.Chiamate.Count == 0)
                {
                    string testo = string.IsNullOrWhiteSpace(risposta.Testo)
                        ? "Non ho trovato una risposta utile. Puoi riformulare la domanda?"
                        : risposta.Testo!;
                    messaggi.Add(new MessaggioChatAi { Ruolo = "assistant", Contenuto = testo });
                    await ChiudiAsync(run, "Ok", null, giri, tokenTotali, messaggi, ct);
                    return new(testo, null, true);
                }

                messaggi.Add(new MessaggioChatAi
                {
                    Ruolo = "assistant",
                    Contenuto = risposta.Testo,
                    Chiamate = [.. risposta.Chiamate],
                });

                foreach (var chiamata in risposta.Chiamate.Take(MassimoChiamatePerGiro))
                {
                    ordine++;
                    string hashArgomenti = AiRuntimeStore.Hash(chiamata.ArgomentiJson);
                    var strumento = strumenti.FirstOrDefault(
                        s => string.Equals(s.Descrittore.Nome, chiamata.Nome, StringComparison.Ordinal));

                    if (strumento is null)
                    {
                        // Injection o allucinazione: lo strumento non offerto non esiste.
                        await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
                            "Sconosciuto", "Strumento non offerto in questo mandato.", 0, 0, ct);
                        RispondiAlModello(messaggi, chiamata.Id, new
                        {
                            errore = "Strumento non disponibile in questo mandato.",
                        });
                        continue;
                    }

                    if (!eseguite.Add($"{chiamata.Nome}|{hashArgomenti}"))
                    {
                        ripetizioni++;
                        await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
                            "Ripetuto", "Chiamata identica già eseguita nel turno.", 0, 0, ct);
                        if (ripetizioni >= MassimoRipetizioni)
                        {
                            const string testo = "Mi fermo: stavo ripetendo la stessa ricerca. "
                                + "Riformula la domanda o restringi il periodo.";
                            await ChiudiAsync(run, "Guardia", "Anti-ripetizione.", giri, tokenTotali, messaggi, ct);
                            return new(testo, null, false);
                        }
                        RispondiAlModello(messaggi, chiamata.Id, new
                        {
                            errore = "Chiamata identica già eseguita: usa il risultato precedente.",
                        });
                        continue;
                    }

                    if (strumento.Descrittore.Effetto != "lettura")
                    {
                        var esitoTurno = await PreparaPropostaAsync(
                            contesto, run, strumento, chiamata, hashArgomenti, ordine,
                            giri, tokenTotali, messaggi, ct);
                        if (esitoTurno is not null) return esitoTurno;
                        continue;
                    }

                    var (fermati, esitoStop) = await EseguiLetturaAsync(
                        contesto, run, strumento, chiamata, hashArgomenti, ordine,
                        giri, tokenTotali, messaggi, ct);
                    if (fermati) return esitoStop!;
                }
            }

            const string limite = "Mi fermo qui: ho fatto già molte ricerche in questo turno. "
                + "Chiedimi il passo successivo con una domanda più mirata.";
            await ChiudiAsync(run, "Guardia", "Massimo giri raggiunto.", giri, tokenTotali, messaggi, ct);
            return new(limite, null, false);
        }
        catch
        {
            await ChiudiAsync(run, "Errore", "Turno interrotto da un errore.", giri, tokenTotali,
                messaggi, CancellationToken.None);
            throw;
        }
    }

    private async Task<(bool Fermati, EsitoTurnoAurora? Esito)> EseguiLetturaAsync(
        ContestoAurora contesto, Paghe.Persistence.Entities.AiRunEntity run,
        IStrumentoAurora strumento, ChiamataStrumentoAi chiamata, string hashArgomenti,
        int ordine, int giri, int? tokenTotali, List<MessaggioChatAi> messaggi, CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        RisultatoStrumentoAurora esito;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeoutStrumento);
        try
        {
            esito = await strumento.EseguiAsync(contesto, chiamata.ArgomentiJson, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
                "Timeout", "Lo strumento non ha risposto in tempo.", (int)cronometro.ElapsedMilliseconds, 0, ct);
            RispondiAlModello(messaggi, chiamata.Id, new
            {
                errore = "Lo strumento non ha risposto in tempo: prosegui senza questo dato.",
            });
            return (false, null);
        }

        await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
            esito.Negato ? "Negato" : "Ok", esito.Motivo,
            (int)cronometro.ElapsedMilliseconds, esito.Righe, ct);
        RispondiAlModello(messaggi, chiamata.Id, esito.ContenutoJson);

        if (!esito.Negato) return (false, null);

        // Un diniego qui è del mandato (quota, revoca, scadenza): continuare sarebbe rumore.
        var (etichetta, testo) = ClassificaDiniego(esito.Motivo);
        await ChiudiAsync(run, etichetta, esito.Motivo, giri, tokenTotali, messaggi, ct);
        return (true, new EsitoTurnoAurora(testo, null, false));
    }

    private async Task<EsitoTurnoAurora?> PreparaPropostaAsync(
        ContestoAurora contesto, Paghe.Persistence.Entities.AiRunEntity run,
        IStrumentoAurora strumento, ChiamataStrumentoAi chiamata, string hashArgomenti,
        int ordine, int giri, int? tokenTotali, List<MessaggioChatAi> messaggi, CancellationToken ct)
    {
        // Anche una bozza consuma il mandato: il Governor vidima risorsa e operazione draft.
        var accesso = await governor.AutorizzaAccessoAsync(
            contesto.TenantId, contesto.UtenteId, contesto.Lease,
            strumento.Descrittore.Risorsa, "draft", 1, ct);
        if (!accesso.Consentito)
        {
            await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
                "Negato", accesso.Motivo, 0, 0, ct);
            var (etichetta, testo) = ClassificaDiniego(accesso.Motivo);
            RispondiAlModello(messaggi, chiamata.Id, new { errore = accesso.Motivo });
            await ChiudiAsync(run, etichetta, accesso.Motivo, giri, tokenTotali, messaggi, ct);
            return new(testo, null, false);
        }

        var proposta = await strumento.PreparaAsync(contesto, chiamata.ArgomentiJson, ct);
        if (!proposta.PuoProcedere)
        {
            await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
                "Ok", "Proposta incompleta.", 0, 0, ct);
            RispondiAlModello(messaggi, chiamata.Id, new
            {
                propostaIncompleta = true,
                motivo = proposta.MotivoBlocco,
            });
            return null; // il modello chiede all'utente i dati mancanti
        }

        var (entita, token) = await store.CreaPropostaAsync(
            contesto.TenantId, contesto.UtenteId, contesto.Manifest?.GrantId, run.Id,
            strumento.Descrittore.Nome, chiamata.ArgomentiJson, proposta.Titolo, proposta.Diff, ct);
        await store.RegistraToolCallAsync(run, ordine, chiamata.Nome, hashArgomenti,
            "Proposta", null, 0, 0, ct);

        string testoProposta = $"Ho preparato: {proposta.Titolo}. Controlla il riepilogo e conferma "
            + "per applicare l'operazione.";
        messaggi.Add(new MessaggioChatAi { Ruolo = "assistant", Contenuto = testoProposta });
        await ChiudiAsync(run, "Proposta", null, giri, tokenTotali, messaggi, ct);
        return new(testoProposta, new PropostaAzioneAurora(
            token, strumento.Descrittore.Nome, proposta.Titolo, proposta.Diff,
            strumento.Descrittore.ResaUi, entita.ScadeUtc), true);
    }

    private async Task ChiudiAsync(
        Paghe.Persistence.Entities.AiRunEntity run, string esito, string? motivo, int giri,
        int? tokenTotali, List<MessaggioChatAi> messaggi, CancellationToken ct) =>
        await store.ChiudiRunAsync(run, esito, motivo, giri, tokenTotali, DigestMessaggi(messaggi), ct);

    private static (string Etichetta, string Testo) ClassificaDiniego(string? motivo) =>
        motivo is not null && motivo.Contains("Quota", StringComparison.OrdinalIgnoreCase)
            ? ("Checkpoint", "Il mandato ha raggiunto il suo checkpoint di letture. "
                + "Il lavoro fatto resta valido: rinnova il mandato dal pannello per continuare.")
        : motivo is not null && motivo.Contains("revocato", StringComparison.OrdinalIgnoreCase)
            ? ("Revocato", "Il mandato è stato revocato: mi fermo qui. "
                + "Puoi emetterne uno nuovo dal pannello quando vuoi.")
            : ("Negato", $"Non posso completare la lettura: {motivo} "
                + "Puoi rinnovare o allargare il mandato dal pannello Aurora.");

    private static void RispondiAlModello(List<MessaggioChatAi> messaggi, string chiamataId, object corpo) =>
        RispondiAlModello(messaggi, chiamataId, JsonSerializer.Serialize(corpo, Json));

    private static void RispondiAlModello(List<MessaggioChatAi> messaggi, string chiamataId, string json) =>
        messaggi.Add(new MessaggioChatAi { Ruolo = "tool", ChiamataId = chiamataId, Contenuto = json });

    /// <summary>
    /// Digest della sequenza completa dei messaggi visti dal modello: rende riconciliabile il
    /// turno (model-visible ⟺ logged) senza conservare alcun testo.
    /// </summary>
    internal static string DigestMessaggi(IReadOnlyList<MessaggioChatAi> messaggi)
    {
        var canonico = new StringBuilder();
        foreach (var m in messaggi)
        {
            canonico.Append(m.Ruolo).Append('\u001F')
                .Append(m.Contenuto ?? "").Append('\u001F')
                .Append(m.ChiamataId ?? "").Append('\u001F');
            foreach (var c in m.Chiamate ?? [])
                canonico.Append(c.Id).Append('|').Append(c.Nome).Append('|').Append(c.ArgomentiJson);
            canonico.Append('\u001E');
        }
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonico.ToString())));
    }
}
