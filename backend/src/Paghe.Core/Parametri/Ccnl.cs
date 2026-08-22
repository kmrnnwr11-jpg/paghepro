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
    /// <summary>Codice alfanumerico unico dell'Archivio nazionale CNEL, versionato per anno.</summary>
    public string? CodiceCnel { get; }
    /// <summary>Riferimento alla fonte usata per identificare contratto e versione.</summary>
    public string? Fonte { get; }
    private readonly Dictionary<string, LivelloCcnl> _livelli;

    public Ccnl(
        string codice,
        string descrizione,
        IEnumerable<LivelloCcnl> livelli,
        string? codiceCnel = null,
        string? fonte = null)
    {
        Codice = codice;
        Descrizione = descrizione;
        CodiceCnel = string.IsNullOrWhiteSpace(codiceCnel) ? null : codiceCnel.Trim().ToUpperInvariant();
        Fonte = string.IsNullOrWhiteSpace(fonte) ? null : fonte.Trim();
        _livelli = livelli.ToDictionary(l => l.Codice);
    }

    public LivelloCcnl Livello(string codice) =>
        _livelli.TryGetValue(codice, out var l)
            ? l
            : throw new ArgumentException($"Livello CCNL '{codice}' non trovato in '{Codice}'.");

    public IReadOnlyCollection<LivelloCcnl> Livelli => _livelli.Values;
}
