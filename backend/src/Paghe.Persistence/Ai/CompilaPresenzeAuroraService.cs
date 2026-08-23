using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Ai;

public sealed class OperazioneAuroraNonConsentitaException(string messaggio)
    : InvalidOperationException(messaggio);

public sealed record PreparazioneCompilaPresenzeAurora(
    string Titolo,
    IReadOnlyList<RigaDiffAurora> Diff,
    string ComandoCanonicoJson,
    string UndoJson,
    string PrecondizioneHash,
    string PostcondizioneHash);

public sealed record EsitoCompilaPresenzeAurora(
    Guid CartellinoId,
    Guid RapportoId,
    int Anno,
    int Mese,
    int Versione,
    int DateModificate,
    string EsitoJson);

/// <summary>
/// Patcher server-side del primo effetto A3. Il modello sceglie soltanto rapporto e date;
/// il server rilegge il cartellino, conserva ogni altro campo e trasforma esclusivamente
/// giornate lavorate pulite in assenze non retribuite. Dati salute fermano l'operazione e
/// non entrano mai in schema, diff, risultato o audit.
/// </summary>
public sealed class CompilaPresenzeAuroraService(
    PagheDbContext db,
    CartellinoBozzaService bozze,
    TimeProvider orologio)
{
    public const string VersioneStrumento = "compila-presenze:v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Comando(
        Guid RapportoId,
        Guid CartellinoId,
        int VersioneAttesa,
        int Anno,
        int Mese,
        DateOnly[] Date,
        string PrecondizioneHash,
        string PostcondizioneHash);

    private sealed record DatiUndo(
        Guid RapportoId,
        Guid CartellinoId,
        int VersionePrima,
        int Anno,
        int Mese,
        GiornataCartellino[] GiornatePrecedenti,
        string PostcondizioneHash);

    private sealed record AmbitoRapporto(
        Guid Id,
        Guid AziendaId,
        DateOnly DataAssunzione,
        DateOnly? DataCessazione,
        decimal OreSettimanali,
        string Nominativo);

    public async Task<PreparazioneCompilaPresenzeAurora> PreparaAsync(
        Guid tenantId,
        Guid aziendaId,
        string periodo,
        string argomentiModelloJson,
        CancellationToken ct = default)
    {
        var (rapportoId, date) = LeggiArgomentiModello(argomentiModelloJson);
        var (anno, mese, inizio, fine) = LeggiPeriodo(periodo);
        var (rapporto, entita, baseCorrente) = await CaricaBaseAsync(
            tenantId, aziendaId, rapportoId, anno, mese, inizio, fine, ct);
        await VerificaBlocchiAsync(tenantId, rapporto, anno, mese, ct);
        VerificaCalendarioSicuro(baseCorrente, rapporto.OreSettimanali, inizio, fine);
        VerificaDate(date, anno, mese, baseCorrente, orologio);

        var precedenti = date.Select(data =>
            baseCorrente.Giornate.Single(g => g.Data == data)).ToArray();
        var nuove = baseCorrente.Giornate.Select(g =>
        {
            if (!date.Contains(g.Data)) return g;
            return new GiornataCartellino
            {
                Data = g.Data,
                Causale = CausaleGiornata.AssenzaNonRetribuita,
                OreAssenzaNonRetribuita = g.OreOrdinarie,
            };
        }).ToArray();
        var dopo = CalendarioCartellino.Compila(
            baseCorrente, nuove, rapporto.OreSettimanali / 5m);
        RegoleCartellino.Valida(dopo);

        string preHash = HashCartellino(baseCorrente);
        string postHash = HashCartellino(dopo);
        var comando = new Comando(
            rapporto.Id, entita.Id, entita.Versione, anno, mese,
            [.. date.Order()], preHash, postHash);
        var undo = new DatiUndo(
            rapporto.Id, entita.Id, entita.Versione, anno, mese,
            [.. precedenti.OrderBy(g => g.Data)], postHash);
        var diff = precedenti.OrderBy(g => g.Data)
            .Select(g => new RigaDiffAurora(
                g.Data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                $"{g.OreOrdinarie:0.##} ore lavorate",
                $"{g.OreOrdinarie:0.##} ore di assenza non retribuita"))
            .ToArray();

        return new(
            $"Registra {date.Length} assenz{(date.Length == 1 ? "a" : "e")} non retribuit{(date.Length == 1 ? "a" : "e")} per {rapporto.Nominativo}",
            diff,
            JsonSerializer.Serialize(comando, Json),
            JsonSerializer.Serialize(undo, Json),
            preHash,
            postHash);
    }

    public static bool ProvaNormalizzaArgomentiModello(
        string? json, out string canonico, out string motivo)
    {
        canonico = "";
        motivo = "Schema del tool presenze non valido.";
        try
        {
            var (rapportoId, date) = LeggiArgomentiModello(json ?? "");
            canonico = JsonSerializer.Serialize(new
            {
                rapportoId,
                date = date.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                causale = "AssenzaNonRetribuita",
            }, Json);
            motivo = "";
            return true;
        }
        catch (OperazioneAuroraNonConsentitaException ex)
        {
            motivo = ex.Message;
            return false;
        }
    }

    public async Task<EsitoCompilaPresenzeAurora> ApplicaAsync(
        Guid tenantId,
        Guid aziendaId,
        string periodo,
        string comandoCanonicoJson,
        CancellationToken ct = default)
    {
        var comando = DeserializzaInterno<Comando>(comandoCanonicoJson);
        var (anno, mese, inizio, fine) = LeggiPeriodo(periodo);
        if (comando.Anno != anno || comando.Mese != mese)
            throw new OperazioneAuroraNonConsentitaException(
                "Il periodo della proposta non coincide più con il mandato.");

        var (rapporto, entita, corrente) = await CaricaBaseAsync(
            tenantId, aziendaId, comando.RapportoId, anno, mese, inizio, fine, ct);
        await VerificaBlocchiAsync(tenantId, rapporto, anno, mese, ct);
        VerificaCalendarioSicuro(corrente, rapporto.OreSettimanali, inizio, fine);
        if (entita.Id != comando.CartellinoId
            || entita.Versione != comando.VersioneAttesa
            || !string.Equals(HashCartellino(corrente), comando.PrecondizioneHash,
                StringComparison.Ordinal))
            throw new OperazioneAuroraNonConsentitaException(
                "Il cartellino è cambiato dopo l'anteprima: nessuna modifica applicata.");

        VerificaDate(comando.Date, anno, mese, corrente, orologio);
        var nuove = corrente.Giornate.Select(g => comando.Date.Contains(g.Data)
            ? new GiornataCartellino
            {
                Data = g.Data,
                Causale = CausaleGiornata.AssenzaNonRetribuita,
                OreAssenzaNonRetribuita = g.OreOrdinarie,
            }
            : g).ToArray();
        var modificato = CalendarioCartellino.Compila(
            corrente, nuove, rapporto.OreSettimanali / 5m);
        if (!string.Equals(HashCartellino(modificato), comando.PostcondizioneHash,
                StringComparison.Ordinal))
            throw new OperazioneAuroraNonConsentitaException(
                "L'esito non coincide con l'anteprima: nessuna modifica applicata.");

        var salvata = await bozze.SalvaAsync(
            tenantId,
            rapporto.Id,
            new SalvaCartellinoBozza(modificato, entita.Id, entita.Versione),
            ct);
        var riletta = await db.Cartellini.AsNoTracking().SingleAsync(c =>
            c.Id == salvata.Id && c.TenantId == tenantId, ct);
        if (!string.Equals(HashCartellino(CartellinoBozzaService.DaEntita(riletta)),
                comando.PostcondizioneHash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "La rilettura autorevole non coincide con l'effetto proposto.");

        string esito = JsonSerializer.Serialize(new
        {
            cartellinoId = salvata.Id,
            rapportoId = rapporto.Id,
            anno,
            mese,
            versione = salvata.Versione,
            dateModificate = comando.Date.Length,
            ripristinabile = true,
        }, Json);
        return new(salvata.Id, rapporto.Id, anno, mese, salvata.Versione,
            comando.Date.Length, esito);
    }

    public async Task<EsitoCompilaPresenzeAurora> RipristinaAsync(
        Guid tenantId,
        Guid aziendaId,
        string periodo,
        string undoJson,
        CancellationToken ct = default)
    {
        var undo = DeserializzaInterno<DatiUndo>(undoJson);
        var (anno, mese, inizio, fine) = LeggiPeriodo(periodo);
        if (undo.Anno != anno || undo.Mese != mese)
            throw new OperazioneAuroraNonConsentitaException(
                "Il periodo dell'annullamento non coincide con la proposta.");

        var (rapporto, entita, corrente) = await CaricaBaseAsync(
            tenantId, aziendaId, undo.RapportoId, anno, mese, inizio, fine, ct);
        await VerificaBlocchiAsync(tenantId, rapporto, anno, mese, ct);
        VerificaCalendarioSicuro(corrente, rapporto.OreSettimanali, inizio, fine);
        if (entita.Id != undo.CartellinoId
            || entita.Versione != undo.VersionePrima + 1
            || !string.Equals(HashCartellino(corrente), undo.PostcondizioneHash,
                StringComparison.Ordinal))
            throw new OperazioneAuroraNonConsentitaException(
                "Il cartellino è cambiato dopo l'applicazione: il ripristino automatico è bloccato.");

        var perData = undo.GiornatePrecedenti.ToDictionary(g => g.Data);
        var ripristinate = corrente.Giornate
            .Select(g => perData.TryGetValue(g.Data, out var prima) ? prima : g)
            .ToArray();
        var precedente = CalendarioCartellino.Compila(
            corrente, ripristinate, rapporto.OreSettimanali / 5m);
        var salvata = await bozze.SalvaAsync(
            tenantId, rapporto.Id,
            new SalvaCartellinoBozza(precedente, entita.Id, entita.Versione), ct);

        string esito = JsonSerializer.Serialize(new
        {
            cartellinoId = salvata.Id,
            rapportoId = rapporto.Id,
            anno,
            mese,
            versione = salvata.Versione,
            dateRipristinate = undo.GiornatePrecedenti.Length,
        }, Json);
        return new(salvata.Id, rapporto.Id, anno, mese, salvata.Versione,
            undo.GiornatePrecedenti.Length, esito);
    }

    private async Task<(AmbitoRapporto Rapporto, CartellinoEntity Entita, Cartellino Cartellino)>
        CaricaBaseAsync(
            Guid tenantId, Guid aziendaId, Guid rapportoId, int anno, int mese,
            DateOnly inizio, DateOnly fine, CancellationToken ct)
    {
        var rapporto = await db.Rapporti.AsNoTracking()
            .Where(r => r.Id == rapportoId && r.TenantId == tenantId
                && r.AziendaId == aziendaId
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId)
            .Select(r => new AmbitoRapporto(
                r.Id, r.AziendaId, r.DataAssunzione, r.DataCessazione, r.OreSettimanali,
                (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim()))
            .FirstOrDefaultAsync(ct)
            ?? throw new OperazioneAuroraNonConsentitaException(
                "Rapporto non trovato nell'azienda del mandato.");
        if (rapporto.DataAssunzione > inizio
            || (rapporto.DataCessazione is { } cessazione && cessazione < fine))
            throw new OperazioneAuroraNonConsentitaException(
                "Il rapporto copre solo una parte del mese: compila le presenze manualmente.");

        var entita = await db.Cartellini.AsNoTracking().FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
            && c.Anno == anno && c.Mese == mese, ct)
            ?? throw new OperazioneAuroraNonConsentitaException(
                "Prima completa il calendario del mese nella pagina Presenze.");
        return (rapporto, entita, CartellinoBozzaService.DaEntita(entita));
    }

    private async Task VerificaBlocchiAsync(
        Guid tenantId, AmbitoRapporto rapporto, int anno, int mese, CancellationToken ct)
    {
        if (await db.Cedolini.AsNoTracking().AnyAsync(c =>
            c.TenantId == tenantId && c.RapportoLavoroId == rapporto.Id
            && c.Anno == anno && c.Mese == mese, ct))
            throw new OperazioneAuroraNonConsentitaException(
                "Il cedolino del mese esiste già: usa il ricalcolo manuale.");
        if (await db.PraticheF24.AsNoTracking().AnyAsync(p =>
            p.TenantId == tenantId && p.AziendaId == rapporto.AziendaId
            && p.Anno == anno && p.Mese == mese
            && (p.Stato == nameof(StatoPraticaTelematica.InviataManualmente)
                || p.Stato == nameof(StatoPraticaTelematica.RicevutaAcquisita)), ct))
            throw new OperazioneAuroraNonConsentitaException(
                "Il periodo ha un F24 finale: le presenze non sono modificabili da Aurora.");
    }

    private static void VerificaCalendarioSicuro(
        Cartellino cartellino, decimal oreSettimanali, DateOnly inizio, DateOnly fine)
    {
        if (cartellino.Giornate.Count != fine.DayNumber - inizio.DayNumber + 1
            || CalendarioCartellino.Verifica(
                    cartellino.Anno, cartellino.Mese, cartellino.Giornate,
                    oreSettimanali / 5m).Any(p => p.Bloccante))
            throw new OperazioneAuroraNonConsentitaException(
                "Il calendario non è completo e coerente: correggilo manualmente.");
        if (cartellino.GiorniMalattia != 0m
            || cartellino.GiorniInfortunio != 0m
            || cartellino.Giornate.Any(g =>
                g.Causale is CausaleGiornata.Malattia or CausaleGiornata.Infortunio))
            throw new OperazioneAuroraNonConsentitaException(
                // Il motivo resta volutamente indistinguibile: il testo arriva al modello
                // insieme al rapporto e non deve rivelare l'esistenza di dati sanitari D3A.
                "Il mese non è idoneo alla modifica automatica: usa la pagina Presenze.");
    }

    private static void VerificaDate(
        IReadOnlyCollection<DateOnly> date, int anno, int mese,
        Cartellino cartellino, TimeProvider orologio)
    {
        var oggi = DateOnly.FromDateTime(orologio.GetLocalNow().DateTime);
        foreach (var data in date)
        {
            if (data.Year != anno || data.Month != mese || data > oggi)
                throw new OperazioneAuroraNonConsentitaException(
                    "Le date devono appartenere al periodo del mandato e non essere future.");
            var giornata = cartellino.Giornate.SingleOrDefault(g => g.Data == data)
                ?? throw new OperazioneAuroraNonConsentitaException(
                    "Una data richiesta non esiste nel calendario autorevole.");
            if (giornata.Causale != CausaleGiornata.Lavorata
                || giornata.OreOrdinarie <= 0m
                || giornata.OrePermessi != 0m
                || giornata.OreExFestivita != 0m
                || giornata.OreAssenzaNonRetribuita != 0m
                || giornata.TotaleStraordinario != 0m)
                throw new OperazioneAuroraNonConsentitaException(
                    "Aurora può modificare solo giornate lavorate complete e senza altre causali o ore.");
        }
    }

    private static (Guid RapportoId, DateOnly[] Date) LeggiArgomentiModello(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4_096)
            throw new OperazioneAuroraNonConsentitaException(
                "Argomenti assenti o troppo lunghi.");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 4,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException();
            var proprieta = doc.RootElement.EnumerateObject().ToArray();
            var nomi = proprieta.Select(p => p.Name).ToArray();
            if (proprieta.Length != 3
                || nomi.Distinct(StringComparer.Ordinal).Count() != 3
                || !nomi.All(n => n is "rapportoId" or "date" or "causale"))
                throw new OperazioneAuroraNonConsentitaException(
                    "Lo schema della richiesta presenze non è valido.");
            var rapportoElement = doc.RootElement.GetProperty("rapportoId");
            var causaleElement = doc.RootElement.GetProperty("causale");
            if (rapportoElement.ValueKind != JsonValueKind.String
                || !Guid.TryParse(rapportoElement.GetString(), out var rapportoId)
                || rapportoId == Guid.Empty)
                throw new OperazioneAuroraNonConsentitaException("Rapporto non valido.");
            if (causaleElement.ValueKind != JsonValueKind.String
                || causaleElement.GetString() != "AssenzaNonRetribuita")
                throw new OperazioneAuroraNonConsentitaException(
                    "La prima versione gestisce soltanto assenze non retribuite.");
            var elementoDate = doc.RootElement.GetProperty("date");
            if (elementoDate.ValueKind != JsonValueKind.Array)
                throw new OperazioneAuroraNonConsentitaException("L'elenco date non è valido.");
            var date = elementoDate.EnumerateArray().Select(e =>
            {
                if (e.ValueKind != JsonValueKind.String
                    || !DateOnly.TryParseExact(e.GetString(), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                    throw new OperazioneAuroraNonConsentitaException(
                        "Ogni data deve usare il formato AAAA-MM-GG.");
                return data;
            }).ToArray();
            if (date.Length is < 1 or > 31 || date.Distinct().Count() != date.Length)
                throw new OperazioneAuroraNonConsentitaException(
                    "Indica da una a 31 date, senza duplicati.");
            return (rapportoId, [.. date.Order()]);
        }
        catch (JsonException)
        {
            throw new OperazioneAuroraNonConsentitaException(
                "Argomenti non conformi allo schema del tool presenze.");
        }
    }

    private static (int Anno, int Mese, DateOnly Inizio, DateOnly Fine) LeggiPeriodo(string periodo)
    {
        if (periodo?.Length != 7
            || !int.TryParse(periodo[..4], out var anno)
            || periodo[4] != '-'
            || !int.TryParse(periodo[5..], out var mese)
            || anno is < 2000 or > 2100 || mese is < 1 or > 12)
            throw new OperazioneAuroraNonConsentitaException(
                "Periodo del mandato non valido.");
        return (anno, mese, new DateOnly(anno, mese, 1),
            new DateOnly(anno, mese, DateTime.DaysInMonth(anno, mese)));
    }

    private static T DeserializzaInterno<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, Json)
        ?? throw new InvalidOperationException("Payload server-side della proposta non leggibile.");

    internal static string HashCartellino(Cartellino cartellino)
    {
        var canonico = JsonSerializer.Serialize(cartellino, JsonHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    // L'hash di pre/postcondizione confronta un cartellino in memoria con la rilettura dal
    // database, e il roundtrip può cambiare la SCALA dei decimali (0 → 0.0 su SQLite) senza
    // cambiarne il valore. Il canone dell'hash normalizza quindi ogni decimale al valore puro.
    private static readonly JsonSerializerOptions JsonHash = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DecimalCanonicoJson() },
    };

    private sealed class DecimalCanonicoJson : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDecimal();

        public override void Write(
            Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(decimal.Parse(
                value.ToString("G29", CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture));
    }
}
