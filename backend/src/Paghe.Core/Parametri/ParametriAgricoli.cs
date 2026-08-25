namespace Paghe.Core.Parametri;

/// <summary>
/// Un contributo del settore agricolo. In agricoltura le trattenute non sono un'unica
/// percentuale INPS: accanto al FAP (la quota pensionistica) ci sono contributi di assistenza
/// contrattuale nazionali e provinciali, diversi da provincia a provincia.
/// </summary>
public sealed record ContributoAgricolo(
    string Codice,
    string Descrizione,
    decimal Aliquota,
    bool ACaricoDipendente);

/// <summary>
/// Riga della tabella salariale provinciale (CPL): la paga oraria di un livello è la somma
/// del salario provinciale e del terzo elemento, la quota che negli operai agricoli sostituisce
/// ferie, festività e mensilità aggiuntive pagandole ora per ora.
/// </summary>
public sealed record TariffaAgricola(
    string Livello,
    string Descrizione,
    decimal SalarioProvincialeOrario,
    decimal TerzoElementoOrario)
{
    public decimal PagaOraria => SalarioProvincialeOrario + TerzoElementoOrario;
}

/// <summary>
/// Parametri del settore agricolo per un anno e una provincia. Come il resto dei parametri
/// normativi sono versionati: aggiornarli significa creare una nuova istanza, così i cedolini
/// già emessi restano ricalcolabili.
/// </summary>
public sealed class ParametriAgricoli
{
    public required int Anno { get; init; }

    /// <summary>Sigla della provincia del CPL (es. "CN").</summary>
    public required string Provincia { get; init; }

    public required IReadOnlyList<TariffaAgricola> Tariffe { get; init; }

    public required IReadOnlyList<ContributoAgricolo> Contributi { get; init; }

    /// <summary>
    /// Quota contributiva che riduce l'imponibile fiscale del mese. NON coincide con il totale
    /// trattenuto: i contributi di assistenza contrattuale non sono deducibili, e sulle buste
    /// reali la quota dedotta risulta inferiore anche alla sola aliquota FAP. È tenuta come
    /// parametro esplicito perché è il valore osservato, non una regola che sappiamo derivare.
    /// </summary>
    public required decimal AliquotaContributiDeducibili { get; init; }

    /// <summary>Da dove vengono questi valori: senza fonte una tabella di paghe non vale nulla.</summary>
    public required string Fonte { get; init; }

    public TariffaAgricola Tariffa(string livello) =>
        Tariffe.FirstOrDefault(t => string.Equals(t.Livello, livello, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException(
            $"Livello '{livello}' non presente nella tabella salariale agricola {Provincia} {Anno}.");

    /// <summary>Somma delle aliquote trattenute al lavoratore.</summary>
    public decimal AliquotaTotaleDipendente =>
        Contributi.Where(c => c.ACaricoDipendente).Sum(c => c.Aliquota);
}
