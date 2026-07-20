namespace Paghe.Persistence.Entities;

/// <summary>
/// Singola credenziale refresh, sempre monouso. <see cref="FamigliaId"/> identifica la sessione
/// stabile riportata nel claim <c>sid</c>; a ogni rotazione nasce una nuova riga della stessa
/// famiglia. Nel database è conservato soltanto l'hash del segreto ad alta entropia.
/// </summary>
public sealed class SessioneRefreshEntity
{
    /// <summary>Selettore casuale pubblico incorporato nel token refresh.</summary>
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public Guid FamigliaId { get; set; }
    public string HashSegreto { get; set; } = "";
    public string Client { get; set; } = "Web";
    public DateTime CreataUtc { get; set; }
    public DateTime ScadeAssolutaUtc { get; set; }
    public DateTime ScadeInattivitaUtc { get; set; }
    public DateTime? UsataUtc { get; set; }
    public DateTime? RevocataUtc { get; set; }
    public string? MotivoRevoca { get; set; }
    public Guid? SostituitaDaId { get; set; }
    public long Versione { get; set; }
}
