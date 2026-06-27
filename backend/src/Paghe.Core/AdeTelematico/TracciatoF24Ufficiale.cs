using System.Globalization;
using System.Text;

namespace Paghe.Core.AdeTelematico;

/// <summary>
/// Writer del tracciato telematico UFFICIALE F24 a lunghezza fissa (record A/M/V/Z, 1900 caratteri
/// per record, CR+LF inclusi), secondo l'Allegato 1 "Specifiche tecniche per la trasmissione
/// telematica dei Modelli F24 da parte dei contribuenti".
/// Implementa il modello con sezione IMU v.2013 (record V tipo "A").
/// Il file va comunque controllato con il modulo ufficiale (ControlliF24A0.jar) prima dell'invio.
/// </summary>
public sealed class TracciatoF24Ufficiale : ITracciatoF24Writer
{
    private const int Lung = 1898; // caratteri di contenuto (1..1898); poi CR LF

    public string Genera(ModelloF24 f24)
    {
        ArgumentNullException.ThrowIfNull(f24);
        if (f24.Erario.Count > 6 || f24.Inps.Count > 4 || f24.Regioni.Count > 4 || f24.TributiLocali.Count > 4)
            throw new NotSupportedException("Troppe righe per un singolo modello F24: servono più record V (non gestito).");

        string cf = (f24.Contribuente.CodiceFiscale ?? "").Trim().ToUpperInvariant();
        bool pnf = f24.Contribuente.Tipo == TipoSoggettoF24.SoggettoDiverso;

        var sb = new StringBuilder();
        sb.Append(RecordA(f24, cf, pnf));
        sb.Append(RecordM(f24, cf, pnf));
        sb.Append(RecordV(f24, cf));
        sb.Append(RecordZ(numeroRecordV: 1));
        return sb.ToString();
    }

    // ---- Record di testa "A" -----------------------------------------------------------------
    private static string RecordA(ModelloF24 f24, string cf, bool pnf)
    {
        var r = new Rec('A');
        r.An(16, 5, "F24A0");                 // codice fornitura
        r.An(21, 2, pnf ? "14" : "04");       // tipo fornitore
        r.An(23, 16, cf);                     // codice fiscale fornitore (= contribuente)
        if (pnf)
            r.An(216, 60, f24.Contribuente.Denominazione);
        // CAP a struttura numerica: azzerare
        r.Nu(84, 8, 0).Nu(211, 5, 0).Nu(353, 5, 0).Nu(435, 5, 0);
        r.An(440, 1, " ");                    // flag origine
        r.Nu(522, 3, 1);                      // progressivo invio = 001
        r.Nu(525, 3, 1);                      // numero totale invii = 001
        return r.ToString();
    }

    // ---- Record dati anagrafici "M" ----------------------------------------------------------
    private static string RecordM(ModelloF24 f24, string cf, bool pnf)
    {
        var r = new Rec('M');
        r.An(2, 16, cf);                      // codice fiscale contribuente
        r.Nu(18, 8, 1);                       // progressivo modulo = 00000001
        r.An(91, 1, "E");                     // valuta delega
        r.Nu(92, 1, 0);                       // esercizio a cavallo
        r.Nu(93, 1, 0);                       // flag versante/firmatario
        r.Nu(110, 1, 0);                      // tipo versante
        r.Nu(156, 8, 0);                      // data nascita versante
        r.Nu(248, 5, 0);                      // cap residenza versante
        r.Nu(330, 5, 0);                      // cap residenza contribuente
        if (pnf)
            r.An(518, 55, f24.Contribuente.Denominazione);
        else
        {
            r.An(438, 24, f24.Contribuente.Cognome);
            r.An(462, 20, f24.Contribuente.Nome);
            r.Nu(482, 8, 0);                  // data nascita PF
        }
        r.An(1869, 4, "EURO");                // valuta
        r.An(1873, 15, ImportoVirgola(f24.SaldoFinale)); // saldo totale a debito (formato con virgola)
        r.An(1888, 10, DataConTrattini(f24.DataVersamento)); // GG-MM-AAAA
        return r.ToString();
    }

