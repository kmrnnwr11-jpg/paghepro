using System.Security.Claims;
using Paghe.Persistence;

namespace Paghe.Api;

/// <summary>Dettaglio anagrafico/contrattuale di un rapporto di lavoro (scheda dipendente).</summary>
public sealed record RapportoDettaglioDto(
    string RapportoId, string DipendenteId,
    string Nome, string Cognome, string CodiceFiscale, string? Iban, bool Anonimizzato,
    string CodiceCcnl, string Livello, string Qualifica, string TipoContratto,
    int NumeroMensilita, bool PartTime, decimal PercentualePartTime,
    int ScattiAnzianita, decimal Superminimo, string Regione, string CodiceCatastaleComune,
    string DataAssunzione,
    decimal FerieResidueIniziali, decimal PermessiResiduiIniziali,
    string? DataCessazione,
    string? TipoPartTimeInps, string? CittadinanzaInps,
    string? QualificaProfessionaleIstat, string? TipoContribuzioneInps,
    string? ProvinciaLavoro,
    decimal? AddizionaleRegionaleConguaglioAnnuo,
    decimal? AddizionaleComunaleSaldoAnnuo,
    decimal? AddizionaleComunaleAccontoAnnuo,
    string? Sesso,
    string? DataDecorrenzaDomicilioFiscale,
    string? RiferimentoProgressiviFiscaliPregressi,
    decimal? RolResiduiIniziali,
    decimal? ExFestivitaResidueIniziali,
    decimal? NettoConcordatoMensile,
    string? NettoConcordatoDecorrenza,
    decimal OreSettimanali,
    decimal? RedditoPresuntoAnnuo = null);

/// <summary>Correzione della scheda dipendente: solo i campi presenti (non null) vengono cambiati.</summary>
public sealed record CorreggiRapportoRequest(
    string? Nome, string? Cognome, string? CodiceFiscale, string? Iban,
    string? CodiceCcnl, string? Livello, string? Qualifica,
    int? NumeroMensilita, bool? PartTime, decimal? PercentualePartTime,
    int? ScattiAnzianita, decimal? Superminimo,
    string? Regione, string? CodiceCatastaleComune, string? DataAssunzione,
    decimal? FondoTfrIniziale,
    decimal? FerieResidueIniziali, decimal? PermessiResiduiIniziali,
    string? DataCessazione, bool? RimuoviDataCessazione,
    string? TipoPartTimeInps, string? CittadinanzaInps,
    string? QualificaProfessionaleIstat, string? TipoContribuzioneInps,
    string? ProvinciaLavoro = null,
    decimal? AddizionaleRegionaleConguaglioAnnuo = null,
    decimal? AddizionaleComunaleSaldoAnnuo = null,
    decimal? AddizionaleComunaleAccontoAnnuo = null,
    string? Sesso = null,
    string? DataDecorrenzaDomicilioFiscale = null,
    string? RiferimentoProgressiviFiscaliPregressi = null,
    decimal? RolResiduiIniziali = null,
    decimal? ExFestivitaResidueIniziali = null,
    decimal? NettoConcordatoMensile = null,
    string? NettoConcordatoDecorrenza = null,
    bool? RimuoviNettoConcordato = null,
    decimal? RedditoPresuntoAnnuo = null,
    bool? RimuoviRedditoPresuntoAnnuo = null);

/// <summary>
/// Endpoint per il dettaglio di un rapporto (scheda dipendente reale), isolato per tenant.
/// Mappato come metodo di estensione per ridurre le modifiche a Program.cs.
/// </summary>
public static class RapportoEndpoints
{
    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;

