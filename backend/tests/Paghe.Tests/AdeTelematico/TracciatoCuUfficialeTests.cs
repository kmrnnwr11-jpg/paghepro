using Paghe.Core.AdeTelematico;
using Xunit;

namespace Paghe.Tests.AdeTelematico;

/// <summary>
/// Golden test posizionale del tracciato telematico CU 2026 (CUR26): struttura della
/// fornitura, allineamenti dei campi e tabella codice+valore dei record variabili.
/// Con la variabile d'ambiente CU_DUMP il tracciato viene scritto su file per il
/// controllo manuale con ModuliControlloCUR26.jar (fuori repo, vedi kit AdE).
/// </summary>
public class TracciatoCuUfficialeTests
{
    private static CertificazioneUnica CuEsempio() => new()
    {
        AnnoRedditi = 2025,
        Sostituto = new SostitutoCu
        {
            CodiceFiscale = "01234561007", Denominazione = "Rossi Impianti Srl",
            Comune = "Modena", Provincia = "MO", Cap = "41121", Indirizzo = "Via Emilia Est 120",
            Email = "amministrazione@rossimpianti.it", Telefono = "059123456",
            CodiceAttivita = "432101",
            RappresentanteCodiceFiscale = "RSSLGU70A01F257E",
            RappresentanteCognome = "Rossi", RappresentanteNome = "Luigi",
        },
        Percipiente = new PercipienteCu
        {
            CodiceFiscale = "RSSMRA85T10F257A", Cognome = "Rossi", Nome = "Mario",
            Sesso = "M", DataNascita = new DateOnly(1985, 12, 10),
            ComuneNascita = "Modena", ProvinciaNascita = "MO",
            DomicilioComune = "Modena", DomicilioProvincia = "MO", DomicilioCodiceComune = "F257",
        },
        DatiFiscali = new DatiFiscaliCu
        {
            RedditoTempoIndeterminato = 28450.32m, GiorniDetrazione = 365,
            DataInizioRapporto = new DateOnly(2019, 3, 1), InForzaAl31Dicembre = true,
            RitenuteIrpef = 6104.55m, AddizionaleRegionale = 483.66m, AddizionaleComunale = 142.25m,
            ImpostaLorda = 7529.10m, DetrazioniLavoroDipendente = 1424.55m,
            TotaleDetrazioni = 1424.55m, ImpostaNetta = 6104.55m,
        },
        Previdenza = new PrevidenzaCu
        {
            MatricolaAzienda = "7010203040",
            ImponibilePrevidenziale = 31230.00m, ContributiLavoratore = 2870.05m,
            MesiRetribuiti = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        },
    };

    private static DatiInvioCu Invio() => new() { DataFirma = new DateOnly(2026, 3, 10) };

    private static string ValoreCampo(string record, string codice)
    {
        int i = record.IndexOf(codice, System.StringComparison.Ordinal);
        Assert.True(i >= 89, $"Campo {codice} non trovato nella tabella non posizionale.");
        Assert.Equal(0, (i - 89) % 24); // l'elemento inizia a passo 24 dal carattere 90
        return record.Substring(i + 8, 16);
    }

    [Fact]
    public void Struttura_fornitura_corretta()
    {
        string tracciato = new TracciatoCuUfficiale().Genera([CuEsempio()], Invio());

        string? dump = Environment.GetEnvironmentVariable("CU_DUMP");
        if (!string.IsNullOrWhiteSpace(dump))
            File.WriteAllText(dump, tracciato);

        var righe = tracciato.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(6, righe.Length); // A, B, C, D, G, Z
        Assert.All(righe, r => Assert.Equal(1898, r.Length));
        Assert.All(righe, r => Assert.Equal('A', r[1897])); // carattere di controllo finale
        Assert.Equal("ABCDGZ", string.Concat(righe.Select(r => r[0])));

        // Record A: codice fornitura, tipo fornitore in proprio, CF fornitore = sostituto.
        Assert.Equal("CUR26", righe[0].Substring(15, 5));
        Assert.Equal("01", righe[0].Substring(20, 2));
        Assert.Equal("01234561007", righe[0].Substring(22, 16).TrimEnd());

        // Record B: CF sostituto, progressivo modulo, denominazione, rappresentante, contatori e firme.
        Assert.Equal("01234561007", righe[1].Substring(1, 16).TrimEnd());
        Assert.Equal("00000001", righe[1].Substring(17, 8));
        Assert.StartsWith("ROSSI IMPIANTI SRL", righe[1].Substring(136, 60));
        Assert.Equal("RSSLGU70A01F257E", righe[1].Substring(310, 16));
        Assert.Equal("01", righe[1].Substring(326, 2));
        Assert.Equal("00000001", righe[1].Substring(401, 8)); // numero certificazioni
        Assert.Equal('1', righe[1][409]); // casella quadro CT
        Assert.Equal('1', righe[1][410]); // firma della comunicazione

        // Record C: CF nel posizionale e nel quadro CT, email presente, Fisconline barrata.
        Assert.Equal("01234561007", righe[2].Substring(1, 16).TrimEnd());
        Assert.Equal("01234561007     ", ValoreCampo(righe[2], "CT001001"));
        Assert.Equal("1".PadLeft(16), ValoreCampo(righe[2], "CT002001"));

        // Record D: chiave della certificazione + anagrafiche.
        Assert.Equal("RSSMRA85T10F257A", righe[3].Substring(25, 16));
        Assert.Equal("00001", righe[3].Substring(41, 5));
        Assert.Equal("RSSMRA85T10F257A", ValoreCampo(righe[3], "DA002001"));
        Assert.Equal("        10121985", ValoreCampo(righe[3], "DA002005")); // data a destra
        Assert.Equal("MO              ", ValoreCampo(righe[3], "DA002021")); // provincia a sinistra
        Assert.Equal("F257            ", ValoreCampo(righe[3], "DA002022"));
        Assert.Equal("        10032026", ValoreCampo(righe[3], "DA003001")); // data firma
        Assert.Equal("1".PadLeft(16), ValoreCampo(righe[3], "DA003002"));

        // Record G: stessa chiave del D + importi con virgola allineati a destra.
        Assert.Equal("RSSMRA85T10F257A", righe[4].Substring(25, 16));
        Assert.Equal("00001", righe[4].Substring(41, 5));
        Assert.Equal("        28450,32", ValoreCampo(righe[4], "DB001001"));
        Assert.Equal("             365", ValoreCampo(righe[4], "DB001006"));
        Assert.Equal("        01032019", ValoreCampo(righe[4], "DB001008"));
        Assert.Equal("1".PadLeft(16), ValoreCampo(righe[4], "DB001010")); // in forza al 31/12
        Assert.Equal("         6104,55", ValoreCampo(righe[4], "DB001021"));
        Assert.Equal("         1424,55", ValoreCampo(righe[4], "DB001374"));
        Assert.Equal("               2", ValoreCampo(righe[4], "DB001390")); // tratt. integrativo non erogato
        Assert.Equal("               1", ValoreCampo(righe[4], "DB001751")); // tipologia reddituale
        Assert.Equal("        28450,32", ValoreCampo(righe[4], "DB001752"));
        Assert.Equal("      7010203040", ValoreCampo(righe[4], "DC001001"));
        Assert.Equal("1".PadLeft(16), ValoreCampo(righe[4], "DC001007")); // UniEmens tutti i mesi
        Assert.DoesNotContain("DB001009", righe[4]); // in forza: niente data cessazione

        // Record Z: contatori della fornitura.
        Assert.Equal("000000001", righe[5].Substring(15, 9)); // B
        Assert.Equal("000000001", righe[5].Substring(24, 9)); // C
        Assert.Equal("000000001", righe[5].Substring(33, 9)); // D
        Assert.Equal("000000001", righe[5].Substring(42, 9)); // G
        Assert.Equal("000000000", righe[5].Substring(51, 9)); // H
        Assert.Equal("000000000", righe[5].Substring(60, 9)); // L
    }

