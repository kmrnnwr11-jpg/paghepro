using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Gdpr;

public sealed record RichiestaAudit(
    Guid TenantId,
    Guid UtenteId,
    string Azione,
    string Entita,
    string ChiaveEntita,
    string MetodoHttp,
    string PercorsoTemplate,
    string Esito,
    int StatusCode,
    string CorrelationId,
    DateTime QuandoUtc);

public sealed record VoceRegistroAudit(
    Guid Id,
    Guid? UtenteId,
    string Utente,
    string Categoria,
    string Azione,
    string Entita,
    string ChiaveEntita,
    string? CampiModificati,
    string? MetodoHttp,
    string? PercorsoTemplate,
    string? Esito,
    int? StatusCode,
    string? CorrelationId,
    DateTime QuandoUtc);

public sealed record PaginaRegistroAudit(
    IReadOnlyList<VoceRegistroAudit> Voci,
    int Pagina,
    int Dimensione,
    int Totale);

/// <summary>
/// Scrive gli accessi applicativi e consulta il registro. Ogni query applica il tenant ricevuto
/// dal token; l'email dell'autore viene risolta al momento e non è duplicata nel log.
/// </summary>
public sealed class AuditLogService(PagheDbContext db)
{
    public async Task RegistraRichiestaAsync(
        RichiestaAudit richiesta,
        CancellationToken cancellationToken = default)
    {
        if (richiesta.TenantId == Guid.Empty || richiesta.UtenteId == Guid.Empty)
            throw new ArgumentException("Tenant e utente sono obbligatori per auditare un accesso.");

        db.AuditLog.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            TenantId = richiesta.TenantId,
            UtenteId = richiesta.UtenteId,
            Categoria = "Accesso",
            Azione = Limita(richiesta.Azione, 40),
            Entita = Limita(richiesta.Entita, 100),
            ChiaveEntita = Limita(richiesta.ChiaveEntita, 500),
            MetodoHttp = Limita(richiesta.MetodoHttp, 12),
            PercorsoTemplate = Limita(richiesta.PercorsoTemplate, 300),
            Esito = Limita(richiesta.Esito, 40),
            StatusCode = richiesta.StatusCode,
            CorrelationId = Limita(richiesta.CorrelationId, 100),
            QuandoUtc = richiesta.QuandoUtc,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginaRegistroAudit> ElencaAsync(
        Guid tenantId,
        int pagina = 1,
        int dimensione = 50,
        string? categoria = null,
        string? azione = null,
        string? entita = null,
        DateTime? daUtc = null,
        DateTime? aUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant obbligatorio.", nameof(tenantId));

        pagina = Math.Max(1, pagina);
        dimensione = Math.Clamp(dimensione, 1, 200);
        categoria = NormalizzaFiltro(categoria, 40);
        azione = NormalizzaFiltro(azione, 40);
        entita = NormalizzaFiltro(entita, 100);
        if (daUtc is not null) daUtc = NormalizzaUtc(daUtc.Value);
        if (aUtc is not null) aUtc = NormalizzaUtc(aUtc.Value);

        var query = db.AuditLog.AsNoTracking().Where(a => a.TenantId == tenantId);
        if (categoria is not null) query = query.Where(a => a.Categoria == categoria);
        if (azione is not null) query = query.Where(a => a.Azione == azione);
        if (entita is not null) query = query.Where(a => a.Entita == entita);
        if (daUtc is not null) query = query.Where(a => a.QuandoUtc >= daUtc.Value);
        if (aUtc is not null) query = query.Where(a => a.QuandoUtc <= aUtc.Value);

        var totale = await query.CountAsync(cancellationToken);
        var ultimaPagina = Math.Max(1, (int)Math.Ceiling(totale / (double)dimensione));
        pagina = Math.Min(pagina, ultimaPagina);
        var righe = await query
            .OrderByDescending(a => a.QuandoUtc)
            .ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * dimensione)
            .Take(dimensione)
            .ToListAsync(cancellationToken);

        var idsUtente = righe.Where(a => a.UtenteId.HasValue)
            .Select(a => a.UtenteId!.Value)
            .Distinct()
            .ToArray();
        var utenti = await db.Utenti.AsNoTracking()
            .Where(u => u.TenantId == tenantId && idsUtente.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var voci = righe.Select(a => new VoceRegistroAudit(
            a.Id,
            a.UtenteId,
            a.UtenteId is { } id && utenti.TryGetValue(id, out var email)
                ? email
                : a.UtenteId is null ? "Sistema" : $"Utente {a.UtenteId.Value.ToString("N")[..8]}",
            a.Categoria,
            a.Azione,
            a.Entita,
            a.ChiaveEntita,
            a.CampiModificati,
            a.MetodoHttp,
            a.PercorsoTemplate,
            a.Esito,
            a.StatusCode,
            a.CorrelationId,
            NormalizzaUtc(a.QuandoUtc))).ToArray();

        return new PaginaRegistroAudit(voci, pagina, dimensione, totale);
    }

    private static string Limita(string? valore, int massimo) =>
        string.IsNullOrWhiteSpace(valore)
            ? ""
            : valore.Trim() is var pulito && pulito.Length <= massimo
                ? pulito
                : pulito[..massimo];

    private static string? NormalizzaFiltro(string? valore, int massimo) =>
        string.IsNullOrWhiteSpace(valore) ? null : Limita(valore, massimo);

    private static DateTime NormalizzaUtc(DateTime valore) => valore.Kind switch
    {
        DateTimeKind.Utc => valore,
        DateTimeKind.Local => valore.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valore, DateTimeKind.Utc),
    };
}
