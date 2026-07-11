using System.Reflection;
using System.Text;
using Paghe.Core.AdeTelematico;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Paghe.Documenti;

/// <summary>
/// Compila il modello ufficiale AA7/10 (Agenzia delle Entrate, versione 2025) scrivendo i dati
/// sopra il PDF originale, incorporato come risorsa: il risultato è il modello ufficiale, non un
/// fac-simile. Coordinate calibrate sulle caselle del modello (origine in alto a sinistra);
/// testo in Courier (font base PDF, nessuna dipendenza da font di sistema).
/// </summary>
public static class Aa710PdfUfficiale
{
    private const string RisorsaModello = "Paghe.Documenti.Modelli.AA7_10_modello_2025.pdf";
    private const double AltezzaPagina = 841.89;
    private const double PassoQuadri = 14.4;   // celle dei quadri A/B
    private const double PassoQuadroC = 10.8;  // celle della riga nascita del quadro C

    public static byte[] Compila(ModuloAa710 m)
    {
        var errori = m.Valida();
        if (errori.Count > 0)
            throw new InvalidOperationException("Modulo AA7/10 non compilabile: " + string.Join(" ", errori));

        using var sorgente = Assembly.GetExecutingAssembly().GetManifestResourceStream(RisorsaModello)
            ?? throw new InvalidOperationException($"Risorsa {RisorsaModello} non trovata.");
        using var copia = new MemoryStream();
        sorgente.CopyTo(copia);
        copia.Position = 0;

        var doc = PdfReader.Open(copia, PdfDocumentOpenMode.Modify);

        // La pagina 2 (indice 1) contiene testata + quadri A, B e C.
        AggiungiTesto(doc, 1, CostruisciPagina2(m));

        // Testata codice fiscale sulle pagine successive del modello.
        if (!string.IsNullOrWhiteSpace(m.CodiceFiscale))
            for (int pagina = 2; pagina <= 4; pagina++)
                AggiungiTesto(doc, pagina, new Overlay()
                    .Comb(224.4, PassoQuadri, 16, 64.0, m.CodiceFiscale));

        using var uscita = new MemoryStream();
        doc.Save(uscita);
        return uscita.ToArray();
    }

    private static Overlay CostruisciPagina2(ModuloAa710 m)
    {
        var o = new Overlay();
        string data = m.Data.ToString("ddMMyyyy");

        o.Comb(224.4, PassoQuadri, 16, 142.0, m.CodiceFiscale);

        // ---- Quadro A: si compila solo la riga del tipo scelto ----
        switch (m.TipoDichiarazione)
        {
            case 1:
                o.Comb(445.0, PassoQuadri, 8, 192.0, data);
                if (m.EstremiRegistrazione is { } er)
                {
                    o.Comb(243.6, PassoQuadri, 8, 218.0, er.Data?.ToString("ddMMyyyy"));
                    o.Comb(358.8, PassoQuadri, 3, 218.0, er.Ufficio);
                    o.Comb(402.0, PassoQuadri, 6, 218.0, er.Numero);
                    o.Comb(488.4, PassoQuadri, 3, 218.0, er.SottoNumero);
                    o.Comb(531.6, PassoQuadri, 2, 218.0, er.Serie);
                }
                break;
            case 2:
                o.Comb(279.6, PassoQuadri, 11, 254.0, m.CodiceFiscale);
                o.Comb(445.0, PassoQuadri, 8, 254.0, data);
                break;
            case 3:
                o.Comb(279.6, PassoQuadri, 11, 290.0, m.PartitaIva);
                o.Comb(445.0, PassoQuadri, 8, 290.0, data);
                break;
            case 4:
                o.Comb(279.6, PassoQuadri, 11, 326.0, m.PartitaIva);
                o.Comb(445.0, PassoQuadri, 8, 326.0, data);
                break;
        }

        // ---- Quadro B ----
        o.Libero(118, 396.5, m.Denominazione, maxLarghezza: 442);
        o.CombDestra(114.0, PassoQuadri, 2, 421.0, m.NaturaGiuridica);
        o.Libero(146, 421.0, m.Sigla, maxLarghezza: 240);
        Sede(o, m.SedeLegale, 456.5, 453.5, 481.0);
        if (m.DomicilioFiscale is { } df) Sede(o, df, 516.5, 513.5, 541.0);
        o.Comb(114.0, PassoQuadri, 6, 577.0, SoloCifre(m.CodiceAttivita));
        o.Libero(204, 577.0, m.DescrizioneAttivita, maxLarghezza: 350);
        if (m.VolumeAffariPresunto is { } volume)
            o.CombDestra(111.1, PassoQuadri, 11, 601.0, decimal.Truncate(volume).ToString("0"));
        if (m.LuogoEsercizio is { } le) Sede(o, le, 624.5, 621.5, 649.0);

        // ---- Quadro C ----
        o.Libero(118, 745.0, m.Rappresentante.Cognome, dimensione: 8, maxLarghezza: 180);
        o.Libero(306, 745.0, m.Rappresentante.Nome, dimensione: 8, maxLarghezza: 105);
        o.Centrato(425.1, 740.9, m.Rappresentante.CodiceCarica, dimensione: 8);
        o.Comb(442.05, 14.75, 8, 745.0, m.Rappresentante.DataInizioProcedimento?.ToString("ddMMyyyy"), dimensione: 8);
        o.Libero(118, 771.5, m.Rappresentante.ComuneNascita, dimensione: 8, maxLarghezza: 160);
        o.Comb(279.7, PassoQuadroC, 2, 771.5, m.Rappresentante.ProvinciaNascita, dimensione: 8);
        o.Comb(301.3, PassoQuadroC, 8, 771.5, m.Rappresentante.DataNascita?.ToString("ddMMyyyy"), dimensione: 8);
        o.Comb(387.7, PassoQuadroC, 16, 771.5, m.Rappresentante.CodiceFiscale, dimensione: 8);

        return o;
    }

