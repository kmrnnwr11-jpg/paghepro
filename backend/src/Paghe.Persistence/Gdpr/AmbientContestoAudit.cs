namespace Paghe.Persistence.Gdpr;

/// <summary>Chi sta operando nella richiesta corrente (per l'audit log).</summary>
public readonly record struct ContestoAudit(Guid? TenantId, Guid? UtenteId);

/// <summary>
/// Contesto "ambientale" dell'utente corrente, propagato via <see cref="AsyncLocal{T}"/> per la
/// durata di una richiesta. Lo imposta lo strato API (dai claim del token); lo legge
/// l'<see cref="AuditSaveChangesInterceptor"/> per sapere <b>chi</b> ha fatto l'operazione.
/// È disaccoppiato dal DbContext: i servizi di persistenza non cambiano firma.
/// </summary>
public static class AmbientContestoAudit
{
    private static readonly AsyncLocal<ContestoAudit> _corrente = new();

    public static ContestoAudit Corrente => _corrente.Value;

    /// <summary>Imposta il contesto per lo scope corrente; il <c>Dispose</c> ripristina il precedente.</summary>
    public static IDisposable Imposta(Guid? tenantId, Guid? utenteId)
    {
        var precedente = _corrente.Value;
        _corrente.Value = new ContestoAudit(tenantId, utenteId);
        return new Ripristino(precedente);
    }

    private sealed class Ripristino(ContestoAudit precedente) : IDisposable
    {
        public void Dispose() => _corrente.Value = precedente;
    }
}