    [Fact]
    public void MesiParziali_Cessazione_E_QuadroCtEscluso()
    {
        var cu = CuEsempio() with
        {
            DatiFiscali = CuEsempio().DatiFiscali with
            {
                InForzaAl31Dicembre = false,
                DataCessazione = new DateOnly(2025, 6, 30),
                GiorniDetrazione = 181,
            },
            Previdenza = CuEsempio().Previdenza with { MesiRetribuiti = [1, 2, 3, 4, 5, 6] },
        };
        string tracciato = new TracciatoCuUfficiale().Genera(
            [cu], Invio() with { QuadroCtGiaPresentato = true });

        string? dump = Environment.GetEnvironmentVariable("CU_DUMP2");
        if (!string.IsNullOrWhiteSpace(dump))
            File.WriteAllText(dump, tracciato);

        var righe = tracciato.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("ABDGZ", string.Concat(righe.Select(r => r[0]))); // niente record C
        Assert.Equal('0', righe[1][409]); // casella quadro CT non barrata
        string g = righe[3];
        Assert.Equal("        30062025", ValoreCampo(g, "DB001009"));
        Assert.DoesNotContain("DB001010", g);
        Assert.Equal("    000000111111", ValoreCampo(g, "DC001008")); // mesi esclusi lug-dic
        Assert.DoesNotContain("DC001007", g);
        Assert.Equal("000000000", righe[4].Substring(24, 9)); // zero record C nel conteggio
    }

    [Fact]
    public void Rifiuta_SostitutiDiversi_AnnoNonSupportato_EQuadroCtSenzaEmail()
    {
        var cu = CuEsempio();
        var altroSostituto = cu with
        {
            Sostituto = cu.Sostituto with { CodiceFiscale = "97103880585" },
        };
        Assert.Throws<ArgumentException>(
            () => new TracciatoCuUfficiale().Genera([cu, altroSostituto], Invio()));
        Assert.Throws<ArgumentException>(
            () => new TracciatoCuUfficiale().Genera([cu with { AnnoRedditi = 2026 }], Invio()));
        var senzaEmail = cu with { Sostituto = cu.Sostituto with { Email = null } };
        Assert.Throws<ArgumentException>(
            () => new TracciatoCuUfficiale().Genera([senzaEmail], Invio()));
    }

    [Fact]
    public void DueCertificazioni_ProgressiviCrescenti()
    {
        var cu1 = CuEsempio();
        var cu2 = cu1 with
        {
            Percipiente = cu1.Percipiente with { CodiceFiscale = "VRDLGI90A01F205J", Cognome = "Verdi", Nome = "Luigi" },
        };
        string tracciato = new TracciatoCuUfficiale().Genera([cu1, cu2], Invio());

        string? dump = Environment.GetEnvironmentVariable("CU_DUMP3");
        if (!string.IsNullOrWhiteSpace(dump))
            File.WriteAllText(dump, tracciato);

        var righe = tracciato.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("ABCDGDGZ", string.Concat(righe.Select(r => r[0])));
        Assert.Equal("00001", righe[3].Substring(41, 5));
        Assert.Equal("00002", righe[5].Substring(41, 5));
        Assert.Equal("VRDLGI90A01F205J", righe[5].Substring(25, 16));
        Assert.Equal("00000002", righe[1].Substring(401, 8)); // due certificazioni nel record B
        Assert.Equal("000000002", righe[7].Substring(33, 9)); // due record D
    }
}
