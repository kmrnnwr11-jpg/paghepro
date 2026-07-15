using System.Reflection;
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
        OverlayModuloUfficiale.Applica(doc, 1, CostruisciPagina2(m));

        // Testata codice fiscale sulle pagine successive del modello.
        if (!string.IsNullOrWhiteSpace(m.CodiceFiscale))
            for (int pagina = 2; pagina <= 4; pagina++)
                OverlayModuloUfficiale.Applica(doc, pagina, new OverlayModuloUfficiale(AltezzaPagina)
                    .Comb(224.4, PassoQuadri, 16, 64.0, m.CodiceFiscale));

        using var uscita = new MemoryStream();
        doc.Save(uscita);
        return uscita.ToArray();
    }

    private static OverlayModuloUfficiale CostruisciPagina2(ModuloAa710 m)
    {
        var o = new OverlayModuloUfficiale(AltezzaPagina);
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
    private static void Sede(OverlayModuloUfficiale o, SedeAa710 s, double yIndirizzo, double yScritture, double yRiga2)
    {
        o.Libero(118, yIndirizzo, s.Indirizzo, maxLarghezza: 410);
        if (s.ScrittureContabili) o.Centrato(546.0, yScritture, "X");
        o.Comb(114.0, PassoQuadri, 5, yRiga2, SoloCifre(s.Cap));
        o.Libero(190, yRiga2, s.Comune, maxLarghezza: 330);
        o.Comb(531.6, PassoQuadri, 2, yRiga2, s.Provincia);
    }

    private static string? SoloCifre(string? testo) =>
        testo is null ? null : new string(testo.Where(char.IsDigit).ToArray());
}
