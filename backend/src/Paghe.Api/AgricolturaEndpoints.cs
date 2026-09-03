using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Calcolo;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Documenti;
using Paghe.Persistence;
using Paghe.Persistence.Entities;

namespace Paghe.Api;

/// <summary>
/// Endpoint del settore agricolo: tabelle salariali provinciali (CPL) e calcolo della busta
/// dell'operaio agricolo. Le tabelle sono condivise tra tutti gli studi come i CCNL, quindi
/// la scrittura è riservata all'amministratore della piattaforma.
/// </summary>
public static class AgricolturaEndpoints
{
    /// <summary>Tabella salariale ricevuta dall'esterno: stesso schema dell'import CCNL.</summary>
    public sealed record TariffaAgricolaInput(
        string Livello, string Descrizione,
        decimal SalarioProvincialeOrario, decimal TerzoElementoOrario);

    public sealed record ContributoAgricoloInput(
        string Codice, string Descrizione, decimal Aliquota, bool ACaricoDipendente);

    public sealed record ImportTabelleAgricoleInput(
        int Anno, string Provincia, decimal AliquotaContributiDeducibili, string Fonte,
        List<TariffaAgricolaInput> Tariffe, List<ContributoAgricoloInput> Contributi);

    /// <summary>Richiesta di calcolo di una busta agricola, senza archiviazione.</summary>
    public sealed record AnteprimaCedolinoAgricoloInput(
        int Anno, int Mese, string Provincia, string Livello, string Tipo,
        decimal OreOrdinarie, int GiornateLavorate, int GiorniDetrazione,
        decimal UnaTantum, decimal DetrazioniGiaGoduteAnno,
        decimal ArrotondamentoRiportato, bool UltimoCedolino,
        decimal? RedditoPresuntoAnnuo, string? Regione, string? CodiceCatastaleComune);

    /// <summary>
    /// Dati variabili del mese. Provincia CPL, livello, OTD/OTI e residenza fiscale non arrivano
    /// dal browser: sono ricavati dal rapporto appartenente al tenant autenticato.
    /// </summary>
    /// <summary>Un gruppo di giorni di trasferta pagati alla stessa indennità giornaliera.</summary>
    public sealed record TrasfertaBatchInput(decimal Giorni, decimal ImportoGiorno);

    public sealed record DatiMensiliCedolinoAgricoloInput(
        int Anno, int Mese, decimal OreOrdinarie, int GiornateLavorate,
        int GiorniDetrazione, decimal UnaTantum, decimal DetrazioniGiaGoduteAnno,
        decimal ArrotondamentoRiportato, bool UltimoCedolino,
        decimal? RedditoPresuntoAnnuo,
        decimal GiorniTrasferta = 0m, decimal IndennitaTrasfertaGiornaliera = 0m,
        decimal RimborsiSpeseDocumentate = 0m,
        List<TrasfertaBatchInput>? Trasferte = null,
        // Solo per l'archiviazione: chi dichiara di aver controllato la busta. Con un
        // nominativo il PDF conservato perde «BOZZA DA VALIDARE»; vuoto = resta bozza.
        string? ValidataDa = null);

    /// <summary>
    /// Richiesta della denuncia PosAgri del mese: periodo più i codici della posizione INPS
    /// (tabelle dell'allegato tecnico UniEmens), che oggi non stanno in anagrafica.
    /// </summary>
    public sealed record PosAgriMeseInput(
        int Anno, int Mese, string Cida, string TipoManodopera, string ZonaTariffaria,
        string CodiceContratto, string CodiceRetribuzione, string CodiceIstatFondo,
        string CategoriaContrattuale, string Area, string Livello,
        string TipoDichiarazione = "P");

    private sealed record CalcoloAgricoloDaRapporto(
        Cedolino Cedolino,
        IntestazioneCedolino Intestazione,
        RapportoAgricolo Rapporto,
        CartellinoAgricolo Cartellino,
        ParametriAgricoli Parametri,
        Guid AziendaId,
        List<SegnalazioneBustaAgricola> Segnalazioni,
        string OrigineRedditoPresunto,
        ElaborazioneAgricolaEntity? Elaborazione);

