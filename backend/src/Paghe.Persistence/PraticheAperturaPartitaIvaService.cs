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
    Guid? AziendaId = null,
    TipoPraticaComUnica TipoPratica = TipoPraticaComUnica.NuovaImpresa,
    bool ImpresaAgricola = false);

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
            TipoPratica = input.TipoPratica.ToString(),
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
            ImpresaAgricola = input.ImpresaAgricola,
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

    /// <summary>Applica una transizione di stato alla pratica, validandola con la macchina a stati.</summary>
    public async Task<PraticaAperturaPartitaIvaEntity> CambiaStatoAsync(
        Guid tenantId, Guid id, string nuovoStato, CancellationToken ct = default)
    {
        var pratica = await GetAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Pratica non trovata.");

        if (!Enum.TryParse<StatoPraticaComUnica>(nuovoStato, ignoreCase: true, out var nuovo))
            throw new ArgumentException($"Stato non riconosciuto: {nuovoStato}.");

        var corrente = ParseStatoPratica(pratica.Stato);
        if (!StatiPraticaComUnica.PuoTransizionare(corrente, nuovo))
            throw new InvalidOperationException($"Transizione non ammessa da {corrente} a {nuovo}.");

        pratica.Stato = nuovo.ToString();
        await _db.SaveChangesAsync(ct);
        return pratica;
    }

    /// <summary>Genera la distinta di riepilogo della pratica con gli stati adempimento archiviati.</summary>
    public async Task<string> DistintaAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await GetAsync(tenantId, id, ct)
            ?? throw new ArgumentException("Pratica non trovata.");

        var dominio = ToDominio(entity);
        var piano = new PianoAperturaPartitaIva
        {
            ModelloAgenziaEntrate = entity.ModelloAgenziaEntrate,
            RichiedeComunicazioneUnica = entity.RichiedeComunicazioneUnica,
            RichiedeRegistroImprese = entity.RichiedeRegistroImprese,
            RichiedeCameraCommercio = entity.RichiedeCameraCommercio,
            Adempimenti = entity.Adempimenti.Select(a => new AdempimentoAperturaPartitaIva(
                a.Codice, a.Ente, a.Descrizione, a.Obbligatorio, ParseStatoAdempimento(a.Stato))).ToList(),
            Avvertenze = PianoAperturaPartitaIvaBuilder.Crea(dominio).Avvertenze,
        };

        return DistintaComUnica.Genera(dominio, piano, entity.Id, entity.Stato, entity.CreataIl);
    }

    public static PianoAperturaPartitaIva Simula(PraticaAperturaPartitaIvaInput input) =>
        PianoAperturaPartitaIvaBuilder.Crea(ToDominio(input));

    private static StatoPraticaComUnica ParseStatoPratica(string stato) =>
        Enum.TryParse<StatoPraticaComUnica>(stato, out var s) ? s : StatoPraticaComUnica.Bozza;

    private static StatoAdempimentoPratica ParseStatoAdempimento(string stato) =>
        Enum.TryParse<StatoAdempimentoPratica>(stato, out var s) ? s : StatoAdempimentoPratica.DaPreparare;

    private static PraticaAperturaPartitaIva ToDominio(PraticaAperturaPartitaIvaInput i) => new()
    {
        TipoPratica = i.TipoPratica,
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
        ImpresaAgricola = i.ImpresaAgricola,
        HaDipendentiPrevisti = i.HaDipendentiPrevisti,
        SoggettaInail = i.SoggettaInail,
    };

    // Ricostruisce il dominio da una pratica archiviata (enum tolleranti su valori storici).
    private static PraticaAperturaPartitaIva ToDominio(PraticaAperturaPartitaIvaEntity e) => new()
    {
        TipoPratica = Enum.TryParse<TipoPraticaComUnica>(e.TipoPratica, out var tipo)
            ? tipo : TipoPraticaComUnica.NuovaImpresa,
        FormaGiuridica = Enum.TryParse<FormaGiuridicaApertura>(e.FormaGiuridica, out var forma)
            ? forma : FormaGiuridicaApertura.ImpresaIndividuale,
        RegimeContabile = Enum.TryParse<RegimeContabilePartitaIva>(e.RegimeContabile, out var regime)
            ? regime : RegimeContabilePartitaIva.Ordinario,
        DataInizioAttivita = e.DataInizioAttivita,
        Denominazione = e.Denominazione,
        CodiceFiscale = e.CodiceFiscale,
        PartitaIva = e.PartitaIva,
        CodiceAteco = e.CodiceAteco,
        DescrizioneAttivita = e.DescrizioneAttivita,
        IndirizzoSede = e.IndirizzoSede,
        Cap = e.Cap,
        Comune = e.Comune,
        Provincia = e.Provincia,
        Pec = e.Pec,
        AttivitaCommercio = e.AttivitaCommercio,
        AttivitaArtigiana = e.AttivitaArtigiana,
        ImpresaAgricola = e.ImpresaAgricola,
        HaDipendentiPrevisti = e.HaDipendentiPrevisti,
        SoggettaInail = e.SoggettaInail,
    };
}
