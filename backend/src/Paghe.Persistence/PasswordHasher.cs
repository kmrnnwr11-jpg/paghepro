using System.Security.Cryptography;

namespace Paghe.Persistence;

/// <summary>
/// Hashing delle password con PBKDF2 (SHA-256), salt casuale per ogni password.
/// Formato memorizzato: "iterazioni.saltBase64.hashBase64". Solo crittografia di base,
/// nessuna dipendenza esterna.
/// </summary>
public static class PasswordHasher
{
    private const int Iterazioni = 100_000;
    private const int DimSalt = 16;
    private const int DimChiave = 32;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(DimSalt);
        byte[] chiave = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterazioni, HashAlgorithmName.SHA256, DimChiave);
        return $"{Iterazioni}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(chiave)}";
    }

    public static bool Verifica(string password, string hashMemorizzato)
    {
        var parti = hashMemorizzato.Split('.');
        if (parti.Length != 3 || !int.TryParse(parti[0], out int iterazioni))
            return false;

        byte[] salt = Convert.FromBase64String(parti[1]);
        byte[] chiaveAttesa = Convert.FromBase64String(parti[2]);
        byte[] chiaveCalcolata = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, iterazioni, HashAlgorithmName.SHA256, chiaveAttesa.Length);

        return CryptographicOperations.FixedTimeEquals(chiaveCalcolata, chiaveAttesa);
    }
}
