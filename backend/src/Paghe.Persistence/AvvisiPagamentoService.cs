using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati per creare un singolo avviso di pagamento cliente.</summary>
public sealed record AvvisoPagamentoInput(
    Guid AziendaId,
    int Anno,
    int Mese,
    decimal Importo,
    DateOnly DataScadenza,
    string? Descrizione = null,
    DateOnly? DataEmissione = null,
    string MetodoPagamento = "Bonifico",
    string? Iban = null,
    string? Causale = null,
    string? Note = null);

/// <summary>Dati per creare lo stesso avviso mensile per tutte le aziende clienti.</summary>
public sealed record AvvisiPagamentoMeseInput(
    int Anno,
    int Mese,
    decimal Importo,
    DateOnly DataScadenza,
    string? Descrizione = null,
    DateOnly? DataEmissione = null,
    string MetodoPagamento = "Bonifico",
    string? Iban = null,
    string? Causale = null,
    string? Note = null);

/// <summary>Dati per cambiare lo stato di un avviso di pagamento.</summary>
public sealed record StatoAvvisoPagamentoInput(
    string Stato,
    DateOnly? PagatoIl = null,
    string? RiferimentoPagamento = null,
    string? Note = null);

/// <summary>
/// Gestione degli avvisi di pagamento verso i clienti dello studio:
/// creazione mensile, solleciti/promemoria e registrazione dell'incasso.
/// </summary>
public sealed class AvvisiPagamentoService
{
    public const string DaPagare = "DaPagare";
    public const string Sollecitato = "Sollecitato";
    public const string Pagato = "Pagato";
    public const string Annullato = "Annullato";

    private static readonly string[] Stati = { DaPagare, Sollecitato, Pagato, Annullato };
    private static readonly CultureInfo CulturaItaliana = CultureInfo.GetCultureInfo("it-IT");

    private readonly PagheDbContext _db;

    public AvvisiPagamentoService(PagheDbContext db) => _db = db;

    public async Task<AvvisoPagamentoEntity> CreaAsync(
        Guid tenantId, AvvisoPagamentoInput input, CancellationToken ct = default)
    {
        ValidaPeriodoImporto(input.Anno, input.Mese, input.Importo);

        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == input.AziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        bool esiste = await _db.AvvisiPagamento.AnyAsync(a =>
            a.TenantId == tenantId &&
            a.AziendaId == input.AziendaId &&
            a.Anno == input.Anno &&
            a.Mese == input.Mese, ct);
        if (esiste)
            throw new ArgumentException("Esiste gia un avviso di pagamento per questo cliente e mese.");

        var entity = CreaEntity(tenantId, azienda, input);
        _db.AvvisiPagamento.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<AvvisoPagamentoEntity>> CreaMensilePerTuttiAsync(
        Guid tenantId, AvvisiPagamentoMeseInput input, CancellationToken ct = default)
    {
        ValidaPeriodoImporto(input.Anno, input.Mese, input.Importo);

        var aziende = await _db.Aziende
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.RagioneSociale)
            .ToListAsync(ct);

        var giaPresenti = await _db.AvvisiPagamento
            .Where(a => a.TenantId == tenantId && a.Anno == input.Anno && a.Mese == input.Mese)
            .Select(a => a.AziendaId)
            .ToListAsync(ct);
        var presenti = giaPresenti.ToHashSet();

        var creati = aziende
            .Where(a => !presenti.Contains(a.Id))
            .Select(a => CreaEntity(tenantId, a, new AvvisoPagamentoInput(
                a.Id,
                input.Anno,
                input.Mese,
                input.Importo,
                input.DataScadenza,
                input.Descrizione,
                input.DataEmissione,
                input.MetodoPagamento,
                input.Iban,
                input.Causale,
                input.Note)))
            .ToList();

        _db.AvvisiPagamento.AddRange(creati);
        await _db.SaveChangesAsync(ct);
        return creati;
    }

    public Task<AvvisoPagamentoEntity?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.AvvisiPagamento
            .Include(a => a.Azienda)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

