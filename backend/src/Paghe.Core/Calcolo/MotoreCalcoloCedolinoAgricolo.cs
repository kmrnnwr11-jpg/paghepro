using Paghe.Core.Domain;
using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Motore di calcolo del cedolino dell'operaio agricolo. È separato da
/// <see cref="MotoreCalcoloCedolino"/> perché il settore ragiona in modo diverso, non perché
/// abbia qualche voce in più: la paga nasce dalle ore effettive per la tariffa del contratto
/// provinciale, le trattenute sono più di una e con aliquote provinciali, l'imponibile
/// contributivo si arrotonda all'euro e il netto si arrotonda trascinando la differenza.
///
/// Quello che questo motore NON fa, e non deve fingere di fare: i contributi a carico
/// dell'azienda in agricoltura li calcola l'INPS sulla denuncia mensile UniEmens/PosAgri e li chiede con un
/// F24 già compilato. Qui si espone soltanto quanto risulta dal contratto, come dato figurativo.
/// </summary>
public sealed class MotoreCalcoloCedolinoAgricolo
{
    private readonly ParametriNormativi _parametri;
    private readonly ParametriAgricoli _agricoli;

    public MotoreCalcoloCedolinoAgricolo(ParametriNormativi parametri, ParametriAgricoli agricoli)
    {
        _parametri = parametri;
        _agricoli = agricoli;
    }

