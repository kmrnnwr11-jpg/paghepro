using System.Globalization;
using Paghe.Core.Calcolo;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Riepilogo mensile aziendale delle buste agricole: una pagina A4 in bianco e nero con
/// una riga per lavoratore, i totali del mese e l'elenco completo delle segnalazioni del
/// validatore. Si genera insieme alle buste; non è un documento fiscale.
/// </summary>
public static class RiepilogoAgricoloPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private const string Nero = "#000000";
    private const string Grigio = "#4A4A4A";
    private const string Bordo = "#B5B5B5";
    private const string Fondo = "#F4F4F4";

    public static byte[] Genera(RiepilogoAgricoloMensile riepilogo, string ragioneSociale)
    {
        ArgumentNullException.ThrowIfNull(riepilogo);
        DocumentiConfig.Inizializza();
        return Document.Create(doc => doc.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.MarginHorizontal(1.1f, Unit.Centimetre);
            pagina.MarginVertical(0.9f, Unit.Centimetre);
            pagina.DefaultTextStyle(s => s.FontFamily("Lato").FontSize(7f).FontColor(Nero));

            pagina.Content().Column(colonna =>
            {
                colonna.Spacing(6);
                colonna.Item().Border(0.9f).BorderColor(Nero).Padding(7).Row(riga =>
                {
                    riga.RelativeItem().Column(c =>
                    {
                        c.Item().Text(ragioneSociale.ToUpperInvariant()).FontSize(11).SemiBold();
                        c.Item().Text("Riepilogo mensile buste agricole OTD").FontSize(7).FontColor(Grigio);
                    });
                    riga.ConstantItem(170).AlignRight().Column(c =>
                    {
                        c.Item().Text($"{Mese(riepilogo.Mese).ToUpperInvariant()} {riepilogo.Anno}")
                            .FontSize(11).SemiBold();
                        c.Item().Text($"Elaborazione: {riepilogo.StatoElaborazione}"
                                + (riepilogo.ValidataDa is { } chi ? $" ({chi})" : ""))
                            .FontSize(7).FontColor(Grigio);
                    });
                });

                colonna.Item().Table(tabella =>
                {
                    tabella.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                    });
                    tabella.Header(h =>
                    {
                        foreach (var titolo in new[]
                        {
                            "Lavoratore", "Ore", "GG", "Lordo", "Trasferte",
                            "Trattenute", "C/lav.", "Arrot.", "Netto",
                        })
                            h.Cell().Background(Fondo).BorderBottom(0.8f).BorderColor(Nero)
                                .PaddingVertical(3).PaddingHorizontal(3)
                                .Text(titolo.ToUpperInvariant()).FontSize(5.6f).SemiBold();
                    });
                    bool alterna = false;
                    foreach (var r in riepilogo.Righe)
                    {
                        string sfondo = alterna ? Fondo : "#FFFFFF";
                        alterna = !alterna;
                        Cella(tabella, r.Nominativo, sfondo);
                        Cella(tabella, r.Ore.ToString("0.##", It), sfondo, destra: true);
                        Cella(tabella, r.Giornate.ToString(It), sfondo, destra: true);
                        Cella(tabella, Euro(r.Lordo), sfondo, destra: true);
                        Cella(tabella, Euro(r.Trasferte), sfondo, destra: true);
                        Cella(tabella, Euro(r.Trattenute), sfondo, destra: true);
                        Cella(tabella, Euro(r.ContributiDipendente), sfondo, destra: true);
                        Cella(tabella, Euro(r.Arrotondamento), sfondo, destra: true);
                        Cella(tabella, Euro(r.Netto), sfondo, destra: true, grassetto: true);
                    }
                    Cella(tabella, "TOTALE", Fondo, grassetto: true);
                    Cella(tabella, riepilogo.TotaleOre.ToString("0.##", It), Fondo, destra: true, grassetto: true);
                    Cella(tabella, riepilogo.TotaleGiornate.ToString(It), Fondo, destra: true, grassetto: true);
                    Cella(tabella, Euro(riepilogo.TotaleLordo), Fondo, destra: true, grassetto: true);
                    Cella(tabella, Euro(riepilogo.TotaleTrasferte), Fondo, destra: true, grassetto: true);
                    Cella(tabella, Euro(riepilogo.TotaleTrattenute), Fondo, destra: true, grassetto: true);
                    Cella(tabella, Euro(riepilogo.TotaleContributiDipendente), Fondo, destra: true, grassetto: true);
                    Cella(tabella, Euro(riepilogo.TotaleArrotondamenti), Fondo, destra: true, grassetto: true);
                    Cella(tabella, Euro(riepilogo.TotaleNetto), Fondo, destra: true, grassetto: true);
                });

                var tutte = riepilogo.TutteLeSegnalazioni.ToList();
                if (tutte.Count > 0)
                    colonna.Item().Border(0.7f).BorderColor(Nero)
                        .PaddingVertical(5).PaddingHorizontal(7).Column(c =>
                    {
                        c.Item().Text($"SEGNALAZIONI DEL VALIDATORE — {riepilogo.Errori} ERRORI, "
                                + $"{riepilogo.Avvisi} AVVISI")
                            .FontSize(6.4f).SemiBold();
                        // Prima gli errori delle singole buste, poi gli avvisi, in coda i
                        // controlli d'insieme: l'ordine di lettura è l'ordine di gravità.
                        foreach (var s in tutte
                            .OrderBy(s => s.Livello == LivelloSegnalazione.Errore ? 0 : 1))
                            c.Item().PaddingTop(1.5f).Text(t =>
                            {
                                t.Span($"{s.Livello} [{s.Codice}]  ").FontSize(6f).SemiBold();
                                t.Span(s.Messaggio).FontSize(6f).FontColor(Grigio);
                            });
                    });
            });

            pagina.Footer().Row(riga =>
            {
                riga.RelativeItem().Text(
                        "Riepilogo di prospetti paga di simulazione - non costituisce LUL, "
                        + "UniEmens/PosAgri o F24.")
                    .FontSize(6).SemiBold();
                riga.ConstantItem(80).AlignRight()
                    .DefaultTextStyle(s => s.FontSize(6).FontColor(Grigio)).Text(t =>
                {
                    t.Span("Pagina ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Cella(
        TableDescriptor tabella, string testo, string sfondo,
        bool destra = false, bool grassetto = false)
    {
        var contenuto = tabella.Cell().Background(sfondo)
            .BorderBottom(0.35f).BorderColor(Bordo)
            .PaddingVertical(2.6f).PaddingHorizontal(3);
        if (destra) contenuto = contenuto.AlignRight();
        var span = contenuto.Text(testo).FontSize(6.6f);
        if (grassetto) span.SemiBold();
    }

    private static string Euro(decimal valore) => valore.ToString("N2", It);
    private static string Mese(int mese) =>
        mese is >= 1 and <= 12 ? It.DateTimeFormat.GetMonthName(mese) : $"Mese {mese}";
}