    // ---- Record dati contabili "V" (modello con sezione IMU v.2013, tipo "A") -----------------
    private static string RecordV(ModelloF24 f24, string cf)
    {
        var r = new Rec('V');
        r.An(2, 16, cf);
        r.Nu(18, 8, 1);                       // progressivo modulo
        r.An(90, 1, "A");                     // tipo modello = sezione IMU v.2013
        r.Nu(94, 11, 0);                      // codice atto (erario)

        // --- Sezione Erario: 6 righe da 58 byte a partire da pos 105 ---
        decimal erDeb = 0m, erCre = 0m;
        for (int i = 0; i < 6; i++)
        {
            int p = 105 + i * 58;
            var v = i < f24.Erario.Count ? f24.Erario[i] : null;
            r.An(p, 4, v?.CodiceTributo);                 // codice tributo
            r.An(p + 4, 16, "");                          // numero certificazione
            r.An(p + 20, 4, v?.Rateazione);              // rateazione/regione/prov
            r.Nu(p + 24, 4, v is null ? 0 : v.AnnoRiferimento);
            r.Nu(p + 28, 15, Cent(v?.ImportoDebito ?? 0m));
            r.Nu(p + 43, 15, Cent(v?.ImportoCredito ?? 0m));
            if (v is not null) { erDeb += v.ImportoDebito; erCre += v.ImportoCredito; }
        }
        Totali(r, 453, 468, 483, 484, erDeb, erCre);

        // --- Sezione INPS: 4 righe da 67 byte a partire da pos 499 ---
        decimal inDeb = 0m, inCre = 0m;
        for (int i = 0; i < 4; i++)
        {
            int p = 499 + i * 67;
            var v = i < f24.Inps.Count ? f24.Inps[i] : null;
            r.Nu(p, 4, ToLong(v?.CodiceSede));
            r.An(p + 4, 4, v?.CausaleContributo);
            r.An(p + 8, 17, v?.MatricolaCodice);
            r.Nu(p + 25, 6, Periodo(v?.PeriodoDa));
            r.Nu(p + 31, 6, Periodo(v?.PeriodoA ?? v?.PeriodoDa));
            r.Nu(p + 37, 15, Cent(v?.ImportoDebito ?? 0m));
            r.Nu(p + 52, 15, Cent(v?.ImportoCredito ?? 0m));
            if (v is not null) { inDeb += v.ImportoDebito; inCre += v.ImportoCredito; }
        }
        Totali(r, 767, 782, 797, 798, inDeb, inCre);

        // --- Sezione Regioni: 4 righe da 44 byte a partire da pos 813 ---
        decimal regDeb = 0m, regCre = 0m;
        for (int i = 0; i < 4; i++)
        {
            int p = 813 + i * 44;
            var v = i < f24.Regioni.Count ? f24.Regioni[i] : null;
            r.Nu(p, 2, ToLong(v?.CodiceRegione));
            r.An(p + 2, 4, v?.CodiceTributo);
            r.An(p + 6, 4, v?.Rateazione);
            r.Nu(p + 10, 4, v is null ? 0 : v.AnnoRiferimento);
            r.Nu(p + 14, 15, Cent(v?.ImportoDebito ?? 0m));
            r.Nu(p + 29, 15, Cent(v?.ImportoCredito ?? 0m));
            if (v is not null) { regDeb += v.ImportoDebito; regCre += v.ImportoCredito; }
        }
        Totali(r, 989, 1004, 1019, 1020, regDeb, regCre);

        // --- Sezione IMU ed altri tributi locali: 4 righe da 68 byte a partire da pos 1053 ---
        decimal locDeb = 0m, locCre = 0m;
        for (int i = 0; i < 4; i++)
        {
            int p = 1053 + i * 68;
            var v = i < f24.TributiLocali.Count ? f24.TributiLocali[i] : null;
            r.An(p, 4, v?.CodiceEnteComune);
            r.Nu(p + 4, 1, v?.Ravvedimento == true ? 1 : 0);
            r.Nu(p + 5, 1, v?.ImmobiliVariati == true ? 1 : 0);
            r.Nu(p + 6, 1, v?.Acconto == true ? 1 : 0);
            r.Nu(p + 7, 1, v?.Saldo == true ? 1 : 0);
            r.Nu(p + 8, 3, v?.NumeroImmobili ?? 0);
            r.Nu(p + 11, 15, 0);                          // detrazione abitazione principale
            r.An(p + 26, 4, v?.CodiceTributo);
            r.An(p + 30, 4, v?.Rateazione);
            r.Nu(p + 34, 4, v is null ? 0 : v.AnnoRiferimento);
            r.Nu(p + 38, 15, Cent(v?.ImportoDebito ?? 0m));
            r.Nu(p + 53, 15, Cent(v?.ImportoCredito ?? 0m));
            if (v is not null) { locDeb += v.ImportoDebito; locCre += v.ImportoCredito; }
        }
        Totali(r, 1325, 1340, 1355, 1356, locDeb, locCre);

        // --- Sezioni INAIL e Altri enti: non usate (azzerate) ---
        r.Nu(1527, 15, 0).Nu(1542, 15, 0).An(1557, 1, " ").Nu(1558, 15, 0); // totali INAIL
        r.Nu(1573, 4, 0);                                                    // codice ente (altri enti)
        r.Nu(1697, 15, 0).Nu(1712, 15, 0).An(1727, 1, " ").Nu(1728, 15, 0); // totali altri enti

        // --- Saldo finale e data versamento ---
        decimal saldo = (erDeb - erCre) + (inDeb - inCre) + (regDeb - regCre) + (locDeb - locCre);
        r.Nu(1793, 15, Cent(saldo));
        r.Nu(1808, 8, DataCompatta(f24.DataVersamento)); // GGMMAAAA
        return r.ToString();
    }

