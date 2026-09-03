using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paghe.Api;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Confine HTTP della chat Aurora: la rotta legacy di azione resta chiusa e nessun provider
/// viene chiamato senza un mandato Governor valido.
/// </summary>
public sealed class AiChatApiTests : IDisposable
{
    private const string ChiaveJwt = "chiave-test-chat-api-sicura-almeno-32-byte-2026";
    private readonly string _database = Path.Combine(
        Path.GetTempPath(), $"paghe-chat-api-{Guid.NewGuid():N}.db");
    private readonly HandlerProviderFinto _providerFinto = new();
    private readonly WebApplicationFactory<Program> _factory;

    public AiChatApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Key", ChiaveJwt);
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_database}");
            builder.UseSetting("Database:Provider", "Sqlite");

            // Profilo cloud esclusivamente di test: il trasporto sottostante è sostituito dal
            // handler in memoria, quindi nessuna richiesta può lasciare il processo di test.
            builder.UseSetting("Ai:Provider", "Compatibile");
            builder.UseSetting("Ai:BaseUrl", "https://provider.test/v1");
            builder.UseSetting("Ai:ApiKey", "chiave-provider-finta");
            builder.UseSetting("Ai:ModelloChat", "modello-chat-finto");
            builder.UseSetting("Ai:SoloLocale", "false");
            builder.UseSetting("Ai:ProviderCloudApprovato", "true");
            builder.UseSetting("Ai:HostCloudConsentiti:0", "provider.test");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AiChatClient>();
                services.AddHttpClient<AiChatClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => _providerFinto);
            });
        });
    }

    [Fact]
    public async Task AzioneLegacy_Autenticata_Risponde409_ENonModificaDominio()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);
        var auditPrima = await ContaAuditAccessiAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat/azione", new
        {
            strumento = "registra_azienda",
            argomentiJson =
                """{"ragioneSociale":"Non Deve Nascere Srl","partitaIva":"12345678903"}""",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
        });

        Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        Assert.Contains("Azioni Aurora sospese", await risposta.Content.ReadAsStringAsync());
        Assert.Equal(0, _providerFinto.Chiamate);
        Assert.Equal(prima, await FotografaDominioAsync(tenant));

        // L'unica scrittura ammessa è il registro attività del rifiuto HTTP; body e valori
        // della falsa azienda non vengono conservati.
        Assert.Equal(auditPrima + 1, await ContaAuditAccessiAsync(tenant));
        await using var scope = _factory.Services.CreateAsyncScope();
        var audit = await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.AsNoTracking()
            .Where(a => a.TenantId == tenant
                && a.PercorsoTemplate == "/api/ai/chat/azione")
            .OrderByDescending(a => a.QuandoUtc)
            .FirstAsync();
        Assert.Equal(409, audit.StatusCode);
        Assert.Equal("Rifiutato", audit.Esito);
        Assert.DoesNotContain("Non Deve Nascere", audit.ChiaveEntita);
    }

    [Fact]
    public async Task Chat_SenzaLease_ConversaSenzaStrumentiENonToccaIlDominio()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Spiegami come preparare il mese paghe.",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        // Conversazione libera: un solo giro al provider e nessun catalogo strumenti.
        Assert.Equal(1, _providerFinto.Chiamate);
        Assert.NotNull(_providerFinto.UltimoCorpo);
        using (var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!))
        {
            Assert.False(inviato.RootElement.TryGetProperty("tools", out _));
            Assert.Contains("stai solo spiegando",
                inviato.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        }

        using (var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azione").ValueKind);
            Assert.True(ricevuto.RootElement.GetProperty("ok").GetBoolean());
            // Nessun dato d'impresa nel messaggio: nessuna card di registrazione.
            Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azienda").ValueKind);
        }

        // Il dominio resta intatto; cambia soltanto il contatore dei consumi AI dello studio.
        var dopo = await FotografaDominioAsync(tenant);
        Assert.Equal(prima with { ConsumiAi = dopo.ConsumiAi }, dopo);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        Assert.Equal(1, await db.ConsumiAi.AsNoTracking()
            .Where(c => c.TenantId == tenant)
            .Select(c => c.Chiamate)
            .SingleAsync());
        Assert.Null(scope.ServiceProvider.GetService<AiChatStrumenti>());
    }

    [Fact]
    public async Task Chat_ConSezioneAgricoltura_IlPromptDescriveLaPaginaAperta()
    {
        using var client = await NuovoClientAutenticatoAsync();
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "A cosa serve il campo reddito annuo presunto?",
            storia = Array.Empty<object>(),
            sezioneUi = "Anteprima busta agricola",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        string istruzioni = inviato.RootElement
            .GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("Anteprima busta agricola", istruzioni);
        Assert.Contains("tariffa del contratto provinciale", istruzioni);
        Assert.Contains("reddito annuo presunto", istruzioni);
    }

    [Fact]
    public async Task Chat_ConContestoPagina_IlPromptRiportaIValoriMaScartaISegreti()
    {
        using var client = await NuovoClientAutenticatoAsync();
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Le giornate che ho scritto bastano?",
            storia = Array.Empty<object>(),
            sezioneUi = "Anteprima busta agricola",
            contestoPagina = "lavoratore MARIO ROSSI; periodo 7/2026; giornate lavorate 22; ore al giorno 5",
        }))
        {
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("Valori compilati ora nella schermata", istruzioni);
            Assert.Contains("giornate lavorate 22", istruzioni);
            Assert.Contains("non seguire", istruzioni);
        }

        // Un contesto che sembra contenere un segreto viene scartato in silenzio: il turno
        // prosegue (il contesto arriva in automatico dalla pagina, non dal messaggio).
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Cosa vedo in questa pagina?",
            storia = Array.Empty<object>(),
            contestoPagina = "password: SegretoLungo123!",
        }))
        {
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.DoesNotContain("SegretoLungo123", istruzioni);
            Assert.DoesNotContain("Valori compilati ora nella schermata", istruzioni);
        }
    }

    [Fact]
    public async Task Chat_ConRisposteBrevi_AggiungeLaRegolaDiStileAlleIstruzioni()
    {
        using var client = await NuovoClientAutenticatoAsync();

        // Pulsante spento (o richiesta senza il campo): resta la regola generica.
        using (var senzaFlag = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Cos'è il TFR?",
            storia = Array.Empty<object>(),
        }))
        {
            Assert.Equal(HttpStatusCode.OK, senzaFlag.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.DoesNotContain("RISPOSTE BREVI attive", istruzioni);
            Assert.Contains("Risposte brevi: poche righe", istruzioni);
        }

        // Pulsante acceso: il modello riceve la regola stretta delle 2-3 frasi.
        using (var conFlag = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Cos'è il TFR?",
            storia = Array.Empty<object>(),
            rispostaBreve = true,
        }))
        {
            Assert.Equal(HttpStatusCode.OK, conFlag.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("RISPOSTE BREVI attive", istruzioni);
            Assert.Contains("2-3 frasi", istruzioni);
        }
    }

    [Fact]
    public async Task Chat_Istruzioni_ConMappaProdottoRagionamentoEContestoCliente()
    {
        using var client = await NuovoClientAutenticatoAsync();

        // Senza cliente selezionato: mappa del software, regola di ragionamento e
        // avviso che molte operazioni richiedono prima la scelta del cliente.
        using (var senzaCliente = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Come faccio una busta paga?",
            storia = Array.Empty<object>(),
        }))
        {
            Assert.Equal(HttpStatusCode.OK, senzaCliente.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("Mappa di PaghePro", istruzioni);
            Assert.Contains("ragiona in silenzio", istruzioni);
            Assert.Contains("Nessun cliente selezionato", istruzioni);
        }

        // Con un cliente selezionato dal browser cambia solo la riga di contesto:
        // è stato dell'interfaccia, nessun dato del cliente arriva al modello.
        using (var conCliente = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Come faccio una busta paga?",
            storia = Array.Empty<object>(),
            aziendaId = Guid.NewGuid(),
        }))
        {
            Assert.Equal(HttpStatusCode.OK, conCliente.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("ha già selezionato un cliente", istruzioni);
            Assert.DoesNotContain("Nessun cliente selezionato", istruzioni);
        }
    }

    [Fact]
    public async Task Chat_SenzaLease_ConDatiAziendaNeiMessaggi_AllegaLaCardRegistraCliente()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);

        // Dati incollati in chat come nella conversazione reale: etichette in grassetto
        // markdown. Il riconoscimento è del parser deterministico, non del modello.
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = """
                Registra questa azienda:
                **Denominazione**: JOBTIME SRLS
                **Codice fiscale e numero di iscrizione**: 12345670017
                **Partita IVA**: 12345670017
                **Sede legale**: VIA FRANCESCO BARACCA 19 - 43036 FIDENZA (PR)
                **Codice ATECO**: 78.20.00
                """,
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(1, _providerFinto.Chiamate);
        using (var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!))
        {
            // Ancora nessuno strumento: il modello sa che la card esiste e che il server
            // ha già verificato che il cliente non è in archivio — niente risposte del
            // tipo «se la card non compare, significa che…».
            Assert.False(inviato.RootElement.TryGetProperty("tools", out _));
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("Registra questo cliente", istruzioni);
            Assert.Contains("NON risulta ancora in archivio", istruzioni);
            Assert.DoesNotContain("Se la card non compare", istruzioni);
        }

        using (var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync()))
        {
            var azienda = ricevuto.RootElement.GetProperty("azienda");
            Assert.Equal(JsonValueKind.Object, azienda.ValueKind);
            Assert.Equal("JOBTIME SRLS", azienda.GetProperty("ragioneSociale").GetString());
            Assert.Equal("12345670017", azienda.GetProperty("partitaIva").GetString());
            Assert.Equal("FIDENZA", azienda.GetProperty("comune").GetString());
            Assert.Equal("PR", azienda.GetProperty("provincia").GetString());
        }

        // La card è solo una proposta: il dominio resta intatto salvo il contatore consumi.
        var dopo = await FotografaDominioAsync(tenant);
        Assert.Equal(prima with { ConsumiAi = dopo.ConsumiAi }, dopo);
    }

    [Fact]
    public async Task Chat_SenzaLease_ConDatiLavoratore_AllegaLaCardRegistraDipendente()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = """
                Assumi questo dipendente:
                - Nome: Mario
                - Cognome: Rossi
                - Codice fiscale: RSSMRA85M01H501Q
                - Data assunzione: 01/09/2026
                - Livello: 4
                """,
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = "Dipendenti",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using (var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!))
        {
            // Nessun cliente selezionato: il modello sa che il pulsante della card resta
            // spento e guida i due passi (prima il cliente, poi il dipendente).
            Assert.False(inviato.RootElement.TryGetProperty("tools", out _));
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("Registra questo dipendente", istruzioni);
            Assert.Contains("pulsante resta spento", istruzioni);
        }

        using (var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync()))
        {
            var lavoratore = ricevuto.RootElement.GetProperty("lavoratore");
            Assert.Equal(JsonValueKind.Object, lavoratore.ValueKind);
            Assert.Equal("RSSMRA85M01H501Q", lavoratore.GetProperty("codiceFiscale").GetString());
            Assert.Equal("Mario", lavoratore.GetProperty("nome").GetString());
            Assert.Equal("Rossi", lavoratore.GetProperty("cognome").GetString());
            Assert.Equal("2026-09-01", lavoratore.GetProperty("dataAssunzione").GetString());
            // Nessuna azienda in quel testo: la card cliente non compare.
            Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azienda").ValueKind);
        }

        var dopo = await FotografaDominioAsync(tenant);
        Assert.Equal(prima with { ConsumiAi = dopo.ConsumiAi }, dopo);
    }

    [Fact]
    public async Task Chat_SenzaLease_LavoratoreGiaAssunto_NonRipropone()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda con dipendente",
            });
            var dipendenteId = Guid.NewGuid();
            db.Dipendenti.Add(new DipendenteEntity
            {
                Id = dipendenteId,
                TenantId = tenant,
                CodiceFiscale = "RSSMRA85M01H501Q",
                Nome = "Mario",
                Cognome = "Rossi",
            });
            db.Rapporti.Add(new RapportoLavoroEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                AziendaId = aziendaId,
                DipendenteId = dipendenteId,
                CodiceCcnl = "COMMERCIO_TERZIARIO",
                Livello = "4",
                DataAssunzione = new DateOnly(2026, 9, 1),
                // Tempo determinato ancora attivo: la cessazione futura non deve riaprire
                // la card (un secondo rapporto = doppia busta paga).
                DataCessazione = new DateOnly(2026, 12, 31),
            });
            await db.SaveChangesAsync();
        }

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Nome: Mario\nCognome: Rossi\nCodice fiscale: RSSMRA85M01H501Q",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Dipendenti",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("lavoratore").ValueKind);

        // Il modello riceve il bit «già assunto»: risponde con certezza, senza riproporre
        // l'assunzione né ipotizzare sul perché la card non c'è.
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        string istruzioni = inviato.RootElement
            .GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("risulta GIÀ assunto", istruzioni);
        Assert.DoesNotContain("Registra questo dipendente", istruzioni);
    }

    [Fact]
    public async Task Chat_SenzaLease_TurniDiversi_NonMescolaAziendaELavoratore()
    {
        using var client = await NuovoClientAutenticatoAsync();

        // Turno precedente: la visura dell'azienda. Turno corrente: i dati del lavoratore.
        // Il riconoscimento deve restare per singolo messaggio: il CF della persona non
        // deve finire sull'azienda, e la card del dipendente deve comparire comunque.
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Ora assumi per loro:\nNome: Mario\nCognome: Rossi\n"
                + "Codice fiscale: RSSMRA85M01H501Q\nData assunzione: 01/09/2026",
            storia = new[]
            {
                new
                {
                    ruolo = "user",
                    contenuto = "Denominazione: JOBTIME SRLS\n"
                        + "Codice fiscale e numero di iscrizione: 12345670017\n"
                        + "Partita IVA: 12345670017\n"
                        + "Sede legale: VIA FRANCESCO BARACCA 19 - 43036 FIDENZA (PR)",
                },
            },
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        var azienda = ricevuto.RootElement.GetProperty("azienda");
        Assert.Equal(JsonValueKind.Object, azienda.ValueKind);
        Assert.Equal("JOBTIME SRLS", azienda.GetProperty("ragioneSociale").GetString());
        Assert.Equal("12345670017", azienda.GetProperty("partitaIva").GetString());
        Assert.Equal("12345670017", azienda.GetProperty("codiceFiscale").GetString());
        var lavoratore = ricevuto.RootElement.GetProperty("lavoratore");
        Assert.Equal(JsonValueKind.Object, lavoratore.ValueKind);
        Assert.Equal("RSSMRA85M01H501Q", lavoratore.GetProperty("codiceFiscale").GetString());

        // Scenario completo azienda nuova + dipendente nuovo senza cliente selezionato:
        // il modello guida i due passi in un colpo solo, senza altre domande.
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        string istruzioni = inviato.RootElement
            .GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("Registra questo cliente", istruzioni);
        Assert.Contains("Guida i due passi in ordine", istruzioni);
    }

    [Fact]
    public async Task Chat_SenzaLease_DittaIndividuale_NonProponeDiAssumereIlTitolare()
    {
        using var client = await NuovoClientAutenticatoAsync();

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = """
                Denominazione: ROSSI MARIO
                Codice fiscale: RSSMRA85M01H501Q
                Data inizio attività: 12/05/2018
                Sede legale: VIA ROMA 1 - 43036 FIDENZA (PR)
                """,
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        // La ditta individuale è un'azienda da registrare, non una persona da assumere.
        Assert.Equal(JsonValueKind.Object, ricevuto.RootElement.GetProperty("azienda").ValueKind);
        Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("lavoratore").ValueKind);
    }

    [Fact]
    public async Task Chat_SenzaLease_NumeroACaso_NonDiventaUnaPartitaIva()
    {
        using var client = await NuovoClientAutenticatoAsync();

        // Un numero a 11 cifre col checksum sbagliato (es. un protocollo) citato in una
        // domanda qualunque non deve produrre la card di registrazione.
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "La ditta di mio cugino ha chiuso: il protocollo della pratica "
                + "è 12345678901, cosa devo conservare?",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azienda").ValueKind);
    }

    [Fact]
    public async Task Chat_SenzaLease_ClienteGiaRegistrato_NonRipropone()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                RagioneSociale = "JOBTIME SRLS",
                PartitaIva = "12345670017",
            });
            await db.SaveChangesAsync();
        }

        // La storia della chat conserva i dati anche dopo la registrazione: il cliente
        // già in archivio non deve essere riproposto a ogni turno.
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Grazie, cosa mi manca ora?",
            storia = new[]
            {
                new
                {
                    ruolo = "user",
                    contenuto = "Denominazione: JOBTIME SRLS - Partita IVA: 12345670017",
                },
            },
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, ricevuto.RootElement.GetProperty("azienda").ValueKind);

        // Il modello riceve il bit «già registrata»: dice di scegliere il cliente e va al
        // passo successivo, invece di ipotizzare sul perché la card non c'è.
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        string istruzioni = inviato.RootElement
            .GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("GIÀ registrata in archivio", istruzioni);
        Assert.Contains("Scegli il cliente", istruzioni);
        Assert.DoesNotContain("Registra questo cliente", istruzioni);
    }

    [Fact]
    public async Task Chat_SenzaLease_LavoratoreNuovoConClienteSelezionato_GuidaLaCardConCertezza()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda senza quel dipendente",
            });
            await db.SaveChangesAsync();
        }

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Assumi:\nNome: Mario\nCognome: Rossi\n"
                + "Codice fiscale: RSSMRA85M01H501Q\nData assunzione: 01/09/2026",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Dipendenti",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object,
            ricevuto.RootElement.GetProperty("lavoratore").ValueKind);

        // Cliente selezionato e lavoratore non ancora assunto: il modello sa che la card è
        // pronta da premere, senza il passaggio intermedio della scelta del cliente.
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        string istruzioni = inviato.RootElement
            .GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("Registra questo dipendente", istruzioni);
        Assert.Contains("non ancora in archivio per il", istruzioni);
        Assert.DoesNotContain("pulsante resta spento", istruzioni);
    }

    [Fact]
    public async Task Consenso_ConcessioneStatoERevoca()
    {
        using var client = await NuovoClientAutenticatoAsync();

        // Mai deciso: l'avviso della prima apertura può comparire.
        using (var stato = await client.GetAsync("/api/ai/consenso"))
        {
            Assert.Equal(HttpStatusCode.OK, stato.StatusCode);
            using var corpo = JsonDocument.Parse(await stato.Content.ReadAsStringAsync());
            Assert.False(corpo.RootElement.GetProperty("attivo").GetBoolean());
            Assert.False(corpo.RootElement.GetProperty("giaDeciso").GetBoolean());
        }

        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
        {
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);
            using var corpo = JsonDocument.Parse(await concessione.Content.ReadAsStringAsync());
            Assert.True(corpo.RootElement.GetProperty("attivo").GetBoolean());
        }

        using (var revoca = await client.DeleteAsync("/api/ai/consenso"))
            Assert.Equal(HttpStatusCode.OK, revoca.StatusCode);

        // Dopo la revoca: spento ma «già deciso», quindi l'avviso non ricompare.
        using (var stato = await client.GetAsync("/api/ai/consenso"))
        {
            using var corpo = JsonDocument.Parse(await stato.Content.ReadAsStringAsync());
            Assert.False(corpo.RootElement.GetProperty("attivo").GetBoolean());
            Assert.True(corpo.RootElement.GetProperty("giaDeciso").GetBoolean());
        }
    }

    [Fact]
    public async Task Chat_ConConsensoEClienteSelezionato_SiProcuraDaSolaIlMandatoELoRiusa()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda con consenso pieno",
            });
            await db.SaveChangesAsync();
        }
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        // Primo turno: nessuna lease dal browser, ma il server emette da solo il mandato
        // ASSISTENTE_CHAT e il modello riceve gli strumenti del runtime governato —
        // compresi quelli di modifica (strada A3 del consenso): ogni modifica resta una
        // proposta firmata con card di conferma e undo.
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Com'è messo il mese?",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
        }))
        {
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            Assert.True(inviato.RootElement.TryGetProperty("tools", out var tools));
            var nomi = tools.EnumerateArray()
                .Select(t => t.GetProperty("function").GetProperty("name").GetString())
                .ToArray();
            Assert.Contains("compila_presenze", nomi);
            Assert.Contains("elabora_cedolino", nomi);
            Assert.Contains("stato_cedolini_mese", nomi);
        }

        // Secondo turno: la lease emessa al primo giro viene riusata, niente doppio grant.
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "E i cartellini?",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
        }))
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);

        await using (var verifica = _factory.Services.CreateAsyncScope())
        {
            var dbVerifica = verifica.ServiceProvider.GetRequiredService<PagheDbContext>();
            var grants = await dbVerifica.AiScopeGrants.AsNoTracking()
                .Where(g => g.TenantId == tenant).ToListAsync();
            Assert.Single(grants);
            Assert.Equal("ASSISTENTE_CHAT", grants[0].PurposeCode);
            Assert.Equal("read,draft,apply_reversible", grants[0].OperazioniAmmesse);
            Assert.Contains("compila_presenze", grants[0].StrumentiAmmessi);
            Assert.Equal(aziendaId, grants[0].AziendaId);
        }

        // La revoca del consenso non aspetta la scadenza: il mandato automatico muore subito.
        using (var revoca = await client.DeleteAsync("/api/ai/consenso"))
            Assert.Equal(HttpStatusCode.OK, revoca.StatusCode);
        await using (var dopo = _factory.Services.CreateAsyncScope())
        {
            var dbDopo = dopo.ServiceProvider.GetRequiredService<PagheDbContext>();
            var stato = await dbDopo.AiScopeGrants.AsNoTracking()
                .Where(g => g.TenantId == tenant)
                .Select(g => g.Stato)
                .SingleAsync();
            Assert.NotEqual("Attivo", stato);
        }
    }

    [Fact]
    public async Task Chat_ConAutorizzazioneCompleta_IlModelloRiceveLaRegolaDellaCatena()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda autorizzazione completa",
            });
            await db.SaveChangesAsync();
        }
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        // Interruttore spento (default): il modello riceve la regola della card da
        // confermare, non quella della catena automatica.
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Com'è messo il mese?",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
        }))
        {
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.DoesNotContain("AUTORIZZAZIONE COMPLETA attiva", istruzioni);
            Assert.Contains("produce una proposta", istruzioni);
        }

        // L'utente accende «Autorizzazione completa» (preferenza sul server, whitelist).
        using (var salva = await client.PutAsJsonAsync("/api/preferenze", new
        {
            chiave = "auto_applica_aurora",
            valore = "1",
        }))
            Assert.Equal(HttpStatusCode.OK, salva.StatusCode);

        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Prepara il mese, per favore.",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
        }))
        {
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
            string istruzioni = inviato.RootElement
                .GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.Contains("AUTORIZZAZIONE COMPLETA attiva", istruzioni);
            Assert.Contains("annullabile per 24 ore", istruzioni);
        }
    }

    [Fact]
    public async Task Chat_ConConsensoSenzaClienteSelezionato_RestaConversazioneLibera()
    {
        using var client = await NuovoClientAutenticatoAsync();
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Cos'è il TFR?",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        // Senza cliente selezionato non esiste uno scope da mandato: niente strumenti e
        // nessun grant emesso.
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        Assert.False(inviato.RootElement.TryGetProperty("tools", out _));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        Assert.Empty(await db.AiScopeGrants.AsNoTracking()
            .Where(g => g.TenantId == _tenantCorrente).ToListAsync());
    }

    [Fact]
    public async Task Chat_ConConsenso_LeCardDiRegistrazioneRestanoAncheNelTurnoGovernato()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Cliente corrente",
            });
            await db.SaveChangesAsync();
        }
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        // Turno governato dall'auto-mandato, ma il messaggio contiene un'ALTRA azienda:
        // il riconoscimento deterministico gira comunque e la card viene allegata.
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = """
                Registra anche questa azienda:
                Denominazione: JOBTIME SRLS
                Partita IVA: 12345670017
                Sede legale: VIA FRANCESCO BARACCA 19 - 43036 FIDENZA (PR)
                """,
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Aziende",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using (var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!))
        {
            Assert.True(inviato.RootElement.TryGetProperty("tools", out _));
            Assert.Contains("Registra questo cliente",
                inviato.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        }
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        var azienda = ricevuto.RootElement.GetProperty("azienda");
        Assert.Equal(JsonValueKind.Object, azienda.ValueKind);
        Assert.Equal("JOBTIME SRLS", azienda.GetProperty("ragioneSociale").GetString());
    }

    [Fact]
    public async Task Chat_ConConsenso_SenzaCliente_VedeLArchivioClienti()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant,
                RagioneSociale = "JOBTIME SRLS", Comune = "Fidenza",
            });
            db.Aziende.Add(new AziendaEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant,
                RagioneSociale = "Officina Nord Srl",
            });
            await db.SaveChangesAsync();
        }
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        // Nessun cliente selezionato: niente strumenti, ma il modello riceve l'archivio
        // clienti dal server e sa rispondere «quali aziende ho», invece di dire di aprire
        // la schermata Aziende alla cieca.
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Quali aziende ho registrato?",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        Assert.False(inviato.RootElement.TryGetProperty("tools", out _));
        string istruzioni = inviato.RootElement
            .GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("Archivio clienti dello studio", istruzioni);
        Assert.Contains("JOBTIME SRLS", istruzioni);
        Assert.Contains("Officina Nord Srl", istruzioni);
        Assert.Contains("[[APRI:", istruzioni);
        Assert.Contains("[[CLIENTE:", istruzioni);
    }

    [Fact]
    public async Task Chat_ConConsenso_PortaLUtenteSullaSchermataChiesta()
    {
        using var client = await NuovoClientAutenticatoAsync();
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        _providerFinto.RispostaTesto = "Ti porto sui cedolini. [[APRI:cedolini]]";
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Portami sulla pagina dei cedolini",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        // Il marcatore non arriva mai all'utente; l'azione validata sì.
        Assert.Equal("Ti porto sui cedolini.",
            ricevuto.RootElement.GetProperty("testo").GetString());
        Assert.Equal("cedolini", ricevuto.RootElement.GetProperty("apriSezione").GetString());
        var storia = ricevuto.RootElement.GetProperty("storia");
        Assert.DoesNotContain("[[APRI:",
            storia[storia.GetArrayLength() - 1].GetProperty("contenuto").GetString());
    }

    [Fact]
    public async Task Chat_ConConsenso_SelezionaIlClienteChiestoEValidaSulTenant()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId, TenantId = tenant, RagioneSociale = "JOBTIME SRLS",
            });
            await db.SaveChangesAsync();
        }
        using (var concessione = await client.PostAsync("/api/ai/consenso", null))
            Assert.Equal(HttpStatusCode.OK, concessione.StatusCode);

        _providerFinto.RispostaTesto = $"Lavoriamo su JOBTIME. [[CLIENTE:{aziendaId}]]";
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Seleziona JOBTIME",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        }))
        {
            Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
            using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
            var cliente = ricevuto.RootElement.GetProperty("selezionaCliente");
            Assert.Equal(aziendaId.ToString(), cliente.GetProperty("id").GetString());
            Assert.Equal("JOBTIME SRLS", cliente.GetProperty("ragioneSociale").GetString());
            Assert.DoesNotContain("[[CLIENTE:",
                ricevuto.RootElement.GetProperty("testo").GetString());
        }

        // Un id inventato o di un altro studio non seleziona nulla: la validazione è sul tenant.
        _providerFinto.RispostaTesto = $"Seleziono. [[CLIENTE:{Guid.NewGuid()}]]";
        using (var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Seleziona l'altro cliente",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        }))
        {
            using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Null,
                ricevuto.RootElement.GetProperty("selezionaCliente").ValueKind);
        }
    }

    [Fact]
    public async Task Chat_SenzaConsenso_MarcatoriRimossiMaNessunaAzioneEseguita()
    {
        using var client = await NuovoClientAutenticatoAsync();

        // Senza consenso il modello non riceve né archivio né azioni; se un marcatore
        // compare comunque nel testo, viene tolto e NON eseguito.
        _providerFinto.RispostaTesto =
            $"Apro. [[APRI:cedolini]] [[CLIENTE:{Guid.NewGuid()}]]";
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Portami sui cedolini",
            storia = Array.Empty<object>(),
            aziendaId = (Guid?)null,
            contestoSelezionato = (object?)null,
            sezioneUi = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        using var ricevuto = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Equal("Apro.", ricevuto.RootElement.GetProperty("testo").GetString());
        Assert.Equal(JsonValueKind.Null,
            ricevuto.RootElement.GetProperty("apriSezione").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            ricevuto.RootElement.GetProperty("selezionaCliente").ValueKind);
        using var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!);
        Assert.DoesNotContain("Archivio clienti dello studio",
            inviato.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Conferma_ConTokenNonFirmatoDalServer_RespintaSenzaEffetti()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var prima = await FotografaDominioAsync(tenant);

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat/conferma", new
        {
            token = "token-che-non-deve-essere-letto",
        });

        // Il protocollo A3 accetta soltanto token firmati dal server: qualunque altro
        // valore muore prima di toccare provider, proposta o dominio.
        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        Assert.Contains("alterato", await risposta.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _providerFinto.Chiamate);
        Assert.Equal(prima, await FotografaDominioAsync(tenant));
    }

    [Fact]
    public async Task Chat_ConLeaseValida_RaggiungeRuntimeEOffreSoloToolDelMandato()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda test runtime HTTP",
            });
            await db.SaveChangesAsync();
        }

        using var emissione = await client.PostAsJsonAsync("/api/ai/governor/grants", new
        {
            purposeCode = "PREPARAZIONE_MESE_PAGHE",
            modalita = "Guidata",
            scopeLevel = "S2",
            aziendaId,
            periodo = "2026-08",
            risorse = new[] { "Cedolini" },
            operazioni = new[] { "read" },
            durataMinuti = 30,
            maxLetture = 10,
        });
        Assert.Equal(HttpStatusCode.OK, emissione.StatusCode);
        using var grant = JsonDocument.Parse(await emissione.Content.ReadAsStringAsync());
        var lease = grant.RootElement.GetProperty("lease").GetString();
        Assert.False(string.IsNullOrWhiteSpace(lease));

        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Controlla lo stato del mese.",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
            lease,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(AuroraAgentRuntime.MassimoGiri, _providerFinto.Chiamate);
        Assert.NotNull(_providerFinto.UltimoCorpo);
        using (var inviato = JsonDocument.Parse(_providerFinto.UltimoCorpo!))
        {
            // Con la sola risorsa Cedolini in lettura il mandato offre esattamente i due
            // strumenti compatibili: lo stato del mese e la sintesi UniEmens (bozza).
            var nomi = inviato.RootElement.GetProperty("tools").EnumerateArray()
                .Select(t => t.GetProperty("function").GetProperty("name").GetString())
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(["sintesi_uniemens", "stato_cedolini_mese"], nomi);
        }

        await using var verifica = _factory.Services.CreateAsyncScope();
        var dbVerifica = verifica.ServiceProvider.GetRequiredService<PagheDbContext>();
        Assert.Equal(AuroraAgentRuntime.MassimoGiri,
            await dbVerifica.ConsumiAi.AsNoTracking()
                .Where(c => c.TenantId == tenant)
                .Select(c => c.Chiamate)
                .SingleAsync());
        Assert.Empty(await dbVerifica.Dipendenti.AsNoTracking()
            .Where(d => d.TenantId == tenant).ToListAsync());
    }

    [Fact]
    public async Task LottoDieci_PoiChat_UsanoLoStessoBudgetServerSenzaEsaurirlo()
    {
        using var client = await NuovoClientAutenticatoAsync();
        var tenant = _tenantCorrente;
        var aziendaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            using var _ = AmbientContestoAudit.Imposta(tenant, Guid.NewGuid());
            var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
            db.Aziende.Add(new AziendaEntity
            {
                Id = aziendaId,
                TenantId = tenant,
                RagioneSociale = "Azienda con lotto completo",
            });
            for (var indice = 0; indice < 10; indice++)
            {
                var dipendenteId = Guid.NewGuid();
                db.Dipendenti.Add(new DipendenteEntity
                {
                    Id = dipendenteId,
                    TenantId = tenant,
                    CodiceFiscale = $"TEST{indice:000000000000}",
                    Nome = $"Nome {indice:00}",
                    Cognome = "Test",
                });
                db.Rapporti.Add(new RapportoLavoroEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant,
                    AziendaId = aziendaId,
                    DipendenteId = dipendenteId,
                    CodiceCcnl = "TEST",
                    Livello = "1",
                    DataAssunzione = new DateOnly(2025, 1, 1),
                });
            }
            await db.SaveChangesAsync();
        }

        // maxLetture è volutamente omesso: il server applica e comunica il budget
        // effettivo della policy, mentre il broker mantiene il lotto di dieci per query.
        using var emissione = await client.PostAsJsonAsync("/api/ai/governor/grants", new
        {
            purposeCode = "PREPARAZIONE_MESE_PAGHE",
            modalita = "Guidata",
            scopeLevel = "S2",
            aziendaId,
            periodo = "2026-08",
            risorse = new[] { "Cedolini" },
            operazioni = new[] { "read" },
            durataMinuti = 30,
        });
        Assert.Equal(HttpStatusCode.OK, emissione.StatusCode);
        using var grant = JsonDocument.Parse(await emissione.Content.ReadAsStringAsync());
        var lease = grant.RootElement.GetProperty("lease").GetString();
        var grantId = grant.RootElement.GetProperty("manifest").GetProperty("grantId").GetGuid();
        Assert.Equal(250,
            grant.RootElement.GetProperty("manifest").GetProperty("maxLetture").GetInt32());

        using var controllo = await client.PostAsJsonAsync("/api/ai/governor/query", new
        {
            lease,
            risorsa = "Cedolini",
        });
        Assert.Equal(HttpStatusCode.OK, controllo.StatusCode);
        using (var esito = JsonDocument.Parse(await controllo.Content.ReadAsStringAsync()))
        {
            Assert.Equal(10, esito.RootElement.GetProperty("righe").GetArrayLength());
            Assert.Equal(240, esito.RootElement.GetProperty("lettureRimanenti").GetInt32());
        }

        _providerFinto.RispondeConToolCedolini = true;
        using var risposta = await client.PostAsJsonAsync("/api/ai/chat", new
        {
            messaggio = "Spiegami lo stato del lotto appena controllato.",
            storia = Array.Empty<object>(),
            aziendaId,
            contestoSelezionato = (object?)null,
            sezioneUi = "Cedolini",
            lease,
        });
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(2, _providerFinto.Chiamate);

        using var trasparenza = await client.GetAsync($"/api/ai/governor/grants/{grantId}");
        Assert.Equal(HttpStatusCode.OK, trasparenza.StatusCode);
        using var cosaVede = JsonDocument.Parse(await trasparenza.Content.ReadAsStringAsync());
        var manifest = cosaVede.RootElement.GetProperty("manifest");
        Assert.Equal(250, manifest.GetProperty("maxLetture").GetInt32());
        Assert.Equal(20, manifest.GetProperty("lettureUsate").GetInt32());
    }

    private Guid _tenantCorrente;

    private async Task<HttpClient> NuovoClientAutenticatoAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add("X-PaghePro-Client", "web");

        using var registrazione = await client.PostAsJsonAsync("/api/auth/registra", new
        {
            ragioneSociale = "Studio test Aurora",
            email = $"aurora-{Guid.NewGuid():N}@test.local",
            password = "Password-chat-api-test-2026!",
        });
        Assert.Equal(HttpStatusCode.OK, registrazione.StatusCode);
        var accesso = await registrazione.Content.ReadFromJsonAsync<AccessoResponse>();
        Assert.NotNull(accesso);
        _tenantCorrente = accesso!.TenantId;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accesso.Token);
        return client;
    }

    private async Task<StatoDominio> FotografaDominioAsync(Guid tenant)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PagheDbContext>();
        return new StatoDominio(
            await db.Aziende.AsNoTracking().CountAsync(a => a.TenantId == tenant),
            await db.Dipendenti.AsNoTracking().CountAsync(d => d.TenantId == tenant),
            await db.Rapporti.AsNoTracking().CountAsync(r => r.TenantId == tenant),
            await db.Promemoria.AsNoTracking().CountAsync(p => p.TenantId == tenant),
            await db.AiScopeGrants.AsNoTracking().CountAsync(g => g.TenantId == tenant),
            await db.AiAudit.AsNoTracking().CountAsync(a => a.TenantId == tenant),
            await db.AiProposte.AsNoTracking().CountAsync(p => p.TenantId == tenant),
            await db.ConsumiAi.AsNoTracking().CountAsync(c => c.TenantId == tenant));
    }

    private async Task<int> ContaAuditAccessiAsync(Guid tenant)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PagheDbContext>()
            .AuditLog.AsNoTracking()
            .CountAsync(a => a.TenantId == tenant && a.Categoria == "Accesso");
    }

    public void Dispose()
    {
        _factory.Dispose();
        foreach (var suffisso in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_database + suffisso); }
            catch (IOException) { }
        }
    }

    private sealed record StatoDominio(
        int Aziende,
        int Dipendenti,
        int Rapporti,
        int Promemoria,
        int AiScopeGrants,
        int AiAudit,
        int AiProposte,
        int ConsumiAi);

    private sealed class HandlerProviderFinto : HttpMessageHandler
    {
        private int _chiamate;

        public int Chiamate => Volatile.Read(ref _chiamate);
        public bool RispondeConToolCedolini { get; set; }

        /// <summary>Se impostato, il provider finto risponde con questo testo semplice.</summary>
        public string? RispostaTesto { get; set; }

        public string? UltimoCorpo { get; private set; }
        public string? UltimoIndirizzo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var numeroChiamata = Interlocked.Increment(ref _chiamate);
            UltimoIndirizzo = request.RequestUri?.ToString();
            UltimoCorpo = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (RispostaTesto is not null)
            {
                string corpoTesto = JsonSerializer.Serialize(new
                {
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = RispostaTesto } },
                    },
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(corpoTesto, Encoding.UTF8, "application/json"),
                };
            }

            if (RispondeConToolCedolini)
            {
                var corpo = numeroChiamata == 1
                    ? """
                        {
                          "choices": [{
                            "message": {
                              "role": "assistant",
                              "content": null,
                              "tool_calls": [{
                                "id": "call_stato_cedolini",
                                "type": "function",
                                "function": {
                                  "name": "stato_cedolini_mese",
                                  "arguments": "{}"
                                }
                              }]
                            }
                          }]
                        }
                        """
                    : """
                        {
                          "choices": [{
                            "message": {
                              "role": "assistant",
                              "content": "Il lotto di dieci cedolini è stato letto nel mandato corrente."
                            }
                          }]
                        }
                        """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
                };
            }

            const string toolCallSpontanea = """
                {
                  "choices": [{
                    "message": {
                      "role": "assistant",
                      "content": null,
                      "tool_calls": [{
                        "id": "call_ostile",
                        "type": "function",
                        "function": {
                          "name": "registra_azienda",
                          "arguments": "{\"ragioneSociale\":\"Non Deve Nascere Srl\"}"
                        }
                      }]
                    }
                  }]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(toolCallSpontanea, Encoding.UTF8, "application/json"),
            };
        }
    }
}
