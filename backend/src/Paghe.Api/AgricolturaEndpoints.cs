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
        List<TrasfertaBatchInput>? Trasferte = null);

    private sealed record CalcoloAgricoloDaRapporto(
        Cedolino Cedolino,
        IntestazioneCedolino Intestazione,
        RapportoAgricolo Rapporto,
        CartellinoAgricolo Cartellino,
        ParametriAgricoli Parametri,
        Guid AziendaId);

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
            HttpResponse response,
            CancellationToken ct) =>
        {
            try
            {
                var calcolo = await CalcolaDaRapportoAsync(
                    Tenant(user), rapportoId, input, db, parametri, ct);
                var pdf = CedolinoAgricoloPdf.Genera(
                    calcolo.Cedolino,
                    calcolo.Intestazione,
                    calcolo.Rapporto,
                    calcolo.Cartellino,
                    calcolo.Parametri);

                response.Headers.CacheControl = "private, no-store";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["X-PaghePro-Stato"] = "BozzaAgricola";
                return Results.File(
                    pdf,
                    "application/pdf",
                    $"busta-agricola-{input.Anno}-{input.Mese:00}-bozza.pdf");
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
            CancellationToken ct) =>
        {
            var utenteId = Utente(user);
            if (utenteId == Guid.Empty) return Results.Unauthorized();
            try
            {
                var calcolo = await CalcolaDaRapportoAsync(
                    Tenant(user), rapportoId, input, db, parametri, ct);
                var pdf = CedolinoAgricoloPdf.Genera(
                    calcolo.Cedolino, calcolo.Intestazione, calcolo.Rapporto,
                    calcolo.Cartellino, calcolo.Parametri);

                var busta = await archivio.ArchiviaAsync(Tenant(user), utenteId, new CedolinoAgricoloEntity
                {
                    AziendaId = calcolo.AziendaId,
                    RapportoLavoroId = rapportoId,
                    Anno = input.Anno,
                    Mese = input.Mese,
                    OreOrdinarie = input.OreOrdinarie,
                    GiornateLavorate = input.GiornateLavorate,
                    GiorniDetrazione = input.GiorniDetrazione,
                    UnaTantum = input.UnaTantum,
                    GiorniTrasferta = input.Trasferte is { Count: > 0 }
                        ? input.Trasferte.Sum(t => t.Giorni)
                        : input.GiorniTrasferta,
                    IndennitaTrasfertaGiornaliera = input.Trasferte is { Count: > 0 }
                        ? 0m
                        : input.IndennitaTrasfertaGiornaliera,
                    TrasferteJson = input.Trasferte is { Count: > 0 }
                        ? System.Text.Json.JsonSerializer.Serialize(input.Trasferte)
                        : null,
                    RimborsiSpeseDocumentate = input.RimborsiSpeseDocumentate,
                    DetrazioniGiaGoduteAnno = input.DetrazioniGiaGoduteAnno,
                    ArrotondamentoRiportato = input.ArrotondamentoRiportato,
                    UltimoCedolino = input.UltimoCedolino,
                    RedditoPresuntoAnnuo = input.RedditoPresuntoAnnuo ?? 0m,
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

                return Results.Ok(new
                {
                    busta.Id,
                    busta.Anno,
                    busta.Mese,
                    busta.NettoInBusta,
                    busta.Versione,
                    busta.HashPdfSha256,
                    archiviatoIl = busta.ArchiviatoIl,
                    avviso = "Busta archiviata come prospetto da validare: non costituisce LUL, "
                        + "PosAgri o F24.",
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

        return app;
    }

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
        if (input.RedditoPresuntoAnnuo is null or <= 0m)
            throw new ArgumentException(
                "Indica il reddito annuo presunto reale del rapporto: per un agricolo non si puo annualizzare il singolo mese.");

        string provincia = (rapportoEntity.ProvinciaLavoro ?? azienda.Provincia).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(provincia))
            throw new ArgumentException(
                "Indica la provincia di lavoro nel rapporto o nella sede aziendale: serve per il CPL agricolo.");
        if (string.IsNullOrWhiteSpace(rapportoEntity.Livello))
            throw new ArgumentException("Indica nel rapporto il livello della tabella salariale provinciale.");

        var nazionali = await parametriService.CaricaParametriAsync(input.Anno, ct);
        var agricoli = await parametriService.CaricaParametriAgricoliAsync(input.Anno, provincia, ct);
        var rapporto = new RapportoAgricolo
        {
            Provincia = agricoli.Provincia,
            Tipo = tipo,
            Livello = rapportoEntity.Livello,
            DetrazioniLavoroDipendente = rapportoEntity.DetrazioniLavoroDipendente,
            RedditoPresuntoAnnuo = input.RedditoPresuntoAnnuo,
            Regione = rapportoEntity.Regione,
            CodiceCatastaleComune = rapportoEntity.CodiceCatastaleComune,
        };
        var cartellino = new CartellinoAgricolo
        {
            Anno = input.Anno,
            Mese = input.Mese,
            OreOrdinarie = input.OreOrdinarie,
            GiornateLavorate = input.GiornateLavorate,
            GiorniDetrazione = input.GiorniDetrazione,
            UnaTantum = input.UnaTantum,
            DetrazioniGiaGoduteAnno = input.DetrazioniGiaGoduteAnno,
            ArrotondamentoRiportato = input.ArrotondamentoRiportato,
            UltimoCedolino = input.UltimoCedolino,
            GiorniTrasferta = input.GiorniTrasferta,
            IndennitaTrasfertaGiornaliera = input.IndennitaTrasfertaGiornaliera,
            RimborsiSpeseDocumentate = input.RimborsiSpeseDocumentate,
            Trasferte = (input.Trasferte ?? [])
                .Where(t => t.Giorni > 0m && t.ImportoGiorno > 0m)
                .Select(t => new TrasfertaGiorniAgricola(t.Giorni, t.ImportoGiorno))
                .ToList(),
        };
        var cedolino = new MotoreCalcoloCedolinoAgricolo(nazionali, agricoli)
            .Calcola(rapporto, cartellino);

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
            Qualifica = rapportoEntity.Qualifica,
            Livello = rapportoEntity.Livello,
            Ccnl = "Operai agricoli e florovivaisti",
            TipoContratto = rapportoEntity.TipoContratto,
            Mensilita = rapportoEntity.NumeroMensilita,
            Orario = rapportoEntity.PartTime
                ? $"Part-time {rapportoEntity.PercentualePartTime:0.#}%"
                : $"{rapportoEntity.OreSettimanali:0.#} ore/sett.",
        };

        return new(cedolino, intestazione, rapporto, cartellino, agricoli, rapportoEntity.AziendaId);
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
