using System.Text;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Fatturazione;
using Paghe.Core.Fatturazione.Trasmissione;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati di una ricevuta/notifica SdI da registrare su una trasmissione.</summary>
public sealed record RicevutaSdiInput(
    string Tipo,
    string? CodiceEsito = null,
    string? Descrizione = null,
    string? ContenutoGrezzo = null);

/// <summary>
/// Orchestratore della trasmissione di una fattura elettronica verso lo SdI: genera l'XML dalla
/// fattura persistita, esegue i controlli semantici SdI, applica/delega la firma, invia tramite il
/// gateway configurato e traccia stato e ricevute. Tutto isolato per tenant.
/// </summary>
public sealed class TrasmissioneFattureService
{
    private readonly PagheDbContext _db;
    private readonly IFirmaFatturaElettronica _firma;
    private readonly IFatturaSdiGateway _gateway;

    public TrasmissioneFattureService(
        PagheDbContext db,
        IFirmaFatturaElettronica firma,
        IFatturaSdiGateway gateway)
    {
        _db = db;
        _firma = firma;
        _gateway = gateway;
    }

    /// <summary>
    /// Trasmette la fattura indicata. Blocca se la fattura non è conforme ai controlli SdI o se
    /// risulta già trasmessa/in corso. Restituisce la trasmissione persistita con lo stato iniziale.
    /// </summary>
    public async Task<TrasmissioneFatturaEntity> TrasmettiAsync(
        Guid tenantId, Guid fatturaId, CancellationToken ct = default)
    {
        var fatturaEntity = await _db.Fatture
            .Include(f => f.Azienda)
            .Include(f => f.Righe)
            .FirstOrDefaultAsync(f => f.Id == fatturaId && f.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Fattura non trovata per il tenant indicato.");

        // Evita doppi invii: se esiste una trasmissione non in errore/non scartata, blocca.
        bool giaInCorso = await _db.TrasmissioniFatture.AnyAsync(t =>
            t.TenantId == tenantId && t.FatturaId == fatturaId &&
            t.Stato != nameof(StatoTrasmissioneSdi.Errore) &&
            t.Stato != nameof(StatoTrasmissioneSdi.ScartataSdi), ct);
        if (giaInCorso)
            throw new InvalidOperationException("La fattura risulta già trasmessa o in corso di trasmissione.");

        var dominio = FattureService.ToDominio(fatturaEntity);

        // Controlli semantici SdI: non inviare fatture che verrebbero scartate.
        var errori = ControlliSdi.Verifica(dominio);
        if (errori.Count > 0)
            throw new ArgumentException(
                "Fattura non conforme ai controlli SdI: " +
                string.Join("; ", errori.Select(e => $"{e.Codice} {e.Descrizione}")));

        string xml = FatturaPaXml.Genera(dominio);
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);

        string idTrasmittente = dominio.Cedente.PartitaIva
            ?? dominio.Cedente.CodiceFiscale
            ?? throw new ArgumentException("Il cedente deve avere Partita IVA o Codice Fiscale per trasmettere.");
        string progressivo = NomeFileFatturaPa.ProgressivoCasuale();
        string nomeFileXml = NomeFileFatturaPa.Genera(dominio.Cedente.Nazione, idTrasmittente, progressivo);

        var firmata = await _firma.FirmaAsync(xmlBytes, nomeFileXml, ct);
        var risultato = await _gateway.TrasmettiAsync(
            new FatturaDaTrasmettere(fatturaId, firmata.NomeFile, firmata.Contenuto, firmata.Firmato), ct);

        var ora = DateTimeOffset.UtcNow;
        var entity = new TrasmissioneFatturaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = fatturaEntity.AziendaId,
            FatturaId = fatturaId,
            Provider = _gateway.Nome,
            NomeFileXml = firmata.NomeFile,
            Firmato = firmata.Firmato,
            Stato = risultato.Stato.ToString(),
            IdentificativoSdi = risultato.IdentificativoSdi,
            CodiceErrore = risultato.CodiceErrore,
            MessaggioErrore = risultato.Stato == StatoTrasmissioneSdi.Errore ? risultato.Messaggio : null,
            CreataIl = ora,
            InviataIl = risultato.Stato is StatoTrasmissioneSdi.Inviata
                or StatoTrasmissioneSdi.ConsegnataSdi
                or StatoTrasmissioneSdi.MancataConsegna ? ora : null,
            UltimoAggiornamento = ora,
        };

