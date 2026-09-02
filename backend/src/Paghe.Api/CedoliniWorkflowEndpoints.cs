using System.Buffers.Binary;
using System.Security.Claims;
using Paghe.Documenti;
using Paghe.Persistence;
using Paghe.Persistence.Cedolini;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

/// <summary>Endpoint piccoli e tenant-isolati per controllo, validazione e chiusura cedolino.</summary>
public static class CedoliniWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapCedoliniWorkflow(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cedolini/{id:guid}/stato", async (
            Guid id, CedolinoWorkflowService workflow, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var stato = await workflow.LeggiStatoAsync(Tenant(user), id, ct);
            return stato is null
                ? Results.NotFound(new { errore = "Cedolino non trovato." })
                : Results.Ok(stato);
        }).RequireAuthorization();

        app.MapPost("/api/cedolini/{id:guid}/controlla", async (
            Guid id, ComandoVersioneCedolino req, CedolinoWorkflowService workflow,
            ClaimsPrincipal user, CancellationToken ct) =>
            await Esegui(() => workflow.ControllaAsync(
                Tenant(user), id, Utente(user), req.VersioneAttesa, ct)))
            .RequireAuthorization();

        app.MapPost("/api/cedolini/{id:guid}/valida", async (
            Guid id, ValidaCedolinoRequest req, CedolinoWorkflowService workflow,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!ÈAdmin(user)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await Esegui(() => workflow.ValidaAsync(
                Tenant(user), id, Utente(user), req.VersioneAttesa,
                new DatiValidazioneCedolino(req.Nominativo, req.Qualifica, req.Riferimento), ct));
        }).RequireAuthorization();

        app.MapPost("/api/cedolini/{id:guid}/chiudi", async (
            Guid id, ChiudiCedolinoRequest req, CedolinoWorkflowService workflow,
            PreferenzeUtenteService preferenze, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!ÈAdmin(user)) return Results.StatusCode(StatusCodes.Status403Forbidden);

            Guid tenantId = Tenant(user);
            Guid utenteId = Utente(user);
            bool stileEsplicito = !string.IsNullOrWhiteSpace(req.Stile);
            string chiaveStile = stileEsplicito
                ? req.Stile!.Trim()
                : await preferenze.LeggiAsync(
                    tenantId, utenteId, PreferenzeUtenteService.StileBusta,
                    CatalogoStiliCedolino.ChiavePredefinita, ct);
            if (!CatalogoStiliCedolino.ProvaDaChiave(chiaveStile, out var stile))
            {
                if (stileEsplicito)
                    return Results.BadRequest(new
                    {
                        errore = $"Stile non valido. Scegli uno dei {CatalogoStiliCedolino.Tutti.Count} stili disponibili.",
                    });

                // Un valore legacy non più riconosciuto non deve impedire la chiusura.
                stile = StileCedolino.Classico;
            }

            string fontFamily = FontDocumenti.Famiglia(await preferenze.LeggiAsync(
                tenantId, utenteId, PreferenzeUtenteService.FontDocumenti, "lato", ct));
            string dicitura = await preferenze.LeggiAsync(
                tenantId, utenteId, PreferenzeUtenteService.DicituraStudio, "", ct);
            return await Esegui(() => workflow.ChiudiAsync(
                tenantId, id, utenteId, req.VersioneAttesa, ct,
                stile, fontFamily, string.IsNullOrWhiteSpace(dicitura) ? null : dicitura));
        }).RequireAuthorization();

        // Nessun GET del file grezzo: l'immagine autentica può comparire soltanto nel PDF chiuso.
        app.MapPost("/api/aziende/{id:guid}/firma-timbro", async (
            Guid id, HttpRequest http, AziendeService aziende, ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!ÈAdmin(user)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!http.HasFormContentType)
                return Results.BadRequest(new { errore = "Inviare un'immagine PNG o JPEG nel campo 'file'." });
            var form = await http.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { errore = "Immagine firma/sigla/timbro mancante." });
            if (file.Length > 500_000)
                return Results.BadRequest(new { errore = "Immagine troppo grande: massimo 500 KB." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var dati = ms.ToArray();
            if (!ImmagineFirmaValida(dati, out var errore))
                return Results.BadRequest(new { errore });

            var salvato = await aziende.SalvaFirmaSiglaTimbroAsync(Tenant(user), id, dati, ct);
            return salvato
                ? Results.Ok(new { salvato = true })
                : Results.NotFound(new { errore = "Azienda non trovata." });
        }).RequireAuthorization().RequireRateLimiting("sensibili").DisableAntiforgery();

        app.MapDelete("/api/aziende/{id:guid}/firma-timbro", async (
            Guid id, AziendeService aziende, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!ÈAdmin(user)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var rimosso = await aziende.SalvaFirmaSiglaTimbroAsync(Tenant(user), id, null, ct);
            return rimosso
                ? Results.NoContent()
                : Results.NotFound(new { errore = "Azienda non trovata." });
        }).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Esegui(Func<Task<StatoWorkflowCedolino>> azione)
    {
        try { return Results.Ok(await azione()); }
        catch (CedolinoNonTrovatoException)
        {
            return Results.NotFound(new { errore = "Cedolino non trovato." });
        }
        catch (ConflittoCedolinoException ex)
        {
            return Results.Conflict(new { errore = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { errore = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { errore = ex.Message });
        }
    }

    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("Tenant non disponibile nel token.");

    private static Guid Utente(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("Utente non disponibile nel token.");

    private static bool ÈAdmin(ClaimsPrincipal user)
    {
        var ruolo = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
        return string.Equals(ruolo, RuoloPolicy.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruolo, AuthService.RuoloSuperAdmin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Controllo strutturale e dimensionale. Il renderer eseguirà anche la decodifica reale prima
    /// della chiusura; qui scartiamo formati estranei e immagini decompression-bomb.
    /// </summary>
    private static bool ImmagineFirmaValida(byte[] dati, out string errore)
    {
        errore = "Formato non supportato: usare PNG o JPEG.";
        if (!DimensioniImmagine(dati, out int larghezza, out int altezza)) return false;
        if (larghezza is < 1 or > 2000 || altezza is < 1 or > 1000
            || (long)larghezza * altezza > 2_000_000)
        {
            errore = "Dimensioni non valide: massimo 2000 × 1000 pixel.";
            return false;
        }
        return true;
    }

    private static bool DimensioniImmagine(byte[] dati, out int larghezza, out int altezza)
    {
        larghezza = altezza = 0;
        if (dati.Length >= 24
            && dati.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            larghezza = BinaryPrimitives.ReadInt32BigEndian(dati.AsSpan(16, 4));
            altezza = BinaryPrimitives.ReadInt32BigEndian(dati.AsSpan(20, 4));
            return true;
        }
        if (dati.Length < 4 || dati[0] != 0xFF || dati[1] != 0xD8) return false;

        int i = 2;
        while (i + 8 < dati.Length)
        {
            if (dati[i++] != 0xFF) continue;
            while (i < dati.Length && dati[i] == 0xFF) i++;
            if (i >= dati.Length) return false;
            byte marker = dati[i++];
            if (marker is 0xD8 or 0xD9) continue;
            if (i + 2 > dati.Length) return false;
            int lunghezza = BinaryPrimitives.ReadUInt16BigEndian(dati.AsSpan(i, 2));
            if (lunghezza < 2 || i + lunghezza > dati.Length) return false;
            bool sof = marker is 0xC0 or 0xC1 or 0xC2 or 0xC3
                or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
            if (sof && lunghezza >= 7)
            {
                altezza = BinaryPrimitives.ReadUInt16BigEndian(dati.AsSpan(i + 3, 2));
                larghezza = BinaryPrimitives.ReadUInt16BigEndian(dati.AsSpan(i + 5, 2));
                return true;
            }
            i += lunghezza;
        }
        return false;
    }
}

public sealed record ComandoVersioneCedolino(int VersioneAttesa);

public sealed record ChiudiCedolinoRequest(int VersioneAttesa, string? Stile = null);

public sealed record ValidaCedolinoRequest(
    int VersioneAttesa,
    string Nominativo,
    string Qualifica,
    string Riferimento);
