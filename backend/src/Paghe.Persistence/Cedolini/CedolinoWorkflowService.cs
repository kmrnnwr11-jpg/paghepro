using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Documenti;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;

namespace Paghe.Persistence.Cedolini;

/// <summary>
/// Macchina a stati del cedolino. Gli attori arrivano dall'identità autenticata; snapshot e PDF
/// sono cifrati e append-only, così una ristampa chiusa non interroga anagrafiche o parametri.
/// </summary>
public sealed class CedolinoWorkflowService
{
    private readonly PagheDbContext _db;
    private readonly CedoliniService _cedolini;
    private readonly TimeProvider _clock;
    private readonly RequisitiCedolinoService _requisiti;

    public CedolinoWorkflowService(
        PagheDbContext db,
        CedoliniService cedolini,
        TimeProvider? clock = null,
        RequisitiCedolinoService? requisiti = null)
    {
        _db = db;
        _cedolini = cedolini;
        _clock = clock ?? TimeProvider.System;
        _requisiti = requisiti ?? new RequisitiCedolinoService(db);
    }

    public async Task<StatoWorkflowCedolino?> LeggiStatoAsync(
        Guid tenantId, Guid cedolinoId, CancellationToken ct = default)
    {
        var c = await _db.Cedolini.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == cedolinoId, ct);
        return c is null ? null : Proietta(c);
    }

    public async Task<StatoWorkflowCedolino> ControllaAsync(
        Guid tenantId,
        Guid cedolinoId,
        Guid attoreId,
        int versioneAttesa,
        CancellationToken ct = default)
    {
        ValidaAttore(attoreId);
        var c = await CaricaAsync(tenantId, cedolinoId, ct);
        BloccaArchivioPreWorkflow(c);
        RichiediVersione(c, versioneAttesa);
        RichiediStato(c, StatoCedolino.Bozza);
        await _requisiti.RichiediCedolinoCompletoAsync(tenantId, cedolinoId, ct);
        if (string.IsNullOrWhiteSpace(c.CodiceCnel))
            throw new ArgumentException(
                "Manca il codice CNEL del contratto applicato. Completa il CCNL prima del controllo.");
        if (string.IsNullOrWhiteSpace(c.ParametriApplicatiProtetti)
            || string.IsNullOrWhiteSpace(c.HashParametriSha256))
            throw new ArgumentException(
                "Manca la fotografia dei parametri applicati: ricalcola questa bozza prima del controllo.");

        string parametriJson = SvelaParametri(c);
        VerificaHash(parametriJson, c.HashParametriSha256, "parametri applicati");
        var dati = await _cedolini.LeggiPerPdfAsync(tenantId, cedolinoId, ct)
            ?? throw new InvalidOperationException("Dati del cedolino non più disponibili.");
        if (string.IsNullOrWhiteSpace(dati.Intestazione.CodiceCnel))
            throw new ArgumentException("Il codice CNEL non è presente nel prospetto da controllare.");

        var rapporto = await _db.Rapporti.AsNoTracking().FirstOrDefaultAsync(
            r => r.TenantId == tenantId && r.Id == c.RapportoLavoroId, ct)
            ?? throw new InvalidOperationException("Rapporto di lavoro del cedolino non trovato.");
        if (dati.Cartellino.Giornate.Count == 0)
            throw new ArgumentException(
                "Manca il calendario giorno per giorno. Compila ogni data, anche riposi e festività, "
                + "prima di congelare il cedolino.");
        decimal? riportoPrimaSettimana = await RiportoOreSettimanaliService.CalcolaAsync(
            _db, tenantId, c.RapportoLavoroId, dati.Cartellino.Anno, dati.Cartellino.Mese,
            rapporto.DataAssunzione, ct);
        var problemiCalendario = CalendarioCartellino.Verifica(
            dati.Cartellino.Anno, dati.Cartellino.Mese, dati.Cartellino.Giornate,
            rapporto.OreSettimanali / 5m,
            oreLavoratePregressePrimaSettimana: riportoPrimaSettimana);
        var bloccantiCalendario = problemiCalendario.Where(p => p.Bloccante).ToList();
        if (bloccantiCalendario.Count > 0)
            throw new ArgumentException(
                "Il calendario non è completo: "
                + string.Join(" ", bloccantiCalendario.Select(p => p.Messaggio)));

        ValidaAssegniFamiliari(dati.Cartellino);

        var snapshotJson = CedolinoSnapshotCodec.SerializzaSnapshot(dati, parametriJson);
        var snapshotId = Guid.NewGuid();
        var snapshot = new CedolinoSnapshotEntity
        {
            Id = snapshotId,
            TenantId = tenantId,
            CedolinoId = cedolinoId,
            VersioneSchema = 1,
            DatiProtetti = _db.Protettore.ProteggiConContesto(
                snapshotJson,
                ContestoDatiProtetti.DatiSnapshotCedolino(tenantId, snapshotId, cedolinoId))!,
            HashDatiSha256 = CedolinoSnapshotCodec.HashTesto(snapshotJson),
            CreatoIl = OraUtc(),
            CreatoDa = attoreId,
        };
        _db.CedoliniSnapshot.Add(snapshot);

        c.Stato = nameof(StatoCedolino.Controllata);
        c.ControllatoIl = snapshot.CreatoIl;
        c.ControllatoDa = attoreId;
        c.Versione++;
        await SalvaConConcorrenzaAsync(ct);
        return Proietta(c, snapshot.HashDatiSha256);
    }

    public async Task<StatoWorkflowCedolino> ValidaAsync(
        Guid tenantId,
        Guid cedolinoId,
        Guid attoreId,
        int versioneAttesa,
        DatiValidazioneCedolino validazione,
        CancellationToken ct = default)
    {
        ValidaAttore(attoreId);
        var c = await CaricaAsync(tenantId, cedolinoId, ct);
        BloccaArchivioPreWorkflow(c);
        ValidaDatiValidazione(validazione);
        RichiediVersione(c, versioneAttesa);
        RichiediStato(c, StatoCedolino.Controllata);
        await _requisiti.RichiediCedolinoCompletoAsync(tenantId, cedolinoId, ct);
        var snapshot = await CaricaSnapshotIntegroAsync(c, ct);

        var json = JsonSerializer.Serialize(validazione);
        c.DatiValidazioneProtetti = _db.Protettore.ProteggiConContesto(
            json, ContestoDatiProtetti.ValidazioneCedolino(tenantId, cedolinoId));
        c.Stato = nameof(StatoCedolino.Validata);
        c.ValidatoIl = OraUtc();
        c.ValidatoDa = attoreId;
        c.Versione++;
        await SalvaConConcorrenzaAsync(ct);
        return Proietta(c, snapshot.HashDatiSha256);
    }

    public async Task<StatoWorkflowCedolino> ChiudiAsync(
        Guid tenantId,
        Guid cedolinoId,
        Guid attoreId,
        int versioneAttesa,
        CancellationToken ct = default,
        StileCedolino stile = CatalogoStiliCedolino.Predefinito,
        string fontFamily = FontDocumenti.Predefinito,
        string? dicituraStudio = null)
    {
        ValidaAttore(attoreId);
        var c = await CaricaAsync(tenantId, cedolinoId, ct);
        BloccaArchivioPreWorkflow(c);
        RichiediVersione(c, versioneAttesa);
        RichiediStato(c, StatoCedolino.Validata);
        await _requisiti.RichiediCedolinoCompletoAsync(tenantId, cedolinoId, ct);
        if (string.IsNullOrWhiteSpace(c.DatiValidazioneProtetti))
            throw new ArgumentException("Manca la validazione registrata del cedolino.");

        var snapshot = await CaricaSnapshotIntegroAsync(c, ct);
        var azienda = await _db.Aziende.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Id == c.AziendaId, ct)
            ?? throw new InvalidOperationException("Azienda del cedolino non trovata.");
        if (azienda.FirmaSiglaTimbro is not { Length: > 0 } firma)
            throw new ArgumentException(
                "Aggiungi una firma, sigla o timbro autentico del datore prima di chiudere il cedolino.");

        string snapshotJson = SvelaSnapshot(snapshot);
        var dati = CedolinoSnapshotCodec.DeserializzaSnapshot(snapshotJson);
        string validazioneJson = _db.Protettore.SvelaConContesto(
            c.DatiValidazioneProtetti,
            ContestoDatiProtetti.ValidazioneCedolino(tenantId, cedolinoId))!;
        var validazione = JsonSerializer.Deserialize<DatiValidazioneCedolino>(validazioneJson)
            ?? throw new InvalidOperationException("Dati della validazione non leggibili.");

        // Anche l'aspetto fa parte del documento immutabile: viene scelto una volta, validato
        // dal catalogo e registrato insieme ai byte. Le ristampe non lo ricalcolano mai.
        _ = CatalogoStiliCedolino.Descrivi(stile);
        string fontConvalidato = FontDocumenti.Famiglia(FontDocumenti.Chiave(fontFamily));
        byte[] pdf = CedolinoPdf.Genera(
            dati.Cedolino,
            dati.Intestazione,
            dati.Cartellino,
            new ContestoEmissioneCedolino(
                nameof(StatoCedolino.Chiusa),
                firma,
                $"Snapshot {snapshot.Id:D}",
                $"Validazione registrata: {validazione.Nominativo} - {validazione.Riferimento}"),
            stile: stile,
            progressivi: dati.Progressivi,
            fontFamily: fontConvalidato,
            dicituraStudio: dicituraStudio);

        var documentoId = Guid.NewGuid();
        var documento = new CedolinoDocumentoEntity
        {
            Id = documentoId,
            TenantId = tenantId,
            CedolinoId = cedolinoId,
            PdfProtetto = _db.Protettore.ProteggiConContesto(
                Convert.ToBase64String(pdf),
                ContestoDatiProtetti.PdfSnapshotCedolino(tenantId, documentoId, cedolinoId))!,
            HashPdfSha256 = CedolinoSnapshotCodec.Hash(pdf),
            HashFirmaSiglaTimbroSha256 = CedolinoSnapshotCodec.Hash(firma),
            VersioneRenderer = $"cedolino-v2:{CatalogoStiliCedolino.Chiave(stile)}:{FontDocumenti.Chiave(fontConvalidato)}",
            CreatoIl = OraUtc(),
            CreatoDa = attoreId,
        };
        _db.CedoliniDocumenti.Add(documento);

        c.Stato = nameof(StatoCedolino.Chiusa);
        c.ChiusoIl = documento.CreatoIl;
        c.ChiusoDa = attoreId;
        c.Versione++;
        await SalvaConConcorrenzaAsync(ct);
        return Proietta(c, snapshot.HashDatiSha256, documento.HashPdfSha256);
    }

    public async Task<DocumentoCedolino?> LeggiPdfAsync(
        Guid tenantId,
        Guid cedolinoId,
        CancellationToken ct = default,
        StileCedolino stile = CatalogoStiliCedolino.Predefinito,
        string fontFamily = "Lato",
        string? dicituraStudio = null)
    {
        var c = await _db.Cedolini.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == cedolinoId, ct);
        if (c is null) return null;
        var stato = Stato(c);

        if (stato == StatoCedolino.Chiusa)
        {
            var documento = await _db.CedoliniDocumenti.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CedolinoId == cedolinoId, ct)
                ?? throw new InvalidOperationException("PDF chiuso mancante: download bloccato.");
            var base64 = _db.Protettore.SvelaConContesto(
                documento.PdfProtetto,
                ContestoDatiProtetti.PdfSnapshotCedolino(tenantId, documento.Id, cedolinoId))!;
            byte[] pdf;
            try { pdf = Convert.FromBase64String(base64); }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("PDF chiuso corrotto: download bloccato.", ex);
            }
            VerificaHash(pdf, documento.HashPdfSha256, "PDF chiuso");
            return new DocumentoCedolino(
                pdf, NomeFile(c, stato), stato, Definitivo: false, documento.HashPdfSha256);
        }

        CedolinoPerPdf dati;
        if (stato == StatoCedolino.PreWorkflow)
        {
            // Un documento storico non riceve retroattivamente una fotografia del nuovo flusso:
            // l'anteprima usa soltanto importi e voci già archiviati e resta non definitiva.
            dati = await _cedolini.LeggiPerPdfAsync(tenantId, cedolinoId, ct)
                ?? throw new InvalidOperationException("Dati del cedolino storico non più disponibili.");
        }
        else if (stato is StatoCedolino.Controllata or StatoCedolino.Validata)
        {
            var snapshot = await CaricaSnapshotIntegroAsync(c, ct);
            dati = CedolinoSnapshotCodec.DeserializzaSnapshot(SvelaSnapshot(snapshot));
        }
        else
        {
            dati = await _cedolini.LeggiPerPdfAsync(tenantId, cedolinoId, ct)
                ?? throw new InvalidOperationException("Dati del cedolino non più disponibili.");
        }

        var anteprima = CedolinoPdf.Genera(
            dati.Cedolino,
            dati.Intestazione,
            dati.Cartellino,
            new ContestoEmissioneCedolino(stato.ToString()),
            stile: stile,
            progressivi: dati.Progressivi,
            fontFamily: fontFamily,
            dicituraStudio: dicituraStudio);
        return new DocumentoCedolino(
            anteprima, NomeFile(c, stato), stato, Definitivo: false,
            CedolinoSnapshotCodec.Hash(anteprima));
    }

    private async Task<CedolinoEntity> CaricaAsync(
        Guid tenantId, Guid cedolinoId, CancellationToken ct) =>
        await _db.Cedolini.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == cedolinoId, ct)
        ?? throw new CedolinoNonTrovatoException();

    private async Task<CedolinoSnapshotEntity> CaricaSnapshotIntegroAsync(
        CedolinoEntity c, CancellationToken ct)
    {
        var snapshot = await _db.CedoliniSnapshot.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == c.TenantId && x.CedolinoId == c.Id, ct)
            ?? throw new InvalidOperationException("Snapshot del cedolino mancante.");
        string json = SvelaSnapshot(snapshot);
        VerificaHash(json, snapshot.HashDatiSha256, "snapshot cedolino");
        return snapshot;
    }

    private string SvelaSnapshot(CedolinoSnapshotEntity snapshot) =>
        _db.Protettore.SvelaConContesto(
            snapshot.DatiProtetti,
            ContestoDatiProtetti.DatiSnapshotCedolino(
                snapshot.TenantId, snapshot.Id, snapshot.CedolinoId))!;

    private string SvelaParametri(CedolinoEntity c) =>
        _db.Protettore.SvelaConContesto(
            c.ParametriApplicatiProtetti,
            ContestoDatiProtetti.ParametriCedolino(c.TenantId, c.Id))!;

    private async Task SalvaConConcorrenzaAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflittoCedolinoException(
                "Il cedolino è stato aggiornato da un altro utente. Ricarica lo stato e riprova.", ex);
        }
    }

    private static void RichiediStato(CedolinoEntity c, StatoCedolino richiesto)
    {
        if (Stato(c) != richiesto)
            throw new ConflittoCedolinoException(
                $"Operazione non consentita: il cedolino è nello stato '{Stato(c)}', " +
                $"ma serve lo stato '{richiesto}'.");
    }

    private static void BloccaArchivioPreWorkflow(CedolinoEntity c)
    {
        if (Stato(c) == StatoCedolino.PreWorkflow)
            throw new ConflittoCedolinoException(
                "Il cedolino appartiene all'archivio pre-workflow: nessuna transizione è consentita.");
    }

    private static void RichiediVersione(CedolinoEntity c, int versioneAttesa)
    {
        if (versioneAttesa < 1 || c.Versione != versioneAttesa)
            throw new ConflittoCedolinoException(
                $"Versione non aggiornata: attesa {versioneAttesa}, corrente {c.Versione}.");
    }

    private static StatoCedolino Stato(CedolinoEntity c)
    {
        if (Enum.TryParse<StatoCedolino>(c.Stato, true, out var stato)
            && Enum.IsDefined(stato))
            return stato;
        throw new InvalidOperationException(
            $"Stato del cedolino non riconosciuto ('{c.Stato}'): operazione bloccata per integrità.");
    }

    private static void ValidaAssegniFamiliari(Cartellino cartellino)
    {
        if (cartellino.StatoAssegniFamiliari == StatoAssegniFamiliari.DaVerificare)
            throw new ArgumentException(
                "Assegni familiari non verificati: indica se non spettano oppure inserisci l'importo dovuto.");
        if (cartellino.StatoAssegniFamiliari == StatoAssegniFamiliari.NonSpettanti
            && cartellino.ImportoAssegniFamiliari != 0m)
            throw new ArgumentException(
                "Gli assegni familiari sono segnati come non spettanti ma hanno un importo.");
        if (cartellino.StatoAssegniFamiliari == StatoAssegniFamiliari.Spettanti
            && cartellino.ImportoAssegniFamiliari <= 0m)
            throw new ArgumentException(
                "Gli assegni familiari sono spettanti: indica un importo maggiore di zero.");
    }

    private static void VerificaHash(string testo, string atteso, string nome) =>
        VerificaHash(System.Text.Encoding.UTF8.GetBytes(testo), atteso, nome);

    private static void VerificaHash(byte[] dati, string atteso, string nome)
    {
        var calcolato = CedolinoSnapshotCodec.Hash(dati);
        if (!string.Equals(calcolato, atteso, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Integrità {nome} non valida: operazione bloccata.");
    }

    private static void ValidaAttore(Guid attoreId)
    {
        if (attoreId == Guid.Empty)
            throw new InvalidOperationException("Identità dell'utente autenticato non disponibile.");
    }

    private static void ValidaDatiValidazione(DatiValidazioneCedolino v)
    {
        if (string.IsNullOrWhiteSpace(v.Nominativo)
            || string.IsNullOrWhiteSpace(v.Qualifica)
            || string.IsNullOrWhiteSpace(v.Riferimento))
            throw new ArgumentException(
                "Indica nominativo, qualifica e riferimento della validazione ricevuta.");
        if (v.Nominativo.Length > 160 || v.Qualifica.Length > 160 || v.Riferimento.Length > 500)
            throw new ArgumentException("I dati della validazione sono troppo lunghi.");
    }

    private DateTime OraUtc() => _clock.GetUtcNow().UtcDateTime;

    private static string NomeFile(CedolinoEntity c, StatoCedolino stato) =>
        $"prospetto-simulazione-{c.Anno}-{c.Mese:00}-{stato.ToString().ToLowerInvariant()}.pdf";

    private static StatoWorkflowCedolino Proietta(
        CedolinoEntity c, string? hashSnapshot = null, string? hashPdf = null) =>
        new(c.Id, Stato(c), c.Versione, c.ControllatoIl, c.ControllatoDa,
            c.ValidatoIl, c.ValidatoDa, c.ChiusoIl, c.ChiusoDa,
            c.CodiceCnel, hashSnapshot, hashPdf);
}

public sealed record DatiValidazioneCedolino(
    string Nominativo,
    string Qualifica,
    string Riferimento);

public sealed record StatoWorkflowCedolino(
    Guid Id,
    StatoCedolino Stato,
    int Versione,
    DateTime? ControllatoIl,
    Guid? ControllatoDa,
    DateTime? ValidatoIl,
    Guid? ValidatoDa,
    DateTime? ChiusoIl,
    Guid? ChiusoDa,
    string? CodiceCnel,
    string? HashSnapshot,
    string? HashPdf);

public sealed record DocumentoCedolino(
    byte[] Dati,
    string NomeFile,
    StatoCedolino Stato,
    bool Definitivo,
    string HashSha256);

public sealed class CedolinoNonTrovatoException : Exception;

public sealed class ConflittoCedolinoException : Exception
{
    public ConflittoCedolinoException(string message) : base(message) { }
    public ConflittoCedolinoException(string message, Exception inner) : base(message, inner) { }
}
