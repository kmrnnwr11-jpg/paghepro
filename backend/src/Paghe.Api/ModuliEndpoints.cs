using System.Security.Claims;
using Paghe.Core.AdeTelematico;
using Paghe.Documenti;
using Paghe.Persistence;

namespace Paghe.Api;

public sealed record PersonaRequest(
    string Cognome, string Nome, string CodiceFiscale,
    string? Sesso = null, DateOnly? DataNascita = null,
    string? ComuneNascita = null, string? ProvinciaNascita = null,
    string? IndirizzoResidenza = null, string? CapResidenza = null,
    string? ComuneResidenza = null, string? ProvinciaResidenza = null,
    string? Telefono = null, string? Email = null);

public sealed record EstremiRegistrazioneRequest(
    DateOnly? Data = null, string? Ufficio = null, string? Numero = null,
    string? SottoNumero = null, string? Serie = null);

/// <summary>
/// Richiesta di compilazione AA7/10: azienda e rappresentante arrivano dagli archivi
/// (si sceglie chi, non si ricopiano i dati); gli altri campi solo se servono.
/// </summary>
public sealed record CompilaAa710Request(
    Guid AziendaId,
    Guid PersonaId,
    int TipoDichiarazione,
    DateOnly Data,
    string NaturaGiuridica,
    string CodiceCarica = "1",
    string? Sigla = null,
    bool ScrittureContabiliInSede = true,
    string? DescrizioneAttivita = null,
    decimal? VolumeAffariPresunto = null,
    DateOnly? DataInizioProcedimento = null,
    EstremiRegistrazioneRequest? EstremiRegistrazione = null);

/// <summary>
/// Archivio Persone + compilazione del modello ufficiale AA7/10 (PDF Agenzia delle Entrate).
/// I dati si pescano dagli archivi esistenti (azienda + persona): niente ricopiatura a mano.
/// Isolato per tenant. Mappato come estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class ModuliEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    private static object Dto(Paghe.Persistence.Entities.PersonaEntity p) => new
    {
        p.Id, p.Cognome, p.Nome, p.CodiceFiscale, p.Sesso,
        dataNascita = p.DataNascita?.ToString("yyyy-MM-dd"),
        p.ComuneNascita, p.ProvinciaNascita,
        p.IndirizzoResidenza, p.CapResidenza, p.ComuneResidenza, p.ProvinciaResidenza,
        p.Telefono, p.Email,
    };

    private static PersonaInput Input(PersonaRequest req) => new(
        req.Cognome, req.Nome, req.CodiceFiscale, req.Sesso, req.DataNascita,
        req.ComuneNascita, req.ProvinciaNascita, req.IndirizzoResidenza, req.CapResidenza,
        req.ComuneResidenza, req.ProvinciaResidenza, req.Telefono, req.Email);

    public static IEndpointRouteBuilder MapModuli(this IEndpointRouteBuilder app)
    {
        // ---- Archivio persone ----
        app.MapGet("/api/persone", async (ClaimsPrincipal user, PersoneService svc) =>
            Results.Ok((await svc.ElencoAsync(Tenant(user))).Select(Dto)))
            .RequireAuthorization();

        app.MapPost("/api/persone", async (PersonaRequest req, ClaimsPrincipal user, PersoneService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.Cognome) || string.IsNullOrWhiteSpace(req.Nome))
                return Results.BadRequest(new { errore = "Cognome e nome sono obbligatori." });
            if (!ValidatoreFiscale.CodiceFiscaleValido(req.CodiceFiscale))
                return Results.BadRequest(new { errore = "Codice fiscale non valido (controlla i 16 caratteri)." });
            try
            {
                var p = await svc.CreaAsync(Tenant(user), Input(req));
                return Results.Created($"/api/persone/{p.Id}", Dto(p));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPut("/api/persone/{id:guid}", async (
            Guid id, PersonaRequest req, ClaimsPrincipal user, PersoneService svc) =>
        {
            if (!ValidatoreFiscale.CodiceFiscaleValido(req.CodiceFiscale))
                return Results.BadRequest(new { errore = "Codice fiscale non valido (controlla i 16 caratteri)." });
            try
            {
                var p = await svc.AggiornaAsync(Tenant(user), id, Input(req));
                return p is null ? Results.NotFound() : Results.Ok(Dto(p));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapDelete("/api/persone/{id:guid}", async (Guid id, ClaimsPrincipal user, PersoneService svc) =>
            await svc.EliminaAsync(Tenant(user), id) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization();

        // ---- Tabelle ufficiali per i menu (istruzioni AA7/10 2025) ----
        app.MapGet("/api/moduli/aa7-10/tabelle", () => Results.Ok(new
        {
            natureGiuridiche = ModuloAa710.NatureGiuridiche
                .OrderBy(kv => int.Parse(kv.Key))
                .Select(kv => new { codice = kv.Key, descrizione = kv.Value }),
            codiciCarica = ModuloAa710.CodiciCarica
                .OrderBy(kv => int.Parse(kv.Key))
                .Select(kv => new { codice = kv.Key, descrizione = kv.Value }),
        })).RequireAuthorization();

        // ---- Modello ufficiale AA7/10 compilato ----
        app.MapPost("/api/moduli/aa7-10/pdf", async (
            CompilaAa710Request req, ClaimsPrincipal user,
            AziendeService aziende, PersoneService persone) =>
        {
            var tenant = Tenant(user);
            var azienda = await aziende.GetAsync(tenant, req.AziendaId);
            if (azienda is null) return Results.NotFound(new { errore = "Azienda non trovata." });
            var persona = await persone.OttieniAsync(tenant, req.PersonaId);
            if (persona is null) return Results.NotFound(new { errore = "Persona non trovata." });

            var modulo = new ModuloAa710
            {
                TipoDichiarazione = req.TipoDichiarazione,
                CodiceFiscale = azienda.CodiceFiscale,
                PartitaIva = azienda.PartitaIva,
                Data = req.Data,
                EstremiRegistrazione = req.EstremiRegistrazione is { } er
                    ? new EstremiRegistrazioneAa710(er.Data, er.Ufficio, er.Numero, er.SottoNumero, er.Serie)
                    : null,
                Denominazione = azienda.RagioneSociale,
                NaturaGiuridica = req.NaturaGiuridica,
                Sigla = req.Sigla,
                SedeLegale = new SedeAa710(
                    azienda.Indirizzo, azienda.Cap, azienda.Comune, azienda.Provincia,
                    req.ScrittureContabiliInSede),
                CodiceAttivita = azienda.CodiceAteco,
                DescrizioneAttivita = req.DescrizioneAttivita ?? azienda.Settore,
                VolumeAffariPresunto = req.VolumeAffariPresunto,
                Rappresentante = new RappresentanteAa710(
                    persona.Cognome, persona.Nome, req.CodiceCarica, persona.CodiceFiscale,
                    persona.ComuneNascita, persona.ProvinciaNascita, persona.DataNascita,
                    req.DataInizioProcedimento),
            };

            var errori = modulo.Valida();
            if (errori.Count > 0)
                return Results.BadRequest(new { errori });

            var pdf = Aa710PdfUfficiale.Compila(modulo);
            return Results.File(pdf, "application/pdf",
                $"AA7-10-{azienda.RagioneSociale.Replace(' ', '-')}.pdf");
        }).RequireAuthorization();

        return app;
    }
}
