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

    /// <summary>
    /// Firma del produttore del software stampata in fondo ai documenti. Null = niente marchio:
    /// non viene mai inventato un nome. Si imposta una volta all'avvio dalla configurazione.
    /// </summary>
    public static MarchioSoftware? Marchio { get; set; }

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

/// <summary>
/// Firma del produttore del software sui documenti generati (es. "Elaborato con PaghePro ·
/// Alpha Group"). Riguarda solo il marchio di chi produce il software: non ha nulla a che
/// vedere con le autorizzazioni di enti pubblici, che si stampano solo se realmente rilasciate.
/// </summary>
/// <param name="Nome">Nome del prodotto o dell'azienda, obbligatorio.</param>
/// <param name="Claim">Riga breve facoltativa (es. "Elaborazione paghe e contributi").</param>
/// <param name="Logo">Logo PNG/JPEG facoltativo, mostrato in piccolo accanto al nome.</param>
public sealed record MarchioSoftware(string Nome, string? Claim = null, byte[]? Logo = null);