    public static IEndpointRouteBuilder MapAgricoltura(this IEndpointRouteBuilder app)
    {
        // Quali province hanno tabelle caricate: senza questo elenco l'utente scoprirebbe che
        // la sua provincia manca solo al momento di elaborare la busta.
        app.MapGet("/api/agricoltura/province", async (PagheDbContext db) =>
        {
            var province = await db.ParametriAgricoli
                .OrderBy(p => p.Provincia).ThenByDescending(p => p.Anno)
                .Select(p => new
                {
                    p.Anno,
                    p.Provincia,
                    p.Fonte,
                    Calcolabile = db.ParametriAnno.Any(n => n.Anno == p.Anno),
                })
                .ToListAsync();

            return Results.Ok(province.Select(p => new
            {
                p.Anno,
                p.Provincia,
                p.Fonte,
                p.Calcolabile,
                motivo = p.Calcolabile
                    ? null
                    : $"Mancano i parametri fiscali e contributivi nazionali {p.Anno}.",
            }));
        })
            .RequireAuthorization();

        app.MapGet("/api/agricoltura/tabelle/{anno:int}/{provincia}", async (
            int anno, string provincia, ParametriNormativiService parametri, CancellationToken ct) =>
        {
            try
            {
                var p = await parametri.CaricaParametriAgricoliAsync(anno, provincia, ct);
                return Results.Ok(new
                {
                    p.Anno, p.Provincia, p.Fonte,
                    p.AliquotaContributiDeducibili,
                    aliquotaTotaleDipendente = p.AliquotaTotaleDipendente,
                    tariffe = p.Tariffe.Select(t => new
                    {
                        t.Livello, t.Descrizione,
                        t.SalarioProvincialeOrario, t.TerzoElementoOrario, t.PagaOraria,
                    }),
                    contributi = p.Contributi,
                });
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { errore = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/agricoltura/tabelle/importa", async (
            ImportTabelleAgricoleInput input, ClaimsPrincipal user,
            ParametriNormativiService parametri, CancellationToken ct) =>
        {
            if (!user.IsInRole(AuthService.RuoloSuperAdmin)
                && user.FindFirst(ClaimTypes.Role)?.Value != AuthService.RuoloSuperAdmin)
                return Results.Json(
                    new { errore = "Operazione riservata all'amministratore della piattaforma." },
                    statusCode: StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(input.Provincia) || input.Tariffe is not { Count: > 0 })
                return Results.BadRequest(new
                {
                    errore = "Servono la provincia e almeno una tariffa salariale.",
                });
            if (string.IsNullOrWhiteSpace(input.Fonte))
                return Results.BadRequest(new
                {
                    errore = "Indica la fonte della tabella: una paga senza fonte non è verificabile.",
                });

            var (tariffe, contributi) = await parametri.ImportaParametriAgricoliAsync(new ParametriAgricoli
            {
                Anno = input.Anno,
                Provincia = input.Provincia,
                Fonte = input.Fonte,
                AliquotaContributiDeducibili = input.AliquotaContributiDeducibili,
                Tariffe = input.Tariffe
                    .Select(t => new TariffaAgricola(
                        t.Livello, t.Descrizione, t.SalarioProvincialeOrario, t.TerzoElementoOrario))
                    .ToList(),
                Contributi = (input.Contributi ?? [])
                    .Select(c => new ContributoAgricolo(
                        c.Codice, c.Descrizione, c.Aliquota, c.ACaricoDipendente))
                    .ToList(),
            }, ct);

            return Results.Ok(new { tariffeCaricate = tariffe, contributiCaricati = contributi });
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // Calcolo di prova: mostra la busta senza archiviarla. Serve al consulente per
        // confrontare il risultato con la busta del vecchio software prima di fidarsi.
        app.MapPost("/api/agricoltura/cedolino/anteprima", async (
            AnteprimaCedolinoAgricoloInput input,
            ParametriNormativiService parametri, CancellationToken ct) =>
        {
            if (!Enum.TryParse<TipoOperaioAgricolo>(input.Tipo, ignoreCase: true, out var tipo))
                return Results.BadRequest(new
                {
                    errore = "Tipo non valido: usare 'Determinato' (OTD) o 'Indeterminato' (OTI).",
                });
            if (input.OreOrdinarie < 0m || input.GiornateLavorate < 0)
                return Results.BadRequest(new { errore = "Ore e giornate non possono essere negative." });

            try
            {
                var nazionali = await parametri.CaricaParametriAsync(input.Anno, ct);
                var agricoli = await parametri.CaricaParametriAgricoliAsync(
                    input.Anno, input.Provincia, ct);

                var cedolino = new MotoreCalcoloCedolinoAgricolo(nazionali, agricoli).Calcola(
                    new RapportoAgricolo
                    {
                        Provincia = agricoli.Provincia,
                        Tipo = tipo,
                        Livello = input.Livello,
                        RedditoPresuntoAnnuo = input.RedditoPresuntoAnnuo,
                        Regione = input.Regione ?? "",
                        CodiceCatastaleComune = input.CodiceCatastaleComune ?? "",
                    },
                    new CartellinoAgricolo
                    {
                        Anno = input.Anno, Mese = input.Mese,
                        OreOrdinarie = input.OreOrdinarie,
                        GiornateLavorate = input.GiornateLavorate,
                        GiorniDetrazione = input.GiorniDetrazione,
                        UnaTantum = input.UnaTantum,
                        DetrazioniGiaGoduteAnno = input.DetrazioniGiaGoduteAnno,
                        ArrotondamentoRiportato = input.ArrotondamentoRiportato,
                        UltimoCedolino = input.UltimoCedolino,
                    });

                return Results.Ok(new { cedolino, fonteTabelle = agricoli.Fonte });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // Anteprima operativa: usa esclusivamente l'anagrafica del rapporto del tenant. Non
        // archivia ancora la busta, perché cartellino agricolo, progressivi e PosAgri non hanno
        // ancora un workflow persistente equivalente a quello dei cedolini ordinari.
        app.MapPost("/api/agricoltura/rapporti/{rapportoId:guid}/cedolino/anteprima", async (
            Guid rapportoId,
            DatiMensiliCedolinoAgricoloInput input,
            ClaimsPrincipal user,
            PagheDbContext db,
            ParametriNormativiService parametri,
            CancellationToken ct) =>
        {
            try
            {
                var calcolo = await CalcolaDaRapportoAsync(
                    Tenant(user), rapportoId, input, db, parametri, ct);
                return Results.Ok(new
                {
                    cedolino = calcolo.Cedolino,
                    fonteTabelle = calcolo.Parametri.Fonte,
                    provinciaCpl = calcolo.Parametri.Provincia,
                    tipoOperaio = Sigla(calcolo.Rapporto.Tipo),
                    livelloTariffa = calcolo.Rapporto.Livello,
                    qualifica = calcolo.Intestazione.Qualifica,
                    giorniDetrazione = calcolo.Cartellino.GiorniDetrazione,
                    redditoPresuntoAnnuo = calcolo.Rapporto.RedditoPresuntoAnnuo,
                    origineRedditoPresunto = calcolo.OrigineRedditoPresunto,
                    segnalazioni = calcolo.Segnalazioni,
                    statoElaborazione = calcolo.Elaborazione?.Stato
                        ?? ElaborazioneAgricolaEntity.StatoBozza,
                    bozzaDaValidare = true,
                    archiviato = false,
                    avvertenza = "Anteprima non archiviata: non costituisce LUL, UniEmens/PosAgri o F24.",
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { errore = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        app.MapPost("/api/agricoltura/rapporti/{rapportoId:guid}/cedolino/pdf", async (
            Guid rapportoId,
            DatiMensiliCedolinoAgricoloInput input,
            ClaimsPrincipal user,
            PagheDbContext db,
            ParametriNormativiService parametri,
            PreferenzeUtenteService preferenze,
            HttpResponse response,
            CancellationToken ct) =>
        {
            try
            {
                var calcolo = await CalcolaDaRapportoAsync(
                    Tenant(user), rapportoId, input, db, parametri, ct);
                var (fontDocumenti, dicituraStudio) = await PreferenzeDocumentiAsync(user, preferenze, ct);
                var pdf = CedolinoAgricoloPdf.Genera(
                    calcolo.Cedolino,
                    calcolo.Intestazione,
                    calcolo.Rapporto,
                    calcolo.Cartellino,
                    calcolo.Parametri,
                    fontFamily: fontDocumenti,
                    dicituraStudio: dicituraStudio,
                    // Lo stato in calce è quello dell'ELABORAZIONE mensile, mai della busta.
                    validataDa: StatoValidato(calcolo.Elaborazione)?.ValidataDa,
                    validataIl: StatoValidato(calcolo.Elaborazione)?.ValidataIl is { } quando
                        ? DateOnly.FromDateTime(quando)
                        : null,
                    segnalazioni: calcolo.Segnalazioni);

                response.Headers.CacheControl = "private, no-store";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["X-PaghePro-Stato"] = "BozzaAgricola";
                return Results.File(
                    pdf,
                    "application/pdf",
                    CedoliniAgricoliService.NomeFileBusta(
                        calcolo.Intestazione.Dipendente, input.Anno, input.Mese, "bozza"));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { errore = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // Archiviazione della busta agricola. Va in un archivio PROPRIO (tabella
        // CedoliniAgricoli): i flussi ordinari F24/UniEmens/CU leggono i cedolini ordinari e
        // una busta agricola lì dentro produrrebbe adempimenti sbagliati in silenzio.
        app.MapPost("/api/agricoltura/rapporti/{rapportoId:guid}/cedolino/archivia", async (
            Guid rapportoId,
            DatiMensiliCedolinoAgricoloInput input,
            ClaimsPrincipal user,
            PagheDbContext db,
            ParametriNormativiService parametri,
            CedoliniAgricoliService archivio,
            PreferenzeUtenteService preferenze,
            CancellationToken ct) =>
        {
            var utenteId = Utente(user);
            if (utenteId == Guid.Empty) return Results.Unauthorized();
            try
            {
                var calcolo = await CalcolaDaRapportoAsync(
                    Tenant(user), rapportoId, input, db, parametri, ct);
                var (fontDocumenti, dicituraStudio) = await PreferenzeDocumentiAsync(user, preferenze, ct);
                // L'archiviazione produce SEMPRE una bozza: lo stato validato appartiene
                // all'elaborazione mensile e si ottiene solo dal suo endpoint di validazione.
                var pdf = CedolinoAgricoloPdf.Genera(
                    calcolo.Cedolino, calcolo.Intestazione, calcolo.Rapporto,
                    calcolo.Cartellino, calcolo.Parametri,
                    fontFamily: fontDocumenti,
                    dicituraStudio: dicituraStudio,
                    segnalazioni: calcolo.Segnalazioni);

                var busta = await archivio.ArchiviaAsync(Tenant(user), utenteId, new CedolinoAgricoloEntity
                {
                    AziendaId = calcolo.AziendaId,
                    RapportoLavoroId = rapportoId,
                    Anno = input.Anno,
                    Mese = input.Mese,
                    OreOrdinarie = input.OreOrdinarie,
                    GiornateLavorate = input.GiornateLavorate,
                    GiorniDetrazione = calcolo.Cartellino.GiorniDetrazione,
                    UnaTantum = input.UnaTantum,
                    GiorniTrasferta = calcolo.Cartellino.Trasferte.Sum(t => t.Giorni),
                    IndennitaTrasfertaGiornaliera = 0m,
                    TrasferteJson = calcolo.Cartellino.Trasferte.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(calcolo.Cartellino.Trasferte
                            .Select(t => new TrasfertaBatchInput(t.Giorni, t.ImportoGiorno)))
                        : null,
                    RimborsiSpeseDocumentate = input.RimborsiSpeseDocumentate,
                    DetrazioniGiaGoduteAnno = input.DetrazioniGiaGoduteAnno,
                    ArrotondamentoRiportato = calcolo.Cartellino.ArrotondamentoRiportato,
                    UltimoCedolino = calcolo.Cartellino.UltimoCedolino,
                    RedditoPresuntoAnnuo = calcolo.Rapporto.RedditoPresuntoAnnuo ?? 0m,
                    OrigineRedditoPresunto = calcolo.OrigineRedditoPresunto,
                    ArrotondamentoDelMese = calcolo.Cartellino.UltimoCedolino
                        ? 0m
                        : calcolo.Cedolino.Righe
                            .FirstOrDefault(r => r.Codice == "AGR_ARROT")?.Importo ?? 0m,
                    SegnalazioniJson = calcolo.Segnalazioni.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(calcolo.Segnalazioni)
                        : null,
                    TotaleCompetenze = calcolo.Cedolino.TotaleCompetenze,
                    TotaleTrattenute = calcolo.Cedolino.TotaleTrattenute,
                    ImponibileInps = calcolo.Cedolino.ImponibileInps,
                    ContributiDipendente = calcolo.Cedolino.ContributiDipendente,
                    ImponibileIrpef = calcolo.Cedolino.ImponibileIrpef,
                    IrpefNetta = calcolo.Cedolino.IrpefNetta,
                    NettoInBusta = calcolo.Cedolino.NettoInBusta,
                    ProvinciaCpl = calcolo.Parametri.Provincia,
                    LivelloTariffa = calcolo.Rapporto.Livello,
                    FonteTabelle = calcolo.Parametri.Fonte,
                }, pdf, ct);

                // Una busta riarchiviata cambia i numeri del mese: un'elaborazione già
                // validata torna bozza, la validazione va ripetuta sui dati nuovi.
                var elaborazione = await db.ElaborazioniAgricole.FirstOrDefaultAsync(
                    e => e.TenantId == Tenant(user) && e.AziendaId == calcolo.AziendaId
                        && e.Anno == input.Anno && e.Mese == input.Mese, ct);
                bool validazioneDecaduta = false;
                if (elaborazione?.Stato == ElaborazioneAgricolaEntity.StatoValidata)
                {
                    elaborazione.Stato = ElaborazioneAgricolaEntity.StatoBozza;
                    elaborazione.ValidataDa = null;
                    elaborazione.ValidataIl = null;
                    await db.SaveChangesAsync(ct);
                    validazioneDecaduta = true;
                }

                return Results.Ok(new
                {
                    busta.Id,
                    busta.Anno,
                    busta.Mese,
                    busta.NettoInBusta,
                    busta.Versione,
                    busta.HashPdfSha256,
                    archiviatoIl = busta.ArchiviatoIl,
                    segnalazioni = calcolo.Segnalazioni,
                    origineRedditoPresunto = calcolo.OrigineRedditoPresunto,
                    statoElaborazione = ElaborazioneAgricolaEntity.StatoBozza,
                    validazioneDecaduta,
                    avviso = "Busta archiviata come prospetto da validare: non costituisce LUL, "
                        + "PosAgri o F24. La validazione avviene sull'elaborazione mensile."
                        + (validazioneDecaduta
                            ? " L'elaborazione del mese era validata ed è tornata bozza."
                            : ""),
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { errore = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // Elenco delle buste agricole archiviate dell'azienda (facoltativi anno e mese).
        app.MapGet("/api/aziende/{aziendaId:guid}/cedolini-agricoli", async (
            Guid aziendaId, ClaimsPrincipal user, PagheDbContext db,
            CedoliniAgricoliService archivio, CancellationToken ct,
            int? anno = null, int? mese = null) =>
        {
            var buste = await archivio.ElencoAsync(Tenant(user), aziendaId, anno, mese, ct);
            var rapportoIds = buste.Select(b => b.RapportoLavoroId).Distinct().ToList();
            var nominativi = await db.Rapporti.AsNoTracking()
                .Where(r => r.TenantId == Tenant(user) && rapportoIds.Contains(r.Id)
                    && r.Dipendente != null)
                .Select(r => new { r.Id, Nominativo = r.Dipendente!.Cognome + " " + r.Dipendente.Nome })
                .ToDictionaryAsync(x => x.Id, x => x.Nominativo.Trim(), ct);
            return Results.Ok(buste.Select(b => new
            {
                b.Id,
                b.RapportoLavoroId,
                nominativo = nominativi.TryGetValue(b.RapportoLavoroId, out var n) ? n : "",
                b.Anno,
                b.Mese,
                b.GiornateLavorate,
                b.OreOrdinarie,
                b.NettoInBusta,
                b.ProvinciaCpl,
                b.LivelloTariffa,
                b.Versione,
                archiviatoIl = b.ArchiviatoIl,
                validataDa = b.ValidataDa,
            }));
        }).RequireAuthorization();

        // Riscarica il PDF archiviato: stesso documento consegnato, impronta verificata.
        app.MapGet("/api/cedolini-agricoli/{id:guid}/pdf", async (
            Guid id, ClaimsPrincipal user, CedoliniAgricoliService archivio,
            HttpResponse response, CancellationToken ct) =>
        {
            try
            {
                var documento = await archivio.LeggiPdfAsync(Tenant(user), id, ct);
                if (documento is null)
                    return Results.NotFound(new { errore = "Busta agricola archiviata non trovata." });
                response.Headers.CacheControl = "private, no-store";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["X-PaghePro-Stato"] = "AgricolaArchiviata";
                response.Headers["X-PaghePro-SHA256"] = documento.HashSha256;
                return Results.File(documento.Pdf, "application/pdf", documento.NomeFile);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // Denuncia mensile della manodopera agricola (sezione PosAgri del flusso UniEmens,
        // erede della DMAG), costruita dalle buste agricole archiviate del periodo. I codici
        // della posizione INPS (CIDA, zona, contratto, inquadramento) arrivano dalla request:
        // sono dati dell'allegato tecnico che l'archivio non ha ancora e non deve inventare.
        app.MapPost("/api/aziende/{aziendaId:guid}/posagri/mese", async (
            Guid aziendaId, PosAgriMeseInput input, ClaimsPrincipal user,
            PosAgriService servizio, CancellationToken ct) =>
        {
            if (input.Anno is < 2000 or > 2100 || input.Mese is < 1 or > 12)
                return Results.BadRequest(new { errore = "Periodo della denuncia non valido." });
            try
            {
                var esito = await servizio.GeneraDelMeseAsync(
                    Tenant(user), aziendaId, input.Anno, input.Mese,
                    new CodiciPosAgri(
                        input.Cida, input.TipoManodopera, input.ZonaTariffaria,
                        input.CodiceContratto, input.CodiceRetribuzione,
                        input.CodiceIstatFondo, input.CategoriaContrattuale,
                        input.Area, input.Livello, input.TipoDichiarazione), ct);
                return Results.Ok(new
                {
                    xml = esito.Xml,
                    erroriSchema = esito.ErroriSchema,
                    daCompletare = esito.DaCompletare,
                    lavoratori = esito.Lavoratori,
                    bozzaDaValidare = true,
                    avvertenza = "Bozza da validare col consulente: la retribuzione denunciata "
                        + "è l'imponibile contributivo delle buste archiviate. Il file va "
                        + "passato dal software di controllo ufficiale INPS e caricato dal "
                        + "cassetto previdenziale o da un intermediario abilitato: il "
                        + "software non trasmette.",
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { errore = ex.Message });
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // ---- Elaborazione mensile: stato unico del mese (bozza/validata) ----------------

        app.MapGet("/api/aziende/{aziendaId:guid}/agricoltura/elaborazioni/{anno:int}/{mese:int}", async (
            Guid aziendaId, int anno, int mese, ClaimsPrincipal user,
            PagheDbContext db, CedoliniAgricoliService archivio, CancellationToken ct) =>
        {
            var quadro = await QuadroElaborazioneAsync(Tenant(user), aziendaId, anno, mese, db, archivio, ct);
            return Results.Ok(new
            {
                anno,
                mese,
                stato = quadro.Elaborazione?.Stato ?? ElaborazioneAgricolaEntity.StatoBozza,
                validataDa = quadro.Elaborazione?.ValidataDa,
                validataIl = quadro.Elaborazione?.ValidataIl,
                buste = quadro.Righe.Count,
                erroriAperti = quadro.Righe.SelectMany(r => r.Segnalazioni)
                    .Concat(quadro.SegnalazioniMensili)
                    .Count(s => s.Livello == LivelloSegnalazione.Errore),
                righe = quadro.Righe.Select(r => new
                {
                    r.Nominativo,
                    r.Busta.RapportoLavoroId,
                    r.Busta.NettoInBusta,
                    segnalazioni = r.Segnalazioni,
                }),
                segnalazioniMensili = quadro.SegnalazioniMensili,
            });
        }).RequireAuthorization();

        // Valida l'elaborazione del mese: rifiuta finché una qualunque busta archiviata ha
        // ERRORI dal validatore, poi ristampa i PDF con lo stato validato in calce.
        app.MapPost("/api/aziende/{aziendaId:guid}/agricoltura/elaborazioni/{anno:int}/{mese:int}/valida", async (
            Guid aziendaId, int anno, int mese, ValidaElaborazioneInput input,
            ClaimsPrincipal user, PagheDbContext db, CedoliniAgricoliService archivio,
            ParametriNormativiService parametri, PreferenzeUtenteService preferenze,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.ValidataDa))
                return Results.BadRequest(new
                {
                    errore = "Indica nome e cognome di chi ha controllato l'elaborazione.",
                });
            var tenantId = Tenant(user);
            var quadro = await QuadroElaborazioneAsync(tenantId, aziendaId, anno, mese, db, archivio, ct);
            if (quadro.Righe.Count == 0)
                return Results.BadRequest(new
                {
                    errore = "Nessuna busta agricola archiviata per il periodo: non c'è nulla da validare.",
                });

            // La validazione non si fida delle segnalazioni fotografate all'archiviazione:
            // ricalcola ogni busta dagli input conservati, con il validatore di oggi, e
            // pretende che i numeri non siano cambiati nel frattempo.
            var errori = new List<object>();
            var ricalcoli = new List<(RigaQuadro Riga, CalcoloAgricoloDaRapporto Calcolo)>();
            foreach (var riga in quadro.Righe)
            {
                CalcoloAgricoloDaRapporto calcolo;
                try
                {
                    calcolo = await CalcolaDaRapportoAsync(
                        tenantId, riga.Busta.RapportoLavoroId,
                        InputDaBustaArchiviata(riga.Busta), db, parametri, ct);
                }
                catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
                {
                    errori.Add(new
                    {
                        lavoratore = riga.Nominativo, codice = "RICALCOLO_FALLITO",
                        messaggio = ex.Message,
                    });
                    continue;
                }
                if (calcolo.Cedolino.NettoInBusta != riga.Busta.NettoInBusta)
                    errori.Add(new
                    {
                        lavoratore = riga.Nominativo, codice = "NUMERI_CAMBIATI",
                        messaggio = $"Il ricalcolo dà un netto di {calcolo.Cedolino.NettoInBusta:0.00} € "
                            + $"contro i {riga.Busta.NettoInBusta:0.00} € archiviati: i parametri sono "
                            + "cambiati, riarchiviare la busta prima di validare.",
                    });
                errori.AddRange(calcolo.Segnalazioni
                    .Where(s => s.Livello == LivelloSegnalazione.Errore)
                    .Select(s => (object)new
                    {
                        lavoratore = riga.Nominativo, codice = s.Codice, messaggio = s.Messaggio,
                    }));
                ricalcoli.Add((riga, calcolo));
            }
            if (string.IsNullOrWhiteSpace(quadro.Azienda?.MatricolaInps))
                errori.Add(new
                {
                    lavoratore = "", codice = "MATRICOLA_INPS_MANCANTE",
                    messaggio = "L'azienda non ha la matricola/posizione INPS: in un'elaborazione "
                        + "validata è obbligatoria.",
                });
            errori.AddRange(quadro.SegnalazioniMensili
                .Where(s => s.Livello == LivelloSegnalazione.Errore)
                .Select(s => (object)new { lavoratore = "", codice = s.Codice, messaggio = s.Messaggio }));
            if (errori.Count > 0)
                return Results.BadRequest(new
                {
                    errore = "L'elaborazione non è validabile: il validatore segnala errori aperti.",
                    errori,
                });

            var elaborazione = quadro.Elaborazione;
            if (elaborazione is null)
            {
                elaborazione = new ElaborazioneAgricolaEntity
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AziendaId = aziendaId,
                    Anno = anno, Mese = mese,
                };
                db.ElaborazioniAgricole.Add(elaborazione);
            }
            else
            {
                db.ElaborazioniAgricole.Attach(elaborazione);
            }
            elaborazione.Stato = ElaborazioneAgricolaEntity.StatoValidata;
            elaborazione.ValidataDa = input.ValidataDa.Trim();
            elaborazione.ValidataIl = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Ristampa dei PDF archiviati con lo stato dell'elaborazione: stessi numeri
            // (garantito dal ricalcolo del quadro), piè di pagina validato.
            var (fontDocumenti, dicituraStudio) = await PreferenzeDocumentiAsync(user, preferenze, ct);
            int ristampate = 0;
            foreach (var (riga, calcolo) in ricalcoli)
            {
                var pdf = CedolinoAgricoloPdf.Genera(
                    calcolo.Cedolino, calcolo.Intestazione, calcolo.Rapporto,
                    calcolo.Cartellino, calcolo.Parametri,
                    fontFamily: fontDocumenti, dicituraStudio: dicituraStudio,
                    validataDa: elaborazione.ValidataDa,
                    validataIl: DateOnly.FromDateTime(elaborazione.ValidataIl.Value));
                await archivio.AggiornaPdfAsync(tenantId, riga.Busta.Id, pdf, ct);
                ristampate++;
            }

            return Results.Ok(new
            {
                stato = elaborazione.Stato,
                validataDa = elaborazione.ValidataDa,
                validataIl = elaborazione.ValidataIl,
                busteRistampate = ristampate,
                avvisi = ricalcoli.SelectMany(r => r.Calcolo.Segnalazioni)
                    .Concat(quadro.SegnalazioniMensili)
                    .Where(s => s.Livello == LivelloSegnalazione.Avviso),
            });
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        // ---- Riepilogo mensile aziendale: totali, elenco e segnalazioni -----------------

        app.MapGet("/api/aziende/{aziendaId:guid}/agricoltura/riepilogo/{anno:int}/{mese:int}", async (
            Guid aziendaId, int anno, int mese, ClaimsPrincipal user,
            PagheDbContext db, CedoliniAgricoliService archivio, HttpResponse response,
            CancellationToken ct, string formato = "json") =>
        {
            var quadro = await QuadroElaborazioneAsync(Tenant(user), aziendaId, anno, mese, db, archivio, ct);
            if (quadro.Righe.Count == 0)
                return Results.NotFound(new
                {
                    errore = "Nessuna busta agricola archiviata per il periodo.",
                });
            var riepilogo = new RiepilogoAgricoloMensile
            {
                Anno = anno,
                Mese = mese,
                StatoElaborazione = quadro.Elaborazione?.Stato ?? ElaborazioneAgricolaEntity.StatoBozza,
                ValidataDa = quadro.Elaborazione?.ValidataDa,
                Righe = quadro.Righe.Select(r => new RigaRiepilogoAgricolo(
                    r.Nominativo,
                    r.Busta.OreOrdinarie,
                    r.Busta.GiornateLavorate,
                    r.Busta.TotaleCompetenze,
                    TotaleTrasferte(r.Busta),
                    r.Busta.TotaleTrattenute,
                    r.Busta.ContributiDipendente,
                    r.Busta.ArrotondamentoDelMese,
                    r.Busta.NettoInBusta,
                    r.Segnalazioni)).ToList(),
                SegnalazioniMensili = quadro.SegnalazioniMensili,
            };

            switch (formato.ToLowerInvariant())
            {
                case "csv":
                    response.Headers.CacheControl = "private, no-store";
                    return Results.File(
                        System.Text.Encoding.UTF8.GetBytes(riepilogo.Csv()),
                        "text/csv",
                        $"riepilogo-agricolo-{anno}-{mese:00}.csv");
                case "pdf":
                    response.Headers.CacheControl = "private, no-store";
                    return Results.File(
                        RiepilogoAgricoloPdf.Genera(
                            riepilogo, quadro.Azienda?.RagioneSociale ?? "Azienda"),
                        "application/pdf",
                        $"riepilogo-agricolo-{anno}-{mese:00}.pdf");
                default:
                    return Results.Ok(riepilogo);
            }
        }).RequireAuthorization().RequireRateLimiting("sensibili");

        return app;
    }

    public sealed record ValidaElaborazioneInput(string ValidataDa);

    private sealed record RigaQuadro(
        CedolinoAgricoloEntity Busta,
        string Nominativo,
        List<SegnalazioneBustaAgricola> Segnalazioni);

    /// <summary>Ricostruisce l'input mensile dai dati conservati nella busta archiviata.</summary>
    private static DatiMensiliCedolinoAgricoloInput InputDaBustaArchiviata(CedolinoAgricoloEntity busta) => new(
        busta.Anno, busta.Mese, busta.OreOrdinarie, busta.GiornateLavorate,
        busta.GiorniDetrazione, busta.UnaTantum, busta.DetrazioniGiaGoduteAnno,
        busta.ArrotondamentoRiportato, busta.UltimoCedolino,
        busta.RedditoPresuntoAnnuo > 0m ? busta.RedditoPresuntoAnnuo : null,
        GiorniTrasferta: busta.TrasferteJson is { Length: > 0 } ? 0m : busta.GiorniTrasferta,
        IndennitaTrasfertaGiornaliera: busta.TrasferteJson is { Length: > 0 }
            ? 0m : busta.IndennitaTrasfertaGiornaliera,
        RimborsiSpeseDocumentate: busta.RimborsiSpeseDocumentate,
        Trasferte: busta.TrasferteJson is { Length: > 0 } json
            ? System.Text.Json.JsonSerializer.Deserialize<List<TrasfertaBatchInput>>(json)
            : null);

    /// <summary>Totale delle indennità di trasferta dichiarate nella busta archiviata.</summary>
    private static decimal TotaleTrasferte(CedolinoAgricoloEntity busta) =>
        busta.TrasferteJson is { Length: > 0 } json
            ? (System.Text.Json.JsonSerializer.Deserialize<List<TrasfertaBatchInput>>(json) ?? [])
                .Sum(t => t.Giorni * t.ImportoGiorno)
            : busta.GiorniTrasferta * busta.IndennitaTrasfertaGiornaliera;

    private sealed record QuadroElaborazione(
        ElaborazioneAgricolaEntity? Elaborazione,
        List<RigaQuadro> Righe,
        List<SegnalazioneBustaAgricola> SegnalazioniMensili,
        AziendaEntity? Azienda);

    /// <summary>
    /// Fotografia dell'elaborazione mensile: per ogni busta archiviata ricalcola dagli
    /// stessi input il cedolino e le segnalazioni (validatore incluso), più i controlli
    /// che hanno senso solo sull'insieme (cessazioni difformi a parità di assunzione,
    /// arrotondamenti rimasti a carico dell'azienda per i cessati).
    /// </summary>
    private static async Task<QuadroElaborazione> QuadroElaborazioneAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese,
        PagheDbContext db, CedoliniAgricoliService archivio, CancellationToken ct)
    {
        var elaborazione = await db.ElaborazioniAgricole.AsNoTracking().FirstOrDefaultAsync(
            e => e.TenantId == tenantId && e.AziendaId == aziendaId
                && e.Anno == anno && e.Mese == mese, ct);
        var azienda = await db.Aziende.AsNoTracking().FirstOrDefaultAsync(
            a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        var buste = await archivio.ElencoAsync(tenantId, aziendaId, anno, mese, ct);

        var rapportoIds = buste.Select(b => b.RapportoLavoroId).ToList();
        var rapporti = await db.Rapporti.AsNoTracking()
            .Where(r => r.TenantId == tenantId && rapportoIds.Contains(r.Id))
            .Include(r => r.Dipendente)
            .ToDictionaryAsync(r => r.Id, ct);

        var righe = new List<RigaQuadro>();
        foreach (var busta in buste.OrderBy(b =>
            rapporti.TryGetValue(b.RapportoLavoroId, out var r)
                ? $"{r.Dipendente?.Cognome} {r.Dipendente?.Nome}" : ""))
        {
            rapporti.TryGetValue(busta.RapportoLavoroId, out var rapporto);
            string nominativo = rapporto?.Dipendente is { } d
                ? $"{d.Cognome} {d.Nome}".Trim() : "";
            var segnalazioni = busta.SegnalazioniJson is { Length: > 0 } json
                ? System.Text.Json.JsonSerializer.Deserialize<List<SegnalazioneBustaAgricola>>(json) ?? []
                : [];
            righe.Add(new RigaQuadro(busta, nominativo, segnalazioni));
        }

        var segnalazioniMensili = ValidatoreBustaAgricola.ValidaCessazioniOmogenee(
            righe.Where(r => rapporti.ContainsKey(r.Busta.RapportoLavoroId))
                .Select(r =>
                {
                    var rapporto = rapporti[r.Busta.RapportoLavoroId];
                    return (r.Nominativo, rapporto.DataAssunzione, rapporto.DataCessazione);
                })
                .ToList());

        // Arrotondamenti anticipati che nessuna busta successiva potrà più recuperare.
        foreach (var riga in righe)
        {
            if (riga.Busta.ArrotondamentoDelMese <= 0m) continue;
            var rapporto = rapporti.GetValueOrDefault(riga.Busta.RapportoLavoroId);
            if (rapporto?.DataCessazione is { } cess
                && cess <= new DateOnly(anno, mese, DateTime.DaysInMonth(anno, mese)))
                segnalazioniMensili.Add(new(LivelloSegnalazione.Avviso, "ARROTONDAMENTO_NON_RECUPERABILE",
                    $"{riga.Nominativo}: arrotondamento di {riga.Busta.ArrotondamentoDelMese:0.00} € "
                    + "anticipato nell'ultima busta del rapporto: resta a carico dell'azienda."));
        }

        return new QuadroElaborazione(elaborazione, righe, segnalazioniMensili, azienda);
    }

    private static ElaborazioneAgricolaEntity? StatoValidato(ElaborazioneAgricolaEntity? e) =>
        e?.Stato == ElaborazioneAgricolaEntity.StatoValidata ? e : null;

    private static async Task<CalcoloAgricoloDaRapporto> CalcolaDaRapportoAsync(
        Guid tenantId,
        Guid rapportoId,
        DatiMensiliCedolinoAgricoloInput input,
        PagheDbContext db,
        ParametriNormativiService parametriService,
        CancellationToken ct)
    {
        Valida(input);

        var rapportoEntity = await db.Rapporti.FirstOrDefaultAsync(
            r => r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("Rapporto agricolo non trovato.");
        if (!string.Equals(
                rapportoEntity.CodiceCcnl, "AGRICOLTURA_OPERAI", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Il rapporto selezionato non usa il CCNL degli operai agricoli.");

        var azienda = await db.Aziende.FirstOrDefaultAsync(
            a => a.Id == rapportoEntity.AziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda del rapporto non trovata per il tenant autenticato.");
        var dipendente = await db.Dipendenti.FirstOrDefaultAsync(
            d => d.Id == rapportoEntity.DipendenteId && d.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Dipendente del rapporto non trovato per il tenant autenticato.");

        VerificaPeriodoRapporto(rapportoEntity, input.Anno, input.Mese);
        var tipo = TipoDaContratto(rapportoEntity.TipoContratto);

        string provincia = (rapportoEntity.ProvinciaLavoro ?? azienda.Provincia).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(provincia))
            throw new ArgumentException(
                "Indica la provincia di lavoro nel rapporto o nella sede aziendale: serve per il CPL agricolo.");
        if (string.IsNullOrWhiteSpace(rapportoEntity.Livello))
            throw new ArgumentException("Indica nel rapporto il livello della tabella salariale provinciale.");

        var nazionali = await parametriService.CaricaParametriAsync(input.Anno, ct);
        var agricoli = await parametriService.CaricaParametriAgricoliAsync(input.Anno, provincia, ct);
        var segnalazioni = new List<SegnalazioneBustaAgricola>();

        // --- Giorni di detrazione: si calcolano dalle date del rapporto (dato autorevole),
        // non dall'input. Un valore diverso arrivato dal client viene corretto e segnalato.
        DateOnly? assunzione = rapportoEntity.DataAssunzione == default
            ? null : rapportoEntity.DataAssunzione;
        int giorniDetrazione = ValidatoreBustaAgricola.GiorniDetrazioneNelMese(
            input.Anno, input.Mese, assunzione, rapportoEntity.DataCessazione);
        if (input.GiorniDetrazione != giorniDetrazione)
            segnalazioni.Add(new(LivelloSegnalazione.Errore, "GIORNI_DETRAZIONE_CORRETTI",
                $"Giorni di detrazione indicati {input.GiorniDetrazione}, ma dal periodo del "
                + $"rapporto nel mese risultano {giorniDetrazione}: corretti automaticamente."));

        // --- Reddito annuo presunto: richiesta → scheda del rapporto → default aziendale →
        // stima dal contratto. L'origine resta tracciata sulla busta.
        (decimal redditoPresunto, string origineReddito) = RedditoPresunto(
            input, rapportoEntity, azienda, agricoli, segnalazioni);

        // --- Trasferte: mapping esplicito giorni/importo, normalizzazione dichiarata
        // (inversioni palesi, gruppi con lo stesso importo) e controlli di coerenza.
        IReadOnlyList<TrasfertaGiorniAgricola> trasferte = ValidatoreBustaAgricola.NormalizzaTrasferte(
            (input.Trasferte ?? [])
                .Where(t => t.Giorni > 0m && t.ImportoGiorno > 0m)
                .Select(t => new TrasfertaGiorniAgricola(t.Giorni, t.ImportoGiorno))
                .Concat(input.GiorniTrasferta > 0m && input.IndennitaTrasfertaGiornaliera > 0m
                    && input.Trasferte is not { Count: > 0 }
                    ? [new TrasfertaGiorniAgricola(input.GiorniTrasferta, input.IndennitaTrasfertaGiornaliera)]
                    : Array.Empty<TrasfertaGiorniAgricola>())
                .ToList(),
            input.GiornateLavorate,
            segnalazioni);

        // --- Arrotondamento del mese precedente: se la busta precedente è in archivio, il
        // riporto è il suo, non quello digitato. Ultimo cedolino anche quando la cessazione
        // cade nel mese elaborato.
        var bustaPrecedente = await db.CedoliniAgricoli.AsNoTracking().FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
                && c.Anno == (input.Mese == 1 ? input.Anno - 1 : input.Anno)
                && c.Mese == (input.Mese == 1 ? 12 : input.Mese - 1), ct);
        decimal riportato = bustaPrecedente?.ArrotondamentoDelMese ?? input.ArrotondamentoRiportato;
        if (bustaPrecedente is not null && input.ArrotondamentoRiportato != 0m
            && input.ArrotondamentoRiportato != riportato)
            segnalazioni.Add(new(LivelloSegnalazione.Avviso, "RIPORTO_ARROTONDAMENTO_ARCHIVIO",
                $"Riporto arrotondamenti indicato {input.ArrotondamentoRiportato:0.00} €, ma la "
                + $"busta archiviata del mese precedente ne anticipa {riportato:0.00} €: vale l'archivio."));
        var fineMese = new DateOnly(input.Anno, input.Mese, DateTime.DaysInMonth(input.Anno, input.Mese));
        bool ultimoCedolino = input.UltimoCedolino
            || (rapportoEntity.DataCessazione is { } cessazioneNelMese
                && cessazioneNelMese >= new DateOnly(input.Anno, input.Mese, 1)
                && cessazioneNelMese <= fineMese);

        segnalazioni.AddRange(ValidatoreBustaAgricola.Valida(
            input.Anno, input.Mese, input.OreOrdinarie, input.GiornateLavorate, trasferte,
            assunzione, rapportoEntity.DataCessazione,
            nazionali.TrasfertaItaliaEsenteGiorno,
            oreGiornaliereContratto: rapportoEntity.OreSettimanali > 0m
                ? Math.Round(rapportoEntity.OreSettimanali / 6m, 2)
                : null,
            matricolaInpsAzienda: azienda.MatricolaInps));

        if (!string.IsNullOrWhiteSpace(input.ValidataDa))
            segnalazioni.Add(new(LivelloSegnalazione.Avviso, "VALIDAZIONE_SINGOLA_IGNORATA",
                "La validazione non è più della singola busta: si valida l'elaborazione "
                + "mensile dell'azienda, che rifiuta finché restano errori."));

        var rapporto = new RapportoAgricolo
        {
            Provincia = agricoli.Provincia,
            Tipo = tipo,
            Livello = rapportoEntity.Livello,
            DetrazioniLavoroDipendente = rapportoEntity.DetrazioniLavoroDipendente,
            RedditoPresuntoAnnuo = redditoPresunto,
            Regione = rapportoEntity.Regione,
            CodiceCatastaleComune = rapportoEntity.CodiceCatastaleComune,
        };
        var cartellino = new CartellinoAgricolo
        {
            Anno = input.Anno,
            Mese = input.Mese,
            OreOrdinarie = input.OreOrdinarie,
            GiornateLavorate = input.GiornateLavorate,
            GiorniDetrazione = giorniDetrazione,
            UnaTantum = input.UnaTantum,
            DetrazioniGiaGoduteAnno = input.DetrazioniGiaGoduteAnno,
            ArrotondamentoRiportato = riportato,
            UltimoCedolino = ultimoCedolino,
            RimborsiSpeseDocumentate = input.RimborsiSpeseDocumentate,
            Trasferte = trasferte,
        };
        var cedolino = new MotoreCalcoloCedolinoAgricolo(nazionali, agricoli)
            .Calcola(rapporto, cartellino);

        var elaborazione = await db.ElaborazioniAgricole.AsNoTracking().FirstOrDefaultAsync(
            e => e.TenantId == tenantId && e.AziendaId == rapportoEntity.AziendaId
                && e.Anno == input.Anno && e.Mese == input.Mese, ct);

        string comuneAzienda = string.Join(" ", new[] { azienda.Cap, azienda.Comune }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(azienda.Provincia))
            comuneAzienda += $" ({azienda.Provincia})";
        var comuneResidenza = ComuniCatastali.Trova(rapportoEntity.CodiceCatastaleComune);
        var intestazione = new IntestazioneCedolino
        {
            Azienda = string.IsNullOrWhiteSpace(azienda.RagioneSociale) ? "Azienda" : azienda.RagioneSociale,
            Logo = azienda.Logo,
            AziendaIndirizzo = azienda.Indirizzo,
            AziendaComune = string.IsNullOrWhiteSpace(comuneAzienda) ? null : comuneAzienda.Trim(),
            PartitaIva = azienda.PartitaIva,
            CodiceFiscaleAzienda = azienda.CodiceFiscale,
            MatricolaInps = azienda.MatricolaInps,
            PatInail = azienda.PatInail,
            Dipendente = $"{dipendente.Cognome} {dipendente.Nome}".Trim(),
            CodiceFiscaleDipendente = dipendente.CodiceFiscale,
            Matricola = rapportoEntity.Matricola,
            IbanDipendente = dipendente.Iban,
            DataAssunzione = rapportoEntity.DataAssunzione == default
                ? null
                : rapportoEntity.DataAssunzione.ToString("dd/MM/yyyy"),
            DataCessazione = rapportoEntity.DataCessazione?.ToString("dd/MM/yyyy"),
            DataNascita = Paghe.Core.AdeTelematico.AnagraficaCodiceFiscale
                .Decodifica(dipendente.CodiceFiscale, input.Anno)?.DataNascita.ToString("dd/MM/yyyy"),
            ComuneResidenza = comuneResidenza is null
                ? null
                : $"{comuneResidenza.Nome} ({comuneResidenza.Provincia})",
            // La qualifica NON viene dalla scheda (dove un default «Impiegato» sarebbe un
            // falso): per un rapporto OTD/OTI col CCNL operai agricoli è per definizione
            // «Operaio agricolo», col livello della tabella provinciale accanto.
            Qualifica = $"Operaio agricolo — Area/Livello: {TitoloLivello(rapportoEntity.Livello)}",
            Livello = rapportoEntity.Livello,
            Ccnl = "Operai agricoli e florovivaisti",
            TipoContratto = rapportoEntity.TipoContratto,
            Mensilita = rapportoEntity.NumeroMensilita,
            Orario = rapportoEntity.PartTime
                ? $"Part-time {rapportoEntity.PercentualePartTime:0.#}%"
                : $"{rapportoEntity.OreSettimanali:0.#} ore/sett.",
        };

        return new(cedolino, intestazione, rapporto, cartellino, agricoli,
            rapportoEntity.AziendaId, segnalazioni, origineReddito, elaborazione);
    }

    /// <summary>
    /// Reddito annuo presunto e sua origine: richiesta esplicita → scheda del rapporto →
    /// default aziendale → stima dal contratto (retribuzione del mese al netto della quota
    /// deducibile × mesi previsti). La stima è dichiarata con un avviso, mai silenziosa.
    /// </summary>
    private static (decimal Reddito, string Origine) RedditoPresunto(
        DatiMensiliCedolinoAgricoloInput input,
        RapportoLavoroEntity rapporto,
        AziendaEntity azienda,
        ParametriAgricoli agricoli,
        List<SegnalazioneBustaAgricola> segnalazioni)
    {
        if (input.RedditoPresuntoAnnuo is { } richiesto and > 0m)
            return (richiesto, "richiesta");
        if (rapporto.RedditoPresuntoAnnuo is { } daScheda and > 0m)
            return (daScheda, "anagrafica");
        if (azienda.RedditoPresuntoAgricoloAnnuo is { } daAzienda and > 0m)
        {
            segnalazioni.Add(new(LivelloSegnalazione.Avviso, "REDDITO_PRESUNTO_DEFAULT",
                $"Reddito annuo presunto dal default aziendale ({daAzienda:0.00} €): "
                + "verificare che valga per questo lavoratore."));
            return (daAzienda, "default");
        }

        var tariffa = agricoli.Tariffa(rapporto.Livello);
        int mesi = MesiPrevistiDiContratto(input.Anno, rapporto.DataAssunzione, rapporto.DataCessazione);
        decimal stima = Math.Round(
            input.OreOrdinarie * tariffa.PagaOraria
                * (1m - agricoli.AliquotaDeducibileEffettiva) * mesi,
            2, MidpointRounding.AwayFromZero);
        if (stima <= 0m)
            throw new ArgumentException(
                "Indica il reddito annuo presunto (in scheda dipendente o nella richiesta): "
                + "senza ore del mese non è nemmeno stimabile dal contratto.");
        segnalazioni.Add(new(LivelloSegnalazione.Avviso, "REDDITO_PRESUNTO_CALCOLATO",
            $"Reddito annuo presunto stimato dal contratto: {stima:0.00} € "
            + $"(imponibile del mese × {mesi} mesi previsti). Confermarlo in scheda dipendente."));
        return (stima, "calcolato");
    }

    /// <summary>Mesi di calendario coperti dal contratto nell'anno elaborato (almeno 1).</summary>
    private static int MesiPrevistiDiContratto(int anno, DateOnly assunzione, DateOnly? cessazione)
    {
        int primoMese = assunzione != default && assunzione.Year == anno ? assunzione.Month
            : assunzione != default && assunzione.Year > anno ? 12 : 1;
        int ultimoMese = cessazione is { } c && c.Year == anno ? c.Month
            : cessazione is { } c2 && c2.Year < anno ? 1 : 12;
        return Math.Max(1, ultimoMese - primoMese + 1);
    }

    /// <summary>«COMUNE» → «Comune»; i codici composti restano come sono.</summary>
    private static string TitoloLivello(string livello)
    {
        string pulito = livello.Trim();
        return pulito.Length > 1 && pulito.All(char.IsLetter)
            ? char.ToUpperInvariant(pulito[0]) + pulito[1..].ToLowerInvariant()
            : pulito;
    }

    private static void Valida(DatiMensiliCedolinoAgricoloInput input)
    {
        if (input.Anno is < 2000 or > 2100 || input.Mese is < 1 or > 12)
            throw new ArgumentException("Periodo della busta agricola non valido.");
        int giorniMese = DateTime.DaysInMonth(input.Anno, input.Mese);
        if (input.OreOrdinarie < 0m || input.GiornateLavorate < 0 || input.GiornateLavorate > giorniMese)
            throw new ArgumentException("Ore e giornate lavorate non sono valide per il mese indicato.");
        if (input.GiorniDetrazione < 0 || input.GiorniDetrazione > giorniMese)
            throw new ArgumentException("I giorni di detrazione non sono validi per il mese indicato.");
        if (input.UnaTantum < 0m || input.DetrazioniGiaGoduteAnno < 0m)
            throw new ArgumentException("Una tantum e detrazioni gia godute non possono essere negative.");
        if (input.GiorniTrasferta < 0m || input.GiorniTrasferta > giorniMese)
            throw new ArgumentException("I giorni di trasferta non sono validi per il mese indicato.");
        if (input.IndennitaTrasfertaGiornaliera < 0m || input.RimborsiSpeseDocumentate < 0m)
            throw new ArgumentException("Trasferta e rimborsi spese non possono essere negativi.");
        if (input.Trasferte is { Count: > 0 })
        {
            if (input.Trasferte.Any(t => t.Giorni < 0m || t.ImportoGiorno < 0m))
                throw new ArgumentException(
                    "I gruppi di trasferta non possono avere giorni o importi negativi.");
            if (input.Trasferte.Sum(t => t.Giorni) > giorniMese)
                throw new ArgumentException(
                    "I giorni di trasferta dei gruppi superano i giorni del mese.");
        }
    }

    private static void VerificaPeriodoRapporto(RapportoLavoroEntity rapporto, int anno, int mese)
    {
        var inizioMese = new DateOnly(anno, mese, 1);
        var fineMese = new DateOnly(anno, mese, DateTime.DaysInMonth(anno, mese));
        if (rapporto.DataAssunzione != default && rapporto.DataAssunzione > fineMese)
            throw new ArgumentException("Il periodo precede l'assunzione del lavoratore.");
        if (rapporto.DataCessazione is { } cessazione && cessazione < inizioMese)
            throw new ArgumentException("Il periodo segue la cessazione del lavoratore.");
    }

    private static TipoOperaioAgricolo TipoDaContratto(string tipoContratto)
    {
        string valore = tipoContratto.Trim();
        if (valore.Equals("OTI", StringComparison.OrdinalIgnoreCase)
            || valore.Contains("indeterminat", StringComparison.OrdinalIgnoreCase))
            return TipoOperaioAgricolo.Indeterminato;
        if (valore.Equals("OTD", StringComparison.OrdinalIgnoreCase)
            || valore.Contains("determinat", StringComparison.OrdinalIgnoreCase))
            return TipoOperaioAgricolo.Determinato;
        throw new ArgumentException(
            "Tipo contratto agricolo non riconoscibile: indicare OTD/tempo determinato o OTI/tempo indeterminato nel rapporto.");
    }

    private static string Sigla(TipoOperaioAgricolo tipo) =>
        tipo == TipoOperaioAgricolo.Determinato ? "OTD" : "OTI";

    /// <summary>Font e dicitura dello studio dalle preferenze dell'utente collegato.</summary>
    private static async Task<(string Font, string? Dicitura)> PreferenzeDocumentiAsync(
        ClaimsPrincipal user, PreferenzeUtenteService preferenze, CancellationToken ct)
    {
        var utente = Utente(user);
        if (utente == Guid.Empty) return (FontDocumenti.Predefinito, null);
        var font = FontDocumenti.Famiglia(await preferenze.LeggiAsync(
            Tenant(user), utente, PreferenzeUtenteService.FontDocumenti, "lato", ct));
        var dicitura = await preferenze.LeggiAsync(
            Tenant(user), utente, PreferenzeUtenteService.DicituraStudio, "", ct);
        return (font, string.IsNullOrWhiteSpace(dicitura) ? null : dicitura);
    }

    private static Guid Utente(ClaimsPrincipal user) =>
        Guid.TryParse(
            user.FindFirst("sub")?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            out var id) ? id : Guid.Empty;

    private static Guid Tenant(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("Tenant non presente nel token.");
}
