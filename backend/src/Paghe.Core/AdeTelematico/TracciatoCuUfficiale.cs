using System.Globalization;
using System.Text;

namespace Paghe.Core.AdeTelematico;

/// <summary>Opzioni dell'invio telematico CU (chi trasmette e come firma).</summary>
public sealed record DatiInvioCu
{
    /// <summary>CF di chi trasmette il file. Se nullo: il sostituto stesso (invio in proprio).</summary>
    public string? CodiceFiscaleFornitore { get; init; }

    /// <summary>Data della firma del sostituto (campo DA003001 di ogni certificazione).</summary>
    public required DateOnly DataFirma { get; init; }

    /// <summary>
    /// Vero se il sostituto ha già presentato in passato una comunicazione CSO o il quadro CT
    /// (in tal caso il record C NON va allegato, pena lo scarto).
    /// </summary>
    public bool QuadroCtGiaPresentato { get; init; }

    /// <summary>Email per il quadro CT (default: email del sostituto).</summary>
    public string? EmailQuadroCt { get; init; }
}

/// <summary>
/// Scrive la fornitura telematica della Certificazione Unica 2026 (codice "CUR26", redditi 2025)
/// secondo le specifiche tecniche ufficiali AdE: record a 1.900 caratteri, A/B/C posizionali o
/// misti, D/G a struttura variabile (tabella di 75 elementi codice+valore), Z di coda.
/// Perimetro: certificazioni di lavoro dipendente di un solo sostituto (niente H/L).
/// Il criterio di correttezza è l'esito positivo di <c>ModuliControlloCUR26.jar</c>.
/// </summary>
public sealed class TracciatoCuUfficiale
{
    /// <summary>Il modello CU 2026 certifica i redditi 2025.</summary>
    public const int AnnoRedditiSupportato = 2025;
    public const string CodiceFornitura = "CUR26";

    private const int Lung = 1898; // contenuto; CR+LF portano il record a 1900

    public string Genera(IReadOnlyList<CertificazioneUnica> certificazioni, DatiInvioCu invio)
    {
        if (certificazioni.Count == 0)
            throw new ArgumentException("Nessuna certificazione da trasmettere.");
        var sostituto = certificazioni[0].Sostituto;
        string cfSostituto = Pulisci(sostituto.CodiceFiscale);
        if (certificazioni.Any(c => Pulisci(c.Sostituto.CodiceFiscale) != cfSostituto))
            throw new ArgumentException("La fornitura può contenere certificazioni di un solo sostituto.");
        if (certificazioni.Any(c => c.AnnoRedditi != AnnoRedditiSupportato))
            throw new ArgumentException(
                $"Il tracciato {CodiceFornitura} certifica soltanto i redditi {AnnoRedditiSupportato}.");

        string cfFornitore = Pulisci(invio.CodiceFiscaleFornitore ?? sostituto.CodiceFiscale);
        bool inProprio = cfFornitore == cfSostituto;
        bool conQuadroCt = !invio.QuadroCtGiaPresentato;
        string? emailCt = invio.EmailQuadroCt ?? sostituto.Email;
        if (conQuadroCt && string.IsNullOrWhiteSpace(emailCt))
            throw new ArgumentException(
                "Il quadro CT richiede l'email del sostituto (o indicare che il quadro CT è già stato presentato).");

        var sb = new StringBuilder();
        sb.Append(RecordA(cfFornitore, inProprio));
        sb.Append(RecordB(sostituto, cfSostituto, certificazioni.Count, conQuadroCt, inProprio, cfFornitore, invio));
        int recordC = 0, recordD = 0, recordG = 0;
        if (conQuadroCt)
        {
            sb.Append(RecordC(cfSostituto, emailCt!));
            recordC = 1;
        }
        for (int i = 0; i < certificazioni.Count; i++)
        {
            int progressivo = i + 1;
            foreach (var r in RecordDPerCu(certificazioni[i], cfSostituto, progressivo, invio.DataFirma))
            { sb.Append(r); recordD++; }
            foreach (var r in RecordGPerCu(certificazioni[i], cfSostituto, progressivo))
            { sb.Append(r); recordG++; }
        }
        sb.Append(RecordZ(recordC, recordD, recordG));
        return sb.ToString();
    }

    // ---- Record posizionali ----

    private static string RecordA(string cfFornitore, bool inProprio)
    {
        var r = new Rec('A');
        r.An(16, 5, CodiceFornitura);
        r.Nu(21, 2, inProprio ? 1 : 10); // 01 = invia le proprie dichiarazioni, 10 = intermediario
        r.An(23, 16, cfFornitore);
        return r.ToString();
    }