    public static IEndpointRouteBuilder MapRapportoDettaglio(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rapporti/{rapportoId:guid}", async (
            Guid rapportoId, ClaimsPrincipal user, AnagraficheLavoroService svc) =>
        {
            var r = await svc.GetRapportoAsync(Tenant(user), rapportoId);
            if (r is null)
                return Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." });
            var d = r.Dipendente;
            return Results.Ok(new RapportoDettaglioDto(
                RapportoId: r.Id.ToString(),
                DipendenteId: r.DipendenteId.ToString(),
                Nome: d?.Nome ?? "",
                Cognome: d?.Cognome ?? "",
                CodiceFiscale: d?.CodiceFiscale ?? "",
                Iban: d?.Iban,
                Anonimizzato: d?.Anonimizzato ?? false,
                CodiceCcnl: r.CodiceCcnl,
                Livello: r.Livello,
                Qualifica: r.Qualifica,
                TipoContratto: r.TipoContratto,
                NumeroMensilita: r.NumeroMensilita,
                PartTime: r.PartTime,
                PercentualePartTime: r.PercentualePartTime,
                ScattiAnzianita: r.ScattiAnzianita,
                Superminimo: r.Superminimo,
                Regione: r.Regione,
                CodiceCatastaleComune: r.CodiceCatastaleComune,
                DataAssunzione: r.DataAssunzione.ToString("dd/MM/yyyy"),
                FerieResidueIniziali: r.FerieResidueIniziali,
                PermessiResiduiIniziali: r.PermessiResiduiIniziali,
                DataCessazione: r.DataCessazione?.ToString("dd/MM/yyyy"),
                TipoPartTimeInps: r.TipoPartTimeInps,
                CittadinanzaInps: r.CittadinanzaInps,
                QualificaProfessionaleIstat: r.QualificaProfessionaleIstat,
                TipoContribuzioneInps: r.TipoContribuzioneInps,
                ProvinciaLavoro: r.ProvinciaLavoro,
                AddizionaleRegionaleConguaglioAnnuo: r.AddizionaleRegionaleConguaglioAnnuo,
                AddizionaleComunaleSaldoAnnuo: r.AddizionaleComunaleSaldoAnnuo,
                AddizionaleComunaleAccontoAnnuo: r.AddizionaleComunaleAccontoAnnuo,
                Sesso: d?.Sesso,
                DataDecorrenzaDomicilioFiscale: r.DataDecorrenzaDomicilioFiscale?.ToString("yyyy-MM-dd"),
                RiferimentoProgressiviFiscaliPregressi: r.RiferimentoProgressiviFiscaliPregressi,
                RolResiduiIniziali: r.RolResiduiIniziali,
                ExFestivitaResidueIniziali: r.ExFestivitaResidueIniziali,
                NettoConcordatoMensile: r.NettoConcordatoMensile,
                NettoConcordatoDecorrenza: r.NettoConcordatoDecorrenza?.ToString("yyyy-MM-dd"),
                OreSettimanali: r.OreSettimanali,
                RedditoPresuntoAnnuo: r.RedditoPresuntoAnnuo));
        }).RequireAuthorization();

        // Correzione della scheda (es. dopo un import UNILAV/OCR letto male): aggiorna solo
        // i campi inviati. Isolato per tenant; dipendente anonimizzato non modificabile.
        app.MapPatch("/api/rapporti/{rapportoId:guid}", async (
            Guid rapportoId, CorreggiRapportoRequest req, ClaimsPrincipal user, AnagraficheLavoroService svc) =>
        {
            DateOnly? dataAssunzione = null;
            if (!string.IsNullOrWhiteSpace(req.DataAssunzione))
            {
                if (!DateOnly.TryParse(req.DataAssunzione, out var da))
                    return Results.BadRequest(new { errore = "Data di assunzione non valida (usa il formato AAAA-MM-GG)." });
                dataAssunzione = da;
            }
            DateOnly? dataCessazione = null;
            if (!string.IsNullOrWhiteSpace(req.DataCessazione))
            {
                if (!DateOnly.TryParse(req.DataCessazione, out var dc))
                    return Results.BadRequest(new { errore = "Data di cessazione non valida (usa il formato AAAA-MM-GG)." });
                dataCessazione = dc;
            }
            DateOnly? decorrenzaDomicilio = null;
            if (!string.IsNullOrWhiteSpace(req.DataDecorrenzaDomicilioFiscale))
            {
                if (!DateOnly.TryParse(req.DataDecorrenzaDomicilioFiscale, out var dd))
                    return Results.BadRequest(new { errore = "Decorrenza domicilio fiscale non valida (usa AAAA-MM-GG)." });
                decorrenzaDomicilio = dd;
            }
            DateOnly? decorrenzaNettoConcordato = null;
            if (!string.IsNullOrWhiteSpace(req.NettoConcordatoDecorrenza))
            {
                if (!DateOnly.TryParse(req.NettoConcordatoDecorrenza, out var dn))
                    return Results.BadRequest(new { errore = "Decorrenza del netto concordato non valida (usa AAAA-MM-GG)." });
                decorrenzaNettoConcordato = dn;
            }

            try
            {
                var r = await svc.CorreggiRapportoAsync(Tenant(user), rapportoId,
                    new AnagraficheLavoroService.CorrezioneRapporto(
                        req.Nome, req.Cognome, req.CodiceFiscale, req.Iban,
                        req.CodiceCcnl, req.Livello, req.Qualifica,
                        req.NumeroMensilita, req.PartTime, req.PercentualePartTime,
                        req.ScattiAnzianita, req.Superminimo,
                        req.Regione, req.CodiceCatastaleComune, dataAssunzione,
                        req.FondoTfrIniziale,
                        req.FerieResidueIniziali, req.PermessiResiduiIniziali,
                        dataCessazione, req.RimuoviDataCessazione,
                        req.TipoPartTimeInps, req.CittadinanzaInps,
                        req.QualificaProfessionaleIstat, req.TipoContribuzioneInps,
                        req.ProvinciaLavoro,
                        req.AddizionaleRegionaleConguaglioAnnuo,
                        req.AddizionaleComunaleSaldoAnnuo,
                        req.AddizionaleComunaleAccontoAnnuo,
                        req.Sesso,
                        decorrenzaDomicilio,
                        req.RiferimentoProgressiviFiscaliPregressi,
                        req.RolResiduiIniziali,
                        req.ExFestivitaResidueIniziali,
                        req.NettoConcordatoMensile,
                        decorrenzaNettoConcordato,
                        req.RimuoviNettoConcordato,
                        req.RedditoPresuntoAnnuo,
                        req.RimuoviRedditoPresuntoAnnuo));
                return r is null
                    ? Results.NotFound(new { errore = "Rapporto non trovato per il tenant indicato." })
                    : Results.Ok(new { aggiornato = true });
            }
            catch (ArgumentException ex) // es. cessazione prima dell'assunzione
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
            catch (InvalidOperationException ex) // anonimizzato GDPR
            {
                return Results.Conflict(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        return app;
    }
}
