using Paghe.Core.Calcolo;
using Paghe.Core.Parametri;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica del parser dei CSV ufficiali MEF: righe costruite nel formato reale (comunale a 34
/// colonne, regionale a 9) e regimi attesi calcolati a mano. Casi: aliquota unica, esenzione a
/// soglia, scaglioni progressivi, "0*"/NON APPLICA (nessuna addizionale).
/// </summary>
public sealed class ImportAddizionaliMefTests
{
    private const string HeaderComunale =
        "CODICE_CATASTALE;COMUNE;PR;NUMERO_DELIBERA;DATA_DELIBERA;DATA_PUBBLICAZIONE;NOTE;MULTIALIQ;ALIQUOTA;FASCIA;ALIQUOTA_2;FASCIA_2;ALIQUOTA_3;FASCIA_3;ALIQUOTA_4;FASCIA_4;ALIQUOTA_5;FASCIA_5;ALIQUOTA_6;FASCIA_6;ALIQUOTA_7;FASCIA_7;ALIQUOTA_8;FASCIA_8;ALIQUOTA_9;FASCIA_9;ALIQUOTA_10;FASCIA_10;ALIQUOTA_11;FASCIA_11;ALIQUOTA_12;FASCIA_12;FLAG_NUOVA;IMPORTO_ESENTE";

    private const string HeaderRegionale =
        "ANNO;REGIONE;NUMERO;DATA PUBBLICAZIONE;DISPOSIZIONE;NORME;NOTE;ALIQUOTA;FASCIA";

    private static string RigaComunale(params (int idx, string val)[] campi)
    {
        var a = new string[34];
        for (int i = 0; i < a.Length; i++) a[i] = "";
        foreach (var (idx, val) in campi) a[idx] = val;
        return string.Join(";", a);
    }

    private static string RigaRegionale(string regione, string aliquota, string fascia)
    {
        var a = new string[9];
        for (int i = 0; i < a.Length; i++) a[i] = "";
        a[0] = "2025"; a[1] = regione; a[7] = aliquota; a[8] = fascia;
        return string.Join(";", a);
    }

    // ---------------------------------------------------------------------------- COMUNALE

    [Fact]
    public void Comunale_AliquotaUnica_SenzaEsenzione()
    {
        var csv = new[]
        {
            HeaderComunale,
            RigaComunale((0, "A008"), (7, "NO"), (8, "0,5"), (9, "Aliquota unica"), (33, "0")),
        };
        var r = ImportAddizionaliMef.LeggiComunali(csv)["A008"];
        Assert.Equal(0m, r.SogliaEsenzione);
        Assert.Equal(0.005m, r.Scaglioni.Single().Aliquota); // 0,50%
        Assert.Equal(50m, CalcoloAddizionali.Dovuta(10000m, r)); // 10000 * 0,5%
    }

    [Fact]
    public void Comunale_EsenzioneASoglia_PiuAliquotaUnica()
    {
        var csv = new[]
        {
            HeaderComunale,
            RigaComunale((0, "A006"), (7, "SI"),
                (8, "0"), (9, "Esenzione per redditi imponibili fino a euro 12.000,00"),
                (10, ",6"), (11, "Aliquota unica"), (33, "12000")),
        };
        var r = ImportAddizionaliMef.LeggiComunali(csv)["A006"];
        Assert.Equal(12000m, r.SogliaEsenzione);
        Assert.Equal(0.006m, r.Scaglioni.Single().Aliquota);
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(11000m, r));   // sotto soglia
        Assert.Equal(120m, CalcoloAddizionali.Dovuta(20000m, r)); // 20000 * 0,6%
    }

    [Fact]
    public void Comunale_ScaglioniProgressivi_ConEsenzione()
    {
        var csv = new[]
        {
            HeaderComunale,
            RigaComunale((0, "A083"), (7, "SI"),
                (8, "0,63"), (9, "Applicabile a scaglione di reddito fino a euro 15.000,00"),
                (10, ",76"), (11, "Applicabile a scaglione di reddito da euro 15.000,01 fino a euro 28.000,00"),
                (12, ",79"), (13, "Applicabile a scaglione di reddito da euro 28.000,01 fino a euro 50.000,00"),
                (14, ",8"),  (15, "Applicabile a scaglione di reddito oltre euro 50.000,00"),
                (16, "0"),   (17, "Esenzione per redditi imponibili fino a euro 10.000,00"),
                (33, "10000")),
        };
        var r = ImportAddizionaliMef.LeggiComunali(csv)["A083"];
        Assert.Equal(10000m, r.SogliaEsenzione);
        Assert.Equal(4, r.Scaglioni.Count);
        // confini attesi 0 / 15000 / 28000 / 50000
        Assert.Equal(new[] { 0m, 15000m, 28000m, 50000m }, r.Scaglioni.Select(s => s.DaImporto).ToArray());
        Assert.Null(r.Scaglioni[^1].AImporto);
        // 30000 (>10000): 15000*0,63% + 13000*0,76% + 2000*0,79% = 94,5 + 98,8 + 15,8 = 209,10
        Assert.Equal(209.10m, CalcoloAddizionali.Dovuta(30000m, r));
    }

    [Fact]
    public void Comunale_NonAdottata_OppureNonApplica_Nessuna()
    {
        var csv = new[]
        {
            HeaderComunale,
            RigaComunale((0, "A001"), (7, "NO"), (8, "0*"), (33, "0")),                       // 0* = non deliberata
            RigaComunale((0, "A113"), (7, "NO"), (8, "0"), (9, "Aliquota unica"), (33, "0")), // NON APPLICA
        };
        var mappa = ImportAddizionaliMef.LeggiComunali(csv);
        Assert.Empty(mappa["A001"].Scaglioni);
        Assert.Empty(mappa["A113"].Scaglioni);
        Assert.Equal(0m, CalcoloAddizionali.Dovuta(40000m, mappa["A001"]));
    }

    // ---------------------------------------------------------------------------- REGIONALE

    [Fact]
    public void Regionale_Lombardia_QuattroScaglioni()
    {
        var csv = new[]
        {
            HeaderRegionale,
            RigaRegionale("REGIONE LOMBARDIA", "1.23", "fino a 15000.00 euro"),
            RigaRegionale("REGIONE LOMBARDIA", "1.58", "oltre 15000.00 e fino a 28000.00 euro"),
            RigaRegionale("REGIONE LOMBARDIA", "1.72", "oltre 28000.00 e fino a 50000.00 euro"),
            RigaRegionale("REGIONE LOMBARDIA", "1.73", "oltre 50000.00 euro"),
        };
        var r = ImportAddizionaliMef.LeggiRegionali(csv)["Lombardia"]; // nome tradotto per il frontend
        Assert.Equal(4, r.Scaglioni.Count);
        Assert.Equal(new[] { 0m, 15000m, 28000m, 50000m }, r.Scaglioni.Select(s => s.DaImporto).ToArray());
        Assert.Equal(0.0123m, r.Scaglioni[0].Aliquota);
        Assert.Equal(0.0173m, r.Scaglioni[^1].Aliquota);
        // 20000: 15000*1,23% + 5000*1,58% = 184,5 + 79 = 263,50
        Assert.Equal(263.50m, CalcoloAddizionali.Dovuta(20000m, r));
    }
}
