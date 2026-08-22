using System.Text.RegularExpressions;

namespace Paghe.Api;

/// <summary>
/// Confine deterministico applicato prima di qualunque modello. Se il testo sembra contenere
/// credenziali o chiavi, l'intero segmento viene rifiutato: un prompt non può autorizzarne
/// l'invio e il valore individuato non viene mai riportato nei log o nella risposta.
/// </summary>
internal static class LocalSecretBoundaryFilter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private const RegexOptions Opzioni =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    private static readonly Regex[] PatternVietati =
    [
        NuovaRegex(@"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----"),
        NuovaRegex("""\b(?:password|passwd|pwd|api[_-]?key|access[_-]?token|client[_-]?secret)\b\s*[:=]\s*['"]?[^\s'",;]{8,}"""),
        NuovaRegex(@"\b(?:sk|gh[pousr]|glpat|xox[baprs])[-_][A-Za-z0-9_-]{16,}\b"),
        NuovaRegex(@"\bAIza[0-9A-Za-z_-]{30,}\b"),
        NuovaRegex(@"\bAKIA[0-9A-Z]{16}\b"),
        NuovaRegex(@"\b[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b"),
        NuovaRegex(@"\b(?:Password|Pwd|AccountKey)\s*=\s*[^;\s]{8,}"),
    ];

    private static readonly Regex SegmentiLunghi =
        NuovaRegex(@"[A-Za-z0-9+/=_-]{32,}");

    internal static bool DeveBloccare(string? testo) =>
        !string.IsNullOrWhiteSpace(testo)
        && (PatternVietati.Any(regex => regex.IsMatch(testo)) || ContieneSegmentoAdAltaEntropia(testo));

    private static bool ContieneSegmentoAdAltaEntropia(string testo) =>
        SegmentiLunghi.Matches(testo).Any(match =>
        {
            string valore = match.Value;
            return valore.Any(char.IsUpper)
                && valore.Any(char.IsLower)
                && valore.Any(char.IsDigit)
                && valore.Distinct().Take(12).Count() == 12;
        });

    private static Regex NuovaRegex(string pattern) => new(pattern, Opzioni, Timeout);
}
