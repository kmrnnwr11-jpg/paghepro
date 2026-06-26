using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Core.Fatturazione;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

public sealed record SpesaInput(
    DateOnly Data,
    string Descrizione,
    string ContoCosto,
    decimal Imponibile,
    decimal AliquotaIva = 22m,
    decimal PercentualeDetraibilita = 100m,
    string? Fornitore = null,
    string? NumeroDocumento = null);

public sealed record LiquidazioneIvaInput(
    DateOnly Dal,
    DateOnly Al,
    PeriodicitaIva Periodicita,
    decimal CreditoPeriodoPrecedente = 0m);

/// <summary>Gestisce registro IVA acquisti e liquidazioni IVA persistite.</summary>
public sealed class ContabilitaIvaService
{
    private readonly PagheDbContext _db;

    public ContabilitaIvaService(PagheDbContext db) => _db = db;

    public async Task<SpesaEntity> SalvaSpesaAsync(
        Guid tenantId,
        Guid aziendaId,
        SpesaInput input,
        CancellationToken ct = default)
    {
        await VerificaAziendaAsync(tenantId, aziendaId, ct);
        var spesa = ToDominio(input);
        var (iva, ivaDetraibile, totale) = CalcoloFattura.CalcolaSpesa(spesa);

        var entity = new SpesaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Data = input.Data,
            Descrizione = input.Descrizione,
            ContoCosto = input.ContoCosto,
            Fornitore = input.Fornitore,
            NumeroDocumento = input.NumeroDocumento,
            Imponibile = input.Imponibile,
            AliquotaIva = input.AliquotaIva,
            PercentualeDetraibilita = input.PercentualeDetraibilita,
            Iva = iva,
            IvaDetraibile = ivaDetraibile,
            Totale = totale,
        };

        _db.Spese.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<SpesaEntity>> ElencoSpeseAsync(
        Guid tenantId,
        Guid aziendaId,
        CancellationToken ct = default) =>
        _db.Spese
            .Where(s => s.TenantId == tenantId && s.AziendaId == aziendaId)
            .OrderByDescending(s => s.Data)
            .ToListAsync(ct);

    public Task<SpesaEntity?> GetSpesaAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.Spese.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, ct);

    public async Task<LiquidazioneIvaEntity> CreaLiquidazioneAsync(
        Guid tenantId,
        Guid aziendaId,
        LiquidazioneIvaInput input,
        CancellationToken ct = default)
    {
        await VerificaAziendaAsync(tenantId, aziendaId, ct);

        var fatture = await _db.Fatture
            .AsNoTracking()
            .Include(f => f.Azienda)
            .Include(f => f.Righe)
            .Where(f => f.TenantId == tenantId && f.AziendaId == aziendaId &&
                f.Data >= input.Dal && f.Data <= input.Al)
            .ToListAsync(ct);

        var spese = await _db.Spese
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.AziendaId == aziendaId &&
                s.Data >= input.Dal && s.Data <= input.Al)
            .ToListAsync(ct);

        var liquidazione = CalcoloLiquidazioneIva.Calcola(
            input.Dal,
            input.Al,
            input.Periodicita,
            fatture.Select(FattureService.ToDominio),
            spese.Select(ToDominio),
            input.CreditoPeriodoPrecedente);

        var entity = new LiquidazioneIvaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = aziendaId,
            Dal = input.Dal,
            Al = input.Al,
            Periodicita = input.Periodicita.ToString(),
            IvaVendite = liquidazione.IvaVendite,
            IvaAcquistiDetraibile = liquidazione.IvaAcquistiDetraibile,
            CreditoPeriodoPrecedente = liquidazione.CreditoPeriodoPrecedente,
            Saldo = liquidazione.Saldo,
            IvaDaVersare = liquidazione.IvaDaVersare,
            CreditoDaRiportare = liquidazione.CreditoDaRiportare,
            Righe = liquidazione.Registri.Select(r => new RigaLiquidazioneIvaEntity
            {
                Id = Guid.NewGuid(),
                Data = r.Data,
                Descrizione = r.Descrizione,
                Tipo = r.Tipo,
                Imponibile = r.Imponibile,
                AliquotaIva = r.AliquotaIva,
                Iva = r.Iva,
                IvaDetraibile = r.IvaDetraibile,
            }).ToList(),
        };

        _db.LiquidazioniIva.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<LiquidazioneIvaEntity>> ElencoLiquidazioniAsync(
        Guid tenantId,
        Guid aziendaId,
        CancellationToken ct = default) =>
        _db.LiquidazioniIva
            .Where(l => l.TenantId == tenantId && l.AziendaId == aziendaId)
            .OrderByDescending(l => l.Al)
            .ToListAsync(ct);

    public Task<LiquidazioneIvaEntity?> GetLiquidazioneAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default) =>
        _db.LiquidazioniIva
            .Include(l => l.Righe)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId, ct);

    private async Task VerificaAziendaAsync(Guid tenantId, Guid aziendaId, CancellationToken ct)
    {
        bool ok = await _db.Aziende.AnyAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct);
        if (!ok)
            throw new ArgumentException("Azienda non trovata per il tenant indicato.");
    }

    private static Spesa ToDominio(SpesaInput s) => new()
    {
        Data = s.Data,
        Descrizione = s.Descrizione,
        ContoCosto = s.ContoCosto,
        Fornitore = s.Fornitore,
        Imponibile = s.Imponibile,
        AliquotaIva = s.AliquotaIva,
        PercentualeDetraibilita = s.PercentualeDetraibilita,
    };

    private static Spesa ToDominio(SpesaEntity s) => new()
    {
        Data = s.Data,
        Descrizione = s.Descrizione,
        ContoCosto = s.ContoCosto,
        Fornitore = s.Fornitore,
        Imponibile = s.Imponibile,
        AliquotaIva = s.AliquotaIva,
        PercentualeDetraibilita = s.PercentualeDetraibilita,
    };
}