    public Task<List<AvvisoPagamentoEntity>> ElencoAsync(
        Guid tenantId,
        Guid? aziendaId = null,
        string? stato = null,
        bool soloScaduti = false,
        CancellationToken ct = default)
    {
        var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.AvvisiPagamento
            .Include(a => a.Azienda)
            .Where(a => a.TenantId == tenantId);

        if (aziendaId is not null)
            query = query.Where(a => a.AziendaId == aziendaId);
        if (!string.IsNullOrWhiteSpace(stato))
        {
            var normalizzato = NormalizzaStato(stato);
            query = query.Where(a => a.Stato == normalizzato);
        }
        if (soloScaduti)
            query = query.Where(a => a.Stato != Pagato && a.Stato != Annullato && a.DataScadenza < oggi);

        return query
            .OrderBy(a => a.Stato == Pagato || a.Stato == Annullato)
            .ThenBy(a => a.DataScadenza)
            .ThenBy(a => a.Azienda!.RagioneSociale)
            .ToListAsync(ct);
    }

    public Task<List<AvvisoPagamentoEntity>> PromemoriaDaFareAsync(
        Guid tenantId, int giorniAnticipo = 7, CancellationToken ct = default)
    {
        if (giorniAnticipo < 0)
            throw new ArgumentException("I giorni di anticipo non possono essere negativi.");

        var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
        var entro = oggi.AddDays(giorniAnticipo);

        return _db.AvvisiPagamento
            .Include(a => a.Azienda)
            .Where(a =>
                a.TenantId == tenantId &&
                a.Stato != Pagato &&
                a.Stato != Annullato &&
                a.DataScadenza <= entro)
            .OrderBy(a => a.DataScadenza)
            .ThenBy(a => a.Azienda!.RagioneSociale)
            .ToListAsync(ct);
    }

    public async Task<AvvisoPagamentoEntity> CambiaStatoAsync(
        Guid tenantId, Guid id, StatoAvvisoPagamentoInput input, CancellationToken ct = default)
    {
        var avviso = await _db.AvvisiPagamento
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Avviso di pagamento non trovato per il tenant indicato.");

        var nuovo = NormalizzaStato(input.Stato);
        avviso.Stato = nuovo;
        avviso.Note = string.IsNullOrWhiteSpace(input.Note) ? avviso.Note : input.Note;
        avviso.UltimoAggiornamento = DateTime.UtcNow;

        if (nuovo == Pagato)
        {
            avviso.PagatoIl = input.PagatoIl ?? DateOnly.FromDateTime(DateTime.UtcNow);
            avviso.RiferimentoPagamento = input.RiferimentoPagamento;
        }
        else if (nuovo is DaPagare or Sollecitato)
        {
            avviso.PagatoIl = null;
            avviso.RiferimentoPagamento = null;
        }

        await _db.SaveChangesAsync(ct);
        return avviso;
    }

    public async Task<AvvisoPagamentoEntity> RegistraPromemoriaAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var avviso = await _db.AvvisiPagamento
            .Include(a => a.Azienda)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Avviso di pagamento non trovato per il tenant indicato.");

        if (avviso.Stato is Pagato or Annullato)
            throw new InvalidOperationException("Non si puo inviare un promemoria per un avviso chiuso.");

