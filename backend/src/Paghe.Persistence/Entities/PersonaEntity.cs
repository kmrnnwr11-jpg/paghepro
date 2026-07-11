namespace Paghe.Persistence.Entities;

/// <summary>
/// Persona fisica dell'archivio anagrafico del tenant (rappresentanti legali, soci, titolari):
/// si registra una volta sola e si riusa per compilare in automatico i moduli ufficiali
/// (AA7/10, deleghe, pratiche) senza reinserire i dati.
/// </summary>
public class PersonaEntity
{
    public Guid Id { get; set; }

    /// <summary>Tenant proprietario del dato (isolamento multi-tenant).</summary>
    public Guid TenantId { get; set; }

    public string Cognome { get; set; } = "";
    public string Nome { get; set; } = "";
    public string CodiceFiscale { get; set; } = "";

    /// <summary>"M" o "F".</summary>
    public string? Sesso { get; set; }
    public DateOnly? DataNascita { get; set; }
    public string? ComuneNascita { get; set; }
    public string? ProvinciaNascita { get; set; }

    public string? IndirizzoResidenza { get; set; }
    public string? CapResidenza { get; set; }
    public string? ComuneResidenza { get; set; }
    public string? ProvinciaResidenza { get; set; }

    public string? Telefono { get; set; }
    public string? Email { get; set; }

    public DateTimeOffset CreataIl { get; set; }
}