        _db.TrasmissioniFatture.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>Registra una ricevuta/notifica SdI (RC/MC/NS/NE/DT) e aggiorna lo stato. Per webhook o inserimento manuale.</summary>
    public async Task<TrasmissioneFatturaEntity> RegistraRicevutaAsync(
        Guid tenantId, Guid trasmissioneId, RicevutaSdiInput input, CancellationToken ct = default)
    {
        var t = await _db.TrasmissioniFatture
            .Include(x => x.Ricevute)
            .FirstOrDefaultAsync(x => x.Id == trasmissioneId && x.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Trasmissione non trovata per il tenant indicato.");

        // La ricevuta è una nuova riga: la si traccia esplicitamente come Added (con chiave generata),
        // senza affidarsi alla rilevazione dalla collection del parent — che su questo grafo veniva
        // interpretata come UPDATE, provocando una DbUpdateConcurrencyException (0 righe modificate).
        var ricevuta = new RicevutaSdiEntity
        {
            Id = Guid.NewGuid(),
            TrasmissioneFatturaId = t.Id,
            Tipo = input.Tipo,
            CodiceEsito = input.CodiceEsito,
            Descrizione = input.Descrizione,
            ContenutoGrezzo = input.ContenutoGrezzo,
            RicevutaIl = DateTimeOffset.UtcNow,
        };
        t.Ricevute.Add(ricevuta);
        _db.Entry(ricevuta).State = EntityState.Added;

        t.Stato = MappaturaRicevuteSdi.DaTipo(input.Tipo, input.CodiceEsito).ToString();
        t.UltimoAggiornamento = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return t;
    }

    /// <summary>Interroga il gateway per l'esito aggiornato e ne salva lo stato (se il canale lo supporta).</summary>
    public async Task<TrasmissioneFatturaEntity> AggiornaEsitoAsync(
        Guid tenantId, Guid trasmissioneId, CancellationToken ct = default)
    {
        var t = await _db.TrasmissioniFatture
            .FirstOrDefaultAsync(x => x.Id == trasmissioneId && x.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Trasmissione non trovata per il tenant indicato.");

        if (string.IsNullOrWhiteSpace(t.IdentificativoSdi))
            return t;

        var esito = await _gateway.InterrogaEsitoAsync(t.IdentificativoSdi, ct);
        if (esito is null)
            return t;

        t.Stato = esito.Stato.ToString();
        if (esito.CodiceEsito is not null) t.CodiceErrore = esito.CodiceEsito;
        t.UltimoAggiornamento = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public Task<List<TrasmissioneFatturaEntity>> ElencoAsync(
        Guid tenantId, Guid? aziendaId = null, CancellationToken ct = default) =>
        _db.TrasmissioniFatture
            .Where(t => t.TenantId == tenantId && (aziendaId == null || t.AziendaId == aziendaId))
            .OrderByDescending(t => t.CreataIl)
            .ToListAsync(ct);

    public Task<TrasmissioneFatturaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.TrasmissioniFatture
            .Include(t => t.Ricevute)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);

    public Task<TrasmissioneFatturaEntity?> GetPerFatturaAsync(Guid tenantId, Guid fatturaId, CancellationToken ct = default) =>
        _db.TrasmissioniFatture
            .Include(t => t.Ricevute)
            .Where(t => t.TenantId == tenantId && t.FatturaId == fatturaId)
            .OrderByDescending(t => t.CreataIl)
            .FirstOrDefaultAsync(ct);
}
