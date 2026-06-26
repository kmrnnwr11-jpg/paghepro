using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Paghe.Persistence.Entities;

namespace Paghe.Api;

/// <summary>Emette i token JWT firmati per gli utenti autenticati.</summary>
public sealed class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string Crea(UtenteEntity utente)
    {
        var chiave = Encoding.UTF8.GetBytes(ChiaveJwt(_config));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, utente.Id.ToString()),
                new Claim("tenant_id", utente.TenantId.ToString()),
                new Claim(ClaimTypes.Role, utente.Ruolo),
                new Claim(JwtRegisteredClaimNames.Email, utente.Email),
            }),
            Expires = DateTime.UtcNow.AddHours(8),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(chiave), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Chiave di firma JWT da configurazione, con default per lo sviluppo.</summary>
    public static string ChiaveJwt(IConfiguration config) =>
        config["Jwt:Key"] ?? "dev-secret-da-cambiare-in-produzione-almeno-32-byte!!";
}
