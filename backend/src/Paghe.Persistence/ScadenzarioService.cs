using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati per registrare un DURC di un'azienda.</summary>
public sealed record DurcInput(
    Guid AziendaId,
    string Numero,
    DateOnly DataEmissione,
    DateOnly? DataScadenza = null,
    bool Regolare = true,
    string? Note = null);

/// <summary>Dati per inserire un promemoria a mano.</summary>
public sealed record PromemoriaManualeInput(
    string Titolo,
    DateOnly DataScadenza,
    Guid? AziendaId = null,
    string? Descrizione = null,
    int GiorniPreavviso = 7);

/// <summary>
/// Scadenzario / promemoria. Tiene i DURC delle aziende e genera in automatico i promemoria
/// (DURC in scadenza, F24 da versare) con il testo del messaggio già precompilato.
/// Non invia nulla: prepara avvisi e bozze pronti da consultare/copiare. Isolato per tenant.
/// </summary>
public sealed class ScadenzarioService
{
    private readonly PagheDbContext _db;

    /// <summary>Validità tipica del DURC: 120 giorni dall'emissione.</summary>
    public const int GiorniValiditaDurc = 120;

    /// <summary>Preavviso di default per il rinnovo DURC.</summary>
    public const int PreavvisoDurc = 30;

    /// <summary>Preavviso di default per il versamento F24.</summary>
    public const int PreavvisoF24 = 7;

    public ScadenzarioService(PagheDbContext db) => _db = db;

    // --- DURC ----------------------------------------------------------------------

    public async Task<DurcEntity> RegistraDurcAsync(Guid tenantId, DurcInput input, CancellationToken ct = default)
    {
        bool aziendaOk = await _db.Aziende.AnyAsync(a => a.Id == input.AziendaId && a.TenantId == tenantId, ct);
        if (!aziendaOk)
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        if (string.IsNullOrWhiteSpace(input.Numero))
            throw new ArgumentException("Il numero di protocollo del DURC è obbligatorio.");

        var scadenza = input.DataScadenza ?? input.DataEmissione.AddDays(GiorniValiditaDurc);
        if (scadenza < input.DataEmissione)
            throw new ArgumentException("La scadenza del DURC non può precedere la data di emissione.");

        var entity = new DurcEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = input.AziendaId,
            Numero = input.Numero.Trim(),
            DataEmissione = input.DataEmissione,
            DataScadenza = scadenza,
            Regolare = input.Regolare,
            Note = input.Note,
            CreatoIl = DateTime.UtcNow,
        };
        _db.Durc.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<DurcEntity>> ElencoDurcAsync(Guid tenantId, Guid? aziendaId = null, CancellationToken ct = default) =>
        _db.Durc
            .Where(d => d.TenantId == tenantId && (aziendaId == null || d.AziendaId == aziendaId))
            .OrderBy(d => d.DataScadenza)
            .ToListAsync(ct);

