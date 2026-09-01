using System.Globalization;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Prospetto A4 dedicato agli operai agricoli OTD. Non passa dal presentatore dei cedolini
/// ordinari: le righe figurative agricole (giornate INPS, contributi c/azienda e arrotondamenti)
/// sono dati essenziali e non devono essere scartate o confuse con ferie, ROL e TFR ordinari.
/// </summary>
public static class CedolinoAgricoloPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private const string Verde = "#1F5B45";
    private const string VerdeChiaro = "#DCEDE5";
    private const string Testo = "#17352B";
    private const string Secondario = "#62766E";
    private const string Bordo = "#C9DAD2";
    private const string Fondo = "#F4F8F6";
    private const string Avviso = "#A33A16";
    private const string Bianco = "#FFFFFF";

    public static byte[] Genera(
        Cedolino cedolino,
        IntestazioneCedolino intestazione,
        RapportoAgricolo rapporto,
        CartellinoAgricolo cartellino,
        ParametriAgricoli parametri,
        string fontFamily = "Lato",
        string? dicituraStudio = null)
    {
        ArgumentNullException.ThrowIfNull(cedolino);
        ArgumentNullException.ThrowIfNull(intestazione);
        ArgumentNullException.ThrowIfNull(rapporto);
        ArgumentNullException.ThrowIfNull(cartellino);
        ArgumentNullException.ThrowIfNull(parametri);

        DocumentiConfig.Inizializza();
        return Document.Create(documento => documento.Page(pagina =>
            Pagina(pagina, cedolino, intestazione, rapporto, cartellino, parametri, fontFamily, dicituraStudio))).GeneratePdf();
    }

    private static void Pagina(
        PageDescriptor pagina,
        Cedolino cedolino,
        IntestazioneCedolino intestazione,
        RapportoAgricolo rapporto,
        CartellinoAgricolo cartellino,
        ParametriAgricoli parametri,
        string fontFamily,
        string? dicituraStudio)
    {
        pagina.Size(PageSizes.A4);
        pagina.MarginHorizontal(0.9f, Unit.Centimetre);
        pagina.MarginTop(0.8f, Unit.Centimetre);
        pagina.MarginBottom(0.8f, Unit.Centimetre);
        pagina.PageColor(Bianco);
        pagina.DefaultTextStyle(stile => stile.FontFamily(fontFamily).FontSize(7f).FontColor(Testo));

        pagina.Content().Column(colonna =>
        {
            colonna.Spacing(5);
            colonna.Item().Element(c => Testata(c, cedolino, intestazione));
            colonna.Item().Element(c => DatiRapporto(c, intestazione, rapporto, parametri));
            colonna.Item().Element(c => Riepilogo(c, cedolino, cartellino));
            colonna.Item().Element(c => Tariffa(c, rapporto, parametri));
            colonna.Item().Element(c => Voci(c, cedolino));
            colonna.Item().ShowEntire().Element(c => Quadri(c, cedolino, rapporto, cartellino, parametri));
            colonna.Item().ShowEntire().Element(c => NotaAgricola(c, parametri));
            colonna.Item().ShowEntire().Element(c => Fonte(c, parametri.Fonte));
        });

        pagina.Footer().Row(riga =>
        {
            riga.RelativeItem().Text("BOZZA DA VALIDARE - non costituisce LUL, UniEmens/PosAgri o F24.")
                .FontSize(6.2f).SemiBold().FontColor(Avviso);
            if (!string.IsNullOrWhiteSpace(dicituraStudio))
                riga.RelativeItem().AlignCenter().Text(dicituraStudio)
                    .FontSize(6f).FontColor(Secondario);
            riga.ConstantItem(90).AlignRight()
                .DefaultTextStyle(stile => stile.FontSize(6).FontColor(Secondario))
                .Text(testo =>
            {
                testo.Span("Pagina ");
                testo.CurrentPageNumber();
                testo.Span(" / ");
                testo.TotalPages();
            });
        });
    }

    private static void Testata(
        IContainer contenitore,
        Cedolino cedolino,
        IntestazioneCedolino intestazione)
    {
        contenitore.Border(0.9f).BorderColor(Verde).Row(riga =>
        {
            riga.RelativeItem().Padding(7).Row(azienda =>
            {
                if (intestazione.Logo is { Length: > 0 } logo)
                    azienda.ConstantItem(58).Height(38).PaddingRight(8).Image(logo).FitArea();
                azienda.RelativeItem().Column(colonna =>
                {
                    colonna.Item().Text(Valore(intestazione.Azienda).ToUpperInvariant())
                        .FontSize(12).SemiBold().FontColor(Testo);
                    colonna.Item().Text(Unisci(intestazione.AziendaIndirizzo, intestazione.AziendaComune))
                        .FontSize(6.4f).FontColor(Secondario);
                    colonna.Item().PaddingTop(3)
                        .DefaultTextStyle(stile => stile.FontSize(6.1f).FontColor(Secondario))
                        .Text(testo =>
                    {
                        testo.Span("C.F. ").SemiBold();
                        testo.Span(Valore(intestazione.CodiceFiscaleAzienda));
                        testo.Span("   P.IVA ").SemiBold();
                        testo.Span(Valore(intestazione.PartitaIva));
                    });
                });
            });

            riga.ConstantItem(205).BorderLeft(0.9f).BorderColor(Verde).Padding(7).Column(destra =>
            {
                destra.Item().Text("BUSTA PAGA AGRICOLA")
                    .FontSize(12).SemiBold().FontColor(Verde).LetterSpacing(0.08f);
                destra.Item().PaddingTop(2).Text("OPERAIO AGRICOLO OTD")
                    .FontSize(6.5f).SemiBold().FontColor(Secondario);
                destra.Item().PaddingTop(6).Text($"{Mese(cedolino.Mese).ToUpperInvariant()} {cedolino.Anno}")
                    .FontSize(13).SemiBold().FontColor(Testo);
                destra.Item().Text("Prospetto retributivo mensile - non costituisce LUL")
                    .FontSize(5.8f).FontColor(Secondario);
            });
        });
    }

    private static void DatiRapporto(
        IContainer contenitore,
        IntestazioneCedolino intestazione,
        RapportoAgricolo rapporto,
        ParametriAgricoli parametri)
    {
        contenitore.Border(0.8f).BorderColor(Verde).Column(colonna =>
        {
            colonna.Item().Element(c => Titolo(c, "01", "Lavoratore e inquadramento"));
            colonna.Item().Row(riga =>
            {
                Campo(riga, "Cognome e nome", intestazione.Dipendente, 2.1f, evidenza: true);
                Campo(riga, "Codice fiscale", intestazione.CodiceFiscaleDipendente, 1.55f);
                Campo(riga, "Matricola", intestazione.Matricola, 1f);
                Campo(riga, "Data assunzione", intestazione.DataAssunzione, 1.15f);
                Campo(riga, "Data cessazione", intestazione.DataCessazione, 1.15f, ultimo: true);
            });
            colonna.Item().BorderTop(0.5f).BorderColor(Bordo).Row(riga =>
            {
                Campo(riga, "Tipo manodopera", Sigla(rapporto.Tipo), 1.1f, evidenza: true);
                Campo(riga, "CPL provincia", parametri.Provincia, 1.1f);
                Campo(riga, "Livello tariffa", rapporto.Livello, 1.5f);
                Campo(riga, "Qualifica", intestazione.Qualifica ?? "Operaio agricolo", 1.7f);
                Campo(riga, "CCNL", intestazione.Ccnl ?? "Operai agricoli e florovivaisti", 2.5f, ultimo: true);
            });
        });
    }

    private static void Riepilogo(
        IContainer contenitore,
        Cedolino cedolino,
        CartellinoAgricolo cartellino)
    {
        var dati = new (string Etichetta, string Valore, string Nota)[]
        {
            ("Netto in busta", Euro(cedolino.NettoInBusta), "da pagare nel mese"),
            ("Lordo", Euro(cedolino.TotaleCompetenze), "competenze"),
            ("Ore lavorate", Numero(cartellino.OreOrdinarie), "ore effettive"),
            ("Giornate INPS", cartellino.GiornateLavorate.ToString(It), "dato distinto dalle ore"),
            ("Trattenute", Euro(cedolino.TotaleTrattenute), "contributi e imposte"),
        };

        contenitore.Border(0.8f).BorderColor(Verde).Background(Fondo).Row(riga =>
        {
            for (int i = 0; i < dati.Length; i++)
            {
                var box = riga.RelativeItem();
                if (i < dati.Length - 1) box = box.BorderRight(0.5f).BorderColor(Bordo);
                var dato = dati[i];
                box.PaddingVertical(5).PaddingHorizontal(6).Column(colonna =>
                {
                    colonna.Item().Text(dato.Etichetta.ToUpperInvariant())
                        .FontSize(5.6f).SemiBold().FontColor(Secondario);
                    colonna.Item().PaddingTop(1).Text(dato.Valore)
                        .FontSize(12).SemiBold().FontColor(Verde);
                    colonna.Item().Text(dato.Nota).FontSize(5.6f).FontColor(Secondario);
                });
            }
        });
    }

    private static void Tariffa(
        IContainer contenitore,
        RapportoAgricolo rapporto,
        ParametriAgricoli parametri)
    {
        var tariffa = parametri.Tariffa(rapporto.Livello);
        contenitore.Border(0.8f).BorderColor(Verde).Column(colonna =>
        {
            colonna.Item().Element(c => Titolo(c, "02", "Tariffa del contratto provinciale"));
            colonna.Item().Row(riga =>
            {
                CampoNumerico(riga, "Salario provinciale / ora", tariffa.SalarioProvincialeOrario, 1f, 5);
                CampoNumerico(riga, "Terzo elemento / ora", tariffa.TerzoElementoOrario, 1f, 5);
                CampoNumerico(riga, "Paga oraria complessiva", tariffa.PagaOraria, 1f, 5, ultimo: true, evidenza: true);
            });
        });
    }

    private static void Voci(IContainer contenitore, Cedolino cedolino)
    {
        contenitore.Border(0.8f).BorderColor(Verde).Column(colonna =>
        {
            colonna.Item().Element(c => Titolo(c, "03", "Sviluppo delle voci"));
            colonna.Item().Table(tabella =>
            {
                tabella.ColumnsDefinition(colonne =>
                {
                    colonne.RelativeColumn(1.6f);
                    colonne.RelativeColumn(3.85f);
                    colonne.RelativeColumn(1.05f);
                    colonne.RelativeColumn(1.25f);
                    colonne.RelativeColumn(1.35f);
                    colonne.RelativeColumn(1.35f);
                    colonne.RelativeColumn(1.35f);
                });
                tabella.Header(header =>
                {
                    IntestazioneTabella(header, "Codice");
                    IntestazioneTabella(header, "Descrizione");
                    IntestazioneTabella(header, "Quantita", destra: true);
                    IntestazioneTabella(header, "Base", destra: true);
                    IntestazioneTabella(header, "Competenze", destra: true);
                    IntestazioneTabella(header, "Trattenute", destra: true);
                    IntestazioneTabella(header, "Informativo", destra: true, ultimo: true);
                });

                for (int i = 0; i < cedolino.Righe.Count; i++)
                {
                    var voce = cedolino.Righe[i];
                    string sfondo = i % 2 == 0 ? Bianco : Fondo;
                    Cella(tabella, voce.Codice, sfondo, piccolo: true);
                    Cella(tabella, voce.Descrizione, sfondo);
                    Cella(tabella, voce.Quantita is null ? "" : Numero(voce.Quantita.Value), sfondo, destra: true);
                    Cella(tabella, voce.Base is null
                        ? ""
                        : Numero(voce.Base.Value,
                            string.Equals(voce.Codice, "AGR_ORD", StringComparison.OrdinalIgnoreCase) ? 5 : 2),
                        sfondo, destra: true);
                    Cella(tabella, voce.Tipo == TipoVoce.Competenza ? Euro(voce.Importo) : "", sfondo, destra: true);
                    Cella(tabella, voce.Tipo == TipoVoce.Trattenuta ? Euro(Math.Abs(voce.Importo)) : "", sfondo, destra: true);
                    Cella(tabella, voce.Tipo == TipoVoce.Figurativa && voce.Importo != 0m
                        ? ImportoInformativo(voce) : "", sfondo, destra: true, ultimo: true);
                }
            });
        });
    }

    private static void Quadri(
        IContainer contenitore,
        Cedolino cedolino,
        RapportoAgricolo rapporto,
        CartellinoAgricolo cartellino,
        ParametriAgricoli parametri)
    {
        contenitore.Row(riga =>
        {
            Quadro(riga, "04", "Previdenziale", new[]
            {
                ("Imponibile contributivo", Euro(cedolino.ImponibileInps)),
                ("Contributi c/lavoratore", Euro(cedolino.ContributiDipendente)),
                ("Aliquota totale c/lavoratore", Percentuale(parametri.AliquotaTotaleDipendente)),
                ("Giornate lavorate", cartellino.GiornateLavorate.ToString(It)),
            });
            riga.ConstantItem(5);
            Quadro(riga, "05", "Fiscale", new[]
            {
                ("Imponibile IRPEF", Euro(cedolino.ImponibileIrpef)),
                ("Reddito annuo presunto", rapporto.RedditoPresuntoAnnuo is { } reddito
                    ? Euro(reddito)
                    : "Non indicato"),
                ("IRPEF lorda", Euro(cedolino.IrpefLorda)),
                ("Giorni di detrazione", cartellino.GiorniDetrazione.ToString(It)),
                ("Detrazioni gia godute", Euro(cartellino.DetrazioniGiaGoduteAnno)),
                ("Detrazioni del mese", Euro(cedolino.DetrazioniLavoroDipendente)),
                ("Contributi deducibili", Percentuale(parametri.AliquotaContributiDeducibili)),
                ("IRPEF netta", Euro(cedolino.IrpefNetta)),
            });
            riga.ConstantItem(5);
            Quadro(riga, "06", "Totali", new[]
            {
                ("Totale competenze", Euro(cedolino.TotaleCompetenze)),
                ("Totale trattenute", Euro(cedolino.TotaleTrattenute)),
                ("Netto esatto/arrotondato", Euro(cedolino.NettoInBusta)),
                ("Lordo + oneri datoriali noti *", Euro(cedolino.CostoAzienda)),
            });
        });
    }

    private static void NotaAgricola(IContainer contenitore, ParametriAgricoli parametri) =>
        contenitore.Border(0.7f).BorderColor(Bordo).Background(VerdeChiaro)
            .PaddingVertical(5).PaddingHorizontal(7).Column(colonna =>
            {
                colonna.Item().Text("COME LEGGERE QUESTA BUSTA AGRICOLA")
                    .FontSize(6.2f).SemiBold().FontColor(Verde);
                colonna.Item().PaddingTop(2).Text(
                    "Le ore effettive determinano la retribuzione; le giornate sono un dato distinto "
                    + "destinato alla denuncia UniEmens/PosAgri. Il terzo elemento e gia compreso nella "
                    + "paga oraria. I contributi definitivi a carico dell'azienda sono calcolati "
                    + "dall'INPS dopo la denuncia e non sono certificati da questo prospetto. "
                    + $"La quota deducibile del {Percentuale(parametri.AliquotaContributiDeducibili)} "
                    + "e il livello tariffario osservato devono essere "
                    + "confermati dal professionista.")
                    .FontSize(6.2f).FontColor(Testo);
            });

    private static void Fonte(IContainer contenitore, string fonte) =>
        contenitore.Border(0.7f).BorderColor(Bordo).PaddingVertical(4).PaddingHorizontal(7)
            .DefaultTextStyle(stile => stile.FontSize(6f))
            .Text(testo =>
            {
                testo.Span("FONTE TABELLA APPLICATA: ").SemiBold().FontColor(Verde);
                testo.Span(Valore(fonte)).FontColor(Testo);
            });

    private static void Titolo(IContainer contenitore, string numero, string titolo) =>
        contenitore.Background(Verde).PaddingVertical(2.7f).PaddingHorizontal(6)
            .DefaultTextStyle(stile => stile.FontSize(6.4f))
            .Text(testo =>
            {
                testo.Span($"{numero}   ").FontColor("#A9D2BE");
                testo.Span(titolo.ToUpperInvariant()).SemiBold().FontColor(Bianco).LetterSpacing(0.08f);
            });

    private static void Campo(
        RowDescriptor riga,
        string etichetta,
        string? valore,
        float peso,
        bool ultimo = false,
        bool evidenza = false)
    {
        var cella = riga.RelativeItem(peso);
        if (!ultimo) cella = cella.BorderRight(0.5f).BorderColor(Bordo);
        cella.Background(evidenza ? Fondo : Bianco).PaddingVertical(4).PaddingHorizontal(6).Column(colonna =>
        {
            colonna.Item().Text(etichetta.ToUpperInvariant()).FontSize(5.4f).SemiBold().FontColor(Secondario);
            colonna.Item().Text(Valore(valore)).FontSize(7.3f).SemiBold().FontColor(Testo);
        });
    }

    private static void CampoNumerico(
        RowDescriptor riga,
        string etichetta,
        decimal valore,
        float peso,
        int decimali,
        bool ultimo = false,
        bool evidenza = false) =>
        Campo(riga, etichetta, $"{Numero(valore, decimali)} EUR", peso, ultimo, evidenza);

    private static void Quadro(
        RowDescriptor riga,
        string numero,
        string titolo,
        IReadOnlyList<(string Etichetta, string Valore)> dati)
    {
        riga.RelativeItem().Border(0.8f).BorderColor(Verde).Column(colonna =>
        {
            colonna.Item().Element(c => Titolo(c, numero, titolo));
            for (int i = 0; i < dati.Count; i++)
            {
                var dato = dati[i];
                var box = colonna.Item();
                if (i > 0) box = box.BorderTop(0.4f).BorderColor(Bordo);
                box.PaddingVertical(2.5f).PaddingHorizontal(6).Row(valori =>
                {
                    valori.RelativeItem().Text(dato.Etichetta).FontSize(6.2f).FontColor(Secondario);
                    valori.ConstantItem(86).AlignRight().Text(dato.Valore).FontSize(6.8f).SemiBold().FontColor(Testo);
                });
            }
        });
    }

    private static void IntestazioneTabella(
        TableCellDescriptor cella,
        string testo,
        bool destra = false,
        bool ultimo = false)
    {
        var interno = cella.Cell().Background(Fondo).BorderBottom(0.7f).BorderColor(Verde);
        if (!ultimo) interno = interno.BorderRight(0.4f).BorderColor(Bordo);
        var contenuto = interno.PaddingVertical(2.8f).PaddingHorizontal(4);
        if (destra) contenuto = contenuto.AlignRight();
        contenuto.Text(testo.ToUpperInvariant()).FontSize(5.4f).SemiBold().FontColor(Verde);
    }

    private static void Cella(
        TableDescriptor tabella,
        string testo,
        string sfondo,
        bool destra = false,
        bool piccolo = false,
        bool ultimo = false)
    {
        var cella = tabella.Cell().Background(sfondo).BorderBottom(0.35f).BorderColor(Bordo);
        if (!ultimo) cella = cella.BorderRight(0.35f).BorderColor(Bordo);
        var contenuto = cella.PaddingVertical(2.4f).PaddingHorizontal(4);
        if (destra) contenuto = contenuto.AlignRight();
        contenuto.Text(testo).FontSize(piccolo ? 5.7f : 6.4f).FontColor(Testo);
    }

    private static string Euro(decimal valore) => valore.ToString("N2", It);
    private static string Numero(decimal valore, int decimali = 2) =>
        valore.ToString($"N{decimali}", It);
    private static string Percentuale(decimal valore) => valore.ToString("P2", It);
    private static string ImportoInformativo(CedolinoRiga voce) =>
        string.Equals(voce.Codice, "AGR_ARROT", StringComparison.OrdinalIgnoreCase)
            ? voce.Importo > 0m
                ? $"+{Euro(voce.Importo)}"
                : $"-{Euro(Math.Abs(voce.Importo))}"
            : Euro(Math.Abs(voce.Importo));
    private static string Mese(int mese) =>
        mese is >= 1 and <= 12 ? It.DateTimeFormat.GetMonthName(mese) : $"Mese {mese}";
    private static string Sigla(TipoOperaioAgricolo tipo) =>
        tipo == TipoOperaioAgricolo.Determinato ? "OTD - tempo determinato" : "OTI - tempo indeterminato";
    private static string Valore(string? valore) => string.IsNullOrWhiteSpace(valore) ? "-" : valore.Trim();
    private static string Unisci(params string?[] parti) =>
        string.Join(" - ", parti.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}
