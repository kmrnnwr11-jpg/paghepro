using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Una voce di bilancio in ingresso: conto, classificazione CEE e saldo.</summary>
public sealed record VoceBilancioInput(string Codice, string Descrizione, MacroVoce Voce, decimal Importo);

/// <summary>
/// Redige e archivia il bilancio d'esercizio (Stato Patrimoniale + Conto Economico, schema CEE)
/// per azienda e anno: riclassifica i saldi, persiste totali/righe, gestisce lo stato della
/// pratica (bozza → approvato → depositato) e può precompilare ricavi e costi dalla contabilità
/// già registrata (fatture, spese, cedolini). Tutto isolato per tenant.
/// </summary>
public sealed class BilancioService
{
    private readonly PagheDbContext _db;

    public BilancioService(PagheDbContext db) => _db = db;

    /// <summary>
    /// Riclassifica le voci indicate, calcola il risultato d'esercizio e archivia il bilancio
    /// dell'anno per l'azienda (stato iniziale: Bozza).
    /// </summary>
    public async Task<BilancioEntity> CreaAsync(
        Guid tenantId, Guid aziendaId, int anno,
        IReadOnlyList<VoceBilancioInput> voci, CancellationToken ct = default)
    {
        await VerificaAziendaAsync(tenantId, aziendaId, ct);
        if (voci is null || voci.Count == 0)
            throw new ArgumentException("Il bilancio deve contenere almeno una voce.");

        var pianoConti = voci.Select(v => new ContoBilancio(v.Codice, v.Descrizione, v.Voce)).ToList();
        var saldi = voci.Select(v => new SaldoConto(v.Codice, v.Importo)).ToList();

        var bilancio = RiclassificazioneBilancio.Riclassifica(pianoConti, saldi);
        var ora = DateTime.UtcNow;

        var entity = new BilancioEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Anno = anno,
            ValoreDellaProduzione = bilancio.ValoreDellaProduzione,
            CostiDellaProduzione = bilancio.CostiDellaProduzione,
            ProventiFinanziari = bilancio.ProventiFinanziari,
            OneriFinanziari = bilancio.OneriFinanziari,
            Imposte = bilancio.Imposte,
            UtilePerditaEsercizio = bilancio.UtilePerditaEsercizio,
            TotaleAttivo = bilancio.TotaleAttivo,
            TotalePassivo = bilancio.TotalePassivo,
            PatrimonioNetto = bilancio.PatrimonioNetto,
            SbilancioAttivoPassivo = bilancio.SbilancioAttivoPassivo,
            Quadra = bilancio.Quadra,
            Stato = StatoBilancio.Bozza.ToString(),
            BilancioJson = JsonSerializer.Serialize(bilancio),
            Bozza = StampaBilancio.Testo(bilancio, $"Bilancio {anno}"),
            CreataIl = ora,
            UltimoAggiornamento = ora,
            Righe = voci.Select(v => new RigaBilancioEntity
            {
                Id = Guid.NewGuid(),
                Codice = v.Codice,
                Descrizione = v.Descrizione,
                Voce = v.Voce.ToString(),
                Importo = v.Importo,
            }).ToList(),
        };

        _db.Bilanci.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Crea un bilancio d'esempio quadrato (piano dei conti CEE di base) per l'anno indicato:
    /// utile per inizializzare e dimostrare il modulo su un'azienda (es. SRLS).
    /// </summary>
    public Task<BilancioEntity> CreaDaEsempioAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        var piano = SeedPianoConti.PianoConti().ToDictionary(c => c.Codice);
        var voci = SeedPianoConti.SaldiEsempio()
            .Select(s => new VoceBilancioInput(
                s.CodiceConto, piano[s.CodiceConto].Descrizione, piano[s.CodiceConto].Voce, s.Importo))
            .ToList();
        return CreaAsync(tenantId, aziendaId, anno, voci, ct);
    }

    /// <summary>
    /// Precompila le voci di Conto Economico pescando dalla contabilità già registrata
    /// dell'esercizio: ricavi dalle fatture attive, costi dalle spese e dal costo del personale
    /// dei cedolini. Le voci di Stato Patrimoniale vanno aggiunte a mano: questo è un punto di
    /// partenza (bozza), non un bilancio completo né garantito a quadratura.
    /// </summary>
    public async Task<List<VoceBilancioInput>> PrecompilaDaContabilitaAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        await VerificaAziendaAsync(tenantId, aziendaId, ct);

        decimal ricavi = await _db.Fatture
            .Where(f => f.TenantId == tenantId && f.AziendaId == aziendaId && f.Anno == anno)
            .SumAsync(f => (decimal?)f.TotaleImponibile, ct) ?? 0m;

        var inizioAnno = new DateOnly(anno, 1, 1);
        var fineAnno = new DateOnly(anno, 12, 31);
        decimal acquisti = await _db.Spese
            .Where(s => s.TenantId == tenantId && s.AziendaId == aziendaId &&
                s.Data >= inizioAnno && s.Data <= fineAnno)
            .SumAsync(s => (decimal?)s.Imponibile, ct) ?? 0m;

        decimal costoPersonale = await _db.Cedolini
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId && c.Anno == anno)
            .SumAsync(c => (decimal?)c.CostoAzienda, ct) ?? 0m;

        var voci = new List<VoceBilancioInput>();
        if (ricavi != 0m)
            voci.Add(new VoceBilancioInput("60.10", "Ricavi delle vendite", MacroVoce.ValoreDellaProduzione, ricavi));
        if (acquisti != 0m)
            voci.Add(new VoceBilancioInput("70.10", "Acquisti e servizi", MacroVoce.CostiDellaProduzione, acquisti));
        if (costoPersonale != 0m)
            voci.Add(new VoceBilancioInput("70.20", "Costi del personale", MacroVoce.CostiDellaProduzione, costoPersonale));
        return voci;
    }

    public Task<List<BilancioEntity>> ElencoAsync(
        Guid tenantId, Guid aziendaId, CancellationToken ct = default) =>
        _db.Bilanci
            .Where(b => b.TenantId == tenantId && b.AziendaId == aziendaId)
            .OrderByDescending(b => b.Anno).ThenByDescending(b => b.CreataIl)
            .ToListAsync(ct);

    public Task<BilancioEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Bilanci
            .Include(b => b.Righe)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, ct);

    /// <summary>Applica una transizione di stato al bilancio, validandola con la macchina a stati.</summary>
    public async Task<BilancioEntity> CambiaStatoAsync(
        Guid tenantId, Guid id, StatoBilancio nuovo, CancellationToken ct = default)
    {
        var b = await _db.Bilanci.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Bilancio non trovato per il tenant indicato.");

        var corrente = Enum.Parse<StatoBilancio>(b.Stato);
        if (!TransizioniBilancio.PuoTransizionare(corrente, nuovo))
            throw new InvalidOperationException($"Transizione non ammessa da {corrente} a {nuovo}.");

        b.Stato = nuovo.ToString();
        b.UltimoAggiornamento = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return b;
    }

    private async Task VerificaAziendaAsync(Guid tenantId, Guid aziendaId, CancellationToken ct)
    {
        bool ok = await _db.Aziende.AnyAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        if (!ok)
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");
    }
}
