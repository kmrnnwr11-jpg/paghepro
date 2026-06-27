using Paghe.Core.AdeTelematico;

namespace Paghe.Tests.AdeTelematico;

public class TracciatoF24UfficialeTests
{
    private static ModelloF24 F24Esempio() => new()
    {
        Contribuente = new ContribuenteF24
        {
            CodiceFiscale = "01234560017",
            Tipo = TipoSoggettoF24.SoggettoDiverso,
            Denominazione = "Acme Srl",
        },
        DataVersamento = new DateOnly(2026, 7, 16),
        Erario = [new VoceErario("1001", 2026, ImportoDebito: 500.00m)],
        Inps = [new VoceInps("7100", "DM10", "1234567890", "062026", "062026", ImportoDebito: 1000.00m)],
        Regioni = [new VoceRegioni("12", "3802", 2026, ImportoDebito: 35.00m)],
        TributiLocali = [new VoceImuTributiLocali("F205", "3848", 2026, ImportoDebito: 15.00m, Saldo: true)],
    };

    [Fact]
    public void Struttura_record_corretta()
    {
        string testo = new TracciatoF24Ufficiale().Genera(F24Esempio());

        // Ogni record è 1900 caratteri compresi CR+LF.
        var righe = testo.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, righe.Length);
        Assert.All(righe, r => Assert.Equal(1898, r.Length));

        Assert.Equal('A', righe[0][0]);
        Assert.Equal('M', righe[1][0]);
        Assert.Equal('V', righe[2][0]);
        Assert.Equal('Z', righe[3][0]);

        // Record A: codice fornitura "F24A0" a pos 16, tipo fornitore "14" (PNF) a pos 21.
        Assert.Equal("F24A0", righe[0].Substring(15, 5));
        Assert.Equal("14", righe[0].Substring(20, 2));

        // Record V: tipo modello "A" (sezione IMU) a pos 90; codice tributo erario "1001" a pos 105.
        Assert.Equal('A', righe[2][89]);
        Assert.Equal("1001", righe[2].Substring(104, 4));
        // Importo a debito erario in centesimi (15 cifre) a pos 133: 500,00 € = 50000 cent.
        Assert.Equal("000000000050000", righe[2].Substring(132, 15));
        // Saldo finale a pos 1793: 1550,00 € = 155000 cent.
        Assert.Equal("000000000155000", righe[2].Substring(1792, 15));

        // Record Z: numero record V = 1 a pos 16.
        Assert.Equal("000000001", righe[3].Substring(15, 9));

        // Dump opzionale per la validazione col JAR ufficiale.
        string? dump = Environment.GetEnvironmentVariable("F24_DUMP");
        if (!string.IsNullOrWhiteSpace(dump))
            File.WriteAllText(dump, testo);
    }

    [Fact]
    public void Importi_in_centesimi_e_saldo_M_con_virgola()
    {
        var f24 = new ModelloF24
        {
            Contribuente = new ContribuenteF24
            {
                CodiceFiscale = "01234560017", Tipo = TipoSoggettoF24.SoggettoDiverso, Denominazione = "Acme Srl",
            },
            DataVersamento = new DateOnly(2026, 7, 16),
            Erario = [new VoceErario("1001", 2026, ImportoDebito: 614.27m)],
        };

        var righe = new TracciatoF24Ufficiale().Genera(f24).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        // Record M: saldo totale a debito con virgola "614,27" allineato a sinistra (pos 1873, len 15).
        Assert.Equal("614,27         ", righe[1].Substring(1872, 15));
        // Record V: saldo finale 614,27 € = 61427 cent.
        Assert.Equal("000000000061427", righe[2].Substring(1792, 15));
    }
}
