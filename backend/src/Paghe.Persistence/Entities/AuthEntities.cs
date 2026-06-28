namespace Paghe.Persistence.Entities;

/// <summary>Account (tenant) che sottoscrive il SaaS.</summary>
public class TenantEntity
{
    public Guid Id { get; set; }
    public string RagioneSociale { get; set; } = "";
    public DateTime CreatoIl { get; set; }
}

/// <summary>Utente applicativo, appartenente a un tenant.</summary>
public class UtenteEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Ruolo { get; set; } = "admin"; // admin | operatore | sola_lettura
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIl { get; set; }

    /// <summary>Secondo fattore (TOTP) attivo per questo utente.</summary>
    public bool DueFattoriAttivo { get; set; }

    /// <summary>Segreto TOTP in Base32 (cifrato a riposo). Null finché il 2FA non è configurato.</summary>
    public string? SegretoTotp { get; set; }
}
