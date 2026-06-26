using Microsoft.EntityFrameworkCore;
using Paghe.Core.PartiteIva;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

public sealed record PraticaAperturaPartitaIvaInput(
    FormaGiuridicaApertura FormaGiuridica,
    RegimeContabilePartitaIva RegimeContabile,
    DateOnly DataInizioAttivita,
    string Denominazione,
    string CodiceFiscale,
    string CodiceAteco,
    string DescrizioneAttivita,
    string IndirizzoSede,
    string Cap,
    string Comune,
    string Provincia,
    string? PartitaIva = null,
    string? Pec = null,
    bool AttivitaCommercio = false,
    bool AttivitaArtigiana = false,
    bool HaDipendentiPrevisti = false,
    bool SoggettaInail = false,
    Guid? AziendaId = null);

/// <summary>Gestisce pratiche di apertura P.IVA/ComUnica isolate per tenant.</summary>
public sealed class PraticheAperturaPartitaIvaService
{
    private readonly PagheDbContext _db;

    public PraticheAperturaPartitaIvaService(PagheDbContext db) => _db = db;

    public async Task<PraticaAperturaPartitaIvaEntity> CreaAsync(
        Guid tenantId,
        PraticaAperturaPartitaIvaInput input,
        CancellationToken ct = default)
    {
        if (input.AziendaId.HasValue)
        {
            bool aziendaOk = await _db.Aziende
                .AnyAsync(a => a.Id == input.AziendaId.Value && a.TenantId == tenantId, ct);
            if (!aziendaOk)
                throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        }

        var pratica = ToDominio(input);
        var piano = PianoAperturaPartitaIvaBuilder.Crea(pratica);

        var entity = new PraticaAperturaPartitaIvaEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = input.AziendaId,
            FormaGiuridica = input.FormaGiuridica.ToString(),
            RegimeContabile = input.RegimeContabile.ToString(),
            DataInizioAttivita = input.DataInizioAttivita,
            Denominazione = input.Denominazione,
            CodiceFiscale = input.CodiceFiscale,
            PartitaIva = input.PartitaIva,
            CodiceAteco = input.CodiceAteco,
            DescrizioneAttivita = input.DescrizioneAttivita,
            IndirizzoSede = input.IndirizzoSede,
            Cap = input.Cap,
            Comune = input.Comune,
            Provincia = input.Provincia,
            Pec = input.Pec,
            AttivitaCommercio = input.AttivitaCommercio,
            AttivitaArtigiana = input.AttivitaArtigiana,
            HaDipendentiPrevisti = input.HaDipendentiPrevisti,
            SoggettaInail = input.SoggettaInail,
            ModelloAgenziaEntrate = piano.ModelloAgenziaEntrate,
            RichiedeComunicazioneUnica = piano.RichiedeComunicazioneUnica,
            RichiedeRegistroImprese = piano.RichiedeRegistroImprese,
            RichiedeCameraCommercio = piano.RichiedeCameraCommercio,
            Adempimenti = piano.Adempimenti.Select(a => new AdempimentoPraticaEntity
            {
                Id = Guid.NewGuid(),
                Codice = a.Codice,
                Ente = a.Ente,
                Descrizione = a.Descrizione,
                Obbligatorio = a.Obbligatorio,
                Stato = a.Stato.ToString(),
            }).ToList(),
        };

        _db.PraticheAperturaPartitaIva.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<List<PraticaAperturaPartitaIvaEntity>> ElencoAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.PraticheAperturaPartitaIva
            .Include(p => p.Adempimenti)
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreataIl)
            .ToListAsync(ct);

    public Task<PraticaAperturaPartitaIvaEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.PraticheAperturaPartitaIva
            .Include(p => p.Adempimenti)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

    public async Task<AdempimentoPraticaEntity> AggiornaAdempimentoAsync(
        Guid tenantId,
        Guid praticaId,
        Guid adempimentoId,
        string stato,
        string? note = null,
        CancellationToken ct = default)
    {
        var pratica = await _db.PraticheAperturaPartitaIva
            .Include(p => p.Adempimenti)
            .FirstOrDefaultAsync(p => p.Id == praticaId && p.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Pratica non trovata.");

        var adempimento = pratica.Adempimenti.FirstOrDefault(a => a.Id == adempimentoId)
            ?? throw new ArgumentException("Adempimento non trovato.");

        adempimento.Stato = stato;
        adempimento.Note = note;
        await _db.SaveChangesAsync(ct);
        return adempimento;
    }

    public static PianoAperturaPartitaIva Simula(PraticaAperturaPartitaIvaInput input) =>
        PianoAperturaPartitaIvaBuilder.Crea(ToDominio(input));

    private static PraticaAperturaPartitaIva ToDominio(PraticaAperturaPartitaIvaInput i) => new()
    {
        FormaGiuridica = i.FormaGiuridica,
        RegimeContabile = i.RegimeContabile,
        DataInizioAttivita = i.DataInizioAttivita,
        Denominazione = i.Denominazione,
        CodiceFiscale = i.CodiceFiscale,
        PartitaIva = i.PartitaIva,
        CodiceAteco = i.CodiceAteco,
        DescrizioneAttivita = i.DescrizioneAttivita,
        IndirizzoSede = i.IndirizzoSede,
        Cap = i.Cap,
        Comune = i.Comune,
        Provincia = i.Provincia,
        Pec = i.Pec,
        AttivitaCommercio = i.AttivitaCommercio,
        AttivitaArtigiana = i.AttivitaArtigiana,
        HaDipendentiPrevisti = i.HaDipendentiPrevisti,
        SoggettaInail = i.SoggettaInail,
    };
}
