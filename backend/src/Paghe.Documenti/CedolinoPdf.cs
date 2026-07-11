using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Genera il PDF di un cedolino paga con il layout classico della busta paga italiana:
/// testata ditta + periodo, riquadro posizioni INPS/INAIL, dati di inquadramento del
/// dipendente, corpo voci (competenze/trattenute), dati previdenziali e fiscali,
/// saldi ferie/permessi, TFR e netto del mese in evidenza.
/// </summary>
public static class CedolinoPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);

    // Palette da stampa: inchiostro scuro, bordi grigio-ardesia, bande chiare.
    private const string Inchiostro = "#111827";
    private const string GrigioTesto = "#475569";
    private const string Bordo = "#64748B";
    private const string BordoLieve = "#CBD5E1";
    private const string Banda = "#EDF1F6";
    private const string NettoFondo = "#1E293B";

    /// <summary>Variante minima (nomi soltanto), per l'endpoint stateless e i test.</summary>
    public static byte[] Genera(Cedolino c, string azienda, string dipendente) =>
        Genera(c, new IntestazioneCedolino { Azienda = azienda, Dipendente = dipendente });

    public static byte[] Genera(Cedolino c, IntestazioneCedolino ana)
    {
        DocumentiConfig.Inizializza();

        string periodo = c.Mese is >= 1 and <= 12
            ? new DateTime(c.Anno, c.Mese, 1).ToString("MMMM yyyy", It).ToUpperInvariant()
            : $"{c.Mese:00}/{c.Anno}";
        var voci = c.Righe.Where(r => r.Tipo != TipoVoce.Figurativa).ToList();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.1f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(Inchiostro));

                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    col.Item().Element(Cornice).Row(row => TestataDitta(row, ana, periodo));
                    col.Item().Element(Cornice).Column(box => RiquadroDipendente(box, ana));
                    col.Item().Element(Cornice).Table(t => TabellaVoci(t, voci));
                    col.Item().Element(Cornice).Column(box => DatiPrevidenzialiFiscali(box, c));
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(7).Element(Cornice).Column(box => SaldiFeriePermessi(box, c));
                        row.ConstantItem(6);
                        row.RelativeItem(5).Element(Cornice).Column(box => TfrECostoAzienda(box, c));
                    });
                    col.Item().Element(Cornice).Row(row => TotaliENetto(row, c));

                    col.Item().Text("(*) valori figurativi: non incidono sul netto in busta.")
                        .FontSize(6.5f).Italic().FontColor(GrigioTesto);
                });

                page.Footer().AlignCenter().Text(
                        "Documento generato da Paghe SaaS — valori di esempio, non certificati.")
                    .FontSize(7).FontColor(GrigioTesto);
            });
        }).GeneratePdf();
    }

    // ---- 1. Testata: ditta a sinistra, periodo e posizioni a destra --------------------
    private static void TestataDitta(RowDescriptor row, IntestazioneCedolino ana, string periodo)
    {
        row.RelativeItem(7).Padding(10).Column(az =>
        {
            az.Spacing(1);
            az.Item().Text(ana.Azienda.ToUpperInvariant()).FontSize(13).Bold();
            if (!string.IsNullOrWhiteSpace(ana.AziendaIndirizzo))
                az.Item().Text(ana.AziendaIndirizzo).FontColor(GrigioTesto);
            if (!string.IsNullOrWhiteSpace(ana.AziendaComune))
                az.Item().Text(ana.AziendaComune).FontColor(GrigioTesto);

            var fiscali = new List<string>();
            if (!string.IsNullOrWhiteSpace(ana.PartitaIva)) fiscali.Add($"P.IVA {ana.PartitaIva}");
            if (!string.IsNullOrWhiteSpace(ana.CodiceFiscaleAzienda)) fiscali.Add($"C.F. {ana.CodiceFiscaleAzienda}");
            if (fiscali.Count > 0)
                az.Item().PaddingTop(3).Text(string.Join("   ·   ", fiscali)).SemiBold();
        });

        row.RelativeItem(5).BorderLeft(1).BorderColor(Bordo).Column(dx =>
        {
            dx.Item().Background(Banda).BorderBottom(0.5f).BorderColor(BordoLieve)
                .Padding(8).Column(p =>
            {
                p.Item().Text("BUSTA PAGA").FontSize(9).Bold().FontColor(GrigioTesto);
                p.Item().Text(periodo).FontSize(13).Bold();
            });
            dx.Item().Row(pos =>
            {
                Campo(pos, 1f, "MATRICOLA INPS", ana.MatricolaInps);
                Campo(pos, 1f, "P.A.T. INAIL", ana.PatInail, ultima: true);
            });
        });
    }

    // ---- 2. Dipendente e inquadramento -------------------------------------------------
    private static void RiquadroDipendente(ColumnDescriptor box, IntestazioneCedolino ana)
    {
        box.Item().Element(TitoloBanda).Text("DIPENDENTE E INQUADRAMENTO");
        box.Item().BorderBottom(0.5f).BorderColor(BordoLieve).Row(r =>
        {
            Campo(r, 4f, "COGNOME E NOME", ana.Dipendente, grande: true);
            Campo(r, 4f, "CODICE FISCALE", ana.CodiceFiscaleDipendente);
            Campo(r, 2f, "MATRICOLA", ana.Matricola);
            Campo(r, 2f, "DATA ASSUNZIONE", ana.DataAssunzione, ultima: true);
        });
        box.Item().Row(r =>
        {
            Campo(r, 3f, "QUALIFICA", ana.Qualifica);
            Campo(r, 2f, "LIVELLO", ana.Livello);
            Campo(r, 4f, "CCNL APPLICATO", ana.Ccnl);
            Campo(r, 1.5f, "MENSILITÀ", ana.Mensilita?.ToString());
            Campo(r, 2.5f, "ORARIO", ana.Orario, ultima: true);
        });
    }

    // ---- 3. Corpo voci ------------------------------------------------------------------
    private static void TabellaVoci(TableDescriptor table, List<CedolinoRiga> voci)
    {
        table.ColumnsDefinition(d =>
        {
            d.RelativeColumn(2.1f);  // voce
            d.RelativeColumn(4.9f);  // descrizione
            d.RelativeColumn(1.2f);  // ore/gg
            d.RelativeColumn(1.5f);  // base
            d.RelativeColumn(1.7f);  // competenze
            d.RelativeColumn(1.7f);  // trattenute
        });

        table.Header(h =>
        {
            h.Cell().Element(IntestazioneVoci).Text("VOCE");
            h.Cell().Element(IntestazioneVoci).Text("DESCRIZIONE");
            h.Cell().Element(IntestazioneVoci).AlignRight().Text("ORE/GG");
            h.Cell().Element(IntestazioneVoci).AlignRight().Text("BASE");
            h.Cell().Element(IntestazioneVoci).AlignRight().Text("COMPETENZE");
            h.Cell().Element(IntestazioneVoci).AlignRight().Text("TRATTENUTE");
        });

        foreach (var r in voci)
        {
            bool trattenuta = r.Tipo == TipoVoce.Trattenuta;
            table.Cell().Element(CellaVoce).Text(r.Codice).FontSize(6.8f).FontColor(GrigioTesto);
            table.Cell().Element(CellaVoce).Text(r.Descrizione);
            table.Cell().Element(CellaVoce).AlignRight().Text(r.Quantita?.ToString("0.##", It) ?? "");
            table.Cell().Element(CellaVoce).AlignRight().Text(r.Base is decimal b ? Eur(b) : "");
            table.Cell().Element(CellaVoce).AlignRight()
                .Text(trattenuta ? "" : Eur(r.Importo)).SemiBold();
            table.Cell().Element(CellaVoce).AlignRight()
                .Text(trattenuta ? Eur(Math.Abs(r.Importo)) : "").SemiBold();
        }
    }

    // ---- 4. Dati previdenziali e fiscali ------------------------------------------------
    private static void DatiPrevidenzialiFiscali(ColumnDescriptor box, Cedolino c)
    {
        box.Item().Element(TitoloBanda).Text("DATI PREVIDENZIALI E FISCALI");
        box.Item().BorderBottom(0.5f).BorderColor(BordoLieve).Row(r =>
        {
            Campo(r, 1f, "IMPONIBILE INPS", Eur(c.ImponibileInps), numerico: true);
            Campo(r, 1f, "CONTRIBUTI INPS", Eur(c.ContributiDipendente), numerico: true);
            Campo(r, 1f, "IMPONIBILE IRPEF", Eur(c.ImponibileIrpef), numerico: true);
            Campo(r, 1f, "IRPEF LORDA", Eur(c.IrpefLorda), numerico: true, ultima: true);
        });
        box.Item().Row(r =>
        {
            Campo(r, 1f, "DETRAZIONI", Eur(c.DetrazioniLavoroDipendente), numerico: true);
            Campo(r, 1f, "IRPEF NETTA", Eur(c.IrpefNetta), numerico: true);
            Campo(r, 1f, "ADD. REGIONALE", Eur(c.AddizionaleRegionale), numerico: true);
            Campo(r, 1f, "ADD. COMUNALE", Eur(c.AddizionaleComunale), numerico: true, ultima: true);
        });
    }

    // ---- 5. Saldi ferie e permessi ------------------------------------------------------
    private static void SaldiFeriePermessi(ColumnDescriptor box, Cedolino c)
    {
        box.Item().Element(TitoloBanda).Text("FERIE E PERMESSI");
        box.Item().Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(3f);
                d.RelativeColumn(2f);
                d.RelativeColumn(2f);
                d.RelativeColumn(2f);
            });
            t.Header(h =>
            {
                h.Cell().Element(IntestazioneVoci).Text("");
                h.Cell().Element(IntestazioneVoci).AlignRight().Text("MATURATO");
                h.Cell().Element(IntestazioneVoci).AlignRight().Text("GODUTO");
                h.Cell().Element(IntestazioneVoci).AlignRight().Text("RESIDUO");
            });
            RigaSaldo(t, "Ferie (giorni)", c.FerieMaturate, c.FerieGodute, c.FerieResidue);
            RigaSaldo(t, "ROL / Ex-fest. (ore)", c.PermessiMaturati, c.PermessiGoduti, c.PermessiResidui);
        });
    }

    private static void RigaSaldo(TableDescriptor t, string nome, decimal mat, decimal god, decimal res)
    {
        t.Cell().Element(CellaVoce).Text(nome);
        t.Cell().Element(CellaVoce).AlignRight().Text(mat.ToString("N2", It));
        t.Cell().Element(CellaVoce).AlignRight().Text(god.ToString("N2", It));
        t.Cell().Element(CellaVoce).AlignRight().Text(res.ToString("N2", It)).SemiBold();
    }

    // ---- 6. TFR e costo azienda (figurativi) --------------------------------------------
    private static void TfrECostoAzienda(ColumnDescriptor box, Cedolino c)
    {
        box.Item().Element(TitoloBanda).Text("TFR E COSTO AZIENDA (*)");
        box.Item().Padding(6).Column(t =>
        {
            t.Spacing(2);
            RigaFigurativa(t, "Quota TFR del mese", c.QuotaTfr);
            RigaFigurativa(t, "Contributi c/datore", c.ContributiDatore);
            RigaFigurativa(t, "Costo totale azienda", c.CostoAzienda, evidenza: true);
        });
    }

    private static void RigaFigurativa(ColumnDescriptor t, string nome, decimal importo, bool evidenza = false)
    {
        t.Item().Row(r =>
        {
            r.RelativeItem().Text(nome).FontColor(evidenza ? Inchiostro : GrigioTesto);
            var testo = r.ConstantItem(70).AlignRight().Text(Eur(importo));
            if (evidenza) testo.SemiBold();
        });
    }

    // ---- 7. Totali e netto ---------------------------------------------------------------
    private static void TotaliENetto(RowDescriptor row, Cedolino c)
    {
        Campo(row, 1f, "TOTALE COMPETENZE", Eur(c.TotaleCompetenze), numerico: true, grande: true);
        Campo(row, 1f, "TOTALE TRATTENUTE", Eur(c.TotaleTrattenute), numerico: true, grande: true);
        row.RelativeItem(1.4f).Background(NettoFondo).Padding(8).Column(n =>
        {
            n.Item().Text("NETTO DEL MESE").FontSize(6.5f).SemiBold().FontColor("#CBD5E1");
            n.Item().AlignRight().Text($"€ {Eur(c.NettoInBusta)}").FontSize(14).Bold().FontColor("#FFFFFF");
        });
    }

    // ---- Elementi grafici ricorrenti -----------------------------------------------------
    /// <summary>Cella etichetta+valore usata per testate e riquadri dati.</summary>
    private static void Campo(RowDescriptor row, float peso, string etichetta, string? valore,
        bool ultima = false, bool grande = false, bool numerico = false)
    {
        var cella = row.RelativeItem(peso);
        if (!ultima) cella = cella.BorderRight(0.5f).BorderColor(BordoLieve);
        cella.PaddingVertical(5).PaddingHorizontal(7).Column(cc =>
        {
            cc.Item().Text(etichetta).FontSize(5.8f).SemiBold().FontColor(GrigioTesto);
            var contenitore = numerico ? cc.Item().AlignRight() : cc.Item();
            contenitore.Text(string.IsNullOrWhiteSpace(valore) ? "—" : valore)
                .FontSize(grande ? 10f : 8.5f).SemiBold();
        });
    }

    private static IContainer Cornice(IContainer c) => c.Border(1).BorderColor(Bordo);

    private static IContainer TitoloBanda(IContainer c) =>
        c.Background(Banda).BorderBottom(0.5f).BorderColor(BordoLieve)
            .PaddingVertical(3).PaddingHorizontal(7)
            .DefaultTextStyle(t => t.FontSize(6.5f).Bold().FontColor(GrigioTesto));

    private static IContainer IntestazioneVoci(IContainer c) =>
        c.Background(Banda).BorderBottom(0.5f).BorderColor(BordoLieve)
            .PaddingVertical(4).PaddingHorizontal(7)
            .DefaultTextStyle(t => t.FontSize(6.5f).Bold().FontColor(GrigioTesto));

    private static IContainer CellaVoce(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(BordoLieve).PaddingVertical(3).PaddingHorizontal(7);
}
