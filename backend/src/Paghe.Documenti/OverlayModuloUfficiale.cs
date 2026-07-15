using System.Text;
using PdfSharp.Pdf;

namespace Paghe.Documenti;

/// <summary>
/// Livello di testo scritto sopra un modello PDF ufficiale (AA7/10, CU, ...). Le coordinate
/// hanno origine in alto a sinistra (come la calibrazione); la conversione a spazio PDF avviene
/// qui. Testo in Courier (font base PDF, nessuna dipendenza da font di sistema); Courier è a
/// passo fisso: larghezza carattere = 0,6 × corpo.
/// </summary>
internal sealed class OverlayModuloUfficiale
{
    private readonly StringBuilder _ops = new();
    private readonly double _altezzaPagina;

    public OverlayModuloUfficiale(double altezzaPagina = 841.89) => _altezzaPagina = altezzaPagina;

    public bool Vuoto => _ops.Length == 0;

    /// <summary>Un carattere centrato in ogni cella della casella a pettine.</summary>
    public OverlayModuloUfficiale Comb(double x0, double passo, int celle, double y, string? testo, int dimensione = 9)
    {
        if (string.IsNullOrWhiteSpace(testo)) return this;
        string t = Normalizza(testo);
        double largheza = 0.6 * dimensione;
        for (int i = 0; i < t.Length && i < celle; i++)
            Scrivi(x0 + passo * i + (passo - largheza) / 2, y, t[i].ToString(), dimensione);
        return this;
    }

    /// <summary>Come Comb ma allineato a destra (importi e codici numerici).</summary>
    public OverlayModuloUfficiale CombDestra(double x0, double passo, int celle, double y, string? testo, int dimensione = 9)
    {
        if (string.IsNullOrWhiteSpace(testo)) return this;
        string t = Normalizza(testo);
        if (t.Length > celle) t = t[^celle..];
        return Comb(x0 + passo * (celle - t.Length), passo, t.Length, y, t, dimensione);
    }

    /// <summary>Testo libero da sinistra, troncato alla larghezza della casella.</summary>
    public OverlayModuloUfficiale Libero(double x, double y, string? testo, int dimensione = 9, double maxLarghezza = 0)
    {
        if (string.IsNullOrWhiteSpace(testo)) return this;
        string t = Normalizza(testo);
        if (maxLarghezza > 0)
        {
            int maxCaratteri = (int)(maxLarghezza / (0.6 * dimensione));
            if (t.Length > maxCaratteri) t = t[..maxCaratteri];
        }
        Scrivi(x, y, t, dimensione);
        return this;
    }

    /// <summary>Testo che termina sul bordo destro della casella (importi nei riquadri aperti).</summary>
    public OverlayModuloUfficiale LiberoDestra(double xDestra, double y, string? testo, int dimensione = 9)
    {
        if (string.IsNullOrWhiteSpace(testo)) return this;
        string t = Normalizza(testo);
        Scrivi(xDestra - t.Length * 0.6 * dimensione, y, t, dimensione);
        return this;
    }

    /// <summary>Testo centrato su un punto (caselle singole tipo codice carica, X di barratura).</summary>
    public OverlayModuloUfficiale Centrato(double xCentro, double y, string? testo, int dimensione = 9)
    {
        if (string.IsNullOrWhiteSpace(testo)) return this;
        string t = Normalizza(testo);
        Scrivi(xCentro - t.Length * 0.6 * dimensione / 2, y, t, dimensione);
        return this;
    }

    private void Scrivi(double x, double yAlto, string testo, int dimensione)
    {
        double y = _altezzaPagina - yAlto;
        _ops.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"BT /FCourPaghe {dimensione} Tf 0 g 1 0 0 1 {x:0.##} {y:0.##} Tm (");
        foreach (char c in testo)
        {
            if (c is '(' or ')' or '\\') _ops.Append('\\');
            _ops.Append(c <= 'ÿ' ? c : '?');
        }
        _ops.Append(") Tj ET\n");
    }

    private static string Normalizza(string testo) => testo.Trim().ToUpperInvariant();

    public byte[] ContenutoStream() => Encoding.Latin1.GetBytes(_ops.ToString());

    /// <summary>Aggiunge il livello di testo alla pagina del modello (registrando il font Courier).</summary>
    public static void Applica(PdfDocument doc, int indicePagina, OverlayModuloUfficiale overlay)
    {
        if (overlay.Vuoto) return;
        var pagina = doc.Pages[indicePagina];

        var font = new PdfDictionary(doc);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", "/Courier");
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        doc.Internals.AddObject(font);

        var risorse = pagina.Elements.GetDictionary("/Resources")
            ?? throw new InvalidOperationException("Pagina del modello senza /Resources.");
        var fonts = risorse.Elements.GetDictionary("/Font");
        if (fonts is null)
        {
            fonts = new PdfDictionary(doc);
            risorse.Elements["/Font"] = fonts;
        }
        fonts.Elements["/FCourPaghe"] = font.Reference;

        var contenuto = pagina.Contents.AppendContent();
        contenuto.CreateStream(overlay.ContenutoStream());
    }
}
