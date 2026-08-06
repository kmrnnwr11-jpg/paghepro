using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Persistence;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Import UNILAV da <b>testo incollato</b>: l'utente copia la comunicazione (o un suo riepilogo con
/// elenchi puntati e grassetti) e il sistema deve registrare azienda, lavoratore e rapporto mettendo
/// ogni dato al proprio posto, poi elencare cosa manca ancora per la busta paga.
/// Il testo è una comunicazione reale (dati del documento fornito dall'utente).
/// </summary>
public sealed class UnilavTestoIncollatoTests : IDisposable
{
    private const string TestoIncollato = """
        **Dati Generali della Comunicazione**
          * **Tipo di Comunicazione:** 01 - Comunicazione Obbligatoria
          * **Codice Comunicazione:** 0603324202094798
          * **Data Ricezione:** 18/07/2024 12:07:51
          * **Tipo Evento:** Avviamento

        **Dati del Soggetto Abilitato (Mittente)**
          * **Codice Fiscale:** 01751560333
          * **Denominazione:** Studio Devoti srls
          * **E-mail:** paghe@studiodevoti.it

        **Dati del Datore di Lavoro**
          * **Codice Fiscale:** SNGMJT82P10Z222Z
          * **Ragione Sociale:** PUNJAB DI SINGH MANJIT
          * **Sede Legale:** VIA NICOLLI FRANCESCO N.2, 29017 FIORENZUOLA D'ARDA
          * **Sede di Lavoro:** VIA NICOLLI FRANCESCO N.2, 29017 FIORENZUOLA D'ARDA
          * **Telefono:** 0523338339
          * **Settore:** 82.99.99 - Altri servizi di supporto alle imprese nca

        **Dati del Rappresentante Legale**
          * **Nome e Cognome:** MANJIT SINGH
          * **Data e Luogo di Nascita:** 10/09/1982 in INDIA (Z222)

        **Dati del Lavoratore**
          * **Codice Fiscale:** 55123449005
          * **Nome e Cognome:** SINGH KARANJIT
          * **Sesso:** M
          * **Data e Luogo di Nascita:** 30/01/2000 in INDIA (Z222)
          * **Cittadinanza:** 330 - INDIANA

        **Dati del Rapporto di Lavoro**
          * **Data Inizio:** 19/07/2024
          * **Data Fine:** 18/07/2025
          * **Tipologia Contrattuale:** A.02.00 - LAVORO A TEMPO DETERMINATO
          * **Orario:** M - TEMPO PARZIALE MISTO
          * **Ore Settimanali Medie:** 20
          * **Qualifica:** 8.1.3.3.0.2 ADDETTO AL VOLANTINAGGIO
          * **Livello di Inquadramento:** 000008 - VII
          * **CCNL:** 0780 - COMMERCIO - Confcommercio
          * **Ente Previdenziale:** 01 - INPS (Codice: 6104084700)
          * **PAT Inail:** 22815130
        """;

    private readonly SqliteConnection _connection;

    public UnilavTestoIncollatoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
        new ParametriNormativiService(db).ImportaSeedSeVuotoAsync().GetAwaiter().GetResult();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public void Parser_LeggeTestoConGrassettiEdElenchi()
    {
        var r = ImportazioneUnilavPdf.Importa(TestoIncollato);

        // Datore: non deve prendere il codice fiscale del mittente (studio che ha inviato).
        Assert.Equal("SNGMJT82P10Z222Z", r.Datore.CodiceFiscale);
        Assert.Equal("PUNJAB DI SINGH MANJIT", r.Datore.Denominazione);
        Assert.Equal("VIA NICOLLI FRANCESCO N.2", r.Datore.Indirizzo);
        Assert.Equal("29017", r.Datore.Cap);
        Assert.Equal("FIORENZUOLA D'ARDA", r.Datore.Comune);
        Assert.Equal("0523338339", r.Datore.Telefono);
        Assert.Equal("82.99.99", r.Datore.CodiceAteco);
        Assert.Equal("6104084700", r.Datore.MatricolaInps);
        Assert.Equal("22815130", r.Datore.PatInail);
        Assert.Equal("MANJIT", r.Datore.RappresentanteNome);
        Assert.Equal("SINGH", r.Datore.RappresentanteCognome);

        // Lavoratore: nel testo incollato nome e cognome erano su un campo unico, si divide
        // seguendo l'ordine dell'etichetta (l'utente lo conferma dopo, vedi dati da completare).
        Assert.Equal("55123449005", r.Lavoratore.CodiceFiscale);
        Assert.Equal("SINGH", r.Lavoratore.Nome);
        Assert.Equal("KARANJIT", r.Lavoratore.Cognome);

        // Rapporto.
        Assert.Equal(new DateOnly(2024, 7, 19), r.Rapporto.DataInizio);
        Assert.Equal(new DateOnly(2025, 7, 18), r.Rapporto.DataFine);
        Assert.Equal(20m, r.Rapporto.OreSettimanali);
        Assert.Contains("COMMERCIO", r.Rapporto.Ccnl);
        Assert.True(ImportazioneUnilav.EParTime(r.Rapporto.TipoOrario), "«TEMPO PARZIALE MISTO» è part-time");
    }

