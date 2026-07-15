using System.Globalization;
using System.Reflection;
using Paghe.Core.AdeTelematico;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Paghe.Documenti;

/// <summary>
/// Compila il modello ufficiale della Certificazione Unica (sintetico, Agenzia delle Entrate
/// 2026) scrivendo i dati sopra il PDF originale incorporato come risorsa: il risultato è il
/// modello ufficiale, non un fac-simile. Compila le pagine del lavoro dipendente: dati
/// anagrafici, parte B dati fiscali, dati previdenziali INPS e annotazioni. Coordinate
/// calibrate sulle caselle del modello (origine in alto a sinistra).
/// Nota: il modello 2026 certifica i redditi 2025; per anni successivi la stampa è una bozza
/// in attesa del modello definitivo dell'anno.
/// </summary>
public static class CuPdfUfficiale
{
    private const string RisorsaModello = "Paghe.Documenti.Modelli.CU_sintetico_2026.pdf";
    private const double AltezzaPagina = 841.89;

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);
    private static string? EurSeDiverso(decimal v) => v == 0m ? null : Eur(v);

    public static byte[] Compila(CertificazioneUnica cu)
    {
        var errori = cu.Valida();
        if (errori.Count > 0)
            throw new InvalidOperationException("CU non compilabile: " + string.Join(" ", errori));

        using var sorgente = Assembly.GetExecutingAssembly().GetManifestResourceStream(RisorsaModello)
            ?? throw new InvalidOperationException($"Risorsa {RisorsaModello} non trovata.");
        using var copia = new MemoryStream();
        sorgente.CopyTo(copia);
        copia.Position = 0;

        var doc = PdfReader.Open(copia, PdfDocumentOpenMode.Modify);

        OverlayModuloUfficiale.Applica(doc, 0, PaginaAnagrafica(cu));
        OverlayModuloUfficiale.Applica(doc, 1, PaginaDatiFiscali(cu));
        OverlayModuloUfficiale.Applica(doc, 4, PaginaPrevidenza(cu));
        OverlayModuloUfficiale.Applica(doc, 6, PaginaAnnotazioni(cu));

        using var uscita = new MemoryStream();
        doc.Save(uscita);
        return uscita.ToArray();
    }

    // ---- Pagina 1 del modello (indice 0): DATI ANAGRAFICI --------------------------------------
    private static OverlayModuloUfficiale PaginaAnagrafica(CertificazioneUnica cu)
    {
        var o = new OverlayModuloUfficiale(AltezzaPagina);
        var s = cu.Sostituto;
        var p = cu.Percipiente;

        // Riquadro "RELATIVA ALL'ANNO" della testata.
        o.Centrato(497, 78.5, cu.AnnoRedditi.ToString());

        // Datore di lavoro / sostituto d'imposta.
        o.Libero(122, 151.5, s.CodiceFiscale, dimensione: 8);
        o.Libero(266, 151.5, s.Denominazione, dimensione: 8, maxLarghezza: 290);
        o.Libero(122, 175.0, s.Comune, dimensione: 8, maxLarghezza: 188);
        o.Libero(317, 175.0, s.Provincia, dimensione: 8);
        o.Libero(345, 175.0, s.Cap, dimensione: 8);
        o.Libero(396, 175.0, s.Indirizzo, dimensione: 8, maxLarghezza: 165);
        o.Libero(122, 199.5, s.Telefono, dimensione: 8, maxLarghezza: 138);
        o.Libero(266, 199.5, s.Email, dimensione: 8, maxLarghezza: 195);
        o.Libero(467, 199.5, s.CodiceAttivita, dimensione: 8, maxLarghezza: 46);

        // Dipendente / percettore.
        o.Libero(122, 222.5, p.CodiceFiscale, dimensione: 8);
        o.Libero(266, 222.5, p.Cognome, dimensione: 8, maxLarghezza: 165);
        o.Libero(438, 222.5, p.Nome, dimensione: 8, maxLarghezza: 120);
        o.Centrato(128, 259.0, p.Sesso, dimensione: 8);
        if (p.DataNascita is { } nascita)
        {
            o.Libero(146, 259.0, nascita.Day.ToString("00"), dimensione: 8);
            o.Libero(170, 259.0, nascita.Month.ToString("00"), dimensione: 8);
            o.Libero(192, 259.0, nascita.Year.ToString("0000"), dimensione: 8);
        }
        o.Libero(230, 259.0, p.ComuneNascita, dimensione: 8, maxLarghezza: 125);
        o.Centrato(370, 259.0, p.ProvinciaNascita, dimensione: 8);

        // Domicilio fiscale all'1/1 dell'anno certificato.
        o.Libero(122, 295.0, p.DomicilioComune, dimensione: 8, maxLarghezza: 295);
        o.Centrato(440, 295.0, p.DomicilioProvincia, dimensione: 8);
        o.Libero(470, 295.0, p.DomicilioCodiceComune, dimensione: 8);

        // Data (di generazione) e firma a stampa del sostituto.
        var oggi = DateTime.Today;
        o.Libero(126, 474.0, oggi.Day.ToString("00"), dimensione: 8);
        o.Libero(151, 474.0, oggi.Month.ToString("00"), dimensione: 8);
        o.Libero(172, 474.0, oggi.Year.ToString("0000"), dimensione: 8);
        o.Libero(338, 474.0, s.Denominazione, dimensione: 8, maxLarghezza: 210);

        return o;
    }

    // ---- Pagina 2 del modello (indice 1): CERTIFICAZIONE LAVORO DIPENDENTE, dati fiscali -------
    private static OverlayModuloUfficiale PaginaDatiFiscali(CertificazioneUnica cu)
    {
        var o = new OverlayModuloUfficiale(AltezzaPagina);
        var f = cu.DatiFiscali;
        Testatina(o, cu);

        // Redditi (punti 1 e 2).
        o.LiberoDestra(222, 110.5, EurSeDiverso(f.RedditoTempoIndeterminato), dimensione: 8);
        o.LiberoDestra(332, 110.5, EurSeDiverso(f.RedditoTempoDeterminato), dimensione: 8);

        // Giorni detrazione (6), data inizio rapporto (8), in forza al 31/12 (10).
        if (f.GiorniDetrazione > 0)
            o.LiberoDestra(272, 152.5, f.GiorniDetrazione.ToString(), dimensione: 8);
        if (f.DataInizioRapporto is { } inizio)
        {
            o.Libero(335, 152.5, inizio.Day.ToString("00"), dimensione: 8);
            o.Libero(359, 152.5, inizio.Month.ToString("00"), dimensione: 8);
            o.Libero(383, 152.5, inizio.Year.ToString("0000"), dimensione: 8);
        }
        if (f.DataCessazione is { } fine)
        {
            o.Libero(421, 152.5, fine.Day.ToString("00"), dimensione: 8);
            o.Libero(445, 152.5, fine.Month.ToString("00"), dimensione: 8);
            o.Libero(469, 152.5, fine.Year.ToString("0000"), dimensione: 8);
        }
        if (f.InForzaAl31Dicembre)
            o.Centrato(511, 152.5, "X", dimensione: 8);

        // Ritenute (21), addizionale regionale (22), saldo addizionale comunale (27).
        o.LiberoDestra(200, 229.5, EurSeDiverso(f.RitenuteIrpef), dimensione: 8);
        o.LiberoDestra(292, 229.5, EurSeDiverso(f.AddizionaleRegionale), dimensione: 8);
        o.LiberoDestra(465, 229.5, EurSeDiverso(f.AddizionaleComunale), dimensione: 8);

        // Imposta lorda (361), detrazioni lavoro dipendente (367), totale detrazioni (374),
        // imposta netta (375).
        o.LiberoDestra(228, 614.0, EurSeDiverso(f.ImpostaLorda), dimensione: 8);
        o.LiberoDestra(450, 609.5, EurSeDiverso(f.DetrazioniLavoroDipendente), dimensione: 8);
        o.LiberoDestra(345, 662.0, EurSeDiverso(f.TotaleDetrazioni), dimensione: 8);
        o.LiberoDestra(450, 662.0, EurSeDiverso(f.ImpostaNetta), dimensione: 8);

        return o;
    }

    // ---- Pagina 5 del modello (indice 4): DATI PREVIDENZIALI INPS ------------------------------
    private static OverlayModuloUfficiale PaginaPrevidenza(CertificazioneUnica cu)
    {
        var o = new OverlayModuloUfficiale(AltezzaPagina);
        var pr = cu.Previdenza;
        Testatina(o, cu);

        o.Libero(122, 87.0, pr.MatricolaAzienda, dimensione: 8, maxLarghezza: 96);
        o.Centrato(240, 87.0, "X", dimensione: 8); // ente: INPS
        o.LiberoDestra(388, 87.0, EurSeDiverso(pr.ImponibilePrevidenziale), dimensione: 8);
        o.LiberoDestra(552, 87.0, EurSeDiverso(pr.ContributiLavoratore), dimensione: 8);

        // Mesi con denuncia UniEmens: "T" se tutto l'anno, altrimenti X sotto i singoli mesi.
        double[] colonneMesi = [149.7, 164.1, 178.5, 192.9, 207.3, 221.7, 236.1, 250.5, 264.9, 279.3, 293.7, 308.1];
        if (pr.MesiRetribuiti.Count == 12)
            o.Centrato(128.5, 119.3, "X", dimensione: 8);
        else
            foreach (int mese in pr.MesiRetribuiti.Where(m => m is >= 1 and <= 12))
                o.Centrato(colonneMesi[mese - 1], 119.3, "X", dimensione: 8);

        return o;
    }

    // ---- Pagina 7 del modello (indice 6): ANNOTAZIONI -------------------------------------------
    private static OverlayModuloUfficiale PaginaAnnotazioni(CertificazioneUnica cu)
    {
        var o = new OverlayModuloUfficiale(AltezzaPagina);
        Testatina(o, cu);

        // A capo automatico: la colonna utile va da x 122 al bordo destro del riquadro (~558).
        const int maxCaratteriRiga = 100; // 436 pt / (0,6 × 7 pt)
        double y = 90.0;
        foreach (var nota in cu.Annotazioni)
        {
            foreach (var riga in SpezzaRighe(nota, maxCaratteriRiga))
            {
                o.Libero(122, y, riga, dimensione: 7);
                y += 11.0;
            }
            y += 2.0;
        }
        return o;
    }

    /// <summary>Testatina delle pagine interne: codice fiscale del percipiente e Mod. N. 1.</summary>
    private static void Testatina(OverlayModuloUfficiale o, CertificazioneUnica cu)
    {
        o.Libero(125, 35.5, cu.Percipiente.CodiceFiscale, dimensione: 8);
        o.Libero(540, 35.5, "1", dimensione: 8);
    }

    /// <summary>Spezza un testo in righe di al più maxCaratteri, senza troncare le parole.</summary>
    private static IEnumerable<string> SpezzaRighe(string testo, int maxCaratteri)
    {
        var riga = new System.Text.StringBuilder();
        foreach (var parola in testo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (riga.Length > 0 && riga.Length + 1 + parola.Length > maxCaratteri)
            {
                yield return riga.ToString();
                riga.Clear();
            }
            if (riga.Length > 0) riga.Append(' ');
            riga.Append(parola);
        }
        if (riga.Length > 0) yield return riga.ToString();
    }
}
