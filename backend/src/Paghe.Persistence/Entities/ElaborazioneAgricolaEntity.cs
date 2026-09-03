namespace Paghe.Persistence.Entities;

/// <summary>
/// Stato dell'elaborazione mensile delle buste agricole di un'azienda. Lo stato
/// (bozza/validata) è un attributo DELL'ELABORAZIONE, non della singola busta: tutte le
/// buste del mese stampano in calce lo stesso stato, con data e nominativo di chi ha
/// validato. La validazione è rifiutata finché il validatore segnala ERRORI su una
/// qualunque busta archiviata del periodo, e decade se una busta viene riarchiviata.
/// </summary>
public sealed class ElaborazioneAgricolaEntity
{
    public const string StatoBozza = "Bozza";
    public const string StatoValidata = "Validata";

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }

    public string Stato { get; set; } = StatoBozza;

    /// <summary>Nominativo (ed eventuale qualifica) di chi ha dichiarato la validazione.</summary>
    public string? ValidataDa { get; set; }
    public DateTime? ValidataIl { get; set; }
}
