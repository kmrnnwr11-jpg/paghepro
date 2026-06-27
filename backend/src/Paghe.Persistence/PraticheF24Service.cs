using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Archivia e gestisce le pratiche F24 generate dalle paghe: salvataggio del modello, stato della
/// pratica telematica (con transizioni validate) ed export del file per il Desktop Telematico.
/// Non effettua invii: prepara dati e file. Tutto isolato per tenant.
/// </summary>
public sealed class PraticheF24Service
{
    private readonly PagheDbContext _db;
    private readonly CedoliniService _cedolini;

    public PraticheF24Service(PagheDbContext db, CedoliniService cedolini)
    {
        _db = db;
        _cedolini = cedolini;
    }

    /// <summary>Genera l'F24 del periodo dai cedolini archiviati e lo salva come pratica (stato Generata).</summary>
    public async Task<PraticaF24Entity> GeneraEArchiviaDelMeseAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese,
        string matricolaInps, string codiceSedeInps = "", CancellationToken ct = default)
    {
        var f24 = await _cedolini.GeneraF24DelMeseAsync(
            tenantId, aziendaId, anno, mese, matricolaInps, codiceSedeInps, ct);
        var esito = ValidazioneF24.Verifica(f24);
        var ora = DateTime.UtcNow;

        var entity = new PraticaF24Entity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Anno = anno,
            Mese = mese,
            DataVersamento = f24.DataVersamento,
            TotaleDebiti = f24.TotaleDebiti,
            TotaleCrediti = f24.TotaleCrediti,
            SaldoFinale = f24.SaldoFinale,
            Stato = StatoPraticaTelematica.Generata.ToString(),
            Valido = esito.Valido,
            ModelloJson = JsonSerializer.Serialize(f24),
            Bozza = new BozzaLeggibileF24Writer().Genera(f24),
            TracciatoUfficiale = new TracciatoF24Ufficiale().Genera(f24),
            CreataIl = ora,
            UltimoAggiornamento = ora,
        };

        _db.PraticheF24.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<PraticaF24Entity>> ElencoAsync(
        Guid tenantId, Guid? aziendaId = null, CancellationToken ct = default) =>
        _db.PraticheF24
            .Where(p => p.TenantId == tenantId && (aziendaId == null || p.AziendaId == aziendaId))
            .OrderByDescending(p => p.Anno).ThenByDescending(p => p.Mese).ThenByDescending(p => p.CreataIl)
            .ToListAsync(ct);

    public Task<PraticaF24Entity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.PraticheF24.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

    /// <summary>Applica una transizione di stato alla pratica, validandola con la macchina a stati.</summary>
    public async Task<PraticaF24Entity> CambiaStatoAsync(
        Guid tenantId, Guid id, StatoPraticaTelematica nuovo, CancellationToken ct = default)
    {
        var p = await GetAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Pratica F24 non trovata per il tenant indicato.");

        var corrente = Enum.Parse<StatoPraticaTelematica>(p.Stato);
        if (!PraticaTelematica.PuoTransizionare(corrente, nuovo))
            throw new InvalidOperationException($"Transizione non ammessa da {corrente} a {nuovo}.");

        p.Stato = nuovo.ToString();
        p.UltimoAggiornamento = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return p;
    }

    /// <summary>
    /// Esporta la bozza dell'F24 nella cartella di consegna per il Desktop Telematico
    /// (<c>{cartellaBase}/agenzia-entrate/da-controllare</c>) e restituisce il percorso del file.
    /// </summary>
    public async Task<string> EsportaAsync(
        Guid tenantId, Guid id, string cartellaBase, CancellationToken ct = default)
    {
        var p = await GetAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Pratica F24 non trovata per il tenant indicato.");

        string dir = Path.Combine(cartellaBase, "agenzia-entrate", "da-controllare");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"F24-{p.Anno}-{p.Mese:00}-{p.Id}.txt");
        // Esporta il tracciato telematico ufficiale (record A/M/V/Z); ripiega sulla bozza se assente.
        string contenuto = string.IsNullOrEmpty(p.TracciatoUfficiale) ? p.Bozza : p.TracciatoUfficiale;
        await File.WriteAllTextAsync(file, contenuto, ct);
        return file;
    }
}
