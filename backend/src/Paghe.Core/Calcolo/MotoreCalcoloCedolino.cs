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
    private readonly RegoleCcnl _regole;

    public MotoreCalcoloCedolino(ParametriNormativi parametri, Ccnl ccnl)
    {
        _parametri = parametri;
        _ccnl = ccnl;
        // Divisori, maggiorazioni e maturazioni cambiano da contratto a contratto: si leggono
        // una volta qui. Se il CCNL non ha regole proprie valgono quelle generali dell'anno.
        _regole = parametri.Regole(ccnl.Codice);
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

        // Paga oraria e giornaliera (su base full-time/part-time) per straordinari, permessi e assenze.
        decimal retribuzioneMensileFullTime = minimo + edr + scatti + superminimo;
        decimal retribuzioneMensileEffettiva = Arrotondamento.Euro(retribuzioneMensileFullTime * coeffPt);
        decimal pagaOraria = retribuzioneMensileFullTime / _regole.DivisoreOrario;
        decimal pagaGiornaliera = Arrotondamento.Euro(
            retribuzioneMensileEffettiva / _regole.DivisoreGiornaliero);
        // RMG per indennità INPS (comprensiva dei ratei 13a e 14a su base 30esimi)
        decimal rmgInps = Arrotondamento.Euro((retribuzioneMensileEffettiva * (rapporto.NumeroMensilita / 12m)) / 30m);

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

        // Ferie e Permessi goduti (voci informative/giustificative)
        if (cartellino.GiorniFerie > 0m)
        {
            AggiungiRiga(cedolino, "FERIE", "Ferie godute", TipoVoce.Competenza,
                quantita: cartellino.GiorniFerie, @base: pagaGiornaliera, importo: 0m);
        }

        decimal orePermessiTot = cartellino.OrePermessi + cartellino.OreExFestivita;
        if (orePermessiTot > 0m)
        {
            AggiungiRiga(cedolino, "ROL", "Permessi ROL / Ex-festività goduti", TipoVoce.Competenza,
                quantita: orePermessiTot, @base: pagaOraria, importo: 0m);
        }

        // Straordinari
        AggiungiStraordinario(cedolino, "STRAORD", "Lavoro straordinario",
            cartellino.OreStraordinario, pagaOraria, _regole.MaggiorazioneStraordinario);
        AggiungiStraordinario(cedolino, "STRAORD_NOTT", "Straordinario notturno",
            cartellino.OreStraordinarioNotturno, pagaOraria, _regole.MaggiorazioneStraordinarioNotturno);
        AggiungiStraordinario(cedolino, "STRAORD_FEST", "Straordinario festivo",
            cartellino.OreStraordinarioFestivo, pagaOraria, _regole.MaggiorazioneStraordinarioFestivo);

        // Malattia INPS + Integrazione Datore
        if (cartellino.GiorniMalattia > 0m)
        {
            // La paga mensile e gia interamente esposta sopra: senza questo storno le indennita
            // di malattia verrebbero sommate a giornate ordinarie gia pagate due volte.
            decimal stornoMalattia = Arrotondamento.Euro(cartellino.GiorniMalattia * pagaGiornaliera);
            AggiungiRiga(cedolino, "MAL_STORNO", "Storno retribuzione per malattia", TipoVoce.Competenza,
                quantita: cartellino.GiorniMalattia, @base: pagaGiornaliera, importo: -stornoMalattia);
            CalcolaMalattia(cedolino, cartellino.GiorniMalattia, cartellino.NumeroEventoMalattiaAnno, pagaGiornaliera, rmgInps);
        }

        // Infortunio INAIL + Integrazione Datore
        if (cartellino.GiorniInfortunio > 0m)
        {
            // Come per la malattia, si sostituisce la quota ordinaria con le specifiche
            // componenti INAIL/datore invece di aggiungerle alla mensilita piena.
            decimal stornoInfortunio = Arrotondamento.Euro(cartellino.GiorniInfortunio * pagaGiornaliera);
            AggiungiRiga(cedolino, "INF_STORNO", "Storno retribuzione per infortunio", TipoVoce.Competenza,
                quantita: cartellino.GiorniInfortunio, @base: pagaGiornaliera, importo: -stornoInfortunio);
            CalcolaInfortunio(cedolino, cartellino.GiorniInfortunio, pagaGiornaliera);
        }

        // Assenze non retribuite: decurtano il lordo.
        if (cartellino.OreAssenzaNonRetribuita > 0m)
        {
            decimal decurtazione = Arrotondamento.Euro(cartellino.OreAssenzaNonRetribuita * pagaOraria);
            AggiungiRiga(cedolino, "ASSENZA", "Assenza non retribuita", TipoVoce.Competenza,
                quantita: cartellino.OreAssenzaNonRetribuita, @base: pagaOraria, importo: -decurtazione);
        }

        // ---- 2-bis. Elementi retributivi agevolati ------------------------------------
        // Le quote esenti entrano nel netto ma non nell'imponibile; le eccedenze sono
        // retribuzione ordinaria a tutti gli effetti.
        var agevolati = AggiungiElementiAgevolati(cedolino, cartellino, _parametri);

        // Gli ANF, quando ancora spettanti, sono esposti come competenza ma non formano imponibile
        // previdenziale/fiscale e sono recuperati dal datore: non aumentano il costo aziendale.
        decimal assegniFamiliari = cartellino.StatoAssegniFamiliari == StatoAssegniFamiliari.Spettanti
            ? cartellino.ImportoAssegniFamiliari
            : 0m;
        if (assegniFamiliari > 0m)
            AggiungiRiga(cedolino, "ANF", "Assegni per il nucleo familiare",
                TipoVoce.Competenza, importo: assegniFamiliari);

        decimal totaleCompetenze = SommaCompetenze(cedolino);

        // ---- 3. Contributi previdenziali ---------------------------------------------
        // Le somme esenti sono escluse dalla base contributiva; il premio di risultato NO:
        // l'agevolazione sostituisce la sola IRPEF, i contributi restano dovuti per intero.
        decimal imponibileInps = totaleCompetenze - agevolati.TotaleEsenti - assegniFamiliari;
        var aliquotaBase = _parametri.AliquotaInps(rapporto.Qualifica);
        // Quota c/dipendente effettiva: base + 0,30% CIG (>15 dip) + fondi di settore (FIS, FSBS...).
        var quotaDip = CalcoloContributi.QuotaInpsDipendente(
            aliquotaBase.Dipendente, rapporto.AziendaOltre15Dipendenti, rapporto.ContributiFondiSettore);
        var aliquota = aliquotaBase with { Dipendente = quotaDip };
        var (contributiDip, contributiDatore) = CalcoloContributi.Calcola(imponibileInps, aliquota);

        AggiungiRiga(cedolino, "INPS_DIP", $"Contributi INPS c/dipendente ({aliquota.Dipendente:P2})",
            TipoVoce.Trattenuta, @base: imponibileInps, importo: -contributiDip);

        // ---- 4. IRPEF -----------------------------------------------------------------
        // Il premio di risultato agevolato esce dalla base IRPEF ordinaria: paga l'imposta
        // sostitutiva calcolata più sotto, al netto dei suoi contributi.
        decimal contributiSuPremio = Arrotondamento.Euro(
            agevolati.PremioAgevolato * aliquota.Dipendente);
        decimal premioNettoContributi = agevolati.PremioAgevolato - contributiSuPremio;

        decimal imponibileIrpefMese = imponibileInps - contributiDip - premioNettoContributi;
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

        // ---- 5. Addizionali regionale e comunale (a scaglioni + esenzione, dati MEF) --
        // Metodo presuntivo mensile: addizionale annua dovuta sul reddito annuo presunto, /12.
        var regimeReg = _parametri.RegimeRegionale(rapporto.Regione);
        var regimeCom = _parametri.RegimeComunale(rapporto.CodiceCatastaleComune);

        decimal addRegMese = Arrotondamento.Euro(
            CalcoloAddizionali.Dovuta(redditoAnnuoPresunto, regimeReg) / 12m);
        decimal addComMese = Arrotondamento.Euro(
            CalcoloAddizionali.Dovuta(redditoAnnuoPresunto, regimeCom) / 12m);

        // Imposta sostitutiva sul premio di risultato (in luogo di IRPEF e addizionali).
        decimal impostaSostitutivaPremio = Arrotondamento.Euro(
            premioNettoContributi * _parametri.PremioRisultatoAliquota);
        if (impostaSostitutivaPremio > 0m)
            AggiungiRiga(cedolino, "PREMIO_IMP",
                $"Imposta sostitutiva premio di risultato ({_parametri.PremioRisultatoAliquota:P0})",
                TipoVoce.Trattenuta, @base: premioNettoContributi, importo: -impostaSostitutivaPremio);

        if (addRegMese > 0m)
            AggiungiRiga(cedolino, "ADD_REG", "Addizionale regionale",
                TipoVoce.Trattenuta, importo: -addRegMese);
        if (addComMese > 0m)
            AggiungiRiga(cedolino, "ADD_COM", "Addizionale comunale",
                TipoVoce.Trattenuta, importo: -addComMese);

        // ---- 6. TFR (figurativo) ------------------------------------------------------
        decimal retribuzioneUtileTfr = Arrotondamento.Euro(retribuzioneMensileFullTime * coeffPt);
        decimal quotaTfr = CalcoloTfr.QuotaMensile(
            retribuzioneUtileTfr, rapporto.NumeroMensilita, _parametri.TfrDivisore, _parametri.TfrContributoFondo);
        AggiungiRiga(cedolino, "TFR", "Quota TFR del mese", TipoVoce.Figurativa, importo: quotaTfr);

        // Contributi c/datore (figurativi, ai fini del costo aziendale).
        AggiungiRiga(cedolino, "INPS_DATORE", $"Contributi INPS c/datore ({aliquota.Datore:P2})",
            TipoVoce.Figurativa, @base: imponibileInps, importo: contributiDatore);

        // ---- 7. Totali e Saldi Ferie/Permessi ----------------------------------------
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
        cedolino.TotaleEsenti = agevolati.TotaleEsenti;
        cedolino.PremioRisultatoAgevolato = agevolati.PremioAgevolato;
        cedolino.ImpostaSostitutivaPremio = impostaSostitutivaPremio;
        cedolino.QuotaTfr = quotaTfr;
        cedolino.CostoAzienda = Arrotondamento.Euro(
            totaleCompetenze - assegniFamiliari + contributiDatore + quotaTfr);

        // Saldi Ferie: quanti giorni al mese lo dice il contratto (regole del CCNL).
        // Il residuo può andare sotto zero (ferie anticipate): è un'informazione vera,
        // usata anche dall'avviso in pagina Presenze.
        decimal ferieMaturate = Arrotondamento.Euro(_regole.GiorniFerieMese * cartellino.Mese * coeffPt);
        decimal ferieGodute = cartellino.GiorniFerie;
        cedolino.FerieMaturate = ferieMaturate;
        cedolino.FerieGodute = ferieGodute;
        cedolino.FerieResidue = ferieMaturate - ferieGodute;

        // Saldi Permessi ROL/Ex-Festività: le ore al mese le dice il contratto.
        decimal permessiMaturati = Arrotondamento.Euro(_regole.OrePermessiMese * cartellino.Mese * coeffPt);
        decimal permessiGoduti = orePermessiTot;
        cedolino.PermessiMaturati = permessiMaturati;
        cedolino.PermessiGoduti = permessiGoduti;
        cedolino.PermessiResidui = permessiMaturati - permessiGoduti;

        return cedolino;
    }

    /// <summary>Esito del blocco agevolati: somme esenti e quota di premio a imposta sostitutiva.</summary>
    private readonly record struct EsitoAgevolati(decimal TotaleEsenti, decimal PremioAgevolato);

    /// <summary>
    /// Aggiunge al cedolino trasferta, rimborsi documentati, fringe benefit, buoni pasto e
    /// premio di risultato, separando per ciascuno la quota esente dall'eccedenza imponibile.
    ///
    /// Tutte le voci sono competenze: entrano nel netto. L'imponibile viene poi ridotto della
    /// sola quota esente, in modo che le eccedenze siano tassate come retribuzione ordinaria.
    /// </summary>
    private static EsitoAgevolati AggiungiElementiAgevolati(
        Cedolino cedolino, Cartellino c, ParametriNormativi p)
    {
        decimal esenti = 0m;

        // Trasferta Italia ed estero: soglie diverse, stessa logica.
        void Trasferta(decimal giorni, decimal limite, string codice, string descrizione)
        {
            var q = CalcoloElementiAgevolati.Trasferta(
                giorni, c.IndennitaTrasfertaGiornaliera, limite, c.RimborsoTrasferta);
            if (q.Totale <= 0m) return;

            if (q.Esente > 0m)
            {
                AggiungiRiga(cedolino, codice, $"{descrizione} (esente)", TipoVoce.Competenza,
                    quantita: giorni, @base: c.IndennitaTrasfertaGiornaliera, importo: q.Esente);
                esenti += q.Esente;
            }
            if (q.Imponibile > 0m)
                AggiungiRiga(cedolino, codice + "_IMP", $"{descrizione} oltre soglia (imponibile)",
                    TipoVoce.Competenza, quantita: giorni, importo: q.Imponibile);
        }

        Trasferta(c.GiorniTrasfertaItalia, p.TrasfertaItaliaEsenteGiorno,
            "TRASF_IT", "Indennità di trasferta Italia");
        Trasferta(c.GiorniTrasfertaEstero, p.TrasfertaEsteroEsenteGiorno,
            "TRASF_EE", "Indennità di trasferta estero");

        // Rimborso a piè di lista: esente se le spese sono documentate e tracciabili.
        if (c.RimborsiSpeseDocumentate > 0m)
        {
            decimal r = Arrotondamento.Euro(c.RimborsiSpeseDocumentate);
            AggiungiRiga(cedolino, "RIMB_SPESE", "Rimborso spese documentate (esente)",
                TipoVoce.Competenza, importo: r);
            esenti += r;
        }

        // Fringe benefit: soglia annua, regola «tutto o niente».
        var fringe = CalcoloElementiAgevolati.FringeBenefit(
            c.FringeBenefit, c.FringeBenefitGiaErogatoAnno,
            CalcoloElementiAgevolati.SogliaFringeBenefit(p, c.FigliACarico));
        if (fringe.Esente > 0m)
        {
            AggiungiRiga(cedolino, "FRINGE", "Fringe benefit (esente)",
                TipoVoce.Competenza, importo: fringe.Esente);
            esenti += fringe.Esente;
        }
        if (fringe.Imponibile > 0m)
            AggiungiRiga(cedolino, "FRINGE_IMP",
                "Fringe benefit oltre soglia annua (imponibile per intero)",
                TipoVoce.Competenza, importo: fringe.Imponibile);

        // Buoni pasto: limite giornaliero, imponibile la sola eccedenza.
        decimal limiteBuono = c.BuonoPastoElettronico
            ? p.BuonoPastoElettronicoEsenteGiorno
            : p.BuonoPastoCartaceoEsenteGiorno;
        var buoni = CalcoloElementiAgevolati.BuoniPasto(
            c.GiorniBuonoPasto, c.ValoreBuonoPasto, limiteBuono);
        if (buoni.Esente > 0m)
        {
            AggiungiRiga(cedolino, "BUONI_PASTO",
                $"Buoni pasto {(c.BuonoPastoElettronico ? "elettronici" : "cartacei")} (esente)",
                TipoVoce.Competenza, quantita: c.GiorniBuonoPasto, @base: c.ValoreBuonoPasto,
                importo: buoni.Esente);
            esenti += buoni.Esente;
        }
        if (buoni.Imponibile > 0m)
            AggiungiRiga(cedolino, "BUONI_IMP", "Buoni pasto oltre soglia (imponibile)",
                TipoVoce.Competenza, quantita: c.GiorniBuonoPasto, importo: buoni.Imponibile);

        // Premio di risultato: la quota agevolata resta contributiva, cambia solo l'IRPEF.
        var (premioAgevolato, premioOrdinario) = CalcoloElementiAgevolati.PremioRisultato(
            c.PremioRisultato, c.RedditoAnnoPrecedente,
            p.PremioRisultatoLimiteAnnuo, p.PremioRisultatoRedditoMassimo);
        if (premioAgevolato > 0m)
            AggiungiRiga(cedolino, "PREMIO", "Premio di risultato (imposta sostitutiva)",
                TipoVoce.Competenza, importo: premioAgevolato);
        if (premioOrdinario > 0m)
            AggiungiRiga(cedolino, "PREMIO_ORD", "Premio di risultato a tassazione ordinaria",
                TipoVoce.Competenza, importo: premioOrdinario);

        return new EsitoAgevolati(esenti, premioAgevolato);
    }

    private static void CalcolaMalattia(Cedolino cedolino, decimal giorniMalattia, int nEvento, decimal pagaGiornaliera, decimal rmgInps)
    {
        // Percentuale integrazione carenza a carico azienda in base al numero di eventi nell'anno
        decimal pctCarenzaDatore = nEvento switch
        {
            1 => 1.00m,
            2 => 1.00m,
            3 => 0.66m,
            4 => 0.50m,
            _ => 0.00m
        };

        // Carenza (Giorni 1-3)
        decimal giorniCarenza = Math.Min(giorniMalattia, 3m);
        if (giorniCarenza > 0m && pctCarenzaDatore > 0m)
        {
            decimal importoCarenza = Arrotondamento.Euro(giorniCarenza * pagaGiornaliera * pctCarenzaDatore);
            AggiungiRiga(cedolino, "MAL_CARENZA", $"Malattia - Carenza a carico azienda ({pctCarenzaDatore:P0})", TipoVoce.Competenza,
                quantita: giorniCarenza, @base: pagaGiornaliera, importo: importoCarenza);
        }

        // Giorni 4-20
        decimal giorniFascia1 = Math.Min(Math.Max(0m, giorniMalattia - 3m), 17m);
        if (giorniFascia1 > 0m)
        {
            decimal indennitaInpsF1 = Arrotondamento.Euro(giorniFascia1 * rmgInps * 0.50m);
            decimal targetLordoF1 = giorniFascia1 * pagaGiornaliera * 0.75m * pctCarenzaDatore;
            decimal integrazioneF1 = Math.Max(0m, Arrotondamento.Euro(targetLordoF1 - indennitaInpsF1));

            AggiungiRiga(cedolino, "MAL_INPS", "Malattia - Indennità INPS (50%)", TipoVoce.Competenza,
                quantita: giorniFascia1, @base: Arrotondamento.Euro(rmgInps * 0.50m), importo: indennitaInpsF1);

            if (integrazioneF1 > 0m)
            {
                AggiungiRiga(cedolino, "MAL_INTEGR", "Malattia - Integrazione azienda (75%)", TipoVoce.Competenza,
                    quantita: giorniFascia1, importo: integrazioneF1);
            }
        }

        // Giorni 21+
        decimal giorniFascia2 = Math.Max(0m, giorniMalattia - 20m);
        if (giorniFascia2 > 0m)
        {
            decimal indennitaInpsF2 = Arrotondamento.Euro(giorniFascia2 * rmgInps * 0.6666m);
            decimal targetLordoF2 = giorniFascia2 * pagaGiornaliera * 1.00m * pctCarenzaDatore;
            decimal integrazioneF2 = Math.Max(0m, Arrotondamento.Euro(targetLordoF2 - indennitaInpsF2));

            AggiungiRiga(cedolino, "MAL_INPS21", "Malattia - Indennità INPS (66,66%)", TipoVoce.Competenza,
                quantita: giorniFascia2, @base: Arrotondamento.Euro(rmgInps * 0.6666m), importo: indennitaInpsF2);

            if (integrazioneF2 > 0m)
            {
                AggiungiRiga(cedolino, "MAL_INTEGR21", "Malattia - Integrazione azienda (100%)", TipoVoce.Competenza,
                    quantita: giorniFascia2, importo: integrazioneF2);
            }
        }
    }

    private static void CalcolaInfortunio(Cedolino cedolino, decimal giorniInfortunio, decimal pagaGiornaliera)
    {
        // Carenza (Giorni 1-3): a carico datore al 60%
        decimal giorniCarenza = Math.Min(giorniInfortunio, 3m);
        if (giorniCarenza > 0m)
        {
            decimal importoCarenza = Arrotondamento.Euro(giorniCarenza * pagaGiornaliera * 0.60m);
            AggiungiRiga(cedolino, "INF_CARENZA", "Infortunio INAIL - Carenza azienda (60%)", TipoVoce.Competenza,
                quantita: giorniCarenza, @base: pagaGiornaliera, importo: importoCarenza);
        }

        // Giorni 4-90 (INAIL 60% + Integrazione datore 40%)
        decimal giorniFascia1 = Math.Min(Math.Max(0m, giorniInfortunio - 3m), 87m);
        if (giorniFascia1 > 0m)
        {
            decimal indennitaInailF1 = Arrotondamento.Euro(giorniFascia1 * pagaGiornaliera * 0.60m);
            decimal integrazioneF1 = Arrotondamento.Euro(giorniFascia1 * pagaGiornaliera * 0.40m);

            AggiungiRiga(cedolino, "INF_INAIL", "Infortunio INAIL - Indennità (60%)", TipoVoce.Competenza,
                quantita: giorniFascia1, @base: Arrotondamento.Euro(pagaGiornaliera * 0.60m), importo: indennitaInailF1);
            AggiungiRiga(cedolino, "INF_INTEGR", "Infortunio INAIL - Integrazione azienda (40%)", TipoVoce.Competenza,
                quantita: giorniFascia1, importo: integrazioneF1);
        }

        // Giorni 91+ (INAIL 75% + Integrazione datore 25%)
        decimal giorniFascia2 = Math.Max(0m, giorniInfortunio - 90m);
        if (giorniFascia2 > 0m)
        {
            decimal indennitaInailF2 = Arrotondamento.Euro(giorniFascia2 * pagaGiornaliera * 0.75m);
            decimal integrazioneF2 = Arrotondamento.Euro(giorniFascia2 * pagaGiornaliera * 0.25m);

            AggiungiRiga(cedolino, "INF_INAIL91", "Infortunio INAIL - Indennità (75%)", TipoVoce.Competenza,
                quantita: giorniFascia2, @base: Arrotondamento.Euro(pagaGiornaliera * 0.75m), importo: indennitaInailF2);
            AggiungiRiga(cedolino, "INF_INTEGR91", "Infortunio INAIL - Integrazione azienda (25%)", TipoVoce.Competenza,
                quantita: giorniFascia2, importo: integrazioneF2);
        }
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