        avviso.Stato = Sollecitato;
        avviso.NumeroPromemoria += 1;
        avviso.UltimoPromemoriaIl = DateTime.UtcNow;
        avviso.UltimoAggiornamento = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return avviso;
    }

    public static string TestoPromemoria(AvvisoPagamentoEntity avviso)
    {
        var cliente = avviso.Azienda?.RagioneSociale ?? "Cliente";
        var periodo = $"{avviso.Mese:00}/{avviso.Anno}";
        var importo = avviso.Importo.ToString("C", CulturaItaliana);
        var scadenza = avviso.DataScadenza.ToString("dd/MM/yyyy", CulturaItaliana);
        var iban = string.IsNullOrWhiteSpace(avviso.Iban) ? "" : $"\nIBAN: {avviso.Iban}";
        var causale = string.IsNullOrWhiteSpace(avviso.Causale) ? "" : $"\nCausale: {avviso.Causale}";

        return $"""
            Gentile {cliente},

            le ricordiamo che risulta da pagare l'avviso "{avviso.Descrizione}" relativo al mese {periodo}.
            Importo: {importo}
            Scadenza: {scadenza}
            Metodo di pagamento: {avviso.MetodoPagamento}{iban}{causale}

            Se ha gia effettuato il pagamento, puo ignorare questo promemoria e inviare la ricevuta allo studio.
            """;
    }

    public static string TestoAvviso(AvvisoPagamentoEntity avviso)
    {
        var cliente = avviso.Azienda?.RagioneSociale ?? "Cliente";
        var periodo = $"{avviso.Mese:00}/{avviso.Anno}";
        var importo = avviso.Importo.ToString("C", CulturaItaliana);
        var emissione = avviso.DataEmissione.ToString("dd/MM/yyyy", CulturaItaliana);
        var scadenza = avviso.DataScadenza.ToString("dd/MM/yyyy", CulturaItaliana);
        var iban = string.IsNullOrWhiteSpace(avviso.Iban) ? "" : $"\nIBAN: {avviso.Iban}";
        var causale = string.IsNullOrWhiteSpace(avviso.Causale) ? "" : $"\nCausale: {avviso.Causale}";

        return $"""
            Avviso di pagamento

            Cliente: {cliente}
            Data emissione: {emissione}
            Periodo di riferimento: {periodo}

            Oggetto: {avviso.Descrizione}
            Importo da pagare: {importo}
            Scadenza pagamento: {scadenza}
            Metodo di pagamento: {avviso.MetodoPagamento}{iban}{causale}

            Procedura:
            1. Effettuare il pagamento entro la scadenza indicata.
            2. Conservare la ricevuta del versamento.
            3. Inviare la ricevuta allo studio per la registrazione dell'incasso.
            """;
    }

    private static AvvisoPagamentoEntity CreaEntity(
        Guid tenantId, AziendaEntity azienda, AvvisoPagamentoInput input)
    {
        var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
        var emissione = input.DataEmissione ?? oggi;
        if (input.DataScadenza < emissione)
            throw new ArgumentException("La scadenza non puo precedere la data di emissione.");

        var descrizione = string.IsNullOrWhiteSpace(input.Descrizione)
            ? $"Compenso commercialista {input.Mese:00}/{input.Anno}"
            : input.Descrizione.Trim();

        return new AvvisoPagamentoEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AziendaId = azienda.Id,
            Azienda = azienda,
            Anno = input.Anno,
            Mese = input.Mese,
            Descrizione = descrizione,
            Importo = decimal.Round(input.Importo, 2),
            DataEmissione = emissione,
            DataScadenza = input.DataScadenza,
            Stato = DaPagare,
            MetodoPagamento = string.IsNullOrWhiteSpace(input.MetodoPagamento) ? "Bonifico" : input.MetodoPagamento.Trim(),
            Iban = string.IsNullOrWhiteSpace(input.Iban) ? null : input.Iban.Trim(),
            Causale = string.IsNullOrWhiteSpace(input.Causale)
                ? $"{descrizione} - {azienda.RagioneSociale}"
                : input.Causale.Trim(),
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            CreataIl = DateTime.UtcNow,
            UltimoAggiornamento = DateTime.UtcNow,
        };
    }

    private static void ValidaPeriodoImporto(int anno, int mese, decimal importo)
    {
        if (anno < 2000 || anno > 2100)
            throw new ArgumentException("Anno non valido.");
        if (mese is < 1 or > 12)
            throw new ArgumentException("Mese non valido.");
        if (importo <= 0)
            throw new ArgumentException("L'importo deve essere maggiore di zero.");
    }

    private static string NormalizzaStato(string stato)
    {
        var pulito = (stato ?? "").Trim().Replace(" ", "").Replace("_", "").Replace("-", "");
        var match = Stati.FirstOrDefault(s => string.Equals(s, pulito, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ArgumentException($"Stato avviso pagamento non valido: {stato}.");
    }
}
