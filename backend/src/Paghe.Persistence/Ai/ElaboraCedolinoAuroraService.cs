using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Ai;

public sealed record PreparazioneElaboraCedolinoAurora(
    string Titolo,
    IReadOnlyList<RigaDiffAurora> Diff,
    string ComandoCanonicoJson,
    string UndoJson,
    string PrecondizioneHash,
    string PostcondizioneHash);

public sealed record EsitoElaboraCedolinoAurora(string EsitoJson);

/// <summary>
/// Secondo effetto A3: elabora il cedolino BOZZA del mese dal cartellino versionato. Il modello
/// sceglie soltanto il rapporto; il calcolo è lo stesso identico percorso dell'archiviazione
/// (<see cref="CedoliniService.CalcolaAnteprimaAsync"/>), quindi l'anteprima confermata produce
/// per costruzione gli stessi importi. Gli importi compaiono soltanto nel diff della card
/// (browser dell'operatore): non tornano mai al modello. L'undo elimina la bozza finché nessuno
/// l'ha portata avanti nel workflow.
/// </summary>
public sealed class ElaboraCedolinoAuroraService(
    PagheDbContext db,
    CedoliniService cedolini)
{
    public const string VersioneStrumento = "elabora-cedolino:v1";

    private sealed record Comando(
        Guid RapportoId,
        Guid CartellinoId,
        int VersioneCartellino,
        int Anno,
        int Mese,
        string HashCartellino,
        string HashImportiAttesi);

    private sealed record DatiUndo(
        Guid RapportoId,
        int Anno,
        int Mese,
        string HashImportiAttesi);

    public async Task<PreparazioneElaboraCedolinoAurora> PreparaAsync(
        Guid tenantId,
        Guid aziendaId,
        string periodo,
        string argomentiModelloJson,
        CancellationToken ct = default)
    {
        var rapportoId = LeggiArgomentiModello(argomentiModelloJson);
        var (anno, mese) = LeggiPeriodo(periodo);
        var (nominativo, entita) = await CaricaBaseAsync(tenantId, aziendaId, rapportoId, anno, mese, ct);
        await VerificaNessunCedolinoAsync(tenantId, rapportoId, anno, mese, ct);

        var cartellino = CartellinoBozzaService.DaEntita(entita);
        Cedolino anteprima;
        try
        {
            anteprima = (await cedolini.CalcolaAnteprimaAsync(tenantId, rapportoId, cartellino, ct)).Cedolino;
        }
        catch (ArgumentException ex)
        {
            // Messaggi del motore già pensati per l'operatore (periodo futuro, F24, calendario).
            throw new OperazioneAuroraNonConsentitaException(ex.Message);
        }

        string hashCartellino = CanoneAurora.Hash(cartellino);
        string hashImporti = HashImporti(rapportoId, anno, mese, anteprima);
        var comando = new Comando(
            rapportoId, entita.Id, entita.Versione, anno, mese, hashCartellino, hashImporti);
        var undo = new DatiUndo(rapportoId, anno, mese, hashImporti);
        var diff = new[]
        {
            Riga("Totale competenze", anteprima.TotaleCompetenze),
            Riga("Totale trattenute", anteprima.TotaleTrattenute),
            Riga("Netto in busta", anteprima.NettoInBusta),
            Riga("Quota TFR", anteprima.QuotaTfr),
            Riga("Costo azienda", anteprima.CostoAzienda),
        };

        return new(
            $"Elabora il cedolino {mese:00}/{anno} per {nominativo} (bozza da controllare)",
            diff,
            JsonSerializer.Serialize(comando, CanoneAurora.Json),
            JsonSerializer.Serialize(undo, CanoneAurora.Json),
            hashCartellino,
            hashImporti);
    }

    public async Task<EsitoElaboraCedolinoAurora> ApplicaAsync(
        Guid tenantId,
        Guid aziendaId,
        string periodo,
        string comandoCanonicoJson,
        CancellationToken ct = default)
    {
        var comando = Deserializza<Comando>(comandoCanonicoJson);
        var (anno, mese) = LeggiPeriodo(periodo);
        if (comando.Anno != anno || comando.Mese != mese)
            throw new OperazioneAuroraNonConsentitaException(
                "Il periodo della proposta non coincide più con il mandato.");

        var (_, entita) = await CaricaBaseAsync(tenantId, aziendaId, comando.RapportoId, anno, mese, ct);
        await VerificaNessunCedolinoAsync(tenantId, comando.RapportoId, anno, mese, ct);
        var cartellino = CartellinoBozzaService.DaEntita(entita);
        if (entita.Id != comando.CartellinoId
            || entita.Versione != comando.VersioneCartellino
            || !string.Equals(CanoneAurora.Hash(cartellino), comando.HashCartellino,
                StringComparison.Ordinal))
            throw new OperazioneAuroraNonConsentitaException(
                "Il cartellino è cambiato dopo l'anteprima: nessun cedolino elaborato.");

        CedolinoEntity creato;
        try
        {
            creato = await cedolini.CalcolaEArchiviaAsync(tenantId, comando.RapportoId, cartellino, ct);
        }
        catch (ArgumentException ex)
        {
            throw new OperazioneAuroraNonConsentitaException(ex.Message);
        }
        if (!string.Equals(HashImporti(comando.RapportoId, anno, mese, creato),
                comando.HashImportiAttesi, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Gli importi elaborati non coincidono con l'anteprima confermata.");

        string esito = JsonSerializer.Serialize(new
        {
            cedolinoId = creato.Id,
            rapportoId = comando.RapportoId,
            anno,
            mese,
            stato = creato.Stato,
            nettoInBusta = creato.NettoInBusta,
            totaleCompetenze = creato.TotaleCompetenze,
            ripristinabile = true,
        }, CanoneAurora.Json);
        return new(esito);
    }

    public async Task<EsitoElaboraCedolinoAurora> RipristinaAsync(
        Guid tenantId,
        Guid aziendaId,
        string periodo,
        string undoJson,
        CancellationToken ct = default)
    {
        var undo = Deserializza<DatiUndo>(undoJson);
        var (anno, mese) = LeggiPeriodo(periodo);
        if (undo.Anno != anno || undo.Mese != mese)
            throw new OperazioneAuroraNonConsentitaException(
                "Il periodo dell'annullamento non coincide con la proposta.");
        _ = await CaricaBaseAsync(tenantId, aziendaId, undo.RapportoId, anno, mese, ct);

        var cedolinoEsistente = await db.Cedolini.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.RapportoLavoroId == undo.RapportoId
            && c.Anno == anno && c.Mese == mese, ct)
            ?? throw new OperazioneAuroraNonConsentitaException(
                "Il cedolino non esiste più: nulla da ripristinare.");
        if (!string.Equals(cedolinoEsistente.Stato, nameof(StatoCedolino.Bozza), StringComparison.Ordinal)
            || cedolinoEsistente.Versione != 1
            || !string.Equals(HashImporti(undo.RapportoId, anno, mese, cedolinoEsistente),
                undo.HashImportiAttesi, StringComparison.Ordinal))
            throw new OperazioneAuroraNonConsentitaException(
                "Il cedolino è stato modificato o portato avanti nel workflow: "
                + "il ripristino automatico è bloccato.");
        if (await db.PraticheF24.AsNoTracking().AnyAsync(p =>
            p.TenantId == tenantId && p.AziendaId == aziendaId
            && p.Anno == anno && p.Mese == mese
            && (p.Stato == nameof(StatoPraticaTelematica.InviataManualmente)
                || p.Stato == nameof(StatoPraticaTelematica.RicevutaAcquisita)), ct))
            throw new OperazioneAuroraNonConsentitaException(
                "Il periodo ha un F24 finale: la bozza non può essere eliminata da Aurora.");

        db.Cedolini.Remove(cedolinoEsistente);
        await db.SaveChangesAsync(ct);

        string esito = JsonSerializer.Serialize(new
        {
            rapportoId = undo.RapportoId,
            anno,
            mese,
            cedolinoEliminato = true,
        }, CanoneAurora.Json);
        return new(esito);
    }

    public static bool ProvaNormalizzaArgomentiModello(
        string? json, out string canonico, out string motivo)
    {
        canonico = "";
        motivo = "Schema del tool cedolino non valido.";
        try
        {
            var rapportoId = LeggiArgomentiModello(json ?? "");
            canonico = JsonSerializer.Serialize(new { rapportoId }, CanoneAurora.Json);
            motivo = "";
            return true;
        }
        catch (OperazioneAuroraNonConsentitaException ex)
        {
            motivo = ex.Message;
            return false;
        }
    }

    private async Task<(string Nominativo, CartellinoEntity Entita)> CaricaBaseAsync(
        Guid tenantId, Guid aziendaId, Guid rapportoId, int anno, int mese, CancellationToken ct)
    {
        var rapporto = await db.Rapporti.AsNoTracking()
            .Where(r => r.Id == rapportoId && r.TenantId == tenantId
                && r.AziendaId == aziendaId
                && r.Dipendente != null && r.Dipendente.TenantId == tenantId)
            .Select(r => new { Nominativo = (r.Dipendente!.Cognome + " " + r.Dipendente.Nome).Trim() })
            .FirstOrDefaultAsync(ct)
            ?? throw new OperazioneAuroraNonConsentitaException(
                "Rapporto non trovato nell'azienda del mandato.");

        var entita = await db.Cartellini.AsNoTracking().FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
            && c.Anno == anno && c.Mese == mese, ct)
            ?? throw new OperazioneAuroraNonConsentitaException(
                "Prima completa il calendario del mese nella pagina Presenze.");
        return (rapporto.Nominativo, entita);
    }

    private async Task VerificaNessunCedolinoAsync(
        Guid tenantId, Guid rapportoId, int anno, int mese, CancellationToken ct)
    {
        if (await db.Cedolini.AsNoTracking().AnyAsync(c =>
            c.TenantId == tenantId && c.RapportoLavoroId == rapportoId
            && c.Anno == anno && c.Mese == mese, ct))
            throw new OperazioneAuroraNonConsentitaException(
                "Il cedolino del mese esiste già: Aurora non ricalcola mai una busta esistente.");
    }

    // Gli importi chiave firmati nell'intento: anteprima, applicazione e undo devono coincidere.
    private static string HashImporti(Guid rapportoId, int anno, int mese, Cedolino c) =>
        CanoneAurora.Hash(new
        {
            rapportoId, anno, mese,
            c.TotaleCompetenze, c.TotaleTrattenute, c.NettoInBusta,
            c.QuotaTfr, c.CostoAzienda, c.ContributiDipendente, c.IrpefNetta,
        });

    private static string HashImporti(Guid rapportoId, int anno, int mese, CedolinoEntity c) =>
        CanoneAurora.Hash(new
        {
            rapportoId, anno, mese,
            c.TotaleCompetenze, c.TotaleTrattenute, c.NettoInBusta,
            c.QuotaTfr, c.CostoAzienda, c.ContributiDipendente, c.IrpefNetta,
        });

    private static RigaDiffAurora Riga(string etichetta, decimal importo) => new(
        etichetta, null, importo.ToString("N2", CultureInfo.GetCultureInfo("it-IT")) + " €");

    private static Guid LeggiArgomentiModello(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1_024)
            throw new OperazioneAuroraNonConsentitaException("Argomenti assenti o troppo lunghi.");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 3,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException();
            var proprieta = doc.RootElement.EnumerateObject().ToArray();
            if (proprieta.Length != 1 || proprieta[0].Name != "rapportoId")
                throw new OperazioneAuroraNonConsentitaException(
                    "Lo schema della richiesta cedolino non è valido.");
            if (proprieta[0].Value.ValueKind != JsonValueKind.String
                || !Guid.TryParse(proprieta[0].Value.GetString(), out var rapportoId)
                || rapportoId == Guid.Empty)
                throw new OperazioneAuroraNonConsentitaException("Rapporto non valido.");
            return rapportoId;
        }
        catch (JsonException)
        {
            throw new OperazioneAuroraNonConsentitaException(
                "Argomenti non conformi allo schema del tool cedolino.");
        }
    }

    private static (int Anno, int Mese) LeggiPeriodo(string periodo)
    {
        if (periodo?.Length != 7
            || !int.TryParse(periodo[..4], out var anno)
            || periodo[4] != '-'
            || !int.TryParse(periodo[5..], out var mese)
            || anno is < 2000 or > 2100 || mese is < 1 or > 12)
            throw new OperazioneAuroraNonConsentitaException("Periodo del mandato non valido.");
        return (anno, mese);
    }

    private static T Deserializza<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, CanoneAurora.Json)
        ?? throw new InvalidOperationException("Payload server-side della proposta non leggibile.");
}