    /// <summary>Blocco sede del quadro B: indirizzo, casella scritture contabili, CAP/comune/provincia.</summary>
    private static void Sede(Overlay o, SedeAa710 s, double yIndirizzo, double yScritture, double yRiga2)
    {
        o.Libero(118, yIndirizzo, s.Indirizzo, maxLarghezza: 410);
        if (s.ScrittureContabili) o.Centrato(546.0, yScritture, "X");
        o.Comb(114.0, PassoQuadri, 5, yRiga2, SoloCifre(s.Cap));
        o.Libero(190, yRiga2, s.Comune, maxLarghezza: 330);
        o.Comb(531.6, PassoQuadri, 2, yRiga2, s.Provincia);
    }

    private static string? SoloCifre(string? testo) =>
        testo is null ? null : new string(testo.Where(char.IsDigit).ToArray());

    private static void AggiungiTesto(PdfDocument doc, int indicePagina, Overlay overlay)
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

    /// <summary>
    /// Costruisce il contenuto PDF del livello di testo. Le coordinate d'ingresso hanno origine
    /// in alto a sinistra (come la calibrazione); la conversione a spazio PDF avviene qui.
    /// Courier è a passo fisso: larghezza carattere = 0,6 × corpo.
    /// </summary>
    private sealed class Overlay
    {
        private readonly StringBuilder _ops = new();
        public bool Vuoto => _ops.Length == 0;

        /// <summary>Un carattere centrato in ogni cella della casella a pettine.</summary>
        public Overlay Comb(double x0, double passo, int celle, double y, string? testo, int dimensione = 9)
        {
            if (string.IsNullOrWhiteSpace(testo)) return this;
            string t = Normalizza(testo);
            double largheza = 0.6 * dimensione;
            for (int i = 0; i < t.Length && i < celle; i++)
                Scrivi(x0 + passo * i + (passo - largheza) / 2, y, t[i].ToString(), dimensione);
            return this;
        }

        /// <summary>Come Comb ma allineato a destra (importi e codici numerici).</summary>
        public Overlay CombDestra(double x0, double passo, int celle, double y, string? testo, int dimensione = 9)
        {
            if (string.IsNullOrWhiteSpace(testo)) return this;
            string t = Normalizza(testo);
            if (t.Length > celle) t = t[^celle..];
            return Comb(x0 + passo * (celle - t.Length), passo, t.Length, y, t, dimensione);
        }

        /// <summary>Testo libero da sinistra, troncato alla larghezza della casella.</summary>
        public Overlay Libero(double x, double y, string? testo, int dimensione = 9, double maxLarghezza = 0)
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

        /// <summary>Testo centrato su un punto (caselle singole tipo codice carica, X di barratura).</summary>
        public Overlay Centrato(double xCentro, double y, string? testo, int dimensione = 9)
        {
            if (string.IsNullOrWhiteSpace(testo)) return this;
            string t = Normalizza(testo);
            Scrivi(xCentro - t.Length * 0.6 * dimensione / 2, y, t, dimensione);
            return this;
        }

        private void Scrivi(double x, double yAlto, string testo, int dimensione)
        {
            double y = AltezzaPagina - yAlto;
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
    }
}
