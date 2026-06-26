using Microsoft.EntityFrameworkCore;
using Paghe.Core.Fatturazione;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati di input per la creazione di una fattura (il cedente è l'azienda).</summary>
public sealed record FatturaInput(
    DateOnly Data,
    DatiAnagrafici Cessionario,
    IReadOnlyList<RigaFattura> Righe,
    string? Numero = null,
    string TipoDocumento = "TD01",
    string Divisa = "EUR",
    string CodiceDestinatario = "0000000");

/// <summary>
/// Servizio applicativo per il salvataggio e la lettura delle fatture.
/// Assegna automaticamente il numero progressivo per azienda e anno, calcola i totali
/// e garantisce l'isolamento per tenant.
/// </summary>
public sealed class FattureService
{
    private readonly PagheDbContext _db;

    public FattureService(PagheDbContext db) => _db = db;

    /// <summary>Salva una fattura per l'azienda indicata, numerandola se necessario.</summary>
    public async Task<FatturaEntity> SalvaAsync(
        Guid tenantId, Guid aziendaId, FatturaInput input, CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        if (input.Righe.Count == 0)
            throw new ArgumentException("La fattura deve avere almeno una riga.");

        int anno = input.Data.Year;
        string numero = string.IsNullOrWhiteSpace(input.Numero)
            ? await ProssimoNumeroAsync(tenantId, aziendaId, anno, ct)
            : input.Numero!;

        // Calcolo dei totali riusando il motore di fatturazione.
        var fatturaDominio = new Fattura
        {
            Numero = numero,
            Data = input.Data,
            TipoDocumento = input.TipoDocumento,
            Divisa = input.Divisa,
            Cedente = AziendaToAnagrafica(azienda),
            Cessionario = input.Cessionario,
            CodiceDestinatario = input.CodiceDestinatario,
            Righe = input.Righe.ToList(),
        };
        var totali = CalcoloFattura.Calcola(fatturaDominio);

        var entity = new FatturaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Numero = numero,
            Anno = anno,
            Data = input.Data,
            TipoDocumento = input.TipoDocumento,
            Divisa = input.Divisa,
            CessionarioDenominazione = input.Cessionario.Denominazione,
            CessionarioPartitaIva = input.Cessionario.PartitaIva,
            CessionarioCodiceFiscale = input.Cessionario.CodiceFiscale,
            CessionarioIndirizzo = input.Cessionario.Indirizzo,
            CessionarioCap = input.Cessionario.Cap,
            CessionarioComune = input.Cessionario.Comune,
            CessionarioProvincia = input.Cessionario.Provincia,
            CessionarioNazione = input.Cessionario.Nazione,
            CodiceDestinatario = input.CodiceDestinatario,
            TotaleImponibile = totali.TotaleImponibile,
            TotaleImposta = totali.TotaleImposta,
            TotaleDocumento = totali.TotaleDocumento,
            Righe = input.Righe.Select(r => new RigaFatturaEntity
            {
                Id = Guid.NewGuid(),
                Descrizione = r.Descrizione,
                Quantita = r.Quantita,
                PrezzoUnitario = r.PrezzoUnitario,
                AliquotaIva = r.AliquotaIva,
                NaturaIva = r.NaturaIva,
            }).ToList(),
        };

        _db.Fatture.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Prossimo numero progressivo nel formato "N/anno" per azienda e anno.
    /// (MVP: basato sul conteggio; in produzione usare una sequenza/lock per la concorrenza.)
    /// </summary>
    public async Task<string> ProssimoNumeroAsync(
        Guid tenantId, Guid aziendaId, int anno, CancellationToken ct = default)
    {
        int progressivo = await _db.Fatture
            .CountAsync(f => f.TenantId == tenantId && f.AziendaId == aziendaId && f.Anno == anno, ct);
        return $"{progressivo + 1}/{anno}";
    }

    public Task<FatturaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Fatture.Include(f => f.Righe)
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ct);

    /// <summary>Ricostruisce la fattura di dominio (con cedente = azienda) per la stampa PDF.</summary>
    public async Task<Fattura?> GetDominioAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.Fatture.Include(f => f.Azienda).Include(f => f.Righe)
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ct);
        if (e is null) return null;

        return ToDominio(e);
    }

    public Task<List<FatturaEntity>> ElencoAsync(
        Guid tenantId, Guid aziendaId, CancellationToken ct = default) =>
        _db.Fatture
            .Where(f => f.TenantId == tenantId && f.AziendaId == aziendaId)
            .OrderBy(f => f.Anno).ThenBy(f => f.Numero)
            .ToListAsync(ct);

    public static Fattura ToDominio(FatturaEntity e) => new()
    {
        Numero = e.Numero,
        Data = e.Data,
        TipoDocumento = e.TipoDocumento,
        Divisa = e.Divisa,
        Cedente = e.Azienda is not null
            ? AziendaToAnagrafica(e.Azienda)
            : new DatiAnagrafici { Denominazione = "", Indirizzo = "", Cap = "", Comune = "", Provincia = "" },
        Cessionario = new DatiAnagrafici
        {
            Denominazione = e.CessionarioDenominazione,
            PartitaIva = e.CessionarioPartitaIva,
            CodiceFiscale = e.CessionarioCodiceFiscale,
            Indirizzo = e.CessionarioIndirizzo,
            Cap = e.CessionarioCap,
            Comune = e.CessionarioComune,
            Provincia = e.CessionarioProvincia,
            Nazione = e.CessionarioNazione,
        },
        CodiceDestinatario = e.CodiceDestinatario,
        Righe = e.Righe.Select(r => new RigaFattura
        {
            Descrizione = r.Descrizione,
            Quantita = r.Quantita,
            PrezzoUnitario = r.PrezzoUnitario,
            AliquotaIva = r.AliquotaIva,
            NaturaIva = r.NaturaIva,
        }).ToList(),
    };

    private static DatiAnagrafici AziendaToAnagrafica(AziendaEntity a) => new()
    {
        Denominazione = a.RagioneSociale,
        PartitaIva = a.PartitaIva,
        CodiceFiscale = a.CodiceFiscale,
        Indirizzo = a.Indirizzo,
        Cap = a.Cap,
        Comune = a.Comune,
        Provincia = a.Provincia,
        Nazione = a.Nazione,
        RegimeFiscale = a.RegimeFiscale,
    };
}
