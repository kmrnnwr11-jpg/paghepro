using System.Diagnostics;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Richiesta di controllo ufficiale di un file telematico tramite i JAR dell'Agenzia delle
/// Entrate. I JAR si invocano con <c>SuppTelem.jar</c> nel classpath più il modulo specifico,
/// es. <c>ControlliF24A0.jar</c> / <c>ModuliControlloCUR26.jar</c>.
/// </summary>
public sealed class RichiestaControlloUfficiale
{
    /// <summary>File telematico da controllare (input).</summary>
    public required string FileInput { get; init; }

    /// <summary>Classpath completo: SuppTelem.jar più il JAR del modello, separati dal separatore di sistema.</summary>
    public required string Classpath { get; init; }

    /// <summary>Classe main del controllo (es. <c>it.finanze.entrate.dic.main.MCUR26</c>).</summary>
    public required string ClasseMain { get; init; }

    /// <summary>Cartella dove scrivere diagnostico (.dgn) e file controllato (.dcm). Default: cartella dell'input.</summary>
    public string? CartellaOutput { get; init; }

    /// <summary>Eseguibile Java da usare.</summary>
    public string JavaExe { get; init; } = "java";
}

/// <summary>Argomenti calcolati per la riga di comando e percorsi di output attesi.</summary>
public sealed record ArgomentiControllo(
    IReadOnlyList<string> Argomenti,
    string FileDiagnostico,
    string FileControllato);

/// <summary>Esito di un controllo ufficiale.</summary>
public sealed record EsitoControlloUfficiale(
    int ExitCode,
    string FileDiagnostico,
    string FileControllato,
    string Log)
{
    /// <summary>Vero se il controllo è andato a buon fine (exit 0 e file controllato prodotto).</summary>
    public bool Ok => ExitCode == 0 && File.Exists(FileControllato);
}

/// <summary>
/// Parametri ufficiali del modulo di controllo F24 (codice fornitura "F24A0"), come da
/// <c>TabellaAssociazioniControlli.json</c> dell'Agenzia delle Entrate.
/// </summary>
public static class ControlloF24A0
{
    public const string CodiceFornitura = "F24A0";
    public const string ClasseMainControllo = "finanze.BQJCB102.main.CtrlFile";
    public const string ClasseMainDiagnostico = "finanze.BQJCB102.generale.MakeDgn";

    /// <summary>
    /// Costruisce la richiesta di controllo per il file F24 indicato. <paramref name="cartellaJar"/>
    /// è la cartella che contiene <c>SuppTelem.jar</c> e <c>ControlliF24A0.jar</c>.
    /// </summary>
    public static RichiestaControlloUfficiale Richiesta(string fileInput, string cartellaJar, string? cartellaOutput = null)
    {
        char sep = OperatingSystem.IsWindows() ? ';' : ':';
        string classpath = string.Join(sep,
            Path.Combine(cartellaJar, "SuppTelem.jar"),
            Path.Combine(cartellaJar, "ControlliF24A0.jar"));
        return new RichiestaControlloUfficiale
        {
            FileInput = fileInput,
            Classpath = classpath,
            ClasseMain = ClasseMainControllo,
            CartellaOutput = cartellaOutput,
        };
    }
}

/// <summary>
/// Parametri ufficiali del modulo di controllo della Certificazione Unica 2026 (codice
/// fornitura "CUR26"), come da <c>TabellaAssociazioniControlli.json</c> dell'Agenzia delle Entrate.
/// </summary>
public static class ControlloCur26
{
    public const string CodiceFornitura = "CUR26";
    public const string ClasseMainControllo = "it.finanze.entrate.dic.main.MCUR26";
    public const string ClasseMainDiagnostico = "it.finanze.entrate.dic.main.VisualCUR26";

    /// <summary>
    /// Costruisce la richiesta di controllo per il file CU indicato. <paramref name="cartellaJar"/>
    /// è la cartella che contiene <c>SuppTelem.jar</c> e <c>ModuliControlloCUR26.jar</c>.
    /// </summary>
    public static RichiestaControlloUfficiale Richiesta(string fileInput, string cartellaJar, string? cartellaOutput = null)
    {
        char sep = OperatingSystem.IsWindows() ? ';' : ':';
        string classpath = string.Join(sep,
            Path.Combine(cartellaJar, "SuppTelem.jar"),
            Path.Combine(cartellaJar, "ModuliControlloCUR26.jar"));
        return new RichiestaControlloUfficiale
        {
            FileInput = fileInput,
            Classpath = classpath,
            ClasseMain = ClasseMainControllo,
            CartellaOutput = cartellaOutput,
        };
    }
}

/// <summary>Esegue il controllo ufficiale di un file telematico.</summary>
public interface IControlliUfficialiRunner
{
    EsitoControlloUfficiale Esegui(RichiestaControlloUfficiale richiesta);
}

/// <summary>
/// Implementazione che lancia i JAR ufficiali come processo Java esterno.
/// Non automatizza login né invio: produce solo diagnostico e file controllato,
/// che poi vengono trasmessi tramite canale ufficiale (Desktop Telematico/Entratel).
/// </summary>
public sealed class ControlliUfficialiRunnerJava : IControlliUfficialiRunner
{
    /// <summary>
    /// Costruisce gli argomenti della riga di comando e i percorsi di output, senza eseguire nulla.
    /// Separato dall'esecuzione per essere verificabile in unit test.
    /// </summary>
    public static ArgomentiControllo CostruisciArgomenti(RichiestaControlloUfficiale r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (string.IsNullOrWhiteSpace(r.FileInput))
            throw new ArgumentException("FileInput è obbligatorio.", nameof(r));
        if (string.IsNullOrWhiteSpace(r.Classpath))
            throw new ArgumentException("Classpath è obbligatorio.", nameof(r));
        if (string.IsNullOrWhiteSpace(r.ClasseMain))
            throw new ArgumentException("ClasseMain è obbligatoria.", nameof(r));

        string cartella = r.CartellaOutput
            ?? Path.GetDirectoryName(Path.GetFullPath(r.FileInput))
            ?? ".";
        string baseNome = Path.GetFileNameWithoutExtension(r.FileInput);
        string dgn = Path.Combine(cartella, baseNome + ".dgn");
        string dcm = Path.Combine(cartella, baseNome + ".dcm");

        var args = new List<string> { "-cp", r.Classpath, r.ClasseMain, r.FileInput, dgn, dcm };
        return new ArgomentiControllo(args, dgn, dcm);
    }

    public EsitoControlloUfficiale Esegui(RichiestaControlloUfficiale richiesta)
    {
        var arg = CostruisciArgomenti(richiesta);

        var psi = new ProcessStartInfo
        {
            FileName = richiesta.JavaExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string a in arg.Argomenti)
            psi.ArgumentList.Add(a);

        using var processo = new Process { StartInfo = psi };
        processo.Start();
        string stdout = processo.StandardOutput.ReadToEnd();
        string stderr = processo.StandardError.ReadToEnd();
        processo.WaitForExit();

        string log = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        return new EsitoControlloUfficiale(processo.ExitCode, arg.FileDiagnostico, arg.FileControllato, log);
    }
}
