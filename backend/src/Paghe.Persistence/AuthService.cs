using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Persistence;

/// <summary>Esito di un tentativo di accesso (login), con la distinzione del secondo fattore.</summary>
public enum EsitoAccesso
{
    CredenzialiErrate,
    ServeCodice2fa,
    Codice2faErrato,
    AccountSospeso,
    Ok,
}

/// <summary>Identità minima usata soltanto per attribuire un tentativo di login nel registro.</summary>
public sealed record IdentitaAccessoAudit(Guid TenantId, Guid UtenteId);

/// <summary>
/// Risultato di un accesso. <see cref="Utente"/> è presente esclusivamente con
/// <see cref="EsitoAccesso.Ok"/>; un account riconosciuto ma non autenticato espone soltanto
/// l'identità minima, non riutilizzabile come sessione, in <see cref="IdentitaAudit"/>.
/// </summary>
public sealed record RisultatoAccesso(
    EsitoAccesso Esito,
    UtenteEntity? Utente,
    IdentitaAccessoAudit? IdentitaAudit = null);

/// <summary>Dati per configurare il 2FA: segreto da salvare nell'app e URI per il QR code.</summary>
public sealed record ConfigurazioneDueFattori(string Segreto, string UriOtpauth);

/// <summary>
/// Registrazione e autenticazione degli utenti. La registrazione crea un nuovo tenant
/// (account) con il primo utente amministratore. L'autenticazione verifica le credenziali.
/// </summary>
public sealed class AuthService
{
    private readonly PagheDbContext _db;
    private readonly IConfiguration? _config;

    public AuthService(PagheDbContext db, IConfiguration? config = null)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Ruolo del super-admin di piattaforma (proprietario del SaaS).</summary>
    public const string RuoloSuperAdmin = "superadmin";