    // ---- Record di coda "Z" ------------------------------------------------------------------
    private static string RecordZ(int numeroRecordV)
    {
        var r = new Rec('Z');
        r.Nu(16, 9, numeroRecordV);
        r.Nu(25, 9, 1);                       // numero record M = 1
        return r.ToString();
    }

    private static void Totali(Rec r, int posDeb, int posCre, int posSegno, int posSaldo, decimal deb, decimal cre)
    {
        r.Nu(posDeb, 15, Cent(deb));
        r.Nu(posCre, 15, Cent(cre));
        bool usata = deb != 0m || cre != 0m;
        r.An(posSegno, 1, !usata ? " " : (cre > deb ? "N" : "P"));
        r.Nu(posSaldo, 15, Cent(Math.Abs(deb - cre)));
    }

    private static long Cent(decimal euro) => (long)Math.Round(euro * 100m, MidpointRounding.AwayFromZero);

    private static long ToLong(string? numerico) =>
        long.TryParse((numerico ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static long Periodo(string? mmAaaa)
    {
        string s = (mmAaaa ?? "").Replace("/", "").Trim();
        return long.TryParse(s, out var n) ? n : 0;
    }

    private static string ImportoVirgola(decimal v)
    {
        var it = CultureInfo.GetCultureInfo("it-IT");
        string s = v.ToString("#,##0.00", it);
        return s.Length > 15 ? v.ToString("0.00", it) : s;
    }

    private static string DataConTrattini(DateOnly d) => $"{d.Day:00}-{d.Month:00}-{d.Year:0000}";
    private static long DataCompatta(DateOnly d) => long.Parse($"{d.Day:00}{d.Month:00}{d.Year:0000}");

    /// <summary>Costruttore di un record a lunghezza fissa (1898 caratteri + "A" finale + CR LF).</summary>
    private sealed class Rec
    {
        private readonly char[] _b = new char[Lung];

        public Rec(char tipo)
        {
            Array.Fill(_b, ' ');
            _b[0] = tipo;
            _b[Lung - 1] = 'A'; // posizione 1898: filler "A"
        }

        public Rec An(int pos, int len, string? v)
        {
            v ??= "";
            for (int i = 0; i < len; i++)
                _b[pos - 1 + i] = i < v.Length ? v[i] : ' ';
            return this;
        }

        public Rec Nu(int pos, int len, long v)
        {
            string s = v.ToString(CultureInfo.InvariantCulture);
            if (s.Length > len) s = s[^len..];
            s = s.PadLeft(len, '0');
            for (int i = 0; i < len; i++)
                _b[pos - 1 + i] = s[i];
            return this;
        }

        public override string ToString() => new string(_b) + "\r\n";
    }
}
