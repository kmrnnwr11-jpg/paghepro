using System.Globalization;
using Paghe.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Paghe.Documenti;

/// <summary>
/// Genera un cedolino nel formato tabellare classico usato dai software paghe italiani.
/// Il documento resta una bozza: l'aspetto professionale non sostituisce la validazione
/// normativa del consulente del lavoro.
/// </summary>
public static class CedolinoPdf
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private const string Nero = "#111111";
    private const string Grigio = "#D9D9D9";
    private const string GrigioChiaro = "#F1F1F1";
    private const string GrigioTesto = "#444444";
    private const string Avviso = "#7A2E0E";

    private static string Eur(decimal valore) => valore.ToString("N2", It);
    private static string Num(decimal valore) => valore.ToString("0.##", It);
    private static string Valore(string? valore) => string.IsNullOrWhiteSpace(valore) ? "-" : valore;

    /// <summary>Variante minima per l'endpoint stateless e i test.</summary>
    public static byte[] Genera(Cedolino cedolino, string azienda, string dipendente) =>
        Genera(cedolino, new IntestazioneCedolino { Azienda = azienda, Dipendente = dipendente }, null);

    public static byte[] Genera(Cedolino cedolino, IntestazioneCedolino intestazione) =>
        Genera(cedolino, intestazione, null);

    public static byte[] Genera(
        Cedolino cedolino,
        IntestazioneCedolino intestazione,
        Cartellino? cartellino)
    {
        DocumentiConfig.Inizializza();

        string periodo = cedolino.Mese is >= 1 and <= 12
            ? new DateTime(cedolino.Anno, cedolino.Mese, 1).ToString("MMMM yyyy", It).ToUpperInvariant()
            : $"{cedolino.Mese:00}/{cedolino.Anno}";
        var voci = cedolino.Righe.Where(r => r.Tipo != TipoVoce.Figurativa).ToList();

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.MarginHorizontal(0.75f, Unit.Centimetre);
                pagina.MarginTop(0.75f, Unit.Centimetre);
                pagina.MarginBottom(0.55f, Unit.Centimetre);
                pagina.DefaultTextStyle(testo => testo.FontSize(6.5f).FontColor(Nero));

                pagina.Content().Column(colonna =>
                {
                    colonna.Spacing(0);
                    colonna.Item().Element(Cornice).Row(riga => Testata(riga, intestazione, periodo));
                    colonna.Item().Element(CorniceSenzaAlto).Column(box => AnagraficaDipendente(box, intestazione));
                    colonna.Item().Element(CorniceSenzaAlto).Column(box => Inquadramento(box, intestazione));
                    colonna.Item().Element(CorniceSenzaAlto).Column(box => SaldiEAssenze(box, cedolino, cartellino));
                    colonna.Item().Element(CorniceSenzaAlto).Row(riga => DatiRetribuzione(riga, cedolino, intestazione, cartellino));
                    colonna.Item().Element(CorniceSenzaAlto).Table(tabella => TabellaVoci(tabella, voci));
                    colonna.Item().Element(CorniceSenzaAlto).Column(box => Previdenza(box, cedolino));
                    colonna.Item().Element(CorniceSenzaAlto).Column(box => Fisco(box, cedolino));
                    colonna.Item().Element(CorniceSenzaAlto).Row(riga => Totali(riga, cedolino));
                    colonna.Item().PaddingTop(5).Row(riga =>
                    {
                        riga.RelativeItem().Text("Firma per quietanza ______________________________")
                            .FontSize(6).FontColor(GrigioTesto);
                        riga.RelativeItem().AlignRight().Text("Cedolino elaborato da PaghePro")
                            .FontSize(6).FontColor(GrigioTesto);
                    });
                });

                pagina.Footer().AlignCenter().Text(
                        "BOZZA DA VALIDARE - Documento non certificato: sottoporre al controllo del professionista.")
                    .FontSize(6).Bold().FontColor(Avviso);
            });
        }).GeneratePdf();
    }

    private static void Testata(RowDescriptor riga, IntestazioneCedolino ana, string periodo)
    {
        riga.RelativeItem(6.2f).Padding(4).Row(blocco =>
        {
            if (ana.Logo is { Length: > 0 })
                blocco.ConstantItem(58).MaxHeight(46).AlignMiddle().PaddingRight(6)
                    .Image(ana.Logo).FitArea();

            blocco.RelativeItem().Column(azienda =>
            {
                azienda.Item().Text(testo =>
                {
                    testo.Span("DATORE DI LAVORO   ").FontSize(5.2f);
                    testo.Span(ana.Azienda.ToUpperInvariant()).FontSize(9).Bold();
                });
                azienda.Item().Text(Valore(ana.AziendaIndirizzo)).FontSize(7.2f);
                azienda.Item().Text(Valore(ana.AziendaComune)).FontSize(7.2f);
                azienda.Item().PaddingTop(2).Text(ComponiDatiFiscali(ana)).FontSize(6.2f);
            });
        });

        riga.RelativeItem(2.4f).BorderLeft(0.6f).Column(posizioni =>
        {
            CellaVerticale(posizioni, "POSIZIONE INPS", ana.MatricolaInps);
            CellaInail(posizioni, ana.PatInail);
        });

        riga.RelativeItem(3.4f).BorderLeft(0.6f).Column(periodoBox =>
        {
            periodoBox.Item().Background(Grigio).Padding(4).Column(c =>
            {
                c.Item().Text("MESE DI RETRIBUZIONE").FontSize(5.2f);
                c.Item().AlignRight().Text(periodo).FontSize(10).Bold();
            });
            periodoBox.Item().BorderTop(0.6f).Padding(4).Column(c =>
            {
                c.Item().Text("CEDOLINO PAGA").FontSize(8).Bold();
                c.Item().Text($"Stampa: {DateTime.Today:dd/MM/yyyy}").FontSize(6);
                c.Item().Text("BOZZA DA VALIDARE").FontSize(6).Bold().FontColor(Avviso);
            });
        });
    }

    private static string ComponiDatiFiscali(IntestazioneCedolino ana)
    {
        var dati = new List<string>();
        if (!string.IsNullOrWhiteSpace(ana.PartitaIva)) dati.Add($"P.IVA {ana.PartitaIva}");
        if (!string.IsNullOrWhiteSpace(ana.CodiceFiscaleAzienda)) dati.Add($"C.F. {ana.CodiceFiscaleAzienda}");
        return dati.Count == 0 ? "P.IVA / C.F. -" : string.Join("   ", dati);
    }

    private static void AnagraficaDipendente(ColumnDescriptor box, IntestazioneCedolino ana)
    {
        box.Item().Row(riga =>
        {
            Campo(riga, 1.2f, "COD. DIP.", ana.Matricola, allineaDestra: true);
            Campo(riga, 4.6f, "COGNOME E NOME", ana.Dipendente, evidenza: true);
            Campo(riga, 3.2f, "CODICE FISCALE", ana.CodiceFiscaleDipendente);
            Campo(riga, 3f, "MATRICOLA", ana.Matricola, ultima: true);
        });
        box.Item().BorderTop(0.4f).Row(riga =>
        {
            Campo(riga, 6f, "RIFERIMENTO BANCARIO / IBAN", ana.IbanDipendente);
            Campo(riga, 3f, "DATA ASSUNZIONE", ana.DataAssunzione);
            Campo(riga, 3f, "MODALITA' DI PAGAMENTO",
                string.IsNullOrWhiteSpace(ana.IbanDipendente) ? null : "Accredito", ultima: true);
        });
    }

    private static void Inquadramento(ColumnDescriptor box, IntestazioneCedolino ana)
    {
        box.Item().Background(Grigio).PaddingVertical(2).AlignCenter()
            .Text("INQUADRAMENTO E DATI CONTRATTUALI").FontSize(5.5f);
        box.Item().BorderTop(0.4f).Row(riga =>
        {
            Campo(riga, 4f, "CONTRATTO COLLETTIVO", ana.Ccnl);
            Campo(riga, 2f, "TIPO CONTRATTO", ana.TipoContratto);
            Campo(riga, 2f, "QUALIFICA", ana.Qualifica);
            Campo(riga, 1f, "LIVELLO", ana.Livello);
            Campo(riga, 1.5f, "MENSILITA'", ana.Mensilita?.ToString(It), allineaDestra: true);
            Campo(riga, 1.5f, "ORARIO", ana.Orario, ultima: true);
        });
    }

    private static void SaldiEAssenze(
        ColumnDescriptor box,
        Cedolino cedolino,
        Cartellino? cartellino)
    {
        box.Item().Row(riga =>
        {
            Saldo(riga, "FERIE (GIORNI)", cedolino.FerieMaturate, cedolino.FerieGodute, cedolino.FerieResidue);
            Saldo(riga, "PERMESSI ROL / EX FEST. (ORE)", cedolino.PermessiMaturati,
                cedolino.PermessiGoduti, cedolino.PermessiResidui);
        });

        if (cartellino is null) return;

        box.Item().BorderTop(0.4f).Row(riga =>
        {
            MiniCampo(riga, "FERIE GODUTE", $"{Num(cartellino.GiorniFerie)} gg");
            MiniCampo(riga, "PERMESSI", $"{Num(cartellino.OrePermessi)} h");
            MiniCampo(riga, "EX FESTIVITA'", $"{Num(cartellino.OreExFestivita)} h");
            MiniCampo(riga, "MALATTIA", $"{Num(cartellino.GiorniMalattia)} gg");
            MiniCampo(riga, "INFORTUNIO", $"{Num(cartellino.GiorniInfortunio)} gg");
            MiniCampo(riga, "ASSENZA N/R", $"{Num(cartellino.OreAssenzaNonRetribuita)} h");
            MiniCampo(riga, "STRAORDINARIO", $"{Num(cartellino.OreStraordinario)} h");
            MiniCampo(riga, "STRAORD. NOTT.", $"{Num(cartellino.OreStraordinarioNotturno)} h");
            MiniCampo(riga, "STRAORD. FEST.", $"{Num(cartellino.OreStraordinarioFestivo)} h", ultima: true);
        });
    }

    private static void Saldo(
        RowDescriptor riga,
        string titolo,
        decimal maturato,
        decimal goduto,
        decimal residuo)
    {
        riga.RelativeItem().Column(blocco =>
        {
            blocco.Item().Background(Grigio).PaddingVertical(2).AlignCenter().Text(titolo).FontSize(5.4f);
            blocco.Item().BorderTop(0.4f).Row(valori =>
            {
                MiniCampo(valori, "MATURATO", Eur(maturato));
                MiniCampo(valori, "GODUTO", Eur(goduto));
                MiniCampo(valori, "RESIDUO TOTALE", Eur(residuo), ultima: true);
            });
        });
    }

    private static void DatiRetribuzione(
        RowDescriptor riga,
        Cedolino cedolino,
        IntestazioneCedolino ana,
        Cartellino? cartellino)
    {
        decimal pagaBase = cedolino.Righe
            .Where(voce => voce.Tipo == TipoVoce.Competenza)
            .Select(voce => voce.Importo)
            .FirstOrDefault();
        decimal oreStraordinario = cartellino is null
            ? 0m
            : cartellino.OreStraordinario + cartellino.OreStraordinarioNotturno + cartellino.OreStraordinarioFestivo;

        MiniCampo(riga, "RETRIBUZIONE BASE", Eur(pagaBase));
        MiniCampo(riga, "ORE STRAORD.", Num(oreStraordinario));
        MiniCampo(riga, "GIORNI FERIE", cartellino is null ? "-" : Num(cartellino.GiorniFerie));
        MiniCampo(riga, "GIORNI MALATTIA", cartellino is null ? "-" : Num(cartellino.GiorniMalattia));
        MiniCampo(riga, "GIORNI INFORTUNIO", cartellino is null ? "-" : Num(cartellino.GiorniInfortunio));
        MiniCampo(riga, "ORARIO CONTRATTUALE", Valore(ana.Orario));
        MiniCampo(riga, "RETRIBUZIONE MENSILE", Eur(cedolino.TotaleCompetenze), ultima: true);
    }

    private static void TabellaVoci(TableDescriptor tabella, List<CedolinoRiga> voci)
    {
        tabella.ColumnsDefinition(colonne =>
        {
            colonne.RelativeColumn(1.2f);
            colonne.RelativeColumn(4.8f);
            colonne.RelativeColumn(1.1f);
            colonne.RelativeColumn(1.4f);
            colonne.RelativeColumn(1.8f);
            colonne.RelativeColumn(1.8f);
        });

        tabella.Header(header =>
        {
            header.Cell().Element(IntestazioneTabella).Text("VOCE");
            header.Cell().Element(IntestazioneTabella).Text("DESCRIZIONE");
            header.Cell().Element(IntestazioneTabella).AlignCenter().Text("Q.TA'");
            header.Cell().Element(IntestazioneTabella).AlignRight().Text("BASE");
            header.Cell().Element(IntestazioneTabella).AlignRight().Text("TRATTENUTE");
            header.Cell().Element(IntestazioneTabellaUltima).AlignRight().Text("COMPETENZE");
        });

        foreach (var voce in voci)
        {
            bool trattenuta = voce.Tipo == TipoVoce.Trattenuta || voce.Importo < 0m;
            tabella.Cell().Element(CellaTabella).AlignRight().Text(voce.Codice).FontSize(5.8f);
            tabella.Cell().Element(CellaTabella).Text(voce.Descrizione);
            tabella.Cell().Element(CellaTabella).AlignRight().Text(voce.Quantita?.ToString("0.###", It) ?? "");
            tabella.Cell().Element(CellaTabella).AlignRight().Text(voce.Base is decimal b ? Eur(b) : "");
            tabella.Cell().Element(CellaTabella).AlignRight().Text(trattenuta ? Eur(Math.Abs(voce.Importo)) : "");
            tabella.Cell().Element(CellaTabellaUltima).AlignRight().Text(trattenuta ? "" : Eur(voce.Importo));
        }

        // Il modulo classico mantiene la griglia delle voci fino ai riepiloghi di fondo,
        // così competenze e trattenute sono leggibili anche con mesi poco movimentati.
        float spazioVuoto = Math.Max(18f, 360f - voci.Count * 11f);
        for (int colonna = 0; colonna < 6; colonna++)
        {
            bool ultima = colonna == 5;
            tabella.Cell().Height(spazioVuoto).Element(ultima ? CellaVuotaUltima : CellaVuota);
        }
    }

    private static void Previdenza(ColumnDescriptor box, Cedolino cedolino)
    {
        box.Item().Row(riga =>
        {
            MiniCampo(riga, "IMPONIBILE CONTRIBUTIVO", Eur(cedolino.ImponibileInps));
            MiniCampo(riga, "CONTRIBUTI DIPENDENTE", Eur(cedolino.ContributiDipendente));
            MiniCampo(riga, "CONTRIBUTI DATORE (*)", Eur(cedolino.ContributiDatore));
            MiniCampo(riga, "RETRIBUZIONE UTILE TFR", Eur(cedolino.TotaleCompetenze));
            MiniCampo(riga, "QUOTA TFR DEL MESE (*)", Eur(cedolino.QuotaTfr));
            MiniCampo(riga, "COSTO AZIENDA (*)", Eur(cedolino.CostoAzienda), ultima: true);
        });
        box.Item().BorderTop(0.4f).Row(riga =>
        {
            Campo(riga, 2.4f, "DESCRIZIONE CONTRIBUTO", "INPS");
            Campo(riga, 1.4f, "IMPONIBILE", Eur(cedolino.ImponibileInps), allineaDestra: true);
            Campo(riga, 1.4f, "IMPORTO", Eur(cedolino.ContributiDipendente), allineaDestra: true);
            Campo(riga, 3f, "POSIZIONE ASSICURATIVA", "Previdenza e assicurazione obbligatoria");
            Campo(riga, 1.8f, "TOTALE CONTRIBUTI", Eur(cedolino.ContributiDipendente),
                ultima: true, allineaDestra: true);
        });
    }

    private static void Fisco(ColumnDescriptor box, Cedolino cedolino)
    {
        box.Item().Background(Grigio).PaddingVertical(2).AlignCenter()
            .Text("DATI FISCALI DEL MESE").FontSize(5.5f);
        box.Item().BorderTop(0.4f).Row(riga =>
        {
            MiniCampo(riga, "IMPONIBILE FISCALE", Eur(cedolino.ImponibileIrpef));
            MiniCampo(riga, "IRPEF LORDA", Eur(cedolino.IrpefLorda));
            MiniCampo(riga, "DETR. LAVORO DIP.", Eur(cedolino.DetrazioniLavoroDipendente));
            MiniCampo(riga, "IRPEF NETTA", Eur(cedolino.IrpefNetta));
            MiniCampo(riga, "ADD. REGIONALE", Eur(cedolino.AddizionaleRegionale));
            MiniCampo(riga, "ADD. COMUNALE", Eur(cedolino.AddizionaleComunale), ultima: true);
        });
    }

    private static void Totali(RowDescriptor riga, Cedolino cedolino)
    {
        riga.RelativeItem(4f);
        Totale(riga, "TOTALE COMPETENZE", cedolino.TotaleCompetenze);
        Totale(riga, "TOTALE TRATTENUTE", cedolino.TotaleTrattenute);
        Totale(riga, "ARROTONDAMENTO", 0m);
        riga.RelativeItem(2.3f).Background(Grigio).BorderLeft(0.6f).Padding(4).Column(netto =>
        {
            netto.Item().Text("NETTO IN BUSTA").FontSize(6).Bold();
            netto.Item().AlignRight().Text($"EUR {Eur(cedolino.NettoInBusta)}").FontSize(11).Bold();
        });
    }

    private static void Totale(RowDescriptor riga, string etichetta, decimal valore)
    {
        riga.RelativeItem(1.6f).BorderLeft(0.4f).Padding(4).Column(cella =>
        {
            cella.Item().Text(etichetta).FontSize(5.2f);
            cella.Item().AlignRight().Text(Eur(valore)).FontSize(8).SemiBold();
        });
    }

    private static void CellaVerticale(
        ColumnDescriptor colonna,
        string etichetta,
        string? valore,
        bool ultima = false)
    {
        var item = colonna.Item();
        if (!ultima) item = item.BorderBottom(0.4f);
        item.Padding(4).Column(cella =>
        {
            cella.Item().Text(etichetta).FontSize(5.2f);
            cella.Item().Text(Valore(valore)).FontSize(7.2f);
        });
    }

    private static void CellaInail(ColumnDescriptor colonna, string? pat)
    {
        colonna.Item().PaddingVertical(2).PaddingHorizontal(4).Row(riga =>
        {
            riga.RelativeItem().Column(cella =>
            {
                // Testo descrittivo, non riproduzione del marchio ufficiale riservato al LUL autorizzato.
                cella.Item().Text("INAIL").FontSize(10).Bold();
                cella.Item().Text("POSIZIONE / P.A.T.").FontSize(5.2f);
            });
            riga.RelativeItem().AlignBottom().AlignRight().Text(Valore(pat)).FontSize(7.2f).SemiBold();
        });
    }

    private static void Campo(
        RowDescriptor riga,
        float peso,
        string etichetta,
        string? valore,
        bool ultima = false,
        bool evidenza = false,
        bool allineaDestra = false)
    {
        var contenitore = riga.RelativeItem(peso);
        if (!ultima) contenitore = contenitore.BorderRight(0.4f);
        contenitore.PaddingVertical(3).PaddingHorizontal(3).Column(cella =>
        {
            cella.Item().Text(etichetta).FontSize(5.1f);
            var valoreContainer = allineaDestra ? cella.Item().AlignRight() : cella.Item();
            valoreContainer.Text(Valore(valore)).FontSize(evidenza ? 8f : 6.8f).SemiBold();
        });
    }

    private static void MiniCampo(
        RowDescriptor riga,
        string etichetta,
        string valore,
        bool ultima = false)
    {
        var contenitore = riga.RelativeItem();
        if (!ultima) contenitore = contenitore.BorderRight(0.4f);
        contenitore.PaddingVertical(2).PaddingHorizontal(3).Column(cella =>
        {
            cella.Item().Text(etichetta).FontSize(4.8f);
            cella.Item().AlignRight().Text(valore).FontSize(6.4f).SemiBold();
        });
    }

    private static IContainer Cornice(IContainer contenitore) =>
        contenitore.Border(0.6f).BorderColor(Nero);

    private static IContainer CorniceSenzaAlto(IContainer contenitore) =>
        contenitore.BorderLeft(0.6f).BorderRight(0.6f).BorderBottom(0.6f).BorderColor(Nero);

    private static IContainer IntestazioneTabella(IContainer contenitore) =>
        contenitore.Background(GrigioChiaro).BorderRight(0.4f).BorderBottom(0.6f)
            .PaddingVertical(2.5f).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(5.5f).SemiBold());

    private static IContainer IntestazioneTabellaUltima(IContainer contenitore) =>
        contenitore.Background(GrigioChiaro).BorderBottom(0.6f)
            .PaddingVertical(2.5f).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(5.5f).SemiBold());

    private static IContainer CellaTabella(IContainer contenitore) =>
        contenitore.BorderRight(0.35f).PaddingVertical(1.3f).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(6.2f));

    private static IContainer CellaTabellaUltima(IContainer contenitore) =>
        contenitore.PaddingVertical(1.3f).PaddingHorizontal(3)
            .DefaultTextStyle(testo => testo.FontSize(6.2f));

    private static IContainer CellaVuota(IContainer contenitore) => contenitore.BorderRight(0.35f);
    private static IContainer CellaVuotaUltima(IContainer contenitore) => contenitore;
}
