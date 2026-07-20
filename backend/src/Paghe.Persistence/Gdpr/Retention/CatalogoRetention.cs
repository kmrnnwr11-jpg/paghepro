namespace Paghe.Persistence.Gdpr.Retention;

public sealed record DefinizioneRetention(
    string Categoria,
    string Titolo,
    string Finalita,
    string Decorrenza,
    string Azione,
    string Avvertenza);

/// <summary>
/// Allowlist dei soli insiemi che il motore sa trattare senza attraversare grafi paghe/fiscali
/// incompleti. La request non può mai indicare tabelle, colonne o predicati SQL.
/// </summary>
public static class CatalogoRetention
{
    public const string RegistroAttivita = "RegistroAttivita";
    public const string PromemoriaManualiChiusi = "PromemoriaManualiChiusi";

    private static readonly DefinizioneRetention[] Definizioni =
    [
        new(
            RegistroAttivita,
            "Registro attività",
            "Tracciabilità degli accessi, sicurezza e responsabilizzazione",
            "Data e ora UTC dell'evento",
            "Cancellazione definitiva dall'archivio operativo",
            "Non riguarda i log del gateway, del database, dell'hosting o i backup."),
        new(
            PromemoriaManualiChiusi,
            "Promemoria manuali chiusi",
            "Organizzazione delle scadenze inserite manualmente dallo studio",
            "Ultimo aggiornamento UTC, solo dopo stato Completato o Annullato",
            "Cancellazione definitiva dall'archivio operativo",
            "Non elimina promemoria automatici, aperti o inviati e non tocca i documenti collegati."),
    ];

    public static IReadOnlyList<DefinizioneRetention> Tutte => Definizioni;

    public static DefinizioneRetention Trova(string categoria) =>
        Definizioni.FirstOrDefault(d =>
            string.Equals(d.Categoria, categoria, StringComparison.OrdinalIgnoreCase))
        ?? throw new RetentionNonValidaException("Categoria di conservazione non supportata.");
}

public static class MotiviBloccoConservazione
{
    public const string RichiestaInteressato = "RichiestaInteressato";
    public const string Contenzioso = "Contenzioso";
    public const string VerificaAutorita = "VerificaAutorita";
    public const string ObbligoLegale = "ObbligoLegale";

    public static readonly IReadOnlySet<string> Tutti = new HashSet<string>(
        [RichiestaInteressato, Contenzioso, VerificaAutorita, ObbligoLegale],
        StringComparer.OrdinalIgnoreCase);
}

public sealed class RetentionNonValidaException(string message) : Exception(message);
public sealed class RetentionConflittoException(string message) : Exception(message);
public sealed class RetentionNonTrovataException(string message) : Exception(message);
