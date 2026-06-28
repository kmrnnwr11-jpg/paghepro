using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Persistence;

/// <summary>Esito di un tentativo di accesso (login), con la distinzione del secondo fattore.</summary>
public enum EsitoAccesso
{
    CredenzialiErrate,
    ServeCodice2fa,
    Codice2faErrato,
    Ok,
}

/// <summary>Risultato di un accesso: esito + utente (presente solo se <see cref="EsitoAccesso.Ok"/>).</summary>
public sealed record RisultatoAccesso(EsitoAccesso Esito, UtenteEntity? Utente);

/// <summary>Dati per configurare il 2FA: segreto da salvare nell'app e URI per il QR code.</summary>
public sealed record ConfigurazioneDueFattori(string Segreto, string UriOtpauth);

/// <summary>
/// Registrazione e autenticazione degli utenti. La registrazione crea un nuovo tenant
/// (account) con il primo utente amministratore. L'autenticazione verifica le credenziali.
/// </summary>
public sealed class AuthService
{
    private readonly PagheDbContext _db;

    public AuthService(PagheDbContext db) => _db = db;

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
        var utente = await AutenticaAsync(email, password, ct);
        if (utente is null)
            return new RisultatoAccesso(EsitoAccesso.CredenzialiErrate, null);

        if (utente.DueFattoriAttivo)
        {
            if (string.IsNullOrWhiteSpace(codice2fa))
                return new RisultatoAccesso(EsitoAccesso.ServeCodice2fa, null);
            if (string.IsNullOrEmpty(utente.SegretoTotp) || !Totp.Verifica(utente.SegretoTotp, codice2fa))
                return new RisultatoAccesso(EsitoAccesso.Codice2faErrato, null);
        }

        return new RisultatoAccesso(EsitoAccesso.Ok, utente);
    }

    /// <summary>
    /// Avvia la configurazione del 2FA: genera un segreto (non ancora attivo) e lo salva, restituendo
    /// segreto e URI <c>otpauth</c> per l'app authenticator. L'attivazione avviene con <see cref="AttivaDueFattoriAsync"/>.
    /// </summary>
    public async Task<ConfigurazioneDueFattori> IniziaDueFattoriAsync(
        Guid utenteId, string emittente = "Paghe", CancellationToken ct = default)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Id == utenteId, ct)
            ?? throw new ArgumentException("Utente inesistente.");

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