    public Cedolino Calcola(RapportoAgricolo rapporto, CartellinoAgricolo cartellino)
    {
        // Le sole tabelle salariali oggi validate derivano da buste OTD. Un OTI ha regole
        // retributive proprie: applicargli salario orario + terzo elemento OTD produrrebbe una
        // busta formalmente plausibile ma sostanzialmente errata. Restiamo quindi fail-closed
        // finche non saranno caricati parametri distinti e un caso golden OTI.
        if (rapporto.Tipo != TipoOperaioAgricolo.Determinato)
            throw new ArgumentException(
                "Il calcolo OTI non e ancora disponibile: le tabelle validate coprono solo gli OTD. " +
                "Caricare parametri OTI distinti e validarli con un professionista prima del calcolo.");

        if (!string.Equals(rapporto.Provincia, _agricoli.Provincia, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Il rapporto è del CPL di {rapporto.Provincia} ma i parametri caricati sono di " +
                $"{_agricoli.Provincia}: le tariffe agricole sono provinciali e non si sostituiscono.");

        var cedolino = new Cedolino { Anno = cartellino.Anno, Mese = cartellino.Mese };
        var tariffa = _agricoli.Tariffa(rapporto.Livello);

        // ---- 1. Competenze: ore effettive × tariffa oraria del CPL ---------------------
        decimal retribuzioneOrdinaria = Arrotondamento.Euro(cartellino.OreOrdinarie * tariffa.PagaOraria);
        AggiungiRiga(cedolino, "AGR_ORD",
            $"Lavoro ordinario {Sigla(rapporto.Tipo)} (salario provinciale + terzo elemento)",
            TipoVoce.Competenza,
            quantita: cartellino.OreOrdinarie, @base: tariffa.PagaOraria, importo: retribuzioneOrdinaria);

        if (cartellino.UnaTantum > 0m)
            AggiungiRiga(cedolino, "AGR_UNATANTUM", "Una tantum", TipoVoce.Competenza,
                importo: Arrotondamento.Euro(cartellino.UnaTantum));

        // Trasferta e rimborsi documentati: stessa regola del motore ordinario. Le voci sono
        // competenze (entrano nel netto); gli imponibili vengono poi ridotti della sola quota
        // esente, così l'eccedenza oltre soglia resta tassata come retribuzione.
        decimal sommeEsenti = 0m;
        IReadOnlyList<TrasfertaGiorniAgricola> gruppiTrasferta = cartellino.Trasferte.Count > 0
            ? cartellino.Trasferte
            : cartellino.GiorniTrasferta > 0m && cartellino.IndennitaTrasfertaGiornaliera > 0m
                ? [new TrasfertaGiorniAgricola(
                    cartellino.GiorniTrasferta, cartellino.IndennitaTrasfertaGiornaliera)]
                : [];
        // Gruppi con lo stesso importo giornaliero → una sola riga in busta (es. 4 g + 1 g
        // a 20 € diventano 5 g × 20 €): stessa aritmetica, lettura più onesta.
        gruppiTrasferta = gruppiTrasferta
            .GroupBy(g => g.ImportoGiorno)
            .Select(g => new TrasfertaGiorniAgricola(g.Sum(x => x.Giorni), g.Key))
            .ToList();
        foreach (var gruppo in gruppiTrasferta)
        {
            var trasferta = CalcoloElementiAgevolati.Trasferta(
                gruppo.Giorni, gruppo.ImportoGiorno,
                _parametri.TrasfertaItaliaEsenteGiorno, TipoRimborsoTrasferta.Forfettario);
            if (trasferta.Esente > 0m)
            {
                AggiungiRiga(cedolino, "AGR_TRASF", "Indennità di trasferta Italia (esente)",
                    TipoVoce.Competenza, quantita: gruppo.Giorni,
                    @base: gruppo.ImportoGiorno, importo: trasferta.Esente);
                sommeEsenti += trasferta.Esente;
            }
            if (trasferta.Imponibile > 0m)
                AggiungiRiga(cedolino, "AGR_TRASF_IMP",
                    "Indennità di trasferta oltre soglia (imponibile)", TipoVoce.Competenza,
                    quantita: gruppo.Giorni, importo: trasferta.Imponibile);
        }
        if (cartellino.RimborsiSpeseDocumentate > 0m)
        {
            decimal rimborso = Arrotondamento.Euro(cartellino.RimborsiSpeseDocumentate);
            AggiungiRiga(cedolino, "AGR_RIMB_SPESE", "Rimborso spese documentate (esente)",
                TipoVoce.Competenza, importo: rimborso);
            sommeEsenti += rimborso;
        }

        // Le giornate non pagano nulla: sono il dato che finisce nella denuncia mensile PosAgri
        // all'INPS e nell'estratto conto del lavoratore. Restano in busta perché sono la cosa
        // che l'operaio agricolo controlla per prima.
        AggiungiRiga(cedolino, "AGR_GG", "Giornate lavorate del mese", TipoVoce.Figurativa,
            quantita: cartellino.GiornateLavorate, importo: 0m);

        decimal totaleCompetenze = Somma(cedolino, TipoVoce.Competenza);

        // ---- 2. Contributi: base arrotondata all'euro, una voce per contributo ---------
        // Le somme esenti (trasferta entro soglia, rimborsi documentati) non entrano nella base.
        decimal imponibileContributivo = Math.Round(
            totaleCompetenze - sommeEsenti, 0, MidpointRounding.AwayFromZero);

        decimal contributiDipendente = 0m;
        decimal contributiDatore = 0m;
        foreach (var contributo in _agricoli.Contributi)
        {
            decimal importo = Arrotondamento.Euro(imponibileContributivo * contributo.Aliquota);
            if (importo == 0m) continue;

            // L'etichetta stampa l'aliquota con TUTTI i decimali significativi: la stessa
            // costante configurata guida calcolo e dicitura, mai due percentuali diverse.
            string etichetta =
                $"{contributo.Descrizione} ({FormatoAliquota.Percentuale(contributo.Aliquota)})";
            if (contributo.ACaricoDipendente)
            {
                AggiungiRiga(cedolino, contributo.Codice, etichetta, TipoVoce.Trattenuta,
                    @base: imponibileContributivo, importo: -importo);
                contributiDipendente += importo;
            }
            else
            {
                AggiungiRiga(cedolino, contributo.Codice, etichetta, TipoVoce.Figurativa,
                    @base: imponibileContributivo, importo: importo);
                contributiDatore += importo;
            }
        }

        // ---- 3. IRPEF ------------------------------------------------------------------
        // Solo una parte dei contributi trattenuti riduce l'imponibile fiscale: decide il
        // flag DeducibileIrpef di ogni contributo (di regola: FAP sì, assistenza contrattuale
        // no), salvo il valore osservato sulle buste reali quando configurato.
        decimal contributiDeducibili = Arrotondamento.Euro(
            imponibileContributivo * _agricoli.AliquotaDeducibileEffettiva);
        decimal imponibileFiscale = Arrotondamento.Euro(
            totaleCompetenze - sommeEsenti - contributiDeducibili);

        // Aliquota del reddito che il lavoratore realizzerà davvero nell'anno, non del mese × 12.
        decimal redditoPresunto = rapporto.RedditoPresuntoAnnuo ?? imponibileFiscale * 12m;
        decimal irpefLorda = Arrotondamento.Euro(imponibileFiscale * AliquotaMedia(redditoPresunto));

        decimal detrazioneAnnua = rapporto.DetrazioniLavoroDipendente
            ? CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(
                redditoPresunto, cartellino.GiorniDetrazione, MinimoDetrazione(rapporto.Tipo))
            : 0m;
        decimal detrazioneResidua = Math.Max(0m, detrazioneAnnua - cartellino.DetrazioniGiaGoduteAnno);
        decimal detrazioneGoduta = Math.Min(irpefLorda, detrazioneResidua);

        decimal irpefNetta = Arrotondamento.Euro(irpefLorda - detrazioneGoduta);
        if (irpefNetta > 0m)
            AggiungiRiga(cedolino, "IRPEF", "IRPEF netta del mese", TipoVoce.Trattenuta,
                @base: imponibileFiscale, importo: -irpefNetta);

        // Trattamento integrativo (art. 1 DL 3/2020): spetta entro la soglia di reddito e
        // SOLO con capienza, cioè se l'IRPEF lorda annua presunta supera la detrazione annua
        // art. 13 c.1 dell'intero anno. Quota del mese rapportata ai giorni di detrazione.
        // Non è retribuzione: resta fuori da imponibile contributivo e fiscale (già calcolati).
        decimal trattamentoIntegrativo = 0m;
        if (rapporto.DetrazioniLavoroDipendente
            && redditoPresunto > 0m
            && redditoPresunto <= _parametri.TrattamentoIntegrativoSogliaReddito
            && cartellino.GiorniDetrazione > 0)
        {
            decimal irpefLordaAnnua = CalcoloIrpef.IrpefLordaAnnua(
                redditoPresunto, _parametri.ScaglioniIrpef);
            decimal detrazioneAnnuaIntera = CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(
                redditoPresunto, 365, MinimoDetrazione(rapporto.Tipo));
            if (irpefLordaAnnua > detrazioneAnnuaIntera)
            {
                trattamentoIntegrativo = Arrotondamento.Euro(
                    _parametri.TrattamentoIntegrativoAnnuo * cartellino.GiorniDetrazione / 365m);
                AggiungiRiga(cedolino, "TRATT_INTEGR",
                    "Trattamento integrativo (art. 1 DL 3/2020)", TipoVoce.Competenza,
                    importo: trattamentoIntegrativo);
                totaleCompetenze += trattamentoIntegrativo;
            }
        }

        // ---- 4. Addizionali ------------------------------------------------------------
        // Ripartite sulla quota di reddito annuo maturata nel mese. Restano a zero finché non
        // si conosce il comune di residenza: inventare un'aliquota qui è una trattenuta finta.
        decimal quotaAnno = redditoPresunto > 0m ? imponibileFiscale / redditoPresunto : 0m;
        decimal addRegionale = Arrotondamento.Euro(
            CalcoloAddizionali.Dovuta(redditoPresunto, _parametri.RegimeRegionale(rapporto.Regione)) * quotaAnno);
        decimal addComunale = Arrotondamento.Euro(
            CalcoloAddizionali.Dovuta(redditoPresunto, _parametri.RegimeComunale(rapporto.CodiceCatastaleComune)) * quotaAnno);

        if (addRegionale > 0m)
            AggiungiRiga(cedolino, "ADD_REG", "Addizionale regionale", TipoVoce.Trattenuta,
                importo: -addRegionale);
        if (addComunale > 0m)
            AggiungiRiga(cedolino, "ADD_COM", "Addizionale comunale", TipoVoce.Trattenuta,
                importo: -addComunale);

        // ---- 5. Netto e arrotondamento -------------------------------------------------
        // L'arrotondamento anticipato nel mese precedente si recupera SUBITO nella busta
        // successiva, come voce esplicita, prima di calcolare il nuovo arrotondamento.
        if (cartellino.ArrotondamentoRiportato != 0m)
            AggiungiRiga(cedolino, "AGR_ARROT_REC", "Recupero arrotondamento mese precedente",
                TipoVoce.Trattenuta,
                importo: -Arrotondamento.Euro(cartellino.ArrotondamentoRiportato));

        decimal totaleTrattenute = Somma(cedolino, TipoVoce.Trattenuta);
        decimal nettoEsatto = Arrotondamento.Euro(totaleCompetenze - totaleTrattenute);

        // Nei mesi ordinari il netto sale all'euro pieno — sempre per eccesso, mai a scendere:
        // sulle buste reali 599,12 diventa 600,00 — e la differenza anticipata si trascina.
        // L'ultimo cedolino paga l'esatto e chiude il conto degli arrotondamenti.
        decimal netto = cartellino.UltimoCedolino
            ? nettoEsatto
            : Math.Ceiling(nettoEsatto);
        decimal arrotondamento = Arrotondamento.Euro(netto - nettoEsatto);

        if (arrotondamento != 0m)
            AggiungiRiga(cedolino, "AGR_ARROT", "Arrotondamento del netto",
                TipoVoce.Figurativa, importo: arrotondamento);

        cedolino.TotaleCompetenze = totaleCompetenze;
        cedolino.TotaleTrattenute = totaleTrattenute;
        cedolino.ImponibileInps = imponibileContributivo;
        cedolino.ContributiDipendente = contributiDipendente;
        cedolino.ContributiDatore = contributiDatore;
        cedolino.ImponibileIrpef = imponibileFiscale;
        cedolino.IrpefLorda = irpefLorda;
        cedolino.DetrazioniLavoroDipendente = detrazioneGoduta;
        cedolino.IrpefNetta = irpefNetta;
        cedolino.AddizionaleRegionale = addRegionale;
        cedolino.AddizionaleComunale = addComunale;
        cedolino.NettoInBusta = netto;
        cedolino.CostoAzienda = Arrotondamento.Euro(totaleCompetenze + contributiDatore);

        return cedolino;
    }

    /// <summary>
    /// Aliquota media dell'anno presunto: l'imposta del mese è la quota di un'imposta annua,
    /// non un calcolo a sé. Sotto i 28.000 € coincide con l'aliquota del primo scaglione.
    /// </summary>
    private decimal AliquotaMedia(decimal redditoPresunto) =>
        redditoPresunto <= 0m
            ? 0m
            : CalcoloIrpef.IrpefLordaAnnua(redditoPresunto, _parametri.ScaglioniIrpef) / redditoPresunto;

    private decimal MinimoDetrazione(TipoOperaioAgricolo tipo) =>
        tipo == TipoOperaioAgricolo.Determinato
            ? _parametri.DetrazioneMinimaTempoDeterminato
            : _parametri.DetrazioneMinimaTempoIndeterminato;

    private static string Sigla(TipoOperaioAgricolo tipo) =>
        tipo == TipoOperaioAgricolo.Determinato ? "OTD" : "OTI";

    private static decimal Somma(Cedolino cedolino, TipoVoce tipo) =>
        cedolino.Righe.Where(r => r.Tipo == tipo).Sum(r => Math.Abs(r.Importo));

    private static void AggiungiRiga(
        Cedolino cedolino, string codice, string descrizione, TipoVoce tipo,
        decimal? quantita = null, decimal? @base = null, decimal importo = 0m) =>
        cedolino.Righe.Add(new CedolinoRiga
        {
            Codice = codice,
            Descrizione = descrizione,
            Tipo = tipo,
            Quantita = quantita,
            Base = @base,
            Importo = importo,
        });
}
