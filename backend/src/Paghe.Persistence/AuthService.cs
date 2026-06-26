using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

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

    /// <summary>Restituisce l'utente se le credenziali sono valide, altrimenti null.</summary>
    public async Task<UtenteEntity?> AutenticaAsync(
        string email, string password, CancellationToken ct = default)
    {
        email = Normalizza(email);
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Email == email && u.Attivo, ct);
        if (utente is null || !PasswordHasher.Verifica(password, utente.PasswordHash))
            return null;
        return utente;
    }

    private static string Normalizza(string email) => email.Trim().ToLowerInvariant();
}
