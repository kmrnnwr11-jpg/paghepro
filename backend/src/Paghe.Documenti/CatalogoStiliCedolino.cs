namespace Paghe.Documenti;

/// <summary>Le tre impaginazioni complete da cui derivano i quindici stili.</summary>
public enum FamigliaStileCedolino
{
    Classico,
    Modulare,
    Editoriale,
}

/// <summary>
/// Voce pubblica e stabile del catalogo. La chiave entra nelle preferenze e nelle API;
/// famiglia e tema identificano invece la combinazione grafica, senza toccare i calcoli.
/// </summary>
public sealed record OpzioneStileCedolino(
    StileCedolino Stile,
    string Chiave,
    string Nome,
    string Descrizione,
    FamigliaStileCedolino Famiglia,
    string TemaId,
    string ColoreAnteprima,
    string SfondoAnteprima);

/// <summary>
/// Unico catalogo backend degli stili ammessi. Evita Enum.TryParse sulle richieste esterne:
/// anche valori numerici o enum non definiti vengono rifiutati invece di cadere sul Classico.
/// </summary>
public static class CatalogoStiliCedolino
{
    /// <summary>
    /// Stile usato quando l'utente non ha ancora espresso una preferenza. La chiave separata
    /// serve ai confini HTTP e viene verificata dai test contro il valore enum.
    /// </summary>
    public const StileCedolino Predefinito = StileCedolino.Colori;
    public const string ChiavePredefinita = "colori";

    private static readonly OpzioneStileCedolino[] VociCatalogo =
    [
        new(StileCedolino.Classico, "classico", "Classico Ufficio",
            "Griglia tradizionale monocromatica, compatta e adatta alla stampa.",
            FamigliaStileCedolino.Classico, "neutro", "#111111", "#F5F5F5"),
        new(StileCedolino.ClassicoBlu, "classico-blu", "Classico Blu",
            "Impostazione da ufficio con griglie blu e gerarchia più marcata.",
            FamigliaStileCedolino.Classico, "blu", "#12395E", "#EEF4F8"),
        new(StileCedolino.ClassicoVerde, "classico-verde", "Classico Verde",
            "Griglia tradizionale con accento verde sobrio.",
            FamigliaStileCedolino.Classico, "verde", "#1F5B45", "#EEF6F1"),
        new(StileCedolino.ClassicoBordeaux, "classico-bordeaux", "Classico Bordeaux",
            "Griglia tradizionale con accento bordeaux istituzionale.",
            FamigliaStileCedolino.Classico, "bordeaux", "#7C2D3E", "#F8F0F2"),
        new(StileCedolino.ClassicoAntracite, "classico-antracite", "Classico Antracite",
            "Versione neutra ad alto contrasto per archivi e stampe in scala di grigi.",
            FamigliaStileCedolino.Classico, "antracite", "#263238", "#F1F3F4"),

        new(StileCedolino.Colori, "colori", "Modulare Blu",
            "Sezioni numerate, sintesi iniziale e calendario orizzontale.",
            FamigliaStileCedolino.Modulare, "blu", "#12395E", "#F4F8FB"),
        new(StileCedolino.ColoriVerde, "colori-verde", "Modulare Verde",
            "Sezioni leggibili con palette verde professionale.",
            FamigliaStileCedolino.Modulare, "verde", "#1F5B45", "#F3F8F5"),
        new(StileCedolino.ColoriBordeaux, "colori-bordeaux", "Modulare Bordeaux",
            "Sezioni leggibili con palette bordeaux calda.",
            FamigliaStileCedolino.Modulare, "bordeaux", "#7C2D3E", "#FAF4F5"),
        new(StileCedolino.ColoriViola, "colori-viola", "Modulare Viola",
            "Sezioni leggibili con palette viola contemporanea.",
            FamigliaStileCedolino.Modulare, "viola", "#4C1D95", "#F7F4FC"),
        new(StileCedolino.ColoriPetrolio, "colori-petrolio", "Modulare Petrolio",
            "Sezioni leggibili con palette petrolio, adatta anche al bianco e nero.",
            FamigliaStileCedolino.Modulare, "petrolio", "#0F6660", "#F1F8F7"),

        new(StileCedolino.Editoriale, "editoriale", "Editoriale Rame",
            "Prospetto esteso su più pagine con dettagli e guida di lettura.",
            FamigliaStileCedolino.Editoriale, "rame", "#9A4F2E", "#F7F5F0"),
        new(StileCedolino.EditorialeBlu, "editoriale-blu", "Editoriale Blu",
            "Prospetto esteso con tono blu autorevole.",
            FamigliaStileCedolino.Editoriale, "blu", "#12395E", "#F5F8FA"),
        new(StileCedolino.EditorialeVerde, "editoriale-verde", "Editoriale Verde",
            "Prospetto esteso con tono verde sobrio.",
            FamigliaStileCedolino.Editoriale, "verde", "#1F5B45", "#F5F8F6"),
        new(StileCedolino.EditorialeBordeaux, "editoriale-bordeaux", "Editoriale Bordeaux",
            "Prospetto esteso con tono bordeaux istituzionale.",
            FamigliaStileCedolino.Editoriale, "bordeaux", "#7C2D3E", "#FAF6F6"),
        new(StileCedolino.EditorialeGrafite, "editoriale-grafite", "Editoriale Grafite",
            "Prospetto esteso essenziale, pensato per lettura e archiviazione.",
            FamigliaStileCedolino.Editoriale, "grafite", "#334155", "#F8FAFC"),
    ];