    private static string RecordB(
        SostitutoCu s, string cfSostituto, int numeroCertificazioni,
        bool conQuadroCt, bool inProprio, string cfFornitore, DatiInvioCu invio)
    {
        var r = new Rec('B');
        r.An(2, 16, cfSostituto);
        r.Nu(18, 8, 1);                       // progressivo modulo
        r.An(91, 1, "0");                     // fornitura di annullamento: no
        r.An(92, 1, "0");                     // fornitura di sostituzione: no
        r.An(137, 60, s.Denominazione);
        r.An(197, 100, s.Email);
        r.An(297, 12, SoloCifre(s.Telefono));
        // Rappresentante firmatario: obbligatorio per i sostituti diversi dalle persone fisiche.
        if (!string.IsNullOrWhiteSpace(s.RappresentanteCodiceFiscale))
        {
            r.An(311, 16, Pulisci(s.RappresentanteCodiceFiscale));
            r.Nu(327, 2, 1);                  // codice carica 1 = rappresentante legale
            r.An(329, 24, s.RappresentanteCognome);
            r.An(353, 20, s.RappresentanteNome);
        }
        r.Nu(373, 11, 0);                     // CF società dichiarante: assente (zero-fill)
        r.Nu(402, 8, numeroCertificazioni);
        r.An(410, 1, conQuadroCt ? "1" : "0");
        r.An(411, 1, "1");                    // firma della comunicazione
        // Impegno alla trasmissione: compilato solo quando trasmette un intermediario.
        if (!inProprio)
        {
            r.An(412, 16, cfFornitore);
            r.Nu(428, 1, 1);
            r.Nu(429, 8, DataCompatta(invio.DataFirma));
            r.An(437, 1, "1");
        }
        else
        {
            r.Nu(428, 1, 0);
            r.Nu(429, 8, 0);
            r.An(437, 1, "0");
        }
        r.An(527, 1, "0");                    // casella casi particolari
        return r.ToString();
    }

    private static string RecordC(string cfSostituto, string email)
    {
        var r = new Rec('C');
        r.An(2, 16, cfSostituto);
        r.Nu(18, 8, 1);
        var t = new Tabella();
        t.Cf("CT001001", cfSostituto);
        t.An("CT001003", email);
        t.Cb("CT002001", true); // sezione A, utenza Fisconline del sostituto
        t.ScriviSu(r);
        return r.ToString();
    }

    private static string RecordZ(int numeroC, int numeroD, int numeroG)
    {
        var r = new Rec('Z');
        r.Nu(16, 9, 1);        // record B
        r.Nu(25, 9, numeroC);
        r.Nu(34, 9, numeroD);
        r.Nu(43, 9, numeroG);
        r.Nu(52, 9, 0);        // record H
        r.Nu(61, 9, 0);        // record L
        return r.ToString();
    }

    // ---- Record a struttura variabile ----

    private static IEnumerable<string> RecordDPerCu(
        CertificazioneUnica cu, string cfSostituto, int progressivo, DateOnly dataFirma)
    {
        var s = cu.Sostituto;
        var p = cu.Percipiente;
        var t = new Tabella();
        // Dati del datore di lavoro.
        t.Cf("DA001001", cfSostituto);
        t.An("DA001002", s.Denominazione);
        t.An("DA001004", s.Comune);
        t.Pr("DA001005", s.Provincia);
        t.An("DA001006", s.Cap);
        t.An("DA001007", s.Indirizzo);
        t.An("DA001008", SoloCifre(s.Telefono));
        t.An("DA001009", s.Email);
        t.An("DA001010", SoloCifre(s.CodiceAttivita));
        // Dati del dipendente.
        t.Cf("DA002001", Pulisci(p.CodiceFiscale));
        t.An("DA002002", p.Cognome);
        t.An("DA002003", p.Nome);
        t.An("DA002004", p.Sesso);
        t.Dt("DA002005", p.DataNascita);
        t.An("DA002006", p.ComuneNascita);
        t.Pr("DA002007", p.ProvinciaNascita);
        // Domicilio fiscale all'1/1 dell'anno certificato e all'1/1 dell'anno successivo.
        t.An("DA002020", p.DomicilioComune);
        t.Pr("DA002021", p.DomicilioProvincia);
        t.An("DA002022", p.DomicilioCodiceComune);
        t.An("DA002024", p.DomicilioComune);
        t.Pr("DA002025", p.DomicilioProvincia);
        t.An("DA002026", p.DomicilioCodiceComune);
        // Firma della certificazione.
        t.Dt("DA003001", dataFirma);
        t.Cb("DA003002", true);

        return t.Records('D', r =>
        {
            r.An(2, 16, cfSostituto);
            r.Nu(18, 8, 1);
            r.An(26, 16, Pulisci(p.CodiceFiscale));
            r.Nu(42, 5, progressivo);
            r.Nu(47, 17, 0);  // protocollo sostituzione: assente (zero-fill)
            r.Nu(64, 6, 0);
            r.An(89, 1, "0"); // flag conferma certificazione
        });
    }

