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
            // Sessione a lunga durata (uso personale): resti dentro finché non fai logout,
            // senza dover rifare l'accesso ogni giorno. Rinnovata a ogni nuovo login.
            Expires = DateTime.UtcNow.AddDays(365),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(chiave), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Default di sviluppo: usato SOLO fuori produzione, mai in ambiente Production.</summary>
    internal const string ChiaveSviluppo = "dev-secret-da-cambiare-in-produzione-almeno-32-byte!!";

    /// <summary>Lunghezza minima (byte) richiesta per la chiave HMAC-SHA256.</summary>
    private const int LunghezzaMinima = 32;

    /// <summary>
    /// Chiave di firma JWT da configurazione (<c>Jwt:Key</c>). In ambiente Production è
    /// obbligatoria una chiave robusta da secret/variabile d'ambiente: il default di sviluppo
    /// e le chiavi troppo corte vengono rifiutati con un errore esplicito.
    /// </summary>
    public static string ChiaveJwt(IConfiguration config) => ChiaveJwt(config, AmbienteProduzione());

    /// <summary>Variante testabile: l'ambiente è passato esplicitamente.</summary>
    public static string ChiaveJwt(IConfiguration config, bool produzione)
    {
        var chiave = config["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(chiave) || chiave == ChiaveSviluppo)
        {
            if (produzione)
                throw new InvalidOperationException(
                    "Chiave JWT mancante o di sviluppo: in produzione configurare 'Jwt:Key' " +
                    "(almeno 32 byte) tramite secret o variabile d'ambiente (es. Jwt__Key).");
            return ChiaveSviluppo;
        }

        if (Encoding.UTF8.GetByteCount(chiave) < LunghezzaMinima)
            throw new InvalidOperationException(
                $"La chiave JWT 'Jwt:Key' deve essere di almeno {LunghezzaMinima} byte.");

        return chiave;
    }

    /// <summary>
    /// Come ASP.NET Core: ambiente NON impostato = Production. Così un deploy che dimentica
    /// la variabile d'ambiente non gira mai con la chiave JWT di sviluppo.
    /// </summary>
    private static bool AmbienteProduzione()
    {
        var ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.IsNullOrWhiteSpace(ambiente)
            || string.Equals(ambiente, "Production", StringComparison.OrdinalIgnoreCase);
    }
}