    public static IReadOnlyList<OpzioneStileCedolino> Tutti { get; } =
        Array.AsReadOnly(VociCatalogo);

    public static IReadOnlyList<string> Chiavi { get; } =
        Array.AsReadOnly(VociCatalogo.Select(voce => voce.Chiave).ToArray());

    public static bool ProvaDaChiave(string? chiave, out StileCedolino stile)
    {
        var voce = VociCatalogo.FirstOrDefault(candidata =>
            string.Equals(candidata.Chiave, chiave?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (voce is null)
        {
            stile = default;
            return false;
        }

        stile = voce.Stile;
        return true;
    }

    public static OpzioneStileCedolino Descrivi(StileCedolino stile) =>
        VociCatalogo.FirstOrDefault(voce => voce.Stile == stile)
        ?? throw new ArgumentOutOfRangeException(nameof(stile), stile, "Stile cedolino non definito.");

    public static string Chiave(StileCedolino stile) => Descrivi(stile).Chiave;

    internal static TemaCedolinoPdf Tema(StileCedolino stile) =>
        TemaCedolinoPdf.DaId(Descrivi(stile).TemaId);
}

/// <summary>
/// Palette immutabile condivisa dai renderer. L'avviso di simulazione resta sempre separato
/// dall'accento scelto, così nessun tema può attenuare le diciture di non certificazione.
/// </summary>
internal sealed record TemaCedolinoPdf(
    string Id,
    string Accento,
    string AccentoChiaro,
    string Inchiostro,
    string Secondario,
    string BordoForte,
    string BordoTenue,
    string Fondo,
    string FondoAlternato,
    string Carta,
    string Avviso = "#9A3412",
    string Bianco = "#FFFFFF")
{
    internal static TemaCedolinoPdf DaId(string id) => id switch
    {
        "neutro" => new(id, "#111111", "#B7B7B7", "#000000", "#4A4A4A",
            "#202020", "#8C8C8C", "#E9E9E9", "#F5F5F5", "#FFFFFF"),
        "blu" => new(id, "#12395E", "#8FB4D6", "#0F2338", "#5B7288",
            "#12395E", "#C7D8E6", "#EEF4F8", "#DCE8F2", "#F7FAFC"),
        "verde" => new(id, "#1F5B45", "#9BC7B4", "#173A2D", "#567B6B",
            "#1F5B45", "#C9DFD5", "#EEF6F1", "#DCEDE4", "#F7FAF8"),
        "bordeaux" => new(id, "#7C2D3E", "#D2A0AA", "#421923", "#855B64",
            "#7C2D3E", "#E2C9CE", "#F8F0F2", "#F0DDE1", "#FCF9FA"),
        "antracite" => new(id, "#263238", "#B0BEC5", "#11181C", "#526169",
            "#263238", "#B8C2C7", "#E7ECEE", "#F1F3F4", "#FFFFFF"),
        "viola" => new(id, "#4C1D95", "#B9A4DF", "#2D1855", "#715D91",
            "#4C1D95", "#D8CDEB", "#F2EDFA", "#E5DCF4", "#FBF9FD"),
        "petrolio" => new(id, "#0F6660", "#8FC8C3", "#123E3B", "#527B78",
            "#0F6660", "#C5DFDC", "#EDF6F5", "#D9ECEA", "#F8FBFB"),
        "rame" => new(id, "#9A4F2E", "#D9AD98", "#17233B", "#4F5663",
            "#9A4F2E", "#D7C8C0", "#F1E3DB", "#E8D3C7", "#F7F5F0"),
        "grafite" => new(id, "#334155", "#A7B1BF", "#172033", "#566173",
            "#334155", "#CBD2DC", "#E8EDF2", "#DCE3EA", "#F8FAFC"),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Tema cedolino non definito."),
    };
}