    public Task<DurcEntity?> GetDurcAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Durc.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);

    // --- Promemoria ----------------------------------------------------------------

    /// <summary>
    /// Rigenera in automatico i promemoria dalle fonti (DURC, F24): ne crea uno per ogni scadenza
    /// non ancora tracciata e aggiorna data/testo di quelli esistenti, senza toccarne lo stato
    /// (un promemoria già completato/annullato resta tale). Restituisce quanti ne ha creati/aggiornati.
    /// </summary>
    public async Task<int> RigeneraAsync(Guid tenantId, CancellationToken ct = default)
    {
        var aziende = await _db.Aziende
            .Where(a => a.TenantId == tenantId)
            .ToDictionaryAsync(a => a.Id, a => a.RagioneSociale, ct);

        var esistenti = await _db.Promemoria
            .Where(p => p.TenantId == tenantId && p.RiferimentoId != null)
            .ToListAsync(ct);
        var perRiferimento = esistenti
            .GroupBy(p => (p.Tipo, p.RiferimentoId))
            .ToDictionary(g => g.Key, g => g.First());

        int tocchi = 0;
        var ora = DateTime.UtcNow;

        // DURC in scadenza
        var durc = await _db.Durc.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        foreach (var d in durc)
        {
            string nomeAzienda = aziende.GetValueOrDefault(d.AziendaId, "azienda");
            string titolo = $"Rinnovo DURC {nomeAzienda}";
            string messaggio = MessaggioDurc(d, nomeAzienda);
            if (Applica(perRiferimento, tenantId, TipoPromemoria.Durc, d.Id, d.AziendaId,
                    titolo, $"DURC n. {d.Numero} in scadenza.", d.DataScadenza, PreavvisoDurc, messaggio, ora))
                tocchi++;
        }

        // F24 da versare
        var f24 = await _db.PraticheF24.Where(p => p.TenantId == tenantId).ToListAsync(ct);
        foreach (var p in f24)
        {
            string nomeAzienda = aziende.GetValueOrDefault(p.AziendaId, "azienda");
            string periodo = $"{p.Mese:00}/{p.Anno}";
            string titolo = $"Versamento F24 {periodo} {nomeAzienda}";
            string messaggio = MessaggioF24(p, nomeAzienda, periodo);
            if (Applica(perRiferimento, tenantId, TipoPromemoria.F24, p.Id, p.AziendaId,
                    titolo, $"F24 periodo {periodo}: saldo {Euro(p.SaldoFinale)}.", p.DataVersamento, PreavvisoF24, messaggio, ora))
                tocchi++;
        }

        if (tocchi > 0)
            await _db.SaveChangesAsync(ct);
        return tocchi;
    }

    /// <summary>Crea o aggiorna il promemoria di una fonte. Ritorna true se ha creato/modificato qualcosa.</summary>
    private bool Applica(
        Dictionary<(string, string?), PromemoriaEntity> indice,
        Guid tenantId, string tipo, Guid riferimento, Guid aziendaId,
        string titolo, string descrizione, DateOnly scadenza, int preavviso, string messaggio, DateTime ora)
    {
        string rif = riferimento.ToString();
        if (indice.TryGetValue((tipo, rif), out var p))
        {
            // Aggiorna solo i contenuti, preserva lo stato deciso dall'utente.
            if (p.Titolo == titolo && p.Descrizione == descrizione &&
                p.DataScadenza == scadenza && p.Messaggio == messaggio && p.GiorniPreavviso == preavviso)
                return false;

            p.Titolo = titolo;
            p.Descrizione = descrizione;
            p.DataScadenza = scadenza;
            p.GiorniPreavviso = preavviso;
            p.Messaggio = messaggio;
            p.UltimoAggiornamento = ora;
            return true;
        }

        var nuovo = new PromemoriaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Tipo = tipo,
            RiferimentoId = rif,
            Titolo = titolo,
            Descrizione = descrizione,
            DataScadenza = scadenza,
            GiorniPreavviso = preavviso,
            Stato = StatoPromemoria.Aperto,
            Messaggio = messaggio,
            CreatoIl = ora,
            UltimoAggiornamento = ora,
        };
        _db.Promemoria.Add(nuovo);
        indice[(tipo, rif)] = nuovo;
        return true;
    }

    /// <summary>Elenca i promemoria (rigenerando prima dalle fonti, salvo richiesta contraria), ordinati per scadenza.</summary>
    public async Task<List<PromemoriaEntity>> ElencoAsync(
        Guid tenantId, bool rigenera = true, CancellationToken ct = default)
    {
        if (rigenera)
            await RigeneraAsync(tenantId, ct);
        return await _db.Promemoria
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.DataScadenza)
            .ToListAsync(ct);
    }

    public Task<PromemoriaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Promemoria.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

    public async Task<PromemoriaEntity> CreaManualeAsync(
        Guid tenantId, PromemoriaManualeInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Titolo))
            throw new ArgumentException("Il titolo del promemoria è obbligatorio.");
        if (input.AziendaId is { } az &&
            !await _db.Aziende.AnyAsync(a => a.Id == az && a.TenantId == tenantId, ct))
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        var ora = DateTime.UtcNow;
        var entity = new PromemoriaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = input.AziendaId,
            Tipo = TipoPromemoria.Manuale,
            RiferimentoId = null,
            Titolo = input.Titolo.Trim(),
            Descrizione = input.Descrizione?.Trim() ?? "",
            DataScadenza = input.DataScadenza,
            GiorniPreavviso = input.GiorniPreavviso < 0 ? 0 : input.GiorniPreavviso,
            Stato = StatoPromemoria.Aperto,
            Messaggio = input.Descrizione?.Trim() ?? input.Titolo.Trim(),
            CreatoIl = ora,
            UltimoAggiornamento = ora,
        };
        _db.Promemoria.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>Cambia lo stato di un promemoria, validando la transizione.</summary>
    public async Task<PromemoriaEntity> CambiaStatoAsync(
        Guid tenantId, Guid id, string nuovoStato, CancellationToken ct = default)
    {
        if (!StatoPromemoria.Tutti.Contains(nuovoStato))
            throw new ArgumentException($"Stato non valido: {nuovoStato}.");

        var p = await GetAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Promemoria non trovato per il tenant indicato.");

        if (StatoPromemoria.Finale(p.Stato))
            throw new InvalidOperationException($"Il promemoria è {p.Stato}: lo stato non può più cambiare.");

        p.Stato = nuovoStato;
        if (nuovoStato == StatoPromemoria.Inviato)
            p.InviatoIl = DateTime.UtcNow;
        p.UltimoAggiornamento = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return p;
    }

    // --- Messaggi precompilati -----------------------------------------------------

    private static string MessaggioDurc(DurcEntity d, string nomeAzienda)
    {
        string scad = Data(d.DataScadenza);
        return $"""
            Oggetto: DURC in scadenza – {nomeAzienda}

            Il DURC n. {d.Numero} di {nomeAzienda} scade il {scad}.
            Si ricorda di richiedere il rinnovo della regolarità contributiva con congruo anticipo
            per non interrompere la validità del documento.
            """;
    }

    private static string MessaggioF24(PraticaF24Entity p, string nomeAzienda, string periodo)
    {
        string scad = Data(p.DataVersamento);
        return $"""
            Oggetto: Scadenza versamento F24 {periodo} – {nomeAzienda}

            Si ricorda che l'F24 del periodo {periodo} di {nomeAzienda} è in scadenza il {scad}
            per un saldo di {Euro(p.SaldoFinale)}.
            Predisporre per tempo la disponibilità e l'invio del modello.
            """;
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Euro(decimal importo) =>
        importo.ToString("N2", CultureInfo.GetCultureInfo("it-IT")) + " €";
}