    private static IEnumerable<string> RecordGPerCu(
        CertificazioneUnica cu, string cfSostituto, int progressivo)
    {
        var f = cu.DatiFiscali;
        var prev = cu.Previdenza;
        var t = new Tabella();
        // Quadro DB — dati fiscali (il rigo del codice campo è il punto del modello).
        t.Vp("DB001001", f.RedditoTempoIndeterminato);
        t.Vp("DB001002", f.RedditoTempoDeterminato);
        t.Nu("DB001006", f.GiorniDetrazione);
        t.Dt("DB001008", f.DataInizioRapporto);
        if (f.DataCessazione is not null && !f.InForzaAl31Dicembre)
            t.Dt("DB001009", f.DataCessazione);
        else
            t.Cb("DB001010", f.InForzaAl31Dicembre);
        t.Vp("DB001021", f.RitenuteIrpef);
        t.Vp("DB001022", f.AddizionaleRegionale);
        t.Vp("DB001027", f.AddizionaleComunale);
        t.Vp("DB001361", f.ImpostaLorda);
        t.Vp("DB001367", f.DetrazioniLavoroDipendente);
        // Punto 374: il totale detrazioni riconosciuto non supera mai l'imposta lorda.
        t.Vp("DB001374", Math.Min(f.TotaleDetrazioni, f.ImpostaLorda));
        t.Vp("DB001375", f.ImpostaNetta);
        bool redditi = f.RedditoTempoIndeterminato > 0m || f.RedditoTempoDeterminato > 0m;
        if (redditi)
        {
            // Trattamento integrativo: il motore non lo eroga, quindi codice 2 (non erogato).
            t.Nu("DB001390", 2);
            // Somma che non concorre alla formazione del reddito: 2 = nessuna agevolazione
            // (impatriati/rientro cervelli) compresa nei redditi certificati. Obbligatorio.
            t.Nu("DB001718", 2);
            // Tipologie reddituali (sezione obbligatoria): codice 1 = lavoro dipendente art. 49 TUIR.
            t.Nu("DB001751", 1);
            t.Vp("DB001752", f.RedditoTempoIndeterminato + f.RedditoTempoDeterminato);
        }
        // Quadro DC — dati previdenziali INPS, sezione 1 lavoratori subordinati.
        if (prev.ImponibilePrevidenziale > 0m || prev.ContributiLavoratore > 0m)
        {
            t.Nu("DC001001", long.TryParse(SoloCifre(prev.MatricolaAzienda), out var m) ? m : 0);
            t.Cb("DC001002", true); // ente pensionistico INPS
            t.Vp("DC001004", prev.ImponibilePrevidenziale);
            t.Vp("DC001006", prev.ContributiLavoratore);
            var mesi = prev.MesiRetribuiti;
            if (mesi.Count >= 12)
                t.Cb("DC001007", true); // denuncia UniEmens presentata per tutti i mesi
            else if (mesi.Count > 0)
                t.Cb12("DC001008", Enumerable.Range(1, 12).Select(m2 => mesi.Contains(m2) ? '0' : '1'));
        }

        return t.Records('G', r =>
        {
            r.An(2, 16, cfSostituto);
            r.Nu(18, 8, 1);
            r.An(26, 16, Pulisci(cu.Percipiente.CodiceFiscale));
            r.Nu(42, 5, progressivo);
            r.Nu(47, 17, 0);
            r.Nu(64, 6, 0);
            r.Nu(89, 1, 0);
        });
    }

    // ---- Helper di formato ----

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private static string Pulisci(string? v) => (v ?? "").Trim().ToUpperInvariant();

    private static string SoloCifre(string? v) =>
        new((v ?? "").Where(char.IsAsciiDigit).ToArray());

    private static long DataCompatta(DateOnly d) => long.Parse($"{d.Day:00}{d.Month:00}{d.Year:0000}");

