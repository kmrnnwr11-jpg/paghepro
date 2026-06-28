using Paghe.Core.AdeTelematico;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test del parser UNILAV contro il <b>testo reale</b> di due ricevute di portali diversi
/// (Emilia-Romagna a due colonne con più campi per riga; Campania/SILF a "Sezione N").
/// </summary>
public sealed class UnilavPdfRealiTests
{
    private const string EmiliaRomagna = """
        Agenzia regionale per il lavoro
        RICEVUTA DI INVIO COMUNICAZIONE ORDINARIA
        Data Ricezione: 24/06/2026 09:02:03 Codice Comunicazione: 0603326301756319
        Tipo di Comunicazione: 01 - Comunicazione Obbligatoria Codice Comunicazione precedente:
        Dati Mittente (se diverso da datore di lavoro)
        Codice fiscale: 91011170338
        Tipologia: 008 - Associazioni di categoria dei Datori di lavoro agricoli (art. 9-bis, co. 6, L. 608/96)
        Denominazione: Unione Agricoltori di Piacenza
        E-mail: servizio.paghe@confagripc.it
        Evento
        Tipo Evento: Proroga
        Datore di Lavoro
        Codice Fiscale: 01174590339
        Ragione Sociale: AZ.AGR.LA SALAMANDRIA DI GORRA GIORGIO-MASSIMO-UMBERTO S.A.
        Comune Sede Legale: A223 - ALSENO Cap sede Legale: 29010
        Indirizzo Sede Legale: Via ROMA 321 Telefono: 0523982386
        Settore: 01.11.00 - Coltivazione di cereali, legumi da granella e semi oleosi, escluso il riso
        Pubblica Amm.ne: NO
        Rappresentante Legale
        Nome:
        Cognome:
        Lavoratore
        Codice Fiscale: TLKKRN04P28Z249M
        Cognome: TALUKDAR Nome: KAMRAN
        Sesso: M Data Nascita: 28/09/2004
        Comune Nascita: Z249 - BANGLADESH Comune Domicilio: B332 - CADEO
        Indirizzo Domicilio: VICOLO DELLA CHIESA 12
        Cittadinanza: 305 - BANGLADESE Titolo di Soggiorno: 1 - PERMESSO
        Numero titolo di soggiorno: I22630488 Motivo titolo di soggiorno: LAVOR - LAVORO SUBORDINATO
        Dati Rapporto
        Data Inizio: 11/04/2026 Data Fine:
        Tipologia contrattuale: A.02.00 - LAVORO A TEMPO DETERMINATO
        Orario: F - TEMPO PIENO Ore Settimanali medie:
        Qualifica: 8.3.1.1.0 - BRACCIANTI AGRICOLI
        CCNL: 0190 - AGRICOLTURA - Operai florovivaisti
        Lavoro in agricoltura: SI Giornate previste: 30
        Livello inquadramento: ND - Non dichiarato Retribuzione/Compenso: 2203
        Proroga
        Data fine proroga: 31/10/2026
        """;

    private const string Campania = """
        Ricevuta Comunicazione Obbligatoria Online
        Comunicazione Obbligatoria Unificato UniLav
        Numero Protocollo 01262468
        Codice comunicazione 0506526346039664
        Sezione 1 - Datore di lavoro
        Codice Fiscale 06137680655
        Denominazione P.P. RENT SRL
        Settore 49.41.00 - Trasporto di merci su strada
        Pubblica amministrazione NO
        Sede Legale
        Indirizzo VIALE BIANCA MARIA 13 - F205 - MILANO - 20100
        Recapiti Email: pprent@pec.it
        Sede Lavoro
        Indirizzo AMBITO TERRITORIALE NAZIONA SNC - I438 - SARNO - 84087
        Sezione 2 - Lavoratore
        Codice fiscale LAISDA04C06Z236R
        Cognome ALI
        Nome ASAD
        Cittadinanza 344 - PACHISTANA
        Data di nascita 06/03/2004
        Comune di nascita Z236 - PAKISTAN
        Comune domicilio A944 - BOLOGNA - 40100
        Indirizzo domicilio VIA D. CORTICELLA , 180
        Sezione 4 - Inizio
        Data inizio 04/06/2026
        Data fine 30/06/2026
        Tipologia contrattuale A.02.00 - LAVORO A TEMPO DETERMINATO
        CCNL 7370 - TRASPORTO MERCI - Art. (App.Ass. 1/2011)
        Livello Inquadramento 000004 - QUALIFICATO - C3
        Tipo orario F - TEMPO PIENO
        Qualifica professionale 7.4.2.3.0 - Conduttori di mezzi pesanti e camion
        Retribuzione 24046
        Sezione 8 - Dati invio
        Codice fiscale soggetto che effettua la comunicazione BRNLNS64M30F912D
        """;

    [Fact]
    public void EmiliaRomagna_DueColonne_PiuCampiPerRiga()
    {
        var r = ImportazioneUnilavPdf.Importa(EmiliaRomagna);

        // Datore reale (non il mittente, che pure contiene "datore di lavoro").
        Assert.Equal("01174590339", r.Datore.CodiceFiscale);
        Assert.Equal("AZ.AGR.LA SALAMANDRIA DI GORRA GIORGIO-MASSIMO-UMBERTO S.A.", r.Datore.Denominazione);

        Assert.Equal("TLKKRN04P28Z249M", r.Lavoratore.CodiceFiscale);
        Assert.Equal("TALUKDAR", r.Lavoratore.Cognome);   // "Cognome: TALUKDAR Nome: KAMRAN" -> taglio su "Nome"
        Assert.Equal("KAMRAN", r.Lavoratore.Nome);
        Assert.Equal(new DateOnly(2004, 9, 28), r.Lavoratore.DataNascita);
        Assert.Equal("B332 - CADEO", r.Lavoratore.ComuneDomicilio);

        Assert.Equal(new DateOnly(2026, 4, 11), r.Rapporto.DataInizio);
        Assert.StartsWith("0190", r.Rapporto.Ccnl);
    }

    [Fact]
    public void Campania_Sezioni_UnCampoPerRiga()
    {
        var r = ImportazioneUnilavPdf.Importa(Campania);

        Assert.Equal("06137680655", r.Datore.CodiceFiscale);
        Assert.Equal("P.P. RENT SRL", r.Datore.Denominazione);

        Assert.Equal("LAISDA04C06Z236R", r.Lavoratore.CodiceFiscale);
        Assert.Equal("ALI", r.Lavoratore.Cognome);
        Assert.Equal("ASAD", r.Lavoratore.Nome);
        Assert.Equal(new DateOnly(2004, 3, 6), r.Lavoratore.DataNascita);

        Assert.Equal(new DateOnly(2026, 6, 4), r.Rapporto.DataInizio);
        Assert.Equal(new DateOnly(2026, 6, 30), r.Rapporto.DataFine);
        Assert.StartsWith("7370", r.Rapporto.Ccnl);
    }
}