    public async Task<UtenteEntity> RegistraAsync(
        string ragioneSociale, string email, string password, CancellationToken ct = default)
    {
        email = Normalizza(email);
        if (string.IsNullOrWhiteSpace(email) || password.Length < 8)
            throw new ArgumentException("Email obbligatoria e password di almeno 8 caratteri.");
        if (await _db.Utenti.AnyAsync(u => u.Email == email, ct))
            throw new ArgumentException("Email già registrata.");

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(), RagioneSociale = ragioneSociale, CreatoIl = DateTime.UtcNow,
        };
        var utente = new UtenteEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Email = email,
            PasswordHash = PasswordHasher.Hash(password), Ruolo = "admin",
            Attivo = true, CreatoIl = DateTime.UtcNow,
        };

        _db.Tenants.Add(tenant);
        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync(ct);
        return utente;
    }

    /// <summary>
    /// Accesso con Google: trovato l'utente per email (verificata da Google nello strato API),
    /// lo restituisce; se non esiste, crea uno studio + utente <b>senza password</b> (accesso solo
    /// via Google). Il ruolo segue la stessa regola del super-admin (email configurata).
    /// </summary>
    public async Task<UtenteEntity> AccediConGoogleAsync(
        string email, string nome, CancellationToken ct = default)
    {
        email = Normalizza(email);
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email Google mancante.");

        var esistente = await _db.Utenti.FirstOrDefaultAsync(u => u.Email == email && u.Attivo, ct);
        if (esistente is not null)
            return esistente;

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            RagioneSociale = string.IsNullOrWhiteSpace(nome) ? email : nome,
            CreatoIl = DateTime.UtcNow,
        };
        var utente = new UtenteEntity
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Email = email,
            PasswordHash = "", // niente password: accesso solo via Google
            Ruolo = "admin", Attivo = true, CreatoIl = DateTime.UtcNow,
        };

        _db.Tenants.Add(tenant);
        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync(ct);
        return utente;
    }

    /// <summary>
    /// Bootstrap del super-admin di piattaforma (unico canale per ottenere il ruolo): promuove
    /// l'utente indicato a <see cref="RuoloSuperAdmin"/> SOLO se il token corrisponde a
    /// <c>SuperAdmin:BootstrapToken</c> (segreto lungo da variabile d'ambiente) e SOLO finché non
    /// esiste già un super-admin. Una volta creato il primo, il canale si chiude da sé: il token
    /// non è più utilizzabile. Non c'è più promozione automatica da email registrabile.
    /// </summary>
    /// <summary>
    /// Cambia la password dell'utente dopo aver verificato quella attuale. Un account senza
    /// password (accesso solo Google) non può impostarla da qui.
    /// </summary>
    public async Task<bool> CambiaPasswordAsync(
        Guid utenteId, string passwordAttuale, string nuovaPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nuovaPassword) || nuovaPassword.Length < 8)
            throw new ArgumentException("La nuova password deve avere almeno 8 caratteri.");
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Id == utenteId, ct);
        if (utente is null || string.IsNullOrEmpty(utente.PasswordHash)) return false;
        if (!PasswordHasher.Verifica(passwordAttuale, utente.PasswordHash)) return false;
        utente.PasswordHash = PasswordHasher.Hash(nuovaPassword);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> BootstrapSuperAdminAsync(Guid utenteId, string? token, CancellationToken ct = default)
    {
        var atteso = _config?["SuperAdmin:BootstrapToken"];
        if (string.IsNullOrWhiteSpace(atteso) || atteso.Length < 32)
            throw new InvalidOperationException(
                "Bootstrap super-admin non configurato: impostare 'SuperAdmin:BootstrapToken' (>= 32 caratteri) via secret.");
        if (string.IsNullOrEmpty(token)
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(atteso)))
            return false;

        if (await _db.Utenti.AnyAsync(u => u.Ruolo == RuoloSuperAdmin, ct))
            throw new InvalidOperationException(
                "Esiste già un super-admin: il bootstrap è a uso singolo. Rimuovere il token dalla configurazione.");

        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Id == utenteId, ct);
        if (utente is null) return false;
        utente.Ruolo = RuoloSuperAdmin;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Restituisce l'utente se le credenziali sono valide, altrimenti null. (Solo password.)</summary>
    public async Task<UtenteEntity?> AutenticaAsync(
        string email, string password, CancellationToken ct = default)
    {
        email = Normalizza(email);
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Email == email && u.Attivo, ct);
        if (utente is null || !PasswordHasher.Verifica(password, utente.PasswordHash))
            return null;
        return utente;
    }

    /// <summary>
    /// Accesso completo: verifica password e, se l'utente ha il 2FA attivo, anche il codice TOTP.
    /// Distingue credenziali errate, codice mancante e codice errato (per una UX corretta).
    /// </summary>
    public async Task<RisultatoAccesso> AccediAsync(
        string email, string password, string? codice2fa = null, CancellationToken ct = default)
    {
        email = Normalizza(email);
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (utente is null)
            return new RisultatoAccesso(EsitoAccesso.CredenzialiErrate, null);
        if (!utente.Attivo || !PasswordHasher.Verifica(password, utente.PasswordHash))
            return Negato(EsitoAccesso.CredenzialiErrate, utente);

        // Studio sospeso dal super-admin: l'accesso è bloccato (il super-admin non sospende sé stesso).
        if (utente.Ruolo != RuoloSuperAdmin)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == utente.TenantId, ct);
            if (tenant is { Sospeso: true })
                return Negato(EsitoAccesso.AccountSospeso, utente);
        }

        if (utente.DueFattoriAttivo)
        {
            if (string.IsNullOrWhiteSpace(codice2fa))
                return Negato(EsitoAccesso.ServeCodice2fa, utente);
            if (string.IsNullOrEmpty(utente.SegretoTotp) || !Totp.Verifica(utente.SegretoTotp, codice2fa))
                return Negato(EsitoAccesso.Codice2faErrato, utente);
        }

        return new RisultatoAccesso(EsitoAccesso.Ok, utente);
    }

    private static RisultatoAccesso Negato(EsitoAccesso esito, UtenteEntity utente) =>
        new(esito, null, new IdentitaAccessoAudit(utente.TenantId, utente.Id));

    /// <summary>
    /// Avvia la configurazione del 2FA: genera un segreto (non ancora attivo) e lo salva, restituendo
    /// segreto e URI <c>otpauth</c> per l'app authenticator. L'attivazione avviene con <see cref="AttivaDueFattoriAsync"/>.
    /// </summary>
    public async Task<ConfigurazioneDueFattori> IniziaDueFattoriAsync(
        Guid utenteId, string emittente = "Paghe", CancellationToken ct = default)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Id == utenteId, ct)
            ?? throw new ArgumentException("Utente inesistente.");
        // Sicurezza: non reiniziare l'enrollment se il 2FA è già attivo, altrimenti una sessione
        // rubata potrebbe disattivarlo. Per riconfigurarlo serve prima DisattivaDueFattoriAsync (con codice).
        if (utente.DueFattoriAttivo)
            throw new InvalidOperationException(
                "Il 2FA è già attivo: disattivalo con un codice valido prima di riconfigurarlo.");

        var segreto = Totp.GeneraSegreto();
        utente.SegretoTotp = segreto;
        utente.DueFattoriAttivo = false; // si attiva solo dopo la verifica del primo codice
        await _db.SaveChangesAsync(ct);

        return new ConfigurazioneDueFattori(segreto, Totp.UriOtpauth(emittente, utente.Email, segreto));
    }

    /// <summary>Conferma e attiva il 2FA verificando un primo codice. Restituisce false se il codice non è valido.</summary>
    public async Task<bool> AttivaDueFattoriAsync(Guid utenteId, string codice, CancellationToken ct = default)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Id == utenteId, ct)
            ?? throw new ArgumentException("Utente inesistente.");
        if (string.IsNullOrEmpty(utente.SegretoTotp) || !Totp.Verifica(utente.SegretoTotp, codice))
            return false;

        utente.DueFattoriAttivo = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Disattiva il 2FA previa verifica di un codice valido (evita disattivazioni non autorizzate).</summary>
    public async Task<bool> DisattivaDueFattoriAsync(Guid utenteId, string codice, CancellationToken ct = default)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Id == utenteId, ct)
            ?? throw new ArgumentException("Utente inesistente.");
        if (!utente.DueFattoriAttivo || string.IsNullOrEmpty(utente.SegretoTotp)
            || !Totp.Verifica(utente.SegretoTotp, codice))
            return false;

        utente.DueFattoriAttivo = false;
        utente.SegretoTotp = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string Normalizza(string email) => email.Trim().ToLowerInvariant();
}