    /// <summary>Maiuscole senza segni diacritici (il tracciato ammette solo alfabetici maiuscoli).</summary>
    private static string Maiuscole(string v)
    {
        string norm = v.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (char c in norm)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToUpperInvariant(c));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Record a lunghezza fissa: 1898 caratteri (l'ultimo è "A") + CR LF.</summary>
    private sealed class Rec
    {
        private readonly char[] _b = new char[Lung];

        public Rec(char tipo)
        {
            Array.Fill(_b, ' ');
            _b[0] = tipo;
            _b[Lung - 1] = 'A'; // posizione 1898: carattere di controllo "A"
        }

        public Rec An(int pos, int len, string? v)
        {
            v = v is null ? "" : Maiuscole(v.Trim());
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

        /// <summary>Copia caratteri già formattati (allineamenti compresi) senza normalizzarli.</summary>
        public Rec Raw(int pos, string v)
        {
            for (int i = 0; i < v.Length; i++)
                _b[pos - 1 + i] = v[i];
            return this;
        }

        public override string ToString() => new string(_b) + "\r\n";
    }

    /// <summary>
    /// Tabella dei campi non posizionali di un record a struttura variabile: elementi da
    /// 24 caratteri (codice campo 8 + valore 16), massimo 75 per record a partire dal
    /// carattere 90; l'eccedenza produce un altro record dello stesso tipo.
    /// Si registrano solo i campi con un valore diverso da zero e da spazi.
    /// </summary>
    private sealed class Tabella
    {
        private const int ElementiPerRecord = 75;
        private readonly List<(string Codice, string Valore)> _campi = [];

        private void Aggiungi(string codice, string valore16) => _campi.Add((codice, valore16));

        /// <summary>Alfanumerico: sinistra; oltre 16 caratteri continua in elementi "+...".</summary>
        public void An(string codice, string? v)
        {
            string s = v is null ? "" : Maiuscole(v.Trim());
            if (s.Length == 0) return;
            Aggiungi(codice, s[..Math.Min(16, s.Length)].PadRight(16));
            for (int i = 16; i < s.Length; i += 15)
                Aggiungi(codice, ("+" + s[i..Math.Min(i + 15, s.Length)]).PadRight(16));
        }

        /// <summary>Codice fiscale: 16 caratteri com'è; 11 caratteri a sinistra con 5 spazi.</summary>
        public void Cf(string codice, string? v)
        {
            string s = Pulisci(v);
            if (s.Length == 0) return;
            Aggiungi(codice, s.PadRight(16));
        }

        /// <summary>Importo (VP): due decimali con virgola, allineato a destra.</summary>
        public void Vp(string codice, decimal v)
        {
            if (v == 0m) return;
            Aggiungi(codice, v.ToString("0.00", It).PadLeft(16));
        }

        /// <summary>Numerico (NU/NP/Nx): allineato a destra con spazi.</summary>
        public void Nu(string codice, long v)
        {
            if (v == 0) return;
            Aggiungi(codice, v.ToString(CultureInfo.InvariantCulture).PadLeft(16));
        }

        /// <summary>Casella barrata: 15 spazi + '1' (assente se non barrata).</summary>
        public void Cb(string codice, bool barrata)
        {
            if (!barrata) return;
            Aggiungi(codice, "1".PadLeft(16));
        }

        /// <summary>Dodici caselle mensili (CB12): 4 spazi + 12 cifre 0/1.</summary>
        public void Cb12(string codice, IEnumerable<char> dodici)
        {
            string s = new(dodici.ToArray());
            if (s.Length != 12 || s.All(c => c == '0')) return;
            Aggiungi(codice, s.PadLeft(16));
        }

        /// <summary>Data GGMMAAAA allineata a destra (8 spazi a sinistra).</summary>
        public void Dt(string codice, DateOnly? d)
        {
            if (d is null) return;
            Aggiungi(codice, $"{d.Value.Day:00}{d.Value.Month:00}{d.Value.Year:0000}".PadLeft(16));
        }

        /// <summary>Sigla di provincia (PR/PN): a sinistra con 14 spazi.</summary>
        public void Pr(string codice, string? sigla)
        {
            string s = Pulisci(sigla);
            if (s.Length == 0) return;
            Aggiungi(codice, s.PadRight(16));
        }

        /// <summary>Scrive i primi 75 elementi sul record indicato (record singolo già costruito).</summary>
        public void ScriviSu(Rec r)
        {
            for (int i = 0; i < _campi.Count && i < ElementiPerRecord; i++)
                r.Raw(90 + i * 24, _campi[i].Codice + _campi[i].Valore);
        }

        /// <summary>Produce i record (uno o più da 75 elementi) del tipo indicato.</summary>
        public IEnumerable<string> Records(char tipo, Action<Rec> testataPosizionale)
        {
            for (int inizio = 0; inizio < _campi.Count; inizio += ElementiPerRecord)
            {
                var r = new Rec(tipo);
                testataPosizionale(r);
                for (int i = inizio; i < _campi.Count && i < inizio + ElementiPerRecord; i++)
                    r.Raw(90 + (i - inizio) * 24, _campi[i].Codice + _campi[i].Valore);
                yield return r.ToString();
            }
        }
    }
}