    [Fact]
    public async Task Import_MetteOgniDatoAlSuoPosto()
    {
        var tenant = Guid.NewGuid();

        EsitoImportUnilav esito;
        await using (var db = NuovoContesto())
            esito = await new ImportUnilavService(db).ImportaDaTestoAsync(tenant, TestoIncollato);

        // Azienda creata con l'anagrafica completa letta dall'UNILAV.
        Assert.True(esito.AziendaCreata);
        Assert.Equal("PUNJAB DI SINGH MANJIT", esito.Azienda.RagioneSociale);
        Assert.Equal("SNGMJT82P10Z222Z", esito.Azienda.CodiceFiscale);
        Assert.Equal("VIA NICOLLI FRANCESCO N.2", esito.Azienda.Indirizzo);
        Assert.Equal("29017", esito.Azienda.Cap);
        Assert.Equal("FIORENZUOLA D'ARDA", esito.Azienda.Comune);
        Assert.Equal("6104084700", esito.Azienda.MatricolaInps);
        Assert.Equal("22815130", esito.Azienda.PatInail);
        Assert.Equal("82.99.99", esito.Azienda.CodiceAteco);
        Assert.Equal("MANJIT", esito.Azienda.LegaleRappresentanteNome);
        Assert.True(esito.Azienda.HaDipendenti);

        // Rapporto: contratto a termine part-time al 50% (20 ore su 40).
        Assert.Equal("Tempo determinato", esito.Rapporto.TipoContratto);
        Assert.Equal(new DateOnly(2024, 7, 19), esito.Rapporto.DataAssunzione);
        Assert.Equal(new DateOnly(2025, 7, 18), esito.Rapporto.DataCessazione);
        Assert.True(esito.Rapporto.PartTime);
        Assert.Equal(50m, esito.Rapporto.PercentualePartTime);
        Assert.Equal(20m, esito.Rapporto.OreSettimanali);
        Assert.Equal("COMMERCIO_TERZIARIO", esito.Rapporto.CodiceCcnl);
        Assert.Equal("7", esito.Rapporto.Livello); // «000008 - VII» → settimo livello del CCNL
        // La qualifica contrattuale non è nell'UNILAV (c'è la professione ISTAT): resta il default
        // e finisce tra i dati da confermare, perché cambia le aliquote INPS.
        Assert.Equal("Impiegato", esito.Rapporto.Qualifica);
        Assert.Contains(esito.DaCompletare, c => c.Campo.StartsWith("Qualifica contrattuale"));

        // Lavoratore.
        Assert.Equal("55123449005", esito.Rapporto.Dipendente!.CodiceFiscale);
        Assert.Equal("SINGH", esito.Rapporto.Dipendente!.Nome);
        Assert.Equal("KARANJIT", esito.Rapporto.Dipendente!.Cognome);
    }

    [Fact]
    public async Task Import_ElencaCosaMancaConIndicazioni()
    {
        var tenant = Guid.NewGuid();

        EsitoImportUnilav esito;
        await using (var db = NuovoContesto())
            esito = await new ImportUnilavService(db).ImportaDaTestoAsync(tenant, TestoIncollato);

        // Quello che l'UNILAV non contiene deve essere elencato, con dove completarlo e dove trovarlo.
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("Regione"));
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("catastale"));
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("Partita IVA"));
        Assert.All(esito.DaCompletare, c =>
        {
            Assert.Contains(c.Dove, new[] { "azienda", "dipendente", "rapporto" });
            Assert.False(string.IsNullOrWhiteSpace(c.Perche));
            Assert.False(string.IsNullOrWhiteSpace(c.DoveTrovarlo));
        });

        // Il codice fiscale del lavoratore non ha 16 caratteri: va segnalato, non accettato in silenzio.
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("Codice fiscale del lavoratore"));
        // Nome e cognome erano su un campo unico: l'ordine va confermato dall'utente.
        Assert.Contains(esito.DaCompletare, c => c.Campo.Contains("Controlla nome e cognome"));

        // Quello che c'era NON deve comparire tra i mancanti.
        Assert.DoesNotContain(esito.DaCompletare, c => c.Campo.Contains("Matricola INPS"));
        Assert.DoesNotContain(esito.DaCompletare, c => c.Campo.Contains("P.A.T. INAIL"));
        Assert.DoesNotContain(esito.DaCompletare, c => c.Campo.Contains("Sede"));
    }

    public void Dispose() => _connection.Dispose();
}
