using Paghe.Core.Domain;
using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Motore di calcolo del cedolino: a partire dal rapporto di lavoro, dal cartellino
/// presenze e dai parametri normativi, produce il cedolino completo (lordo → netto).
///
/// Metodo IRPEF/addizionali (MVP): metodo presuntivo su base 12. L'imponibile del mese
/// viene annualizzato (× 12), si calcola l'imposta annua e la si riporta al mese (/ 12).
/// Il conguaglio di fine anno, le mensilità aggiuntive e la rateazione effettiva delle
/// addizionali sono demandati a una fase successiva.
/// </summary>
public sealed class MotoreCalcoloCedolino
{
    private readonly ParametriNormativi _parametri;
    private readonly Ccnl _ccnl;

    public MotoreCalcoloCedolino(ParametriNormativi parametri, Ccnl ccnl)
    {
        _parametri = parametri;
        _ccnl = ccnl;
    }

    public Cedolino Calcola(RapportoLavoro rapporto, Cartellino cartellino)
    {
        var cedolino = new Cedolino { Anno = cartellino.Anno, Mese = cartellino.Mese };

        // ---- 1. Retribuzione di base (full-time) -------------------------------------
        var liv = _ccnl.Livello(rapporto.Livello);
        decimal coeffPt = rapporto.PartTime
            ? rapporto.PercentualePartTime / 100m
            : 1m;

        decimal minimo = liv.MinimoTabellare;
        decimal edr = liv.Edr;
        decimal scatti = liv.ImportoScatto * rapporto.ScattiAnzianita;
        decimal superminimo = rapporto.Superminimo;

        // Paga oraria (su base full-time) per straordinari e assenze.
        decimal retribuzioneMensileFullTime = minimo + edr + scatti + superminimo;
        decimal pagaOraria = retribuzioneMensileFullTime / _parametri.DivisoreOrario;

        // ---- 2. Competenze ------------------------------------------------------------
        AggiungiRiga(cedolino, "PAGA_BASE", "Minimo tabellare", TipoVoce.Competenza,
            importo: Arrotondamento.Euro(minimo * coeffPt));

        if (edr > 0m)
            AggiungiRiga(cedolino, "EDR", "Elemento distinto retribuzione", TipoVoce.Competenza,
                importo: Arrotondamento.Euro(edr * coeffPt));

        if (rapporto.ScattiAnzianita > 0)
            AggiungiRiga(cedolino, "SCATTI", $"Scatti di anzianità ({rapporto.ScattiAnzianita})",
                TipoVoce.Competenza, quantita: rapporto.ScattiAnzianita, @base: liv.ImportoScatto,
                importo: Arrotondamento.Euro(scatti * coeffPt));

        if (superminimo > 0m)
            AggiungiRiga(cedolino, "SUPERMIN", "Superminimo individuale", TipoVoce.Competenza,
                importo: Arrotondamento.Euro(superminimo * coeffPt));

        AggiungiStraordinario(cedolino, "STRAORD", "Lavoro straordinario",
            cartellino.OreStraordinario, pagaOraria, _parametri.MaggiorazioneStraordinario);
        AggiungiStraordinario(cedolino, "STRAORD_NOTT", "Straordinario notturno",
            cartellino.OreStraordinarioNotturno, pagaOraria, _parametri.MaggiorazioneStraordinarioNotturno);
        AggiungiStraordinario(cedolino, "STRAORD_FEST", "Straordinario festivo",
            cartellino.OreStraordinarioFestivo, pagaOraria, _parametri.MaggiorazioneStraordinarioFestivo);

        // Assenze non retribuite: decurtano il lordo.
        if (cartellino.OreAssenzaNonRetribuita > 0m)
        {
            decimal decurtazione = Arrotondamento.Euro(cartellino.OreAssenzaNonRetribuita * pagaOraria);
            AggiungiRiga(cedolino, "ASSENZA", "Assenza non retribuita", TipoVoce.Competenza,
                quantita: cartellino.OreAssenzaNonRetribuita, @base: pagaOraria, importo: -decurtazione);
        }

        decimal totaleCompetenze = SommaCompetenze(cedolino);

        // ---- 3. Contributi previdenziali ---------------------------------------------
        decimal imponibileInps = totaleCompetenze;
        var aliquotaBase = _parametri.AliquotaInps(rapporto.Qualifica);
        // Aziende >15 dipendenti (CIG): +0,30% di contributo aggiuntivo c/dipendente (9,19% -> 9,49%).
        // Regola generale, non legata al settore.
        var aliquota = rapporto.AziendaOltre15Dipendenti
            ? aliquotaBase with { Dipendente = aliquotaBase.Dipendente + 0.003m }
            : aliquotaBase;
        var (contributiDip, contributiDatore) = CalcoloContributi.Calcola(imponibileInps, aliquota);

        AggiungiRiga(cedolino, "INPS_DIP", $"Contributi INPS c/dipendente ({aliquota.Dipendente:P2})",
            TipoVoce.Trattenuta, @base: imponibileInps, importo: -contributiDip);

        // ---- 4. IRPEF -----------------------------------------------------------------
        decimal imponibileIrpefMese = imponibileInps - contributiDip;
        decimal redditoAnnuoPresunto = imponibileIrpefMese * 12m;

        decimal irpefLordaAnnua = CalcoloIrpef.IrpefLordaAnnua(redditoAnnuoPresunto, _parametri.ScaglioniIrpef);
        decimal irpefLordaMese = Arrotondamento.Euro(irpefLordaAnnua / 12m);

        decimal detrazioneAnnua = rapporto.DetrazioniLavoroDipendente
            ? CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(redditoAnnuoPresunto, rapporto.GiorniDetrazioneAnno)
            : 0m;
        decimal detrazioneMese = Arrotondamento.Euro(detrazioneAnnua / 12m);

        decimal irpefNetta = Math.Max(0m, irpefLordaMese - detrazioneMese);
        AggiungiRiga(cedolino, "IRPEF", "IRPEF netta del mese", TipoVoce.Trattenuta,
            @base: imponibileIrpefMese, importo: -irpefNetta);

        // ---- 5. Addizionali regionale e comunale -------------------------------------
        decimal aliquotaReg = _parametri.AddizionaleRegionale.GetValueOrDefault(rapporto.Regione, 0m);
        decimal aliquotaCom = _parametri.AddizionaleComunale.GetValueOrDefault(rapporto.CodiceCatastaleComune, 0m);

        decimal addRegMese = Arrotondamento.Euro(redditoAnnuoPresunto * aliquotaReg / 12m);
        decimal addComMese = Arrotondamento.Euro(redditoAnnuoPresunto * aliquotaCom / 12m);

        if (addRegMese > 0m)
            AggiungiRiga(cedolino, "ADD_REG", $"Addizionale regionale ({aliquotaReg:P2})",
                TipoVoce.Trattenuta, importo: -addRegMese);
        if (addComMese > 0m)
            AggiungiRiga(cedolino, "ADD_COM", $"Addizionale comunale ({aliquotaCom:P2})",
                TipoVoce.Trattenuta, importo: -addComMese);

        // ---- 6. TFR (figurativo) ------------------------------------------------------
        decimal retribuzioneUtileTfr = Arrotondamento.Euro(retribuzioneMensileFullTime * coeffPt);
        decimal quotaTfr = CalcoloTfr.QuotaMensile(
            retribuzioneUtileTfr, rapporto.NumeroMensilita, _parametri.TfrDivisore, _parametri.TfrContributoFondo);
        AggiungiRiga(cedolino, "TFR", "Quota TFR del mese", TipoVoce.Figurativa, importo: quotaTfr);

        // Contributi c/datore (figurativi, ai fini del costo aziendale).
        AggiungiRiga(cedolino, "INPS_DATORE", $"Contributi INPS c/datore ({aliquota.Datore:P2})",
            TipoVoce.Figurativa, @base: imponibileInps, importo: contributiDatore);

        // ---- 7. Totali ----------------------------------------------------------------
        decimal totaleTrattenute = SommaTrattenute(cedolino);

        cedolino.TotaleCompetenze = totaleCompetenze;
        cedolino.TotaleTrattenute = totaleTrattenute;
        cedolino.ImponibileInps = imponibileInps;
        cedolino.ContributiDipendente = contributiDip;
        cedolino.ContributiDatore = contributiDatore;
        cedolino.ImponibileIrpef = imponibileIrpefMese;
        cedolino.IrpefLorda = irpefLordaMese;
        cedolino.DetrazioniLavoroDipendente = detrazioneMese;
        cedolino.IrpefNetta = irpefNetta;
        cedolino.AddizionaleRegionale = addRegMese;
        cedolino.AddizionaleComunale = addComMese;
        cedolino.NettoInBusta = Arrotondamento.Euro(totaleCompetenze - totaleTrattenute);
        cedolino.QuotaTfr = quotaTfr;
        cedolino.CostoAzienda = Arrotondamento.Euro(totaleCompetenze + contributiDatore + quotaTfr);

        return cedolino;
    }

