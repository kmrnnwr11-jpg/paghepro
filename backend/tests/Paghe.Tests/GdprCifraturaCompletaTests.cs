using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Gate tecnico Fase 3: raw DB senza IBAN/salute, AAD anti-swap, backfill legacy idempotente e
/// isolamento tenant del runner.
/// </summary>
public sealed class GdprCifraturaCompletaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ProtettoreDati _protettore = ProtettoreDati.Corrente;

    public GdprCifraturaCompletaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        NuovoContesto(_protettore);

    private PagheDbContext NuovoContesto(ProtettoreDati protettore) =>
        new(
            new DbContextOptionsBuilder<PagheDbContext>()
                .UseSqlite(_connection)
                .Options,
            protettore);

    [Fact]
    public async Task IbanSaluteEVoci_SonoSoloPayloadContestuali_MaIlDominioFaRoundTrip()
    {
        var dati = await InserisciDatasetCompletoAsync();

        var ibanAzienda = await ScalareAsync(
            "SELECT IbanProtetto FROM Aziende WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.AziendaId);
        var ibanDipendente = await ScalareAsync(
            "SELECT IbanProtetto FROM Dipendenti WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.DipendenteId);
        var ibanAvviso = await ScalareAsync(
            "SELECT IbanProtetto FROM AvvisiPagamento WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.AvvisoId);
        var salute = await ScalareAsync(
            "SELECT DatiSaluteProtetti FROM Cartellini WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.CartellinoId);
        var voce = await ScalareAsync(
            "SELECT DatiVoceProtetti FROM CedolinoVoceEntity WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.VoceId);

        foreach (var cifrato in new[] { ibanAzienda, ibanDipendente, ibanAvviso, salute, voce })
            Assert.StartsWith(_protettore.PrefissoChiaveCorrente, cifrato);
        Assert.DoesNotContain(dati.Iban, string.Join('|', ibanAzienda, ibanDipendente, ibanAvviso));
        Assert.DoesNotContain("5.5", salute);
        Assert.DoesNotContain("MAL_", voce);
        Assert.DoesNotContain("malattia", voce, StringComparison.OrdinalIgnoreCase);

        await using var db = NuovoContesto();
        Assert.Equal(dati.Iban, (await db.Aziende.SingleAsync(a => a.Id == dati.AziendaId)).Iban);
        Assert.Equal(dati.Iban, (await db.Dipendenti.SingleAsync(d => d.Id == dati.DipendenteId)).Iban);
        Assert.Equal(dati.Iban, (await db.AvvisiPagamento.SingleAsync(a => a.Id == dati.AvvisoId)).Iban);
        var cartellino = await db.Cartellini.SingleAsync(c => c.Id == dati.CartellinoId);
        Assert.Equal(5.5m, cartellino.GiorniMalattia);
        Assert.Equal(2, cartellino.NumeroEventoMalattiaAnno);
        Assert.Equal(1.25m, cartellino.GiorniInfortunio);
        var riga = await db.Set<CedolinoVoceEntity>().SingleAsync(v => v.Id == dati.VoceId);
        Assert.Equal("MAL_INPS", riga.Codice);
        Assert.Equal("Indennita malattia INPS", riga.Descrizione);
        Assert.Equal(5.5m, riga.Quantita);
    }

    [Fact]
    public async Task CiphertextCopiatoSuAltroTenant_FallisceIlTagAad()
    {
        const string iban = "IT60X0542811101000000123456";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            db.Tenants.AddRange(
                new TenantEntity { Id = tenantA, RagioneSociale = "A", CreatoIl = DateTime.UtcNow },
                new TenantEntity { Id = tenantB, RagioneSociale = "B", CreatoIl = DateTime.UtcNow });
            db.Dipendenti.AddRange(
                new DipendenteEntity
                {
                    Id = idA,
                    TenantId = tenantA,
                    CodiceFiscale = "RSSMRA80A01F205X",
                    Nome = "Mario",
                    Cognome = "Rossi",
                    Iban = iban,
                },
                new DipendenteEntity
                {
                    Id = idB,
                    TenantId = tenantB,
                    CodiceFiscale = "RSSMRA80A01F205X",
                    Nome = "Mario",
                    Cognome = "Rossi",
                    Iban = iban,
                });
            await db.SaveChangesAsync();
        }

        var cifratoA = await ScalareAsync(
            "SELECT IbanProtetto FROM Dipendenti WHERE TenantId = $tenant AND Id = $id",
            tenantA, idA);
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "UPDATE Dipendenti SET IbanProtetto = $valore WHERE TenantId = $tenant AND Id = $id";
            cmd.Parameters.AddWithValue("$valore", cifratoA);
            cmd.Parameters.AddWithValue("$tenant", tenantB);
            cmd.Parameters.AddWithValue("$id", idB);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var verifica = NuovoContesto();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => verifica.Dipendenti.SingleAsync(d => d.TenantId == tenantB && d.Id == idB));
    }

    [Fact]
    public async Task BackfillLegacy_MigraPlaintextV1EV2_DropColonne_EIdempotente()
    {
        await AggiungiColonneLegacyAsync();
        var dati = await InserisciDatasetCompletoAsync();
        var legacyV1 = CifraV1(ProtettoreDati.PassphraseSviluppo, dati.Iban);
        var legacyV2 = _protettore.Proteggi(dati.Iban)!;

        await EseguiAsync(
            "UPDATE Aziende SET IbanProtetto = NULL, Iban = $legacy WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.AziendaId, dati.Iban);
        await EseguiAsync(
            "UPDATE Dipendenti SET IbanProtetto = NULL, Iban = $legacy WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.DipendenteId, legacyV1);
        await EseguiAsync(
            "UPDATE AvvisiPagamento SET IbanProtetto = NULL, Iban = $legacy WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.AvvisoId, legacyV2);
        await EseguiAsync(
            "UPDATE Cartellini SET DatiSaluteProtetti = '', GiorniMalattia = '5.5', " +
            "NumeroEventoMalattiaAnno = '2', GiorniInfortunio = '1.25' " +
            "WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.CartellinoId, null);
        await EseguiAsync(
            "UPDATE CedolinoVoceEntity SET DatiVoceProtetti = '', Codice = 'MAL_INPS', " +
            "Descrizione = 'Indennita malattia INPS', Tipo = 'Competenza', Quantita = '5.5', " +
            "Base = '50.25', Importo = '276.38' WHERE TenantId = $tenant AND Id = $id",
            dati.TenantId, dati.VoceId, null);

        EsitoMigrazioneDatiSensibili esito;
        await using (var db = NuovoContesto())
            esito = await new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync();

        Assert.Equal(1, esito.TenantEsaminati);
        Assert.True(esito.ValoriMigrati >= 5);
        foreach (var legacy in new[]
        {
            ("Aziende", "Iban"), ("Dipendenti", "Iban"), ("AvvisiPagamento", "Iban"),
            ("Cartellini", "GiorniMalattia"), ("Cartellini", "NumeroEventoMalattiaAnno"),
            ("Cartellini", "GiorniInfortunio"), ("CedolinoVoceEntity", "Codice"),
            ("CedolinoVoceEntity", "Descrizione"), ("CedolinoVoceEntity", "Tipo"),
            ("CedolinoVoceEntity", "Quantita"), ("CedolinoVoceEntity", "Base"),
            ("CedolinoVoceEntity", "Importo"),
        })
            Assert.False(await ColonnaEsisteAsync(legacy.Item1, legacy.Item2));

        await using (var db = NuovoContesto())
        {
            await new MigrazioneDatiSensibili(db, _protettore).VerificaCutoverCompletatoAsync();
            Assert.Equal(dati.Iban, (await db.Dipendenti.SingleAsync(d => d.Id == dati.DipendenteId)).Iban);
            var cartellino = await db.Cartellini.SingleAsync(c => c.Id == dati.CartellinoId);
            Assert.Equal(5.5m, cartellino.GiorniMalattia);
            var riga = await db.Set<CedolinoVoceEntity>().SingleAsync(v => v.Id == dati.VoceId);
            Assert.Equal("MAL_INPS", riga.Codice);
        }

        await using (var db = NuovoContesto())
        {
            var secondo = await new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync();
            Assert.Equal(0, secondo.ValoriMigrati);
        }
    }

    [Fact]
    public async Task BackfillPerTenant_NonToccaIlTenantDiverso()
    {
        await using (var db = NuovoContesto())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Dipendenti ADD COLUMN Iban TEXT NULL;");

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Tenants.AddRange(
                new TenantEntity { Id = tenantA, RagioneSociale = "A", CreatoIl = DateTime.UtcNow },
                new TenantEntity { Id = tenantB, RagioneSociale = "B", CreatoIl = DateTime.UtcNow });
            db.Dipendenti.AddRange(
                new DipendenteEntity { Id = idA, TenantId = tenantA, CodiceFiscale = "AAA", Nome = "A", Cognome = "A" },
                new DipendenteEntity { Id = idB, TenantId = tenantB, CodiceFiscale = "BBB", Nome = "B", Cognome = "B" });
            await db.SaveChangesAsync();
        }
        await EseguiAsync(
            "UPDATE Dipendenti SET IbanProtetto = NULL, Iban = $legacy WHERE TenantId = $tenant AND Id = $id",
            tenantA, idA, "IT60X0542811101000000123456");
        await EseguiAsync(
            "UPDATE Dipendenti SET IbanProtetto = NULL, Iban = $legacy WHERE TenantId = $tenant AND Id = $id",
            tenantB, idB, "IT02L1234512345123456789012");

        await using (var db = NuovoContesto())
            Assert.Equal(1, await new MigrazioneDatiSensibili(db, _protettore).EseguiPerTenantAsync(tenantA));

        Assert.StartsWith(_protettore.PrefissoChiaveCorrente, await ScalareAsync(
            "SELECT IbanProtetto FROM Dipendenti WHERE TenantId = $tenant AND Id = $id", tenantA, idA));
        Assert.Null(await ScalareNullableAsync(
            "SELECT IbanProtetto FROM Dipendenti WHERE TenantId = $tenant AND Id = $id", tenantB, idB));
    }

    [Fact]
    public async Task BackfillSchemaStoricoParziale_UsaDefaultSanitariSenzaInventareDati()
    {
        await using (var db = NuovoContesto())
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Cartellini ADD COLUMN GiorniMalattia TEXT NULL;");

        var tenantId = Guid.NewGuid();
        var cartellinoId = Guid.NewGuid();
        await using (var db = NuovoContesto())
        {
            db.Tenants.Add(new TenantEntity
            {
                Id = tenantId,
                RagioneSociale = "Storico",
                CreatoIl = DateTime.UtcNow,
            });
            db.Cartellini.Add(new CartellinoEntity
            {
                Id = cartellinoId,
                TenantId = tenantId,
                RapportoLavoroId = Guid.NewGuid(),
                Anno = 2024,
                Mese = 1,
                GiorniMalattia = 3.5m,
            });
            await db.SaveChangesAsync();
        }
        await EseguiAsync(
            "UPDATE Cartellini SET DatiSaluteProtetti = '', GiorniMalattia = '3.5' " +
            "WHERE TenantId = $tenant AND Id = $id",
            tenantId, cartellinoId, null);

        await using (var db = NuovoContesto())
            await new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync();

        await using (var db = NuovoContesto())
        {
            var cartellino = await db.Cartellini.SingleAsync(c => c.Id == cartellinoId);
            Assert.Equal(3.5m, cartellino.GiorniMalattia);
            Assert.Equal(1, cartellino.NumeroEventoMalattiaAnno);
            Assert.Equal(0m, cartellino.GiorniInfortunio);
        }
    }

    [Fact]
    public async Task GateConNuovaChiave_FallisceSenzaRiscrivereLaSentinellaK1()
    {
        await InserisciDatasetCompletoAsync();
        await using (var db = NuovoContesto())
            await new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync();

        var sentinellaPrima = await ScalareSempliceAsync(
            "SELECT ValoreProtetto FROM MetadatiProtezioneDati WHERE Id = 'master'");
        var schemaPrima = await ScalareSempliceAsync(
            "SELECT group_concat(name || ':' || sql, '|') FROM sqlite_master " +
            "WHERE type IN ('table','index') ORDER BY name");
        var nuovaChiave = new ProtettoreDati(
            "chiave-nuova-per-gate-read-only",
            [ProtettoreDati.PassphraseSviluppo],
            produzione: false);

        await using (var db = NuovoContesto(nuovaChiave))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new MigrazioneDatiSensibili(db, nuovaChiave).VerificaCutoverCompletatoAsync());
        }

        Assert.Equal(sentinellaPrima, await ScalareSempliceAsync(
            "SELECT ValoreProtetto FROM MetadatiProtezioneDati WHERE Id = 'master'"));
        Assert.Equal(schemaPrima, await ScalareSempliceAsync(
            "SELECT group_concat(name || ':' || sql, '|') FROM sqlite_master " +
            "WHERE type IN ('table','index') ORDER BY name"));
    }

    [Fact]
    public async Task BootstrapDatabaseVuoto_CreaSentinellaMaIlGatePuroNonLaCrea()
    {
        await using (var db = NuovoContesto())
        {
            var runner = new MigrazioneDatiSensibili(db, _protettore);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.VerificaCutoverCompletatoAsync());
        }
        Assert.False(await ColonnaEsisteAsync(
            "MetadatiProtezioneDati", "ValoreProtetto"));

        await using (var db = NuovoContesto())
        {
            var runner = new MigrazioneDatiSensibili(db, _protettore);
            Assert.True(await runner.InizializzaSeDatabaseVuotoAsync());
            await runner.VerificaCutoverCompletatoAsync();
            Assert.False(await runner.InizializzaSeDatabaseVuotoAsync());
        }
    }

    [Fact]
    public async Task VoceConTenantDiscordante_AnnullaIlCutover()
    {
        var dati = await InserisciDatasetCompletoAsync();
        await EseguiSempliceAsync("PRAGMA foreign_keys = OFF;");
        try
        {
            await EseguiAsync(
                "UPDATE CedolinoVoceEntity SET TenantId = $altro WHERE Id = $id",
                dati.TenantId,
                dati.VoceId,
                null,
                Guid.NewGuid());

            await using var db = NuovoContesto();
            var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync());
            Assert.Contains("tenant diverso", errore.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await EseguiSempliceAsync("PRAGMA foreign_keys = ON;");
        }
    }

    [Fact]
    public async Task VoceOrfana_AnnullaIlCutover()
    {
        var dati = await InserisciDatasetCompletoAsync();
        await EseguiSempliceAsync("PRAGMA foreign_keys = OFF;");
        try
        {
            await EseguiAsync(
                "UPDATE CedolinoVoceEntity SET CedolinoId = $altro WHERE Id = $id",
                dati.TenantId,
                dati.VoceId,
                null,
                Guid.NewGuid());

            await using var db = NuovoContesto();
            var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync());
            Assert.Contains("orfane", errore.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await EseguiSempliceAsync("PRAGMA foreign_keys = ON;");
        }
    }

    [Fact]
    public async Task WriterLegacyConcorrente_NonPuoSuperareDropAtomico()
    {
        var percorso = Path.Combine(Path.GetTempPath(), $"paghe-gdpr-lock-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={percorso};Default Timeout=5";
        var tenantId = Guid.NewGuid();
        var dipendenteId = Guid.NewGuid();
        const string ibanPrima = "IT60X0542811101000000123456";
        const string ibanConcorrente = "IT02L1234512345123456789012";
        var rilascioRunner = new ManualResetEventSlim(false);

        try
        {
            await using var connessioneRunner = new SqliteConnection(connectionString);
            await connessioneRunner.OpenAsync();
            PagheDbContext CreaContesto() => new(
                new DbContextOptionsBuilder<PagheDbContext>()
                    .UseSqlite(connessioneRunner)
                    .Options,
                _protettore);

            await using (var db = CreaContesto())
            {
                await db.Database.EnsureCreatedAsync();
                db.Tenants.Add(new TenantEntity
                {
                    Id = tenantId,
                    RagioneSociale = "Lock test",
                    CreatoIl = DateTime.UtcNow,
                });
                db.Dipendenti.Add(new DipendenteEntity
                {
                    Id = dipendenteId,
                    TenantId = tenantId,
                    CodiceFiscale = "LOCKTEST",
                    Nome = "Test",
                    Cognome = "Lock",
                    Iban = ibanPrima,
                });
                await db.SaveChangesAsync();
                await new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync();
            }

            await using (var prepara = connessioneRunner.CreateCommand())
            {
                prepara.CommandText =
                    "ALTER TABLE Dipendenti ADD COLUMN Iban TEXT NULL; " +
                    "UPDATE Dipendenti SET IbanProtetto = NULL, Iban = $iban WHERE Id = $id;";
                prepara.Parameters.AddWithValue("$iban", ibanPrima);
                prepara.Parameters.AddWithValue("$id", dipendenteId);
                await prepara.ExecuteNonQueryAsync();
            }

            var runnerAllaSentinella = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            connessioneRunner.CreateFunction("pausa_finale_cutover", () =>
            {
                runnerAllaSentinella.TrySetResult();
                rilascioRunner.Wait(TimeSpan.FromSeconds(10));
                return 0;
            });
            await using (var trigger = connessioneRunner.CreateCommand())
            {
                trigger.CommandText =
                    "CREATE TRIGGER PausaFinaleCutover " +
                    "AFTER UPDATE ON MetadatiProtezioneDati " +
                    "BEGIN SELECT pausa_finale_cutover(); END;";
                await trigger.ExecuteNonQueryAsync();
            }

            var migrazione = Task.Run(async () =>
            {
                await using var db = CreaContesto();
                await new MigrazioneDatiSensibili(db, _protettore).EseguiTuttiAsync();
            });
            await runnerAllaSentinella.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await using var connessioneWriter = new SqliteConnection(connectionString);
            await connessioneWriter.OpenAsync();
            var writer = Task.Run(async () =>
            {
                await using var cmd = connessioneWriter.CreateCommand();
                cmd.CommandText = "UPDATE Dipendenti SET Iban = $iban WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$iban", ibanConcorrente);
                cmd.Parameters.AddWithValue("$id", dipendenteId);
                return await cmd.ExecuteNonQueryAsync();
            });

            await Task.Delay(150);
            Assert.False(writer.IsCompleted);
            rilascioRunner.Set();
            await migrazione.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<Exception>(async () => await writer);

            await using var verifica = CreaContesto();
            Assert.Equal(
                ibanPrima,
                (await verifica.Dipendenti.SingleAsync(d => d.Id == dipendenteId)).Iban);
        }
        finally
        {
            rilascioRunner.Set();
            rilascioRunner.Dispose();
            foreach (var file in new[] { percorso, percorso + "-wal", percorso + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private async Task<Dataset> InserisciDatasetCompletoAsync()
    {
        const string iban = "IT60X0542811101000000123456";
        var tenantId = Guid.NewGuid();
        var aziendaId = Guid.NewGuid();
        var dipendenteId = Guid.NewGuid();
        var avvisoId = Guid.NewGuid();
        var cartellinoId = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var voceId = Guid.NewGuid();

        await using var db = NuovoContesto();
        db.Tenants.Add(new TenantEntity
        {
            Id = tenantId,
            RagioneSociale = "Studio Test",
            CreatoIl = DateTime.UtcNow,
        });
        db.Aziende.Add(new AziendaEntity
        {
            Id = aziendaId,
            TenantId = tenantId,
            RagioneSociale = "Alfa Srl",
            Iban = iban,
        });
        db.Dipendenti.Add(new DipendenteEntity
        {
            Id = dipendenteId,
            TenantId = tenantId,
            CodiceFiscale = "RSSMRA80A01F205X",
            Nome = "Mario",
            Cognome = "Rossi",
            Iban = iban,
        });
        db.AvvisiPagamento.Add(new AvvisoPagamentoEntity
        {
            Id = avvisoId,
            TenantId = tenantId,
            AziendaId = aziendaId,
            Anno = 2026,
            Mese = 7,
            Descrizione = "Compenso",
            Importo = 100m,
            DataEmissione = new DateOnly(2026, 7, 1),
            DataScadenza = new DateOnly(2026, 7, 31),
            Iban = iban,
        });
        db.Cartellini.Add(new CartellinoEntity
        {
            Id = cartellinoId,
            TenantId = tenantId,
            RapportoLavoroId = Guid.NewGuid(),
            Anno = 2026,
            Mese = 7,
            GiorniMalattia = 5.5m,
            NumeroEventoMalattiaAnno = 2,
            GiorniInfortunio = 1.25m,
        });
        db.Cedolini.Add(new CedolinoEntity
        {
            Id = cedolinoId,
            TenantId = tenantId,
            AziendaId = aziendaId,
            RapportoLavoroId = Guid.NewGuid(),
            Anno = 2026,
            Mese = 7,
            CalcolatoIl = DateTime.UtcNow,
            Voci =
            [
                new CedolinoVoceEntity
                {
                    Id = voceId,
                    TenantId = tenantId,
                    CedolinoId = cedolinoId,
                    Codice = "MAL_INPS",
                    Descrizione = "Indennita malattia INPS",
                    Tipo = "Competenza",
                    Quantita = 5.5m,
                    Base = 50.25m,
                    Importo = 276.38m,
                },
            ],
        });
        await db.SaveChangesAsync();
        return new Dataset(
            tenantId, aziendaId, dipendenteId, avvisoId, cartellinoId, voceId, iban);
    }

    private async Task AggiungiColonneLegacyAsync()
    {
        await using var db = NuovoContesto();
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE Aziende ADD COLUMN Iban TEXT NULL;
            ALTER TABLE Dipendenti ADD COLUMN Iban TEXT NULL;
            ALTER TABLE AvvisiPagamento ADD COLUMN Iban TEXT NULL;
            ALTER TABLE Cartellini ADD COLUMN GiorniMalattia TEXT NULL;
            ALTER TABLE Cartellini ADD COLUMN NumeroEventoMalattiaAnno TEXT NULL;
            ALTER TABLE Cartellini ADD COLUMN GiorniInfortunio TEXT NULL;
            ALTER TABLE CedolinoVoceEntity ADD COLUMN Codice TEXT NULL;
            ALTER TABLE CedolinoVoceEntity ADD COLUMN Descrizione TEXT NULL;
            ALTER TABLE CedolinoVoceEntity ADD COLUMN Tipo TEXT NULL;
            ALTER TABLE CedolinoVoceEntity ADD COLUMN Quantita TEXT NULL;
            ALTER TABLE CedolinoVoceEntity ADD COLUMN Base TEXT NULL;
            ALTER TABLE CedolinoVoceEntity ADD COLUMN Importo TEXT NULL;
            """);
    }

    private async Task EseguiAsync(
        string sql,
        Guid tenantId,
        Guid id,
        string? legacy,
        Guid? altro = null)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        if (sql.Contains("$tenant", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("$tenant", tenantId);
        if (sql.Contains("$id", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("$id", id);
        if (sql.Contains("$legacy", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("$legacy", (object?)legacy ?? DBNull.Value);
        if (sql.Contains("$altro", StringComparison.Ordinal))
            cmd.Parameters.AddWithValue("$altro", altro ?? Guid.Empty);
        Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
    }

    private async Task EseguiSempliceAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> ScalareSempliceAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return (string?)await cmd.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Valore testuale atteso ma nullo.");
    }

    private async Task<string> ScalareAsync(string sql, Guid tenantId, Guid id) =>
        (string?)await ScalareNullableAsync(sql, tenantId, id)
        ?? throw new InvalidOperationException("Valore raw atteso ma nullo.");

    private async Task<string?> ScalareNullableAsync(string sql, Guid tenantId, Guid id)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$tenant", tenantId);
        cmd.Parameters.AddWithValue("$id", id);
        return await cmd.ExecuteScalarAsync() as string;
    }

    private async Task<bool> ColonnaEsisteAsync(string tabella, string colonna)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabella}') WHERE name = $colonna";
        cmd.Parameters.AddWithValue("$colonna", colonna);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static string CifraV1(string passphrase, string valore)
    {
        var chiave = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var chiaro = Encoding.UTF8.GetBytes(valore);
        var cifrato = new byte[chiaro.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(chiave, 16);
        aes.Encrypt(nonce, chiaro, cifrato, tag);
        return "enc:v1:" + Convert.ToBase64String([.. nonce, .. tag, .. cifrato]);
    }

    public void Dispose() => _connection.Dispose();

    private sealed record Dataset(
        Guid TenantId,
        Guid AziendaId,
        Guid DipendenteId,
        Guid AvvisoId,
        Guid CartellinoId,
        Guid VoceId,
        string Iban);
}
