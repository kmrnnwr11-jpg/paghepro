using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>Genera il PDF di un cedolino paga.</summary>
public static class CedolinoPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);

    public static byte[] Genera(Cedolino c, string azienda, string dipendente)
    {
        DocumentiConfig.Inizializza();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("CEDOLINO PAGA").FontSize(16).Bold();
                    col.Item().Text(azienda).SemiBold();
                    col.Item().Text($"Dipendente: {dipendente}");
                    col.Item().Text($"Periodo: {c.Mese:00}/{c.Anno}");
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(d =>
                        {
                            d.RelativeColumn(2);
                            d.RelativeColumn(6);
                            d.RelativeColumn(2);
                            d.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(Intestazione).Text("Codice");
                            h.Cell().Element(Intestazione).Text("Descrizione");
                            h.Cell().Element(Intestazione).AlignRight().Text("Q.tà");
                            h.Cell().Element(Intestazione).AlignRight().Text("Importo");
                        });

                        foreach (var r in c.Righe)
                        {
                            string segno = r.Tipo == TipoVoce.Trattenuta ? "-"
                                : r.Tipo == TipoVoce.Figurativa ? "*" : "";
                            table.Cell().Element(Cella).Text(r.Codice);
                            table.Cell().Element(Cella).Text(r.Descrizione);
                            table.Cell().Element(Cella).AlignRight()
                                .Text(r.Quantita?.ToString("0.##", It) ?? "");
                            table.Cell().Element(Cella).AlignRight()
                                .Text($"{segno}{Eur(Math.Abs(r.Importo))}");
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Column(t =>
                    {
                        t.Item().Text($"Totale competenze: {Eur(c.TotaleCompetenze)}");
                        t.Item().Text($"Totale trattenute: {Eur(c.TotaleTrattenute)}");
                        t.Item().Text($"NETTO IN BUSTA: {Eur(c.NettoInBusta)}").FontSize(13).Bold();
                        t.Item().PaddingTop(6).Text($"Quota TFR del mese (*): {Eur(c.QuotaTfr)}").FontColor(Colors.Grey.Darken1);
                        t.Item().Text($"Contributi c/datore (*): {Eur(c.ContributiDatore)}").FontColor(Colors.Grey.Darken1);
                        t.Item().Text($"Costo totale azienda (*): {Eur(c.CostoAzienda)}").FontColor(Colors.Grey.Darken1);
                    });

                    col.Item().PaddingTop(10).Text("(*) voci figurative, non incidono sul netto.")
                        .FontSize(7).Italic().FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Documento generato da Paghe SaaS — valori di esempio, non certificati. ")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    private static IContainer Intestazione(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(4).DefaultTextStyle(t => t.SemiBold());

    private static IContainer Cella(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);
}
