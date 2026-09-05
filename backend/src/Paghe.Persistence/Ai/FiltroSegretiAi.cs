using System.Text;
using System.Text.RegularExpressions;

namespace Paghe.Persistence.Ai;

/// <summary>
/// Confine deterministico condiviso da API e persistenza. Un testo che sembra contenere
/// credenziali viene rifiutato prima sia del provider sia della cifratura nel database.
/// </summary>
public static class FiltroSegretiAi
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private const RegexOptions Opzioni =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private static readonly Regex[] PatternVietati =
    [
        NuovaRegex(@"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----"),
        NuovaRegex("""\b(?:pass\s*word|passwd|pwd|api[_\s-]?key|access[_\s-]?token|refresh[_\s-]?token|id[_\s-]?token|session[_\s-]?(?:token|secret|id)|client[_\s-]?secret|cookie|set-cookie)\b\s*[:=]\s*['"]?[^\s'",;]{1,}"""),
        NuovaRegex(@"\b(?:pin|totp|otp|passcode|codice\s+mfa)\b\s*[:=]\s*\d{4,12}\b"),
        NuovaRegex(@"\bauthorization\s*:\s*(?:bearer|basic)\s+\S{6,}"),
        NuovaRegex(@"\b(?:sk|gh[pousr]|glpat|xox[baprs])[-_][A-Za-z0-9_-]{16,}\b"),
        NuovaRegex(@"\bAIza[0-9A-Za-z_-]{30,}\b"),
        NuovaRegex(@"\bAKIA[0-9A-Z]{16}\b"),
        NuovaRegex(@"\b[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b"),
        NuovaRegex(@"\b(?:Password|Pwd|AccountKey)\s*=\s*[^;\s]{8,}"),
    ];

    private static readonly Regex SegmentiLunghi = NuovaRegex(@"[A-Za-z0-9+/=_-]{32,}");

    public static bool DeveBloccare(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return false;
        // Forme Unicode compatibili e caratteri zero-width non possono spezzare le etichette
        // dei segreti per aggirare il confine locale.
        var canonico = testo.Normalize(NormalizationForm.FormKC)
            .Replace("\u200B", "", StringComparison.Ordinal)
            .Replace("\u200C", "", StringComparison.Ordinal)
            .Replace("\u200D", "", StringComparison.Ordinal)
            .Replace("\uFEFF", "", StringComparison.Ordinal);
        return PatternVietati.Any(regex => regex.IsMatch(canonico))
            || ContieneSegmentoAdAltaEntropia(canonico);
    }

    private static bool ContieneSegmentoAdAltaEntropia(string testo) =>
        SegmentiLunghi.Matches(testo).Any(match =>
        {
            var valore = match.Value;
            return valore.Any(char.IsUpper)
                && valore.Any(char.IsLower)
                && valore.Any(char.IsDigit)
                && valore.Distinct().Take(12).Count() == 12;
        });

    private static Regex NuovaRegex(string pattern) => new(pattern, Opzioni, Timeout);
}
