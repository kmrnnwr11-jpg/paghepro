using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Configurazione della libreria PDF (QuestPDF).
///
/// QuestPDF è gratuito con licenza Community per organizzazioni con fatturato annuo
/// inferiore a 1M USD; oltre tale soglia richiede la licenza Professional/Enterprise.
/// In alternativa, per una licenza MIT senza vincoli, si può sostituire QuestPDF con
/// PDFsharp/MigraDoc (questo è l'unico punto del codice da cambiare).
/// </summary>
public static class DocumentiConfig
{
    private static bool _inizializzato;
    private static readonly object _lock = new();

    /// <summary>Imposta la licenza QuestPDF. Idempotente; chiamato dai generatori.</summary>
    public static void Inizializza()
    {
        if (_inizializzato) return;
        lock (_lock)
        {
            if (_inizializzato) return;
            QuestPDF.Settings.License = LicenseType.Community;
            _inizializzato = true;
        }
    }
}
