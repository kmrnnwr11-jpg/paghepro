using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Genera la pagina mensile del <b>Libro Unico del Lavoro (LUL)</b> per un dipendente: anagrafica,
/// riepilogo presenze (dal cartellino) e riepilogo economico (dal cedolino). Versione di sintesi
/// mensile; il LUL ufficiale va tenuto/numerato secondo le modalità di legge.
/// </summary>
public static class LibroUnicoPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);
    private static string Num(decimal v) => v.ToString("0.##", It);

    public static byte[] Genera(
        Cartellino cartellino, Cedolino cedolino, string azienda, string dipendente, string codiceFiscale)
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
                    col.Item().Text("LIBRO UNICO DEL LAVORO").FontSize(16).Bold();
                    col.Item().Text(azienda).SemiBold();
                    col.Item().Text($"Dipendente: {dipendente}  —  C.F.: {codiceFiscale}");
                    col.Item().Text($"Periodo di competenza: {cartellino.Mese:00}/{cartellino.Anno}");
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text("Riepilogo presenze").FontSize(11).Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(d => { d.RelativeColumn(7); d.RelativeColumn(3); });
                        void Riga(string voce, string valore)
                        {
                            table.Cell().Element(Cella).Text(voce);
                            table.Cell().Element(Cella).AlignRight().Text(valore);
                        }
                        Riga("Giorni di ferie", Num(cartellino.GiorniFerie));
                        Riga("Giorni di malattia", Num(cartellino.GiorniMalattia));
                        Riga("Ore di permesso", Num(cartellino.OrePermessi));
                        Riga("Ore di straordinario", Num(cartellino.OreStraordinario));
                        Riga("Ore straordinario notturno", Num(cartellino.OreStraordinarioNotturno));
                        Riga("Ore straordinario festivo", Num(cartellino.OreStraordinarioFestivo));
                        Riga("Ore di assenza non retribuita", Num(cartellino.OreAssenzaNonRetribuita));
                    });

                    col.Item().PaddingTop(14).Text("Riepilogo economico").FontSize(11).Bold();
                    col.Item().PaddingTop(4).Column(t =>
                    {
                        t.Item().Text($"Totale competenze: {Eur(cedolino.TotaleCompetenze)}");
                        t.Item().Text($"Totale trattenute: {Eur(cedolino.TotaleTrattenute)}");
                        t.Item().Text($"NETTO IN BUSTA: {Eur(cedolino.NettoInBusta)}").FontSize(12).Bold();
                        t.Item().PaddingTop(4).Text($"Quota TFR del mese: {Eur(cedolino.QuotaTfr)}")
                            .FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                    t.Span("LUL di sintesi generato da Paghe SaaS — valori di esempio, non certificati. " +
                           "Il Libro Unico va tenuto e numerato secondo le modalità di legge.")
                        .FontSize(7).FontColor(Colors.Grey.Medium));
            });
        }).GeneratePdf();
    }

    private static IContainer Cella(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);
}
