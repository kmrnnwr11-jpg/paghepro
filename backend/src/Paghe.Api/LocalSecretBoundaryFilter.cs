using Paghe.Persistence.Ai;

namespace Paghe.Api;

/// <summary>
/// Confine deterministico applicato prima di qualunque modello. Se il testo sembra contenere
/// credenziali o chiavi, l'intero segmento viene rifiutato: un prompt non può autorizzarne
/// l'invio e il valore individuato non viene mai riportato nei log o nella risposta.
/// </summary>
internal static class LocalSecretBoundaryFilter
{
    internal static bool DeveBloccare(string? testo) =>
        FiltroSegretiAi.DeveBloccare(testo);
}
