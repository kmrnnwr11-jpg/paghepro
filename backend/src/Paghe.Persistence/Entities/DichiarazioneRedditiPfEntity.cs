namespace Paghe.Persistence.Entities;

/// <summary>
/// Dati manuali della dichiarazione Redditi PF / 730 per un rapporto e un anno: i redditi e gli
/// oneri che il software non ricava dai cedolini (altri redditi, oneri deducibili/detraibili, acconti).
/// Il reddito da lavoro dipendente e le ritenute operate sono invece aggregati dai cedolini.
/// </summary>
public class DichiarazioneRedditiPfEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RapportoLavoroId { get; set; }
    public int Anno { get; set; }

    /// <summary>Altri redditi imponibili oltre al lavoro dipendente (es. fabbricati, autonomo occasionale).</summary>
    public decimal AltriRedditi { get; set; }

    /// <summary>Oneri deducibili dal reddito complessivo (es. contributi previdenziali, assegni).</summary>
    public decimal OneriDeducibili { get; set; }

    /// <summary>Oneri su cui spetta la detrazione del 19% (es. spese mediche oltre franchigia, interessi mutuo).</summary>
    public decimal OneriDetraibili19 { get; set; }

    /// <summary>Acconti IRPEF già versati nell'anno.</summary>
    public decimal Acconti { get; set; }

    public DateTimeOffset AggiornataIl { get; set; }
}
