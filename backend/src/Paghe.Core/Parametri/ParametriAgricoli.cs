namespace Paghe.Core.Parametri;

/// <summary>
/// Un contributo del settore agricolo. In agricoltura le trattenute non sono un'unica
/// percentuale INPS: accanto al FAP (la quota pensionistica) ci sono contributi di assistenza
/// contrattuale nazionali e provinciali, diversi da provincia a provincia.
/// <paramref name="DeducibileIrpef"/> dice se la trattenuta riduce l'imponibile fiscale:
/// il FAP (contributo di legge) sì, i contributi di assistenza contrattuale di regola no.
/// È configurazione, non codice: la conferma spetta al Consulente del Lavoro.
/// </summary>
public sealed record ContributoAgricolo(
    string Codice,
    string Descrizione,
    decimal Aliquota,
    bool ACaricoDipendente,
    bool DeducibileIrpef = false);

/// <summary>
/// Formattazione delle aliquote in busta: tutti i decimali significativi, mai un
/// arrotondamento che stampi una percentuale diversa da quella applicata (un'aliquota
/// 0,3333% non deve presentarsi come «0,33%»). Minimo due decimali, come in busta.
/// </summary>
public static class FormatoAliquota
{
    private static readonly System.Globalization.CultureInfo It =
        System.Globalization.CultureInfo.GetCultureInfo("it-IT");

    public static string Percentuale(decimal aliquota) =>
        (aliquota * 100m).ToString("0.00############", It) + "%";
}

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
    /// Quota contributiva OSSERVATA che riduce l'imponibile fiscale del mese, quando le buste
    /// reali di riferimento mostrano un valore che i flag non spiegano (Cuneo 2025: 7,80%,
    /// meno anche del solo FAP — il perché è ancora da chiarire col consulente). Zero = la
    /// quota deducibile si calcola dai flag <see cref="ContributoAgricolo.DeducibileIrpef"/>.
    /// </summary>
    public required decimal AliquotaContributiDeducibili { get; init; }

    /// <summary>
    /// Aliquota effettivamente dedotta dall'imponibile fiscale: il valore osservato quando
    /// c'è, altrimenti la somma delle aliquote dei contributi c/lavoratore marcati deducibili.
    /// È questa la percentuale stampata in busta.
    /// </summary>
    public decimal AliquotaDeducibileEffettiva =>
        AliquotaContributiDeducibili > 0m
            ? AliquotaContributiDeducibili
            : Contributi.Where(c => c.ACaricoDipendente && c.DeducibileIrpef).Sum(c => c.Aliquota);

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
