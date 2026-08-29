namespace Paghe.Persistence.Entities;

/// <summary>
/// Consenso pieno ad Aurora, concesso dall'utente alla prima apertura (o dal pannello) e
/// sempre revocabile. Con il consenso attivo la chat emette da sola il mandato di lettura
/// Guidata/S2 sul cliente selezionato, senza passare dal pannello. Non abilita scritture:
/// le modifiche restano sul protocollo A3 (proposta firmata + conferma + undo).
/// </summary>
public class ConsensoAuroraEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UtenteId { get; set; }

    public DateTime ConcessoUtc { get; set; }

    /// <summary>Valorizzato quando l'utente revoca: il consenso non viene mai cancellato.</summary>
    public DateTime? RevocatoUtc { get; set; }
}
