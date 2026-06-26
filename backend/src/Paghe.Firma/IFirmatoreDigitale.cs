namespace Paghe.Firma;

/// <summary>
/// Astrazione di un firmatario digitale CAdES-BES (busta <c>.p7m</c>), indipendente
/// da dove risiede la chiave privata: file PFX/P12 (<see cref="FirmatorePfx"/>),
/// smart card / CNS (<see cref="FirmatoreCns"/>) o, in futuro, HSM di firma remota.
/// </summary>
public interface IFirmatoreDigitale
{
    /// <summary>
    /// Firma <paramref name="contenuto"/> e produce una busta CAdES-BES (<c>.p7m</c>) con il
    /// contenuto incapsulato e digest SHA-256, nel profilo accettato dal Sistema di Interscambio.
    /// </summary>
    /// <param name="nomeFileOriginale">
    /// Nome del file di origine (es. <c>IT01234567890_00001.xml</c>): serve solo a comporre
    /// il nome del file firmato aggiungendo <c>.p7m</c>. Se nullo viene usato un nome generico.
    /// </param>
    RisultatoFirma Firma(byte[] contenuto, string? nomeFileOriginale = null);
}
