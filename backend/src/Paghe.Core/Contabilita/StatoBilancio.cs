namespace Paghe.Core.Contabilita;

/// <summary>
/// Stato del bilancio d'esercizio archiviato. Il ciclo tipico per una società (es. SRLS):
/// si redige una <c>Bozza</c>, la si <c>Approva</c> (assemblea dei soci) e infine la si
/// segna come <c>Depositata</c> al Registro Imprese. Una bozza o un'approvazione possono
/// essere <c>Annullate</c>; un'approvazione può tornare in bozza per correzioni.
/// </summary>
public enum StatoBilancio
{
    Bozza,
    Approvato,
    Depositato,
    Annullato
}

/// <summary>Macchina a stati del bilancio: definisce le transizioni ammesse.</summary>
public static class TransizioniBilancio
{
    private static readonly Dictionary<StatoBilancio, StatoBilancio[]> Ammesse = new()
    {
        [StatoBilancio.Bozza] = new[] { StatoBilancio.Approvato, StatoBilancio.Annullato },
        [StatoBilancio.Approvato] = new[] { StatoBilancio.Depositato, StatoBilancio.Bozza, StatoBilancio.Annullato },
        [StatoBilancio.Depositato] = Array.Empty<StatoBilancio>(),
        [StatoBilancio.Annullato] = Array.Empty<StatoBilancio>(),
    };

    public static bool PuoTransizionare(StatoBilancio da, StatoBilancio a) =>
        Ammesse.TryGetValue(da, out var consentiti) && consentiti.Contains(a);
}
