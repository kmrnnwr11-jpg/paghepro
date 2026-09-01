namespace Paghe.Persistence.Entities;

/// <summary>
/// Preferenza d'uso di un utente (stile busta, font dei documenti, aspetto dell'interfaccia).
/// Coppie chiave→valore con whitelist applicata dal servizio: non è un deposito libero di
/// dati, e non contiene mai dati di dominio (quelli stanno sulle entità vere).
/// </summary>
public sealed class PreferenzaUtenteEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }
    public string Chiave { get; set; } = "";
    public string Valore { get; set; } = "";
    public DateTime AggiornatoIl { get; set; }
}
