using System.Globalization;
using Paghe.Core.Contabilita;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>Riga di dettaglio del bilancio da stampare (conto + macro-voce CEE + saldo).</summary>
public sealed record RigaBilancioPdf(string Codice, string Descrizione, string Voce, decimal Importo);

/// <summary>
/// Genera il fascicolo PDF del bilancio d'esercizio: copertina-intestazione, sintesi,
/// Stato Patrimoniale (Attivo/Passivo affiancati), Conto Economico scalare e dettaglio
/// dei conti. Prospetto redatto secondo lo schema CEE (art. 2424/2425 c.c.); non
/// sostituisce il deposito ufficiale XBRL al Registro Imprese.
/// </summary>
public static class BilancioPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It) + " €";

    // Palette del fascicolo: blu notte + accento oro, grigi neutri per le righe.
    private const string Navy = "#0F172A";
    private const string NavySoft = "#1E293B";
    private const string Oro = "#B48A3C";
    private const string GrigioRiga = "#E2E8F0";
    private const string GrigioFondo = "#F8FAFC";
    private const string Verde = "#15803D";
    private const string Rosso = "#B91C1C";

    /// <summary>Etichette CEE delle macro-voci, nell'ordine di schema.</summary>
    private static readonly Dictionary<string, string> EtichettaVoce = new()
    {
        [nameof(MacroVoce.CreditiVersoSoci)] = "A) Crediti verso soci",
        [nameof(MacroVoce.Immobilizzazioni)] = "B) Immobilizzazioni",
        [nameof(MacroVoce.AttivoCircolante)] = "C) Attivo circolante",
        [nameof(MacroVoce.RateiRiscontiAttivi)] = "D) Ratei e risconti attivi",
        [nameof(MacroVoce.PatrimonioNetto)] = "A) Patrimonio netto",
        [nameof(MacroVoce.FondiRischiOneri)] = "B) Fondi per rischi e oneri",
        [nameof(MacroVoce.TrattamentoFineRapporto)] = "C) Trattamento di fine rapporto",
        [nameof(MacroVoce.Debiti)] = "D) Debiti",
        [nameof(MacroVoce.RateiRiscontiPassivi)] = "E) Ratei e risconti passivi",
        [nameof(MacroVoce.ValoreDellaProduzione)] = "A) Valore della produzione",
        [nameof(MacroVoce.CostiDellaProduzione)] = "B) Costi della produzione",
        [nameof(MacroVoce.ProventiFinanziari)] = "C) Proventi finanziari",
        [nameof(MacroVoce.OneriFinanziari)] = "C) Oneri finanziari",
        [nameof(MacroVoce.Imposte)] = "20) Imposte sul reddito",
    };

    public static byte[] Genera(
        Bilancio b, int anno, string ragioneSociale, string? partitaIva, string stato,
        IReadOnlyList<RigaBilancioPdf> righe)
    {
        DocumentiConfig.Inizializza();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.6f, Unit.Centimetre);
                page.MarginVertical(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(NavySoft));

                page.Header().Element(c => Intestazione(c, anno, ragioneSociale, partitaIva, stato));

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Element(c => RiquadriSintesi(c, b));
                    col.Item().Element(c => StatoPatrimoniale(c, b));
                    col.Item().Element(c => ContoEconomico(c, b));
                    if (righe.Count > 0)
                        col.Item().Element(c => DettaglioConti(c, righe));
                    col.Item().Element(c => NotaQuadratura(c, b));
                });

                page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(GrigioRiga).Row(row =>
                {
                    row.RelativeItem().Text(
                        "Prospetto redatto secondo lo schema CEE (artt. 2424 e 2425 c.c.). " +
                        "Documento interno: non costituisce deposito ufficiale al Registro Imprese.")
                        .FontSize(6.5f).FontColor(Colors.Grey.Medium);
                    row.ConstantItem(80).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
                        t.Span("Pag. ");
                        t.CurrentPageNumber();
                        t.Span(" di ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    // --- Intestazione: banda blu notte con filo d'oro ---
    private static void Intestazione(IContainer c, int anno, string ragioneSociale, string? partitaIva, string stato)
    {
        c.Column(col =>
        {
            col.Item().Background(Navy).Padding(16).Row(row =>
            {
                row.RelativeItem().Column(sx =>
                {
                    sx.Item().Text(ragioneSociale.ToUpper(It)).FontSize(11).SemiBold().FontColor(Colors.White);
                    if (!string.IsNullOrWhiteSpace(partitaIva))
                        sx.Item().Text($"P.IVA {partitaIva}").FontSize(8).FontColor("#94A3B8");
                    sx.Item().PaddingTop(8).Text($"BILANCIO D'ESERCIZIO {anno}")
                        .FontSize(20).Bold().FontColor(Colors.White);
                    sx.Item().Text("Stato Patrimoniale e Conto Economico — schema CEE, artt. 2424/2425 c.c.")
                        .FontSize(8).FontColor("#94A3B8");
                });
                row.ConstantItem(90).AlignRight().AlignTop()
                    .Background(NavySoft).Padding(6)
                    .Text(stato.ToUpper(It)).FontSize(9).SemiBold().FontColor(Oro);
            });
            col.Item().Height(3).Background(Oro);
        });
    }

    // --- Tre riquadri di sintesi: attivo, patrimonio netto, risultato ---
    private static void RiquadriSintesi(IContainer c, Bilancio b)
    {
        var utile = b.UtilePerditaEsercizio;
        c.Row(row =>
        {
            row.Spacing(10);
            Riquadro(row, "TOTALE ATTIVO", Eur(b.TotaleAttivo), NavySoft);
            Riquadro(row, "PATRIMONIO NETTO", Eur(b.PatrimonioNetto), NavySoft);
            Riquadro(row, utile >= 0 ? "UTILE D'ESERCIZIO" : "PERDITA D'ESERCIZIO",
                Eur(Math.Abs(utile)), utile >= 0 ? Verde : Rosso);
        });

        static void Riquadro(RowDescriptor row, string titolo, string valore, string colore) =>
            row.RelativeItem().Background(GrigioFondo).BorderLeft(2.5f).BorderColor(colore)
                .Padding(10).Column(cx =>
                {
                    cx.Item().Text(titolo).FontSize(7).SemiBold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.08f);
                    cx.Item().PaddingTop(2).Text(valore).FontSize(14).Bold().FontColor(colore);
                });
    }

    // --- Stato Patrimoniale: Attivo e Passivo affiancati ---
    private static void StatoPatrimoniale(IContainer c, Bilancio b)
    {
        c.Column(col =>
        {
            col.Item().Element(TitoloSezione("STATO PATRIMONIALE"));
            col.Item().PaddingTop(6).Row(row =>
            {
                row.Spacing(14);
                // AlignTop: senza, i due riquadri si stirano alla stessa altezza e l'Attivo
                // (4 voci contro 5) mostrerebbe una fascia vuota sotto il totale.
                row.RelativeItem().AlignTop().Element(x => Prospetto(x, "ATTIVO", new[]
                {
                    ("A) Crediti verso soci", b.CreditiVersoSoci),
                    ("B) Immobilizzazioni", b.Immobilizzazioni),
                    ("C) Attivo circolante", b.AttivoCircolante),
                    ("D) Ratei e risconti attivi", b.RateiRiscontiAttivi),
                }, "TOTALE ATTIVO", b.TotaleAttivo));
                row.RelativeItem().AlignTop().Element(x => Prospetto(x, "PASSIVO", new[]
                {
                    ("A) Patrimonio netto", b.PatrimonioNetto),
                    ("B) Fondi per rischi e oneri", b.FondiRischiOneri),
                    ("C) Trattamento di fine rapporto", b.TrattamentoFineRapporto),
                    ("D) Debiti", b.Debiti),
                    ("E) Ratei e risconti passivi", b.RateiRiscontiPassivi),
                }, "TOTALE PASSIVO", b.TotalePassivo));
            });
        });

        static void Prospetto(IContainer c, string lato, (string, decimal)[] voci, string labelTotale, decimal totale)
        {
            c.Border(0.5f).BorderColor(GrigioRiga).Column(col =>
            {
                col.Item().Background(GrigioFondo).PaddingVertical(5).PaddingHorizontal(8)
                    .Text(lato).FontSize(9).Bold().FontColor(Navy).LetterSpacing(0.06f);
                foreach (var (label, importo) in voci)
                    col.Item().BorderTop(0.5f).BorderColor(GrigioRiga)
                        .PaddingVertical(5).PaddingHorizontal(8).Row(r =>
                        {
                            r.RelativeItem().Text(label).FontSize(8.5f);
                            r.ConstantItem(85).AlignRight().Text(Eur(importo)).FontSize(8.5f);
                        });
                col.Item().Background(Navy).PaddingVertical(6).PaddingHorizontal(8).Row(r =>
                {
                    r.RelativeItem().Text(labelTotale).FontSize(9).Bold().FontColor(Colors.White);
                    r.ConstantItem(95).AlignRight().Text(Eur(totale)).FontSize(9).Bold().FontColor(Colors.White);
                });
            });
        }
    }

    // --- Conto Economico scalare ---
    private static void ContoEconomico(IContainer c, Bilancio b)
    {
        var utile = b.UtilePerditaEsercizio;
        c.Column(col =>
        {
            col.Item().Element(TitoloSezione("CONTO ECONOMICO"));
            col.Item().PaddingTop(6).Border(0.5f).BorderColor(GrigioRiga).Column(ce =>
            {
                Voce(ce, "A) Valore della produzione", b.ValoreDellaProduzione);
                Voce(ce, "B) Costi della produzione", -b.CostiDellaProduzione);
                Subtotale(ce, "Differenza tra valore e costi della produzione (A − B)", b.DifferenzaValoreCosti);
                Voce(ce, "C) Proventi finanziari", b.ProventiFinanziari);
                Voce(ce, "C) Oneri finanziari", -b.OneriFinanziari);
                Subtotale(ce, "Risultato prima delle imposte", b.RisultatoPrimaDelleImposte);
                Voce(ce, "20) Imposte sul reddito dell'esercizio", -b.Imposte);

                ce.Item().Background(utile >= 0 ? Verde : Rosso).PaddingVertical(7).PaddingHorizontal(8).Row(r =>
                {
                    r.RelativeItem().Text($"21) {(utile >= 0 ? "UTILE" : "PERDITA")} DELL'ESERCIZIO")
                        .FontSize(10).Bold().FontColor(Colors.White);
                    r.ConstantItem(100).AlignRight().Text(Eur(utile)).FontSize(10).Bold().FontColor(Colors.White);
                });
            });
        });

        static void Voce(ColumnDescriptor col, string label, decimal importo) =>
            col.Item().BorderBottom(0.5f).BorderColor(GrigioRiga)
                .PaddingVertical(5).PaddingHorizontal(8).Row(r =>
                {
                    r.RelativeItem().Text(label).FontSize(8.5f);
                    r.ConstantItem(100).AlignRight().Text(Eur(importo)).FontSize(8.5f);
                });

        static void Subtotale(ColumnDescriptor col, string label, decimal importo) =>
            col.Item().Background(GrigioFondo).BorderBottom(0.5f).BorderColor(GrigioRiga)
                .PaddingVertical(6).PaddingHorizontal(8).Row(r =>
                {
                    r.RelativeItem().Text(label).FontSize(8.5f).SemiBold().FontColor(Navy);
                    r.ConstantItem(100).AlignRight().Text(Eur(importo)).FontSize(8.5f).SemiBold().FontColor(Navy);
                });
    }

    // --- Dettaglio dei conti (allegato) ---
    private static void DettaglioConti(IContainer c, IReadOnlyList<RigaBilancioPdf> righe)
    {
        c.Column(col =>
        {
            col.Item().Element(TitoloSezione("DETTAGLIO DEI CONTI"));
            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(d =>
                {
                    d.ConstantColumn(50);
                    d.RelativeColumn(5);
                    d.RelativeColumn(4);
                    d.ConstantColumn(85);
                });
                table.Header(h =>
                {
                    h.Cell().Element(TestataTabella).Text("Conto");
                    h.Cell().Element(TestataTabella).Text("Descrizione");
                    h.Cell().Element(TestataTabella).Text("Voce CEE");
                    h.Cell().Element(TestataTabella).AlignRight().Text("Saldo");
                });
                foreach (var r in righe.OrderBy(x => x.Codice, StringComparer.Ordinal))
                {
                    table.Cell().Element(CellaTabella).Text(r.Codice).FontSize(8);
                    table.Cell().Element(CellaTabella).Text(r.Descrizione).FontSize(8);
                    table.Cell().Element(CellaTabella)
                        .Text(EtichettaVoce.GetValueOrDefault(r.Voce, r.Voce)).FontSize(8);
                    table.Cell().Element(CellaTabella).AlignRight().Text(Eur(r.Importo)).FontSize(8);
                }
            });
        });

        static IContainer TestataTabella(IContainer c) =>
            c.Background(Navy).PaddingVertical(4).PaddingHorizontal(6)
                .DefaultTextStyle(t => t.FontSize(8).SemiBold().FontColor(Colors.White));

        static IContainer CellaTabella(IContainer c) =>
            c.BorderBottom(0.5f).BorderColor(GrigioRiga).PaddingVertical(3).PaddingHorizontal(6);
    }

    // --- Nota di quadratura ---
    private static void NotaQuadratura(IContainer c, Bilancio b)
    {
        var (colore, testo) = b.Quadra
            ? (Verde, "Il bilancio quadra: totale Attivo e totale Passivo coincidono.")
            : (Rosso, $"Attenzione: il bilancio NON quadra. Sbilancio Attivo − Passivo: {Eur(b.SbilancioAttivoPassivo)}.");
        c.Background(GrigioFondo).BorderLeft(2.5f).BorderColor(colore).Padding(9)
            .Text(testo).FontSize(8.5f).FontColor(colore);
    }

    private static Action<IContainer> TitoloSezione(string titolo) => c =>
        c.BorderBottom(1.5f).BorderColor(Oro).PaddingBottom(3)
            .Text(titolo).FontSize(11).Bold().FontColor(Navy).LetterSpacing(0.08f);
}
