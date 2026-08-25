namespace Paghe.Core.Parametri;

/// <summary>
/// Parametri retributivi di un livello di inquadramento del CCNL.
/// <paramref name="MinimoTabellare"/> è il minimo mensile COMPLESSIVO (conglobato): quando la
/// <paramref name="Contingenza"/> è indicata, ne è una quota interna da esporre come voce
/// distinta in busta (revisione consulente 24/08/2026: paga base e contingenza devono restare
/// distinguibili nel prospetto; l'EDR storico è dentro la contingenza).
/// </summary>
public sealed record LivelloCcnl(
    string Codice,
    string Descrizione,
    decimal MinimoTabellare,
    decimal Edr,            // Elemento Distinto della Retribuzione
    decimal ImportoScatto,  // valore di un singolo scatto di anzianità
    decimal Contingenza = 0m);

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
    private readonly Dictionary<string, decimal> _terzoElemento;

    public Ccnl(
        string codice,
        string descrizione,
        IEnumerable<LivelloCcnl> livelli,
        string? codiceCnel = null,
        string? fonte = null,
        IReadOnlyDictionary<string, decimal>? terzoElemento = null)
    {
        Codice = codice;
        Descrizione = descrizione;
        CodiceCnel = string.IsNullOrWhiteSpace(codiceCnel) ? null : codiceCnel.Trim().ToUpperInvariant();
        Fonte = string.IsNullOrWhiteSpace(fonte) ? null : fonte.Trim();
        _livelli = livelli.ToDictionary(l => l.Codice);
        _terzoElemento = terzoElemento is null
            ? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, decimal>(terzoElemento, StringComparer.OrdinalIgnoreCase);
    }

    public LivelloCcnl Livello(string codice) =>
        _livelli.TryGetValue(codice, out var l)
            ? l
            : throw new ArgumentException($"Livello CCNL '{codice}' non trovato in '{Codice}'.");

    public IReadOnlyCollection<LivelloCcnl> Livelli => _livelli.Values;

    /// <summary>Importi del terzo elemento per sigla provincia; la chiave "*" è il valore nazionale.</summary>
    public IReadOnlyDictionary<string, decimal> TerzoElementoProvinciale => _terzoElemento;

    /// <summary>
    /// Terzo elemento mensile per la provincia dell'unità operativa. Se la provincia non ha una
    /// voce propria vale quella nazionale ("*"); un'altra provincia non viene MAI ereditata.
    /// Zero se il contratto non prevede il terzo elemento.
    /// </summary>
    public decimal TerzoElemento(string? provincia)
    {
        if (_terzoElemento.Count == 0)
            return 0m;
        var sigla = (provincia ?? "").Trim().ToUpperInvariant();
        if (sigla.Length > 0 && _terzoElemento.TryGetValue(sigla, out var provinciale))
            return provinciale;
        return _terzoElemento.TryGetValue("*", out var nazionale) ? nazionale : 0m;
    }
}