    private static void AggiungiStraordinario(
        Cedolino cedolino, string codice, string descrizione,
        decimal ore, decimal pagaOraria, decimal maggiorazione)
    {
        if (ore <= 0m)
            return;
        decimal pagaOrariaMagg = pagaOraria * (1m + maggiorazione);
        decimal importo = Arrotondamento.Euro(ore * pagaOrariaMagg);
        AggiungiRiga(cedolino, codice, $"{descrizione} (+{maggiorazione:P0})", TipoVoce.Competenza,
            quantita: ore, @base: Arrotondamento.Euro(pagaOrariaMagg), importo: importo);
    }

    private static void AggiungiRiga(
        Cedolino cedolino, string codice, string descrizione, TipoVoce tipo,
        decimal importo, decimal? quantita = null, decimal? @base = null)
    {
        cedolino.Righe.Add(new CedolinoRiga
        {
            Codice = codice,
            Descrizione = descrizione,
            Tipo = tipo,
            Quantita = quantita,
            Base = @base,
            Importo = importo
        });
    }

    private static decimal SommaCompetenze(Cedolino cedolino) =>
        cedolino.Righe.Where(r => r.Tipo == TipoVoce.Competenza).Sum(r => r.Importo);

    private static decimal SommaTrattenute(Cedolino cedolino) =>
        -cedolino.Righe.Where(r => r.Tipo == TipoVoce.Trattenuta).Sum(r => r.Importo);
}
