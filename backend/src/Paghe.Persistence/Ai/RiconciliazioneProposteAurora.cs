using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Paghe.Persistence.Ai;

public sealed record AnomaliaRiconciliazioneAurora(
    string Tipo, Guid? PropostaId, Guid? GrantId, string Dettaglio);

public sealed record EsitoRiconciliazioneAurora(
    int ProposteTotali,
    IReadOnlyDictionary<string, int> PropostePerStato,
    int EventiLifecycle,
    bool Integra,
    IReadOnlyList<AnomaliaRiconciliazioneAurora> Anomalie);

/// <summary>
/// Riconciliazione del piano §12/G5 fra proposta, applicazione e audit: ogni effetto o undo
/// deve avere il proprio evento di lifecycle, nessun evento può restare orfano o contraddire
/// lo stato della proposta, gli stati intermedi non possono sopravvivere a un riavvio e i
/// contatori del mandato devono coincidere con le proposte realmente persistite. È una
/// lettura pura: non ripara nulla, espone le discordanze al professionista.
/// </summary>
public sealed class RiconciliazioneProposteAurora(PagheDbContext db)
{
    private const string PrefissoDettaglio = "proposta=";
    private static readonly string[] EventiLifecycleProposta =
        ["PropostaApplicata", "PropostaAnnullata", "PropostaRipristinata"];
    private static readonly string[] StatiApplicati = ["Applicata", "Ripristinata"];

    public async Task<EsitoRiconciliazioneAurora> VerificaAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        // Fotografia coerente: proposte, eventi e mandati letti nella stessa transazione,
        // così una conferma concorrente non produce false discordanze.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        var proposte = await db.AiProposte.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { p.Id, p.GrantId, p.Stato })
            .ToListAsync(ct);
        var eventi = await db.AiAudit.AsNoTracking()
            .Where(a => a.TenantId == tenantId && EventiLifecycleProposta.Contains(a.Evento))
            .OrderBy(a => a.Sequenza)
            .Select(a => new { a.Sequenza, a.Evento, a.Dettaglio })
            .ToListAsync(ct);
        var grantIds = proposte.Where(p => p.GrantId is not null)
            .Select(p => p.GrantId!.Value).Distinct().ToArray();
        var grants = await db.AiScopeGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId && grantIds.Contains(g.Id))
            .Select(g => new { g.Id, g.ProposteUsate, g.EffettiUsati })
            .ToListAsync(ct);
        await tx.RollbackAsync(CancellationToken.None);

        var anomalie = new List<AnomaliaRiconciliazioneAurora>();
        var statoPerProposta = proposte.ToDictionary(p => p.Id, p => p.Stato);
        var idPerEvento = EventiLifecycleProposta.ToDictionary(
            e => e, _ => new List<Guid>(), StringComparer.Ordinal);

        foreach (var evento in eventi)
        {
            if (!ProvaLeggiPropostaId(evento.Dettaglio, out var propostaId))
            {
                anomalie.Add(new("AuditNonRiconducibile", null, null,
                    $"Evento {evento.Evento} in sequenza {evento.Sequenza} senza riferimento proposta."));
                continue;
            }
            idPerEvento[evento.Evento].Add(propostaId);
            if (!statoPerProposta.TryGetValue(propostaId, out var stato))
            {
                anomalie.Add(new("AuditOrfano", propostaId, null,
                    $"Evento {evento.Evento} riferito a una proposta inesistente."));
                continue;
            }
            bool coerente = evento.Evento switch
            {
                "PropostaApplicata" => StatiApplicati.Contains(stato, StringComparer.Ordinal),
                "PropostaRipristinata" => stato == "Ripristinata",
                "PropostaAnnullata" => stato == "Annullata",
                _ => true,
            };
            if (!coerente)
                anomalie.Add(new("AuditIncoerente", propostaId, null,
                    $"Evento {evento.Evento} ma la proposta risulta {stato}."));
        }

        foreach (var (evento, ids) in idPerEvento)
            foreach (var duplicato in ids.GroupBy(id => id).Where(g => g.Count() > 1))
                anomalie.Add(new("AuditDuplicato", duplicato.Key, null,
                    $"Evento {evento} registrato {duplicato.Count()} volte."));

        var applicate = idPerEvento["PropostaApplicata"].ToHashSet();
        var ripristinate = idPerEvento["PropostaRipristinata"].ToHashSet();
        var annullate = idPerEvento["PropostaAnnullata"].ToHashSet();
        foreach (var proposta in proposte)
        {
            if (proposta.Stato is "InApplicazione" or "InRipristino")
                anomalie.Add(new("StatoIntermedioPersistito", proposta.Id, proposta.GrantId,
                    $"La proposta è rimasta {proposta.Stato}: la transazione non doveva committarlo."));
            if (proposta.GrantId is null)
                anomalie.Add(new("MandatoAssente", proposta.Id, null,
                    "La proposta non è legata ad alcun mandato."));
            if (StatiApplicati.Contains(proposta.Stato, StringComparer.Ordinal)
                && !applicate.Contains(proposta.Id))
                anomalie.Add(new("EffettoSenzaAudit", proposta.Id, proposta.GrantId,
                    "Operazione applicata senza evento PropostaApplicata."));
            if (proposta.Stato == "Ripristinata" && !ripristinate.Contains(proposta.Id))
                anomalie.Add(new("RipristinoSenzaAudit", proposta.Id, proposta.GrantId,
                    "Operazione ripristinata senza evento PropostaRipristinata."));
            if (proposta.Stato == "Annullata" && !annullate.Contains(proposta.Id))
                anomalie.Add(new("AnnulloSenzaAudit", proposta.Id, proposta.GrantId,
                    "Proposta annullata senza evento PropostaAnnullata."));
        }

        var grantPerId = grants.ToDictionary(g => g.Id);
        foreach (var gruppo in proposte.Where(p => p.GrantId is not null)
            .GroupBy(p => p.GrantId!.Value))
        {
            if (!grantPerId.TryGetValue(gruppo.Key, out var grant))
            {
                anomalie.Add(new("MandatoAssente", null, gruppo.Key,
                    $"{gruppo.Count()} proposte riferite a un mandato inesistente nel tenant."));
                continue;
            }
            int attese = gruppo.Count();
            int effetti = gruppo.Count(p =>
                StatiApplicati.Contains(p.Stato, StringComparer.Ordinal));
            if (grant.ProposteUsate != attese)
                anomalie.Add(new("QuotaProposteDiscordante", null, gruppo.Key,
                    $"Il mandato conta {grant.ProposteUsate} proposte, ne esistono {attese}."));
            if (grant.EffettiUsati != effetti)
                anomalie.Add(new("QuotaEffettiDiscordante", null, gruppo.Key,
                    $"Il mandato conta {grant.EffettiUsati} effetti, le proposte applicate sono {effetti}."));
        }

        return new(
            proposte.Count,
            proposte.GroupBy(p => p.Stato)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            eventi.Count,
            anomalie.Count == 0,
            anomalie);
    }

    private static bool ProvaLeggiPropostaId(string dettaglio, out Guid propostaId)
    {
        propostaId = Guid.Empty;
        return dettaglio.StartsWith(PrefissoDettaglio, StringComparison.Ordinal)
            && Guid.TryParseExact(
                dettaglio[PrefissoDettaglio.Length..], "N", out propostaId)
            && propostaId != Guid.Empty;
    }
}
