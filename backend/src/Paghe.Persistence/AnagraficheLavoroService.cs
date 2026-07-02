using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Gestione di dipendenti e rapporti di lavoro, isolata per tenant.</summary>
public sealed class AnagraficheLavoroService
{
    private readonly PagheDbContext _db;

    public AnagraficheLavoroService(PagheDbContext db) => _db = db;

    /// <summary>Crea un dipendente e il relativo rapporto di lavoro per un'azienda.</summary>
    public async Task<RapportoLavoroEntity> CreaDipendenteERapportoAsync(
        Guid tenantId, Guid aziendaId, DipendenteEntity dipendente, RapportoLavoroEntity rapporto,
        CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        dipendente.Id = dipendente.Id == Guid.Empty ? Guid.NewGuid() : dipendente.Id;
        dipendente.TenantId = tenantId;

        rapporto.Id = Guid.NewGuid();
        rapporto.TenantId = tenantId;
        rapporto.AziendaId = aziendaId;
        rapporto.DipendenteId = dipendente.Id;

        _db.Dipendenti.Add(dipendente);
        _db.Rapporti.Add(rapporto);
        await _db.SaveChangesAsync(ct);
        return rapporto;
    }

    public Task<List<RapportoLavoroEntity>> ElencoRapportiAsync(
        Guid tenantId, Guid aziendaId, CancellationToken ct = default) =>
        _db.Rapporti.Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);

    public Task<RapportoLavoroEntity?> GetRapportoAsync(
        Guid tenantId, Guid rapportoId, CancellationToken ct = default) =>
        _db.Rapporti.Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);

    /// <summary>Campi correggibili di dipendente + rapporto (es. dopo un import UNILAV/OCR impreciso). Null = non cambiare.</summary>
    public sealed record CorrezioneRapporto(
        string? Nome = null, string? Cognome = null, string? CodiceFiscale = null, string? Iban = null,
        string? CodiceCcnl = null, string? Livello = null, string? Qualifica = null,
        int? NumeroMensilita = null, bool? PartTime = null, decimal? PercentualePartTime = null,
        int? ScattiAnzianita = null, decimal? Superminimo = null,
        string? Regione = null, string? CodiceCatastaleComune = null, DateOnly? DataAssunzione = null);

    /// <summary>
    /// Corregge i dati di un dipendente/rapporto (isolato per tenant). Pensato per rivedere ciò che
    /// l'import automatico (UNILAV, OCR) ha letto male. Restituisce il rapporto aggiornato,
    /// null se non esiste per il tenant. Un dipendente anonimizzato (GDPR) non è modificabile.
    /// </summary>
    public async Task<RapportoLavoroEntity?> CorreggiRapportoAsync(
        Guid tenantId, Guid rapportoId, CorrezioneRapporto c, CancellationToken ct = default)
    {
        var r = await _db.Rapporti.Include(x => x.Dipendente)
            .FirstOrDefaultAsync(x => x.Id == rapportoId && x.TenantId == tenantId, ct);
        if (r is null) return null;
        if (r.Dipendente is { Anonimizzato: true })
            throw new InvalidOperationException("Dipendente anonimizzato (GDPR): non è modificabile.");

        var d = r.Dipendente!;
        if (c.Nome is not null) d.Nome = c.Nome.Trim();
        if (c.Cognome is not null) d.Cognome = c.Cognome.Trim();
        if (c.CodiceFiscale is not null) d.CodiceFiscale = c.CodiceFiscale.Trim().ToUpperInvariant();
        if (c.Iban is not null) d.Iban = c.Iban.Trim().Length == 0 ? null : c.Iban.Trim();

        if (c.CodiceCcnl is not null) r.CodiceCcnl = c.CodiceCcnl.Trim();
        if (c.Livello is not null) r.Livello = c.Livello.Trim();
        if (c.Qualifica is not null) r.Qualifica = c.Qualifica.Trim();
        if (c.NumeroMensilita is { } nm) r.NumeroMensilita = nm;
        if (c.PartTime is { } pt) r.PartTime = pt;
        if (c.PercentualePartTime is { } ppt) r.PercentualePartTime = ppt;
        if (c.ScattiAnzianita is { } sa) r.ScattiAnzianita = sa;
        if (c.Superminimo is { } sm) r.Superminimo = sm;
        if (c.Regione is not null) r.Regione = c.Regione.Trim();
        if (c.CodiceCatastaleComune is not null) r.CodiceCatastaleComune = c.CodiceCatastaleComune.Trim().ToUpperInvariant();
        if (c.DataAssunzione is { } da) r.DataAssunzione = da;

        await _db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>
    /// GDPR — diritto all'oblio (art. 17): anonimizza i dati personali del dipendente (nome, cognome,
    /// codice fiscale, IBAN) <b>mantenendo</b> i record contabili collegati (cedolini), soggetti a
    /// conservazione fiscale obbligatoria. È idempotente e isolato per tenant; l'audit log registra
    /// automaticamente l'operazione. Restituisce false se il dipendente non esiste.
    /// </summary>
    public async Task<bool> AnonimizzaDipendenteAsync(
        Guid tenantId, Guid dipendenteId, CancellationToken ct = default)
    {
        var dip = await _db.Dipendenti
            .FirstOrDefaultAsync(d => d.Id == dipendenteId && d.TenantId == tenantId, ct);
        if (dip is null) return false;
        if (dip.Anonimizzato) return true; // già anonimizzato: idempotente

        dip.Nome = "(anonimizzato)";
        dip.Cognome = "(anonimizzato)";
        dip.CodiceFiscale = $"ANON-{Guid.NewGuid():N}"; // resta univoco per (tenant, codice fiscale)
        dip.Iban = null;
        dip.Anonimizzato = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
