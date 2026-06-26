using System.Globalization;
using Paghe.Core.Fatturazione;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>Genera il PDF di cortesia di una fattura.</summary>
public static class FatturaPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);

    public static byte[] Genera(Fattura f)
    {
        DocumentiConfig.Inizializza();
        var totali = CalcoloFattura.Calcola(f);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("FATTURA").FontSize(16).Bold();
                    col.Item().Text($"N. {f.Numero} del {f.Data.ToString("dd/MM/yyyy", It)}");
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Cedente / Prestatore").SemiBold();
                            c.Item().Text(f.Cedente.Denominazione);
                            if (!string.IsNullOrWhiteSpace(f.Cedente.PartitaIva))
                                c.Item().Text($"P.IVA {f.Cedente.PartitaIva}");
                            c.Item().Text($"{f.Cedente.Indirizzo}, {f.Cedente.Cap} {f.Cedente.Comune} ({f.Cedente.Provincia})");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Cessionario / Committente").SemiBold();
                            c.Item().Text(f.Cessionario.Denominazione);
                            if (!string.IsNullOrWhiteSpace(f.Cessionario.PartitaIva))
                                c.Item().Text($"P.IVA {f.Cessionario.PartitaIva}");
                            c.Item().Text($"{f.Cessionario.Indirizzo}, {f.Cessionario.Cap} {f.Cessionario.Comune} ({f.Cessionario.Provincia})");
                        });
                    });

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(d =>
                        {
                            d.RelativeColumn(6);
                            d.RelativeColumn(2);
                            d.RelativeColumn(2);
                            d.RelativeColumn(2);
                            d.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(Intestazione).Text("Descrizione");
                            h.Cell().Element(Intestazione).AlignRight().Text("Q.tà");
                            h.Cell().Element(Intestazione).AlignRight().Text("Prezzo");
                            h.Cell().Element(Intestazione).AlignRight().Text("IVA %");
                            h.Cell().Element(Intestazione).AlignRight().Text("Totale");
                        });

                        foreach (var r in f.Righe)
                        {
                            table.Cell().Element(Cella).Text(r.Descrizione);
                            table.Cell().Element(Cella).AlignRight().Text(r.Quantita.ToString("0.##", It));
                            table.Cell().Element(Cella).AlignRight().Text(Eur(r.PrezzoUnitario));
                            table.Cell().Element(Cella).AlignRight().Text(r.AliquotaIva.ToString("0.##", It));
                            table.Cell().Element(Cella).AlignRight().Text(Eur(CalcoloFattura.ImponibileRiga(r)));
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Column(t =>
                    {
                        foreach (var rip in totali.Riepiloghi)
                            t.Item().Text($"Imponibile {rip.Aliquota.ToString("0.##", It)}%: {Eur(rip.Imponibile)}  —  IVA: {Eur(rip.Imposta)}");
                        t.Item().PaddingTop(4).Text($"Totale imponibile: {Eur(totali.TotaleImponibile)}");
                        t.Item().Text($"Totale IVA: {Eur(totali.TotaleImposta)}");
                        t.Item().Text($"TOTALE DOCUMENTO: {Eur(totali.TotaleDocumento)} {f.Divisa}").FontSize(13).Bold();
                    });
                });

                page.Footer().AlignCenter().Text("Copia di cortesia — l'originale fiscale è il file XML FatturaPA trasmesso allo SdI.")
                    .FontSize(7).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    private static IContainer Intestazione(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(4).DefaultTextStyle(t => t.SemiBold());

    private static IContainer Cella(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);
}
