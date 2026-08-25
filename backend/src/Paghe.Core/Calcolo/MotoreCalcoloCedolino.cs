using Paghe.Core.Domain;
using Paghe.Core.Parametri;

namespace Paghe.Core.Calcolo;

/// <summary>
/// Motore di calcolo del cedolino: a partire dal rapporto di lavoro, dal cartellino
/// presenze e dai parametri normativi, produce il cedolino completo (lordo → netto).
///
/// L'IRPEF ordinaria resta una proiezione nelle simulazioni infrannuali; le addizionali,
/// invece, sono applicate soltanto come rate di importi annuali gia determinati al conguaglio.
/// Senza domicilio, progressivi e saldi annuali il percorso archiviabile si arresta.
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

        // Il minimo tabellare è il conglobato: quando la tabella dichiara la contingenza
        // (EDR storico incluso), paga base e contingenza vanno esposte come voci distinte.
        decimal contingenza = liv.Contingenza;
        decimal pagaBase = liv.MinimoTabellare - contingenza;
        decimal edr = liv.Edr;
        // Terzo elemento della provincia dell'unità operativa (o nazionale): Terziario.
        decimal terzoElemento = _ccnl.TerzoElemento(rapporto.ProvinciaLavoro);
        int scattiMaturati = ScattiMaturati(rapporto, cartellino);
        decimal scatti = liv.ImportoScatto * scattiMaturati;
        decimal superminimo = rapporto.Superminimo;

        // Paga oraria e giornaliera (su base full-time/part-time) per straordinari, permessi e assenze.
        decimal retribuzioneMensileFullTime = liv.MinimoTabellare + edr + terzoElemento + scatti + superminimo;
        decimal retribuzioneMensileEffettiva = Arrotondamento.Euro(retribuzioneMensileFullTime * coeffPt);
        decimal pagaOraria = retribuzioneMensileFullTime / _regole.DivisoreOrario;
        // Le tariffe restano alla precisione effettivamente usata nel prodotto. Arrotondarle a
        // due decimali prima della moltiplicazione rende impossibile riconciliare quantita x
        // tariffa con l'importo esposto (es. 2.015,15 / 168 = 11,9949...).
        decimal pagaGiornaliera = retribuzioneMensileEffettiva / _regole.DivisoreGiornaliero;
        cedolino.RetribuzioneFissa = retribuzioneMensileEffettiva;
        cedolino.TariffaOraria = pagaOraria;
        cedolino.TariffaGiornaliera = pagaGiornaliera;

        // ---- 2. Competenze ------------------------------------------------------------
        AggiungiRiga(cedolino, "PAGA_BASE", contingenza > 0m ? "Paga base" : "Minimo tabellare",
            TipoVoce.Competenza, importo: Arrotondamento.Euro(pagaBase * coeffPt));

        if (contingenza > 0m)
            AggiungiRiga(cedolino, "CONTINGENZA", "Indennità di contingenza (EDR incluso)",
                TipoVoce.Competenza, importo: Arrotondamento.Euro(contingenza * coeffPt));

        if (edr > 0m)
            AggiungiRiga(cedolino, "EDR", "Elemento distinto retribuzione", TipoVoce.Competenza,
                importo: Arrotondamento.Euro(edr * coeffPt));

        if (terzoElemento > 0m)
            AggiungiRiga(cedolino, "TERZO_EL", "Terzo elemento", TipoVoce.Competenza,
                importo: Arrotondamento.Euro(terzoElemento * coeffPt));

        if (scattiMaturati > 0)
            AggiungiRiga(cedolino, "SCATTI", $"Scatti di anzianità ({scattiMaturati})",
                TipoVoce.Competenza, quantita: scattiMaturati, @base: liv.ImportoScatto,
                importo: Arrotondamento.Euro(scatti * coeffPt));

        if (superminimo > 0m)
            AggiungiRiga(cedolino, "SUPERMIN", "Superminimo individuale", TipoVoce.Competenza,
                importo: Arrotondamento.Euro(superminimo * coeffPt));

        // Le mensilita aggiuntive sono documenti/elaborazioni separati. Non si sommano mai in
        // automatico al cedolino ordinario: a luglio il percorso archiviabile richiede il
        // riferimento della quattordicesima pagata il 1° luglio.
        decimal retribuzioneFissaMese = retribuzioneMensileEffettiva;
        decimal mensilitaAggiuntivaLorda = 0m;
        if (_regole.MeseErogazioneQuattordicesima is int meseQuattordicesima
            && cartellino.Mese == meseQuattordicesima && rapporto.NumeroMensilita >= 14)
        {
            AggiungiRiga(cedolino, "QUATTORDICESIMA_SEPARATA",
                string.IsNullOrWhiteSpace(cartellino.RiferimentoQuattordicesimaSeparata)
                    ? "Quattordicesima: prospetto separato da documentare"
                    : $"Quattordicesima: prospetto separato {cartellino.RiferimentoQuattordicesimaSeparata}",
                TipoVoce.Figurativa, importo: 0m);
        }

        // Ferie e Permessi goduti (voci informative/giustificative)
        if (cartellino.GiorniFerie > 0m)
        {
            AggiungiRiga(cedolino, "FERIE", "Ferie godute", TipoVoce.Competenza,
                quantita: cartellino.GiorniFerie, @base: pagaGiornaliera, importo: 0m);
        }

        decimal orePermessiTot = cartellino.OrePermessi + cartellino.OreExFestivita;
        if (cartellino.OrePermessi > 0m)
            AggiungiRiga(cedolino, "ROL", "Permessi ROL goduti", TipoVoce.Competenza,
                quantita: cartellino.OrePermessi, @base: pagaOraria, importo: 0m);
        if (cartellino.OreExFestivita > 0m)
            AggiungiRiga(cedolino, "EX_FEST", "Ex festivita godute", TipoVoce.Competenza,
                quantita: cartellino.OreExFestivita, @base: pagaOraria, importo: 0m);

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
            CalcolaMalattia(cedolino, cartellino, pagaGiornaliera);
        }

        // Infortunio INAIL + Integrazione Datore
        if (cartellino.GiorniInfortunio > 0m)
        {
            // Come per la malattia, si sostituisce la quota ordinaria con le specifiche
            // componenti INAIL/datore invece di aggiungerle alla mensilita piena.
            decimal stornoInfortunio = Arrotondamento.Euro(cartellino.GiorniInfortunio * pagaGiornaliera);
            AggiungiRiga(cedolino, "INF_STORNO", "Storno retribuzione per infortunio", TipoVoce.Competenza,
                quantita: cartellino.GiorniInfortunio, @base: pagaGiornaliera, importo: -stornoInfortunio);
            CalcolaInfortunio(cedolino, cartellino, pagaGiornaliera);
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
        // Prestazioni anticipate per conto INPS/INAIL: sono competenze fiscali ma non formano
        // imponibile contributivo e sono recuperabili dal datore. Restano righe distinte.
        decimal prestazioniRecuperabili = cedolino.Righe
            .Where(r => r.Tipo == TipoVoce.Competenza
                && (r.Codice.StartsWith("MAL_INPS", StringComparison.Ordinal)
                    || r.Codice.StartsWith("INF_INAIL", StringComparison.Ordinal)))
            .Sum(r => r.Importo);
        decimal imponibileInps = totaleCompetenze - agevolati.TotaleEsenti - assegniFamiliari
            - prestazioniRecuperabili;
        var aliquotaBase = _parametri.AliquotaInps(rapporto.Qualifica);
        // Nel percorso archiviabile entrambe le quote arrivano dal profilo contributivo reale
        // risolto con CSC, CA, organico, FIS e agevolazioni. Il riferimento per qualifica resta
        // disponibile soltanto alle simulazioni legacy, mai come scorciatoia per una chiusura.
        var quotaDip = rapporto.AliquotaInpsDipendenteProfilo
            ?? CalcoloContributi.QuotaInpsDipendente(
                aliquotaBase.Dipendente, rapporto.AziendaOltre15Dipendenti,
                rapporto.ContributiFondiSettore);
        // Quota c/datore: il profilo contributivo dell'azienda (CSC, autorizzazioni, dimensione)
        // vince sulla tabella per qualifica, che resta un riferimento non certificato.
        var quotaDatore = rapporto.AliquotaInpsDatoreProfilo ?? aliquotaBase.Datore;
        var aliquota = aliquotaBase with { Dipendente = quotaDip, Datore = quotaDatore };
        var (contributiDip, contributiDatore) = CalcoloContributi.Calcola(imponibileInps, aliquota);

        AggiungiRiga(cedolino, "INPS_DIP", $"Contributi INPS c/dipendente ({Percentuale(aliquota.Dipendente)})",
            TipoVoce.Trattenuta, @base: imponibileInps, importo: -contributiDip);

        // ---- 4. IRPEF -----------------------------------------------------------------
        // Il premio di risultato agevolato esce dalla base IRPEF ordinaria: paga l'imposta
        // sostitutiva calcolata più sotto, al netto dei suoi contributi.
        decimal contributiSuPremio = Arrotondamento.Euro(
            agevolati.PremioAgevolato * aliquota.Dipendente);
        decimal premioNettoContributi = agevolati.PremioAgevolato - contributiSuPremio;

        decimal imponibileIrpefMese = imponibileInps + prestazioniRecuperabili
            - contributiDip - premioNettoContributi;

        // Le mensilità aggiuntive si tassano a parte (art. 23 c.2 DPR 600/73): scaglioni
        // ragguagliati a mese, senza detrazioni. La parte ordinaria segue il metodo di sempre,
        // verificato al centesimo su 26 buste reali di 5 gestionali.
        decimal aggiuntivaNettaContributi = Arrotondamento.Euro(
            mensilitaAggiuntivaLorda * (1m - aliquota.Dipendente));
        decimal imponibileOrdinarioMese = imponibileIrpefMese - aggiuntivaNettaContributi;

        // Reddito annuo presunto per detrazioni e addizionali: 12 mensilità ordinarie più le
        // mensilità aggiuntive del contratto. Proiettare su 12 un contratto a 14 mensilità
        // gonfia la detrazione (revisione consulente 24/08/2026).
        decimal redditoAnnuoPresunto = imponibileOrdinarioMese * 12m
            + Arrotondamento.Euro(retribuzioneFissaMese * (1m - aliquota.Dipendente))
                * Math.Max(0, rapporto.NumeroMensilita - 12);

        decimal irpefLordaAnnua = CalcoloIrpef.IrpefLordaAnnua(
            imponibileOrdinarioMese * 12m, _parametri.ScaglioniIrpef);
        decimal irpefLordaMese = Arrotondamento.Euro(irpefLordaAnnua / 12m);
        decimal irpefMensilitaAggiuntiva = aggiuntivaNettaContributi > 0m
            ? Arrotondamento.Euro(CalcoloIrpef.IrpefLordaAnnua(
                aggiuntivaNettaContributi * 12m, _parametri.ScaglioniIrpef) / 12m)
            : 0m;

        // Detrazione art. 13 e ulteriore detrazione cuneo (L. 207/2024) restano DISTINTE:
        // nel quadro fiscale hanno caselle diverse e sommarle nella stessa casella è un
        // errore di esposizione (revisione consulente 25/08/2026).
        decimal detrazioneLavoroAnnua = 0m, cuneoAnnua = 0m;
        if (rapporto.DetrazioniLavoroDipendente)
        {
            detrazioneLavoroAnnua = CalcoloIrpef.DetrazioneLavoroDipendenteAnnua(
                redditoAnnuoPresunto, rapporto.GiorniDetrazioneAnno);
            cuneoAnnua = CalcoloIrpef.UlterioreDetrazioneCuneoAnnua(
                redditoAnnuoPresunto, rapporto.GiorniDetrazioneAnno,
                _parametri.CuneoUlterioreDetrazioneAnnua, _parametri.CuneoRedditoOltre,
                _parametri.CuneoRedditoFinoA, _parametri.CuneoRedditoAzzeramento);
        }
        decimal detrazioneMese = Arrotondamento.Euro(detrazioneLavoroAnnua / 12m);
        decimal cuneoMese = Arrotondamento.Euro(cuneoAnnua / 12m);

        decimal irpefNetta = Math.Max(0m, irpefLordaMese - detrazioneMese - cuneoMese)
            + irpefMensilitaAggiuntiva;
        AggiungiRiga(cedolino, "IRPEF", "IRPEF netta del mese", TipoVoce.Trattenuta,
            @base: imponibileIrpefMese, importo: -irpefNetta);
        irpefLordaMese += irpefMensilitaAggiuntiva;

        // ---- 5. Addizionali regionale e comunale ---------------------------------------
        // Le addizionali sono esclusivamente rate di importi determinati al conguaglio annuale
        // in base al domicilio fiscale. Mai applicare un'aliquota direttamente al singolo mese.

        decimal addRegMese, addComMese;
        if (rapporto.AddizionaleRegionaleConguaglioAnnuo is decimal regionaleAnnua)
        {
            addRegMese = RataAddizionale(regionaleAnnua, cartellino.Mese, primaRata: 1, rate: 11);
            if (addRegMese > 0m)
                AggiungiRiga(cedolino, "ADD_REG",
                    $"Addizionale regionale {cartellino.Anno - 1} — rata {cartellino.Mese} di 11",
                    TipoVoce.Trattenuta, importo: -addRegMese);
        }
        else
        {
            addRegMese = 0m;
            AggiungiRiga(cedolino, "ADD_REG_MANCANTE",
                "Addizionale regionale: manca il conguaglio annuale, calcolo definitivo bloccato",
                TipoVoce.Figurativa, importo: 0m);
        }

        if (rapporto.AddizionaleComunaleSaldoAnnuo is not null
            || rapporto.AddizionaleComunaleAccontoAnnuo is not null)
        {
            decimal saldo = RataAddizionale(
                rapporto.AddizionaleComunaleSaldoAnnuo ?? 0m, cartellino.Mese, primaRata: 1, rate: 11);
            decimal acconto = RataAddizionale(
                rapporto.AddizionaleComunaleAccontoAnnuo ?? 0m, cartellino.Mese, primaRata: 3, rate: 9);
            if (saldo > 0m)
                AggiungiRiga(cedolino, "ADD_COM",
                    $"Addizionale comunale {cartellino.Anno - 1} (saldo) — rata {cartellino.Mese} di 11",
                    TipoVoce.Trattenuta, importo: -saldo);
            if (acconto > 0m)
                AggiungiRiga(cedolino, "ADD_COM_ACC",
                    $"Addizionale comunale {cartellino.Anno} (acconto 30%) — rata {cartellino.Mese - 2} di 9",
                    TipoVoce.Trattenuta, importo: -acconto);
            addComMese = saldo + acconto;
        }
        else
        {
            addComMese = 0m;
            AggiungiRiga(cedolino, "ADD_COM_MANCANTE",
                "Addizionale comunale: mancano saldo/acconto da conguaglio, calcolo definitivo bloccato",
                TipoVoce.Figurativa, importo: 0m);
        }

        // Imposta sostitutiva sul premio di risultato (in luogo di IRPEF e addizionali).
        decimal impostaSostitutivaPremio = Arrotondamento.Euro(
            premioNettoContributi * _parametri.PremioRisultatoAliquota);
        if (impostaSostitutivaPremio > 0m)
            AggiungiRiga(cedolino, "PREMIO_IMP",
                $"Imposta sostitutiva premio di risultato ({_parametri.PremioRisultatoAliquota:P0})",
                TipoVoce.Trattenuta, @base: premioNettoContributi, importo: -impostaSostitutivaPremio);

        // ---- 5-bis. Fondi contrattuali (assistenza sanitaria ed ente bilaterale) -------
        // Quote previste dal CCNL (Terziario: Fondo Est 2 € lavoratore + 13 € azienda dal
        // 01/04/2025; EBiTer 0,05% + 0,10% su paga base e contingenza, mensilità aggiuntive
        // comprese). Le quote lavoratore sono trattenute dal netto senza toccare gli
        // imponibili (semplificazione dichiarata); il contributo di solidarietà 10% sui
        // versamenti datoriali resta un punto aperto.
        decimal baseEnteBilaterale = 0m;
        if (rapporto.AderisceFondiContrattuali
            && (_regole.EnteBilateraleAliquotaLavoratore is not null
                || _regole.EnteBilateraleAliquotaDatore is not null))
        {
            decimal basePagaContingenza = Arrotondamento.Euro((pagaBase + contingenza) * coeffPt);
            baseEnteBilaterale = basePagaContingenza;
            if (mensilitaAggiuntivaLorda > 0m && retribuzioneFissaMese > 0m)
                baseEnteBilaterale += Arrotondamento.Euro(
                    basePagaContingenza * mensilitaAggiuntivaLorda / retribuzioneFissaMese);
        }

        decimal fondoSanitarioLavoratore = rapporto.AderisceFondiContrattuali
            ? _regole.AssistenzaSanitariaLavoratoreMese ?? 0m
            : 0m;
        if (fondoSanitarioLavoratore > 0m)
            AggiungiRiga(cedolino, "FONDO_SAN", "Fondo assistenza sanitaria CCNL (quota lavoratore)",
                TipoVoce.Trattenuta, importo: -fondoSanitarioLavoratore);

        decimal enteBilateraleLavoratore = Arrotondamento.Euro(
            baseEnteBilaterale * (_regole.EnteBilateraleAliquotaLavoratore ?? 0m));
        if (enteBilateraleLavoratore > 0m)
            AggiungiRiga(cedolino, "ENTE_BIL",
                $"Ente bilaterale — quota lavoratore ({Percentuale(_regole.EnteBilateraleAliquotaLavoratore!.Value)})",
                TipoVoce.Trattenuta, @base: baseEnteBilaterale, importo: -enteBilateraleLavoratore);

        decimal fondoSanitarioDatore = rapporto.AderisceFondiContrattuali
            ? _regole.AssistenzaSanitariaDatoreMese ?? 0m
            : 0m;
        decimal enteBilateraleDatore = Arrotondamento.Euro(
            baseEnteBilaterale * (_regole.EnteBilateraleAliquotaDatore ?? 0m));

        // Contributo di solidarietà (art. 9-bis L. 166/1991) sui versamenti datoriali al fondo
        // di assistenza sanitaria; l'ente bilaterale non vi è soggetto.
        decimal solidarietaFondi = Arrotondamento.Euro(
            fondoSanitarioDatore * _parametri.SolidarietaFondiAliquota);

        // ---- 6. TFR (figurativo) ------------------------------------------------------
        var componentiTfr = cedolino.Righe
            .Where(r => r.Tipo == TipoVoce.Competenza
                && r.Codice is "PAGA_BASE" or "CONTINGENZA" or "EDR" or "TERZO_EL"
                    or "SCATTI" or "SUPERMIN")
            .ToList();
        decimal quotaTfr = 0m;
        foreach (var componente in componentiTfr)
        {
            decimal quota = CalcoloTfr.QuotaMensile(
                componente.Importo, rapporto.NumeroMensilita,
                _parametri.TfrDivisore, _parametri.TfrContributoFondo);
            quotaTfr += quota;
            AggiungiRiga(cedolino, $"TFR_{componente.Codice}",
                $"Quota TFR su {componente.Descrizione} (ratei inclusi)",
                TipoVoce.Figurativa, @base: componente.Importo, importo: quota);
        }
        quotaTfr = Arrotondamento.Euro(quotaTfr);

        // Contributi e fondi c/datore (figurativi, ai fini del costo aziendale).
        AggiungiRiga(cedolino, "INPS_DATORE", $"Contributi INPS c/datore ({Percentuale(aliquota.Datore)})",
            TipoVoce.Figurativa, @base: imponibileInps, importo: contributiDatore);
        if (fondoSanitarioDatore > 0m)
            AggiungiRiga(cedolino, "FONDO_SAN_DATORE", "Fondo assistenza sanitaria CCNL c/azienda",
                TipoVoce.Figurativa, importo: fondoSanitarioDatore);
        if (enteBilateraleDatore > 0m)
            AggiungiRiga(cedolino, "ENTE_BIL_DATORE",
                $"Ente bilaterale c/azienda ({Percentuale(_regole.EnteBilateraleAliquotaDatore!.Value)})",
                TipoVoce.Figurativa, @base: baseEnteBilaterale, importo: enteBilateraleDatore);
        if (solidarietaFondi > 0m)
            AggiungiRiga(cedolino, "SOLIDARIETA_FONDI",
                $"Contributo di solidarietà su fondi sanitari c/azienda ({Percentuale(_parametri.SolidarietaFondiAliquota)})",
                TipoVoce.Figurativa, @base: fondoSanitarioDatore, importo: solidarietaFondi);

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
        cedolino.UlterioreDetrazioneCuneo = cuneoMese;
        cedolino.IrpefNetta = irpefNetta;
        cedolino.AddizionaleRegionale = addRegMese;
        cedolino.AddizionaleComunale = addComMese;
        cedolino.NettoInBusta = Arrotondamento.Euro(totaleCompetenze - totaleTrattenute);
        cedolino.TotaleEsenti = agevolati.TotaleEsenti;
        cedolino.PremioRisultatoAgevolato = agevolati.PremioAgevolato;
        cedolino.ImpostaSostitutivaPremio = impostaSostitutivaPremio;
        cedolino.QuotaTfr = quotaTfr;
        cedolino.PrestazioniRecuperabili = prestazioniRecuperabili;
        if (prestazioniRecuperabili > 0m)
            AggiungiRiga(cedolino, "PREST_RECUPERABILI",
                "Prestazioni INPS/INAIL anticipate recuperabili (riduzione costo)",
                TipoVoce.Figurativa, importo: -prestazioniRecuperabili);
        cedolino.CostoAzienda = Arrotondamento.Euro(
            totaleCompetenze - assegniFamiliari - prestazioniRecuperabili
            + contributiDatore + quotaTfr
            + fondoSanitarioDatore + enteBilateraleDatore + solidarietaFondi);

        // Costo medio mensile annualizzato: la retribuzione contrattuale annua (mensilità
        // aggiuntive comprese) con contributi c/datore, TFR e fondi contrattuali, divisa
        // per 12. Il costo del singolo mese qui sopra non contiene i ratei di 13ª/14ª: sono
        // due numeri diversi e vanno esposti entrambi (revisione consulente 24/08/2026).
        // Entrambi restano PARZIALI, dichiarati: mancano INAIL e solidarietà sui fondi.
        decimal retribuzioneAnnuaContrattuale = retribuzioneFissaMese * rapporto.NumeroMensilita;
        decimal tfrAnnuoNetto = retribuzioneAnnuaContrattuale / _parametri.TfrDivisore
            - retribuzioneAnnuaContrattuale * _parametri.TfrContributoFondo;
        decimal fondiDatoreAnnui = (fondoSanitarioDatore + solidarietaFondi) * 12m
            + (rapporto.AderisceFondiContrattuali
                ? Arrotondamento.Euro((pagaBase + contingenza) * coeffPt)
                    * (_regole.EnteBilateraleAliquotaDatore ?? 0m) * rapporto.NumeroMensilita
                : 0m);
        cedolino.CostoMedioAnnualizzato = Arrotondamento.Euro(
            (retribuzioneAnnuaContrattuale * (1m + aliquota.Datore) + tfrAnnuoNetto + fondiDatoreAnnui) / 12m);

        // Saldi Ferie: quanti giorni al mese lo dice il contratto (regole del CCNL).
        // Il residuo può andare sotto zero (ferie anticipate): è un'informazione vera,
        // usata anche dall'avviso in pagina Presenze.
        decimal ferieMaturate = Arrotondamento.Euro(_regole.GiorniFerieMese * cartellino.Mese * coeffPt);
        decimal ferieGodute = cartellino.GiorniFerie;
        cedolino.FerieMaturate = ferieMaturate;
        cedolino.FerieGodute = ferieGodute;
        cedolino.FerieResidue = ferieMaturate - ferieGodute;

        CalcolaSaldiRolEdExFestivita(cedolino, rapporto, cartellino, coeffPt);

        return cedolino;
    }

    /// <summary>
    /// Scatti maturati alla data della busta. Se il contratto dichiara la periodicità e la data
    /// di assunzione è nota, il conteggio è automatico: lo scatto n-esimo decorre dal primo
    /// giorno del mese successivo a quello nel quale si compie il periodo: assunto il
    /// 01/09/2022, primo triennio il 01/09/2025, scatto in paga dal 01/10/2025.
    /// Altrimenti vale il numero indicato a mano sul rapporto.
    /// </summary>
    private int ScattiMaturati(RapportoLavoro rapporto, Cartellino cartellino)
    {
        if (_regole.ScattiOgniMesi is not int ogniMesi || ogniMesi <= 0
            || rapporto.DataAssunzione is not DateOnly assunzione)
            return rapporto.ScattiAnzianita;

        var primoDelMese = new DateOnly(cartellino.Anno, cartellino.Mese, 1);
        int scatti = 0;
        DateOnly Decorrenza(int numero)
        {
            var maturazione = assunzione.AddMonths(ogniMesi * numero);
            return new DateOnly(maturazione.Year, maturazione.Month, 1).AddMonths(1);
        }
        while (Decorrenza(scatti + 1) <= primoDelMese)
            scatti++;
        return _regole.ScattiMassimo is int massimo && scatti > massimo ? massimo : scatti;
    }

    /// <summary>
    /// Aggiunge la voce di una mensilità aggiuntiva maturata per dodicesimi dall'inizio della
    /// finestra di competenza (o dall'assunzione, se successiva; la frazione di mese di almeno
    /// 15 giorni conta come mese intero). Restituisce l'importo lordo erogato.
    /// </summary>
    private static decimal AggiungiMensilitaAggiuntiva(
        Cedolino cedolino, string codice, string descrizione,
        decimal retribuzioneFissaMese, DateOnly? assunzione, DateOnly inizioFinestra)
    {
        int mesi = 12;
        if (assunzione is DateOnly a && a > inizioFinestra)
        {
            var fineFinestra = inizioFinestra.AddMonths(11);
            if (a > fineFinestra.AddDays(DateTime.DaysInMonth(fineFinestra.Year, fineFinestra.Month) - 1))
                return 0m;
            mesi = (fineFinestra.Year - a.Year) * 12 + fineFinestra.Month - a.Month;
            if (a.Day <= 15)
                mesi += 1;
            mesi = Math.Clamp(mesi, 0, 12);
        }
        if (mesi <= 0)
            return 0m;

        decimal importo = Arrotondamento.Euro(retribuzioneFissaMese * mesi / 12m);
        // Niente «12 × 169,76 = 2.037,10»: la base arrotondata al centesimo non riproduce
        // l'importo (12 × 169,76 = 2.037,12). Si stampa la mensilità intera; nel pro-rata la
        // frazione sta nella descrizione e nella quantità, senza base arrotondata.
        AggiungiRiga(cedolino, codice, mesi == 12 ? descrizione : $"{descrizione} ({mesi}/12)",
            TipoVoce.Competenza, quantita: mesi == 12 ? null : mesi, importo: importo);
        return importo;
    }

    /// <summary>
    /// Rata del mese di un'addizionale da conguaglio: importo annuo diviso per il numero di
    /// rate a partire da <paramref name="primaRata"/>; l'ultima rata recupera gli
    /// arrotondamenti anticipati. Zero nei mesi fuori dal piano (dicembre resta libero).
    /// </summary>
    private static decimal RataAddizionale(decimal importoAnnuo, int mese, int primaRata, int rate)
    {
        if (importoAnnuo <= 0m || mese < primaRata || mese >= primaRata + rate)
            return 0m;
        decimal rata = Arrotondamento.Euro(importoAnnuo / rate);
        return mese == primaRata + rate - 1
            ? Arrotondamento.Euro(importoAnnuo - rata * (rate - 1))
            : rata;
    }

    /// <summary>
    /// Aliquota in forma leggibile con tutti i decimali significativi (fino a 4): la stampa a
    /// due decimali di un'aliquota a quattro (9,36% per 9,3567%) non riproduce il calcolo
    /// (revisione consulente 25/08/2026).
    /// </summary>
    private static string Percentuale(decimal aliquota) =>
        (aliquota * 100m).ToString("0.00##") + "%";

    private void CalcolaSaldiRolEdExFestivita(
        Cedolino cedolino, RapportoLavoro rapporto, Cartellino cartellino, decimal coeffPt)
    {
        if (_regole.OreRolAnno is decimal rolAnno
            && _regole.OreExFestivitaAnno is decimal exFestAnno)
        {
            decimal monteRol = rapporto.AziendaOltre15Dipendenti
                && _regole.OreRolAnnoOltre15 is decimal oltre15 ? oltre15 : rolAnno;
            decimal maturatoRol = 0m;
            decimal maturatoExFest = 0m;
            for (int mese = 1; mese <= cartellino.Mese; mese++)
            {
                var fineMese = new DateOnly(
                    cartellino.Anno, mese, DateTime.DaysInMonth(cartellino.Anno, mese));
                decimal fattoreRol = FattoreMaturazioneRol(rapporto.DataAssunzione, fineMese);
                maturatoRol += monteRol / 12m * fattoreRol * coeffPt;
                maturatoExFest += exFestAnno / 12m * coeffPt;
            }

            cedolino.RolSaldoPrecedente = rapporto.RolResiduiIniziali ?? 0m;
            cedolino.RolMaturati = Arrotondamento.Euro(maturatoRol);
            cedolino.RolGoduti = cartellino.OrePermessi;
            cedolino.RolResidui = cedolino.RolSaldoPrecedente
                + cedolino.RolMaturati - cedolino.RolGoduti;

            cedolino.ExFestivitaSaldoPrecedente = rapporto.ExFestivitaResidueIniziali ?? 0m;
            cedolino.ExFestivitaMaturate = Arrotondamento.Euro(maturatoExFest);
            cedolino.ExFestivitaGodute = cartellino.OreExFestivita;
            cedolino.ExFestivitaResidue = cedolino.ExFestivitaSaldoPrecedente
                + cedolino.ExFestivitaMaturate - cedolino.ExFestivitaGodute;

            // Totale storico mantenuto per compatibilita con API/report precedenti; la stampa
            // nuova espone sempre le due famiglie distintamente.
            cedolino.PermessiMaturati = cedolino.RolMaturati + cedolino.ExFestivitaMaturate;
            cedolino.PermessiGoduti = cedolino.RolGoduti + cedolino.ExFestivitaGodute;
            cedolino.PermessiResidui = cedolino.RolResidui + cedolino.ExFestivitaResidue;
            return;
        }

        decimal orePermessiMese = rapporto.AziendaOltre15Dipendenti
            && _regole.OrePermessiMeseOltre15 is decimal orePermessiOltre15
            ? orePermessiOltre15
            : _regole.OrePermessiMese;
        cedolino.PermessiMaturati = Arrotondamento.Euro(
            orePermessiMese * cartellino.Mese * coeffPt);
        cedolino.PermessiGoduti = cartellino.OrePermessi + cartellino.OreExFestivita;
        cedolino.PermessiResidui = cedolino.PermessiMaturati - cedolino.PermessiGoduti;
    }

    private decimal FattoreMaturazioneRol(DateOnly? assunzione, DateOnly fineMese)
    {
        if (assunzione is not DateOnly dataAssunzione)
            return 1m;

        int mesiAnzianita = (fineMese.Year - dataAssunzione.Year) * 12
            + fineMese.Month - dataAssunzione.Month;
        if (fineMese.Day >= dataAssunzione.Day)
            mesiAnzianita++;

        int senzaRol = _regole.MesiAnzianitaSenzaRol ?? 0;
        if (mesiAnzianita <= senzaRol)
            return 0m;
        int ridotti = _regole.MesiAnzianitaRolRidotto ?? 0;
        if (mesiAnzianita <= senzaRol + ridotti)
            return _regole.PercentualeRolRidotta ?? 1m;
        return 1m;
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

    private static void CalcolaMalattia(
        Cedolino cedolino, Cartellino cartellino, decimal pagaGiornaliera)
    {
        decimal baseRmg = (cartellino.RetribuzioneMediaGiornalieraPrecedente
                ?? pagaGiornaliera)
            + (cartellino.RateiGiornalieri ?? 0m);
        decimal lordizzazione = cartellino.CoefficienteLordizzazione ?? 1m;
        string evento = string.IsNullOrWhiteSpace(cartellino.IdentificativoEvento)
            ? "evento non documentato"
            : $"evento {cartellino.IdentificativoEvento}";

        var giorni = GiorniEvento(
            cartellino, CausaleGiornata.Malattia, cartellino.GiorniMalattia);
        decimal pctCarenzaDatore = cartellino.NumeroEventoMalattiaAnno switch
        {
            1 or 2 => 1m,
            3 => 0.66m,
            4 => 0.50m,
            _ => 0m,
        };

        AggiungiFasciaEvento(cedolino, giorni.Where(g => g <= 3), "MAL_CARENZA",
            $"Malattia {evento} - carenza azienda ({Percentuale(pctCarenzaDatore)})",
            pagaGiornaliera * pctCarenzaDatore);

        void Fascia(
            IEnumerable<int> selezione,
            string suffisso,
            decimal percentualeInps,
            decimal obiettivoCcnl)
        {
            var lista = selezione.ToList();
            if (lista.Count == 0) return;
            decimal indennitaGiornaliera = baseRmg * percentualeInps;
            decimal indennitaLordizzata = indennitaGiornaliera * lordizzazione;
            decimal integrazioneGiornaliera = Math.Max(
                0m, pagaGiornaliera * obiettivoCcnl - indennitaLordizzata);
            AggiungiFasciaEvento(cedolino, lista, $"MAL_INPS{suffisso}",
                $"Malattia {evento} - INPS {Percentuale(percentualeInps)}; "
                + $"RMG precedente + ratei {baseRmg:0.0000}, lordizzazione {lordizzazione:0.0000}",
                indennitaGiornaliera);
            AggiungiFasciaEvento(cedolino, lista, $"MAL_INTEGR{suffisso}",
                $"Malattia {evento} - integrazione azienda a {Percentuale(obiettivoCcnl)}",
                integrazioneGiornaliera);
        }

        Fascia(giorni.Where(g => g is >= 4 and <= 20), "", 0.50m, 0.75m);
        Fascia(giorni.Where(g => g >= 21), "21", 0.6666m, 1m);
    }

    private static void CalcolaInfortunio(
        Cedolino cedolino, Cartellino cartellino, decimal pagaGiornaliera)
    {
        decimal baseInail = (cartellino.RetribuzioneMediaGiornalieraPrecedente
                ?? pagaGiornaliera)
            + (cartellino.RateiGiornalieri ?? 0m);
        string evento = string.IsNullOrWhiteSpace(cartellino.IdentificativoEvento)
            ? "evento non documentato"
            : $"evento {cartellino.IdentificativoEvento}";
        var giorni = GiorniEvento(
            cartellino, CausaleGiornata.Infortunio, cartellino.GiorniInfortunio);

        // Il giorno dell'evento e retribuito al 100% dal datore; seguono esattamente tre
        // giorni al 60%. L'INAIL interviene dal quarto giorno successivo all'evento (giorno 5).
        AggiungiFasciaEvento(cedolino, giorni.Where(g => g == 1), "INF_EVENTO",
            $"Infortunio {evento} - giorno dell'evento azienda 100%", pagaGiornaliera);
        AggiungiFasciaEvento(cedolino, giorni.Where(g => g is >= 2 and <= 4), "INF_CARENZA",
            $"Infortunio {evento} - tre giorni successivi azienda 60%",
            pagaGiornaliera * 0.60m);

        void Fascia(
            IEnumerable<int> selezione,
            string suffisso,
            decimal percentualeInail,
            decimal obiettivoCcnl)
        {
            var lista = selezione.ToList();
            if (lista.Count == 0) return;
            decimal inailGiornaliera = baseInail * percentualeInail;
            decimal integrazioneGiornaliera = Math.Max(
                0m, pagaGiornaliera * obiettivoCcnl - inailGiornaliera);
            AggiungiFasciaEvento(cedolino, lista, $"INF_INAIL{suffisso}",
                $"Infortunio {evento} - indennita INAIL {Percentuale(percentualeInail)}",
                inailGiornaliera);
            AggiungiFasciaEvento(cedolino, lista, $"INF_INTEGR{suffisso}",
                $"Infortunio {evento} - integrazione azienda a {Percentuale(obiettivoCcnl)}",
                integrazioneGiornaliera);
        }

        Fascia(giorni.Where(g => g is >= 5 and <= 20), "", 0.60m, 0.90m);
        Fascia(giorni.Where(g => g is >= 21 and <= 90), "21", 0.60m, 1m);
        Fascia(giorni.Where(g => g >= 91), "91", 0.75m, 1m);
    }

    private static IReadOnlyList<int> GiorniEvento(
        Cartellino cartellino, CausaleGiornata causale, decimal totaleLegacy)
    {
        var giornate = cartellino.Giornate
            .Where(g => g.Causale == causale)
            .OrderBy(g => g.Data)
            .ToList();
        if (giornate.Count > 0 && cartellino.DataInizioEvento is DateOnly inizio)
            return giornate.Select(g => Math.Max(1, g.Data.DayNumber - inizio.DayNumber + 1)).ToList();

        int totale = Math.Max(0, decimal.ToInt32(decimal.Truncate(totaleLegacy)));
        return Enumerable.Range(1, totale).ToList();
    }

    private static void AggiungiFasciaEvento(
        Cedolino cedolino,
        IEnumerable<int> giorniEvento,
        string codice,
        string descrizione,
        decimal tariffaGiornaliera)
    {
        int quantita = giorniEvento.Count();
        if (quantita == 0 || tariffaGiornaliera <= 0m)
            return;
        AggiungiRiga(cedolino, codice, descrizione, TipoVoce.Competenza,
            quantita: quantita, @base: tariffaGiornaliera,
            importo: Arrotondamento.Euro(quantita * tariffaGiornaliera));
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
            quantita: ore, @base: pagaOrariaMagg, importo: importo);
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
