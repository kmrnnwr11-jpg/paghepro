namespace Paghe.Core.Parametri;

/// <summary>Parametri retributivi di un livello di inquadramento del CCNL.</summary>
public sealed record LivelloCcnl(
    string Codice,
    string Descrizione,
    decimal MinimoTabellare,
    decimal Edr,            // Elemento Distinto della Retribuzione
    decimal ImportoScatto); // valore di un singolo scatto di anzianità

/// <summary>
/// Tabella di un CCNL: livelli con i relativi minimi. Versione MVP (CCNL Commercio).
/// In produzione queste tabelle sono versionate per data di validità.
/// </summary>
public sealed class Ccnl
{
    public string Codice { get; }
    public string Descrizione { get; }
    private readonly Dictionary<string, LivelloCcnl> _livelli;

    public Ccnl(string codice, string descrizione, IEnumerable<LivelloCcnl> livelli)
    {
        Codice = codice;
        Descrizione = descrizione;
        _livelli = livelli.ToDictionary(l => l.Codice);
    }

    public LivelloCcnl Livello(string codice) =>
        _livelli.TryGetValue(codice, out var l)
            ? l
            : throw new ArgumentException($"Livello CCNL '{codice}' non trovato in '{Codice}'.");

    public IReadOnlyCollection<LivelloCcnl> Livelli => _livelli.Values;
}
