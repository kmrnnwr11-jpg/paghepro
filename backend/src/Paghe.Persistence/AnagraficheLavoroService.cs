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

        // Regola anti-doppione: lo stesso codice fiscale non può risultare assunto due volte
        // nella stessa azienda (un doppio rapporto produrrebbe una doppia busta paga per sbaglio).
        // Se la persona esiste già nell'archivio (stesso CF nel tenant) si riusa la sua
        // anagrafica e si crea solo il nuovo rapporto: può lavorare per due clienti dello
        // studio, e ogni busta paga nasce dal rapporto della propria azienda di assunzione.
        string cf = dipendente.CodiceFiscale?.Trim().ToUpperInvariant() ?? "";
        dipendente.CodiceFiscale = cf;
        var anagrafica = dipendente;
        if (cf.Length > 0)
        {
            var esistente = await _db.Dipendenti
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.CodiceFiscale == cf, ct);
            if (esistente is not null)
            {
                bool giaAssuntoQui = await _db.Rapporti.AnyAsync(r =>
                    r.TenantId == tenantId && r.AziendaId == aziendaId
                    && r.DipendenteId == esistente.Id, ct);
                if (giaAssuntoQui)
                    throw new ArgumentException(
                        $"Il codice fiscale {cf} risulta già assunto in «{azienda.RagioneSociale}»: "
                        + "non creo un doppione. Per correggere il rapporto esistente usare la scheda dipendente.");
                anagrafica = esistente;
            }
        }

        if (anagrafica == dipendente)
        {
            anagrafica.Id = anagrafica.Id == Guid.Empty ? Guid.NewGuid() : anagrafica.Id;
            anagrafica.TenantId = tenantId;
            _db.Dipendenti.Add(anagrafica);
        }

        rapporto.Id = Guid.NewGuid();
        rapporto.TenantId = tenantId;
        rapporto.AziendaId = aziendaId;
        rapporto.DipendenteId = anagrafica.Id;

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
        string? Regione = null, string? CodiceCatastaleComune = null, DateOnly? DataAssunzione = null,
        decimal? FondoTfrIniziale = null,
        decimal? FerieResidueIniziali = null, decimal? PermessiResiduiIniziali = null,
        DateOnly? DataCessazione = null, bool? RimuoviDataCessazione = null);

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
        if (c.FondoTfrIniziale is { } ft) r.FondoTfrIniziale = ft;
        if (c.FerieResidueIniziali is { } ferie) r.FerieResidueIniziali = ferie;
        if (c.PermessiResiduiIniziali is { } permessi) r.PermessiResiduiIniziali = permessi;
        if (c.RimuoviDataCessazione == true) r.DataCessazione = null;
        else if (c.DataCessazione is { } dc) r.DataCessazione = dc;

        if (r.DataCessazione is { } cessazione && r.DataAssunzione != default && cessazione < r.DataAssunzione)
            throw new ArgumentException(
                $"La data di cessazione ({cessazione:dd/MM/yyyy}) non può precedere "
                + $"l'assunzione ({r.DataAssunzione:dd/MM/yyyy}).");

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
