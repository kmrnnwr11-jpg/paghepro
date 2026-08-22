using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Test di <see cref="SchemaDatabase"/>: lo schema è gestito dalle migrazioni EF versionate e i
/// database creati prima delle migrazioni (con <c>EnsureCreated</c> e DDL scritto a mano) vengono
/// adottati senza perdere dati.
/// </summary>
public sealed class MigrazioniSchemaTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MigrazioniSchemaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    // Le migrazioni SQLite vivono in un assembly dedicato (EF ne ammette uno solo per provider):
    // la stessa configurazione usata dall'host in Program.cs.
    private PagheDbContext NuovoContesto() => new(
        new DbContextOptionsBuilder<PagheDbContext>()
            .UseSqlite(_connection, sql => sql.MigrationsAssembly("Paghe.Migrazioni.Sqlite"))
            .Options);

    private List<string> Tabelle()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        using var reader = cmd.ExecuteReader();
        var nomi = new List<string>();
        while (reader.Read()) nomi.Add(reader.GetString(0));
        return nomi;
    }

    private List<string> MigrazioniApplicate()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT \"MigrationId\" FROM \"{HistoryRepository.DefaultTableName}\";";
        using var reader = cmd.ExecuteReader();
        var righe = new List<string>();
        while (reader.Read()) righe.Add(reader.GetString(0));
        return righe;
    }

    /// <summary>
    /// Lo storico deve contenere esattamente le migrazioni dell'assembly, quali che siano.
    /// Confrontare con l'elenco reale invece che con un numero fisso: così il test non si rompe
    /// quando si aggiunge una migrazione, ma continua ad accorgersi se una resta indietro.
    /// </summary>
    private void AssertSchemaAllineato()
    {
        using var db = NuovoContesto();
        Assert.Equal(
            db.Database.GetMigrations().OrderBy(m => m, StringComparer.Ordinal),
            MigrazioniApplicate().OrderBy(m => m, StringComparer.Ordinal));
    }

    private void InserisciCedolinoMinimo(Guid id, Guid tenantId, int mese)
    {
        using var inserisci = _connection.CreateCommand();
        inserisci.CommandText =
            """
            INSERT INTO "Cedolini" (
                "Id", "TenantId", "AziendaId", "RapportoLavoroId", "Anno", "Mese",
                "TotaleCompetenze", "TotaleTrattenute", "ImponibileInps",
                "ContributiDipendente", "ContributiDatore", "ImponibileIrpef", "IrpefNetta",
                "AddizionaleRegionale", "AddizionaleComunale", "NettoInBusta", "QuotaTfr",
                "CostoAzienda", "FerieMaturate", "FerieGodute", "FerieResidue",
                "PermessiMaturati", "PermessiGoduti", "PermessiResidui", "ConguaglioIrpef",
                "CalcolatoIl")
            VALUES (
                $id, $tenant, $azienda, $rapporto, 2026, $mese,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, $calcolato);
            """;
        inserisci.Parameters.AddWithValue("$id", id);
        inserisci.Parameters.AddWithValue("$tenant", tenantId);
        inserisci.Parameters.AddWithValue("$azienda", Guid.NewGuid());
        inserisci.Parameters.AddWithValue("$rapporto", Guid.NewGuid());
        inserisci.Parameters.AddWithValue("$mese", mese);
        inserisci.Parameters.AddWithValue("$calcolato", DateTime.UtcNow.ToString("O"));
        Assert.Equal(1, inserisci.ExecuteNonQuery());
    }

    private void InserisciCedolinoDopoWorkflow(
        Guid id,
        Guid tenantId,
        int mese,
        string stato = "Bozza",
        int versione = 1,
        string parametri = "",
        string hash = "")
    {
        using var inserisci = _connection.CreateCommand();
        inserisci.CommandText =
            """
            INSERT INTO "Cedolini" (
                "Id", "TenantId", "AziendaId", "RapportoLavoroId", "Anno", "Mese",
                "TotaleCompetenze", "TotaleTrattenute", "ImponibileInps",
                "ContributiDipendente", "ContributiDatore", "ImponibileIrpef", "IrpefNetta",
                "AddizionaleRegionale", "AddizionaleComunale", "NettoInBusta", "QuotaTfr",
                "CostoAzienda", "FerieMaturate", "FerieGodute", "FerieResidue",
                "PermessiMaturati", "PermessiGoduti", "PermessiResidui", "ConguaglioIrpef",
                "CalcolatoIl", "DescrizioneCcnl", "DetrazioniLavoroDipendente",
                "HashParametriSha256", "ImpostaSostitutivaPremio", "IrpefLorda",
                "ParametriApplicatiProtetti", "PremioRisultatoAgevolato", "TotaleEsenti",
                "Stato", "Versione")
            VALUES (
                $id, $tenant, $azienda, $rapporto, 2026, $mese,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, $calcolato,
                '', 0, $hash, 0, 0, $parametri, 0, 0, $stato, $versione);
            """;
        inserisci.Parameters.AddWithValue("$id", id);
        inserisci.Parameters.AddWithValue("$tenant", tenantId);
        inserisci.Parameters.AddWithValue("$azienda", Guid.NewGuid());
        inserisci.Parameters.AddWithValue("$rapporto", Guid.NewGuid());
        inserisci.Parameters.AddWithValue("$mese", mese);
        inserisci.Parameters.AddWithValue("$calcolato", DateTime.UtcNow.ToString("O"));
        inserisci.Parameters.AddWithValue("$hash", hash);
        inserisci.Parameters.AddWithValue("$parametri", parametri);
        inserisci.Parameters.AddWithValue("$stato", stato);
        inserisci.Parameters.AddWithValue("$versione", versione);
        Assert.Equal(1, inserisci.ExecuteNonQuery());
    }

    private void InserisciSnapshotMinimo(Guid tenantId, Guid cedolinoId)
    {
        using var inserisci = _connection.CreateCommand();
        inserisci.CommandText =
            """
            INSERT INTO "CedoliniSnapshot" (
                "Id", "TenantId", "CedolinoId", "VersioneSchema", "DatiProtetti",
                "HashDatiSha256", "CreatoIl", "CreatoDa")
            VALUES ($id, $tenant, $cedolino, 1, 'snapshot-protetto', 'HASH-SNAPSHOT',
                $creato, $attore);
            """;
        inserisci.Parameters.AddWithValue("$id", Guid.NewGuid());
        inserisci.Parameters.AddWithValue("$tenant", tenantId);
        inserisci.Parameters.AddWithValue("$cedolino", cedolinoId);
        inserisci.Parameters.AddWithValue("$creato", DateTime.UtcNow.ToString("O"));
        inserisci.Parameters.AddWithValue("$attore", Guid.NewGuid());
        Assert.Equal(1, inserisci.ExecuteNonQuery());
    }

    private void InserisciDocumentoMinimo(Guid tenantId, Guid cedolinoId)
    {
        using var inserisci = _connection.CreateCommand();
        inserisci.CommandText =
            """
            INSERT INTO "CedoliniDocumenti" (
                "Id", "TenantId", "CedolinoId", "PdfProtetto", "HashPdfSha256",
                "HashFirmaSiglaTimbroSha256", "VersioneRenderer", "CreatoIl", "CreatoDa")
            VALUES ($id, $tenant, $cedolino, 'pdf-protetto', 'HASH-PDF', 'HASH-FIRMA',
                'test-v1', $creato, $attore);
            """;
        inserisci.Parameters.AddWithValue("$id", Guid.NewGuid());
        inserisci.Parameters.AddWithValue("$tenant", tenantId);
        inserisci.Parameters.AddWithValue("$cedolino", cedolinoId);
        inserisci.Parameters.AddWithValue("$creato", DateTime.UtcNow.ToString("O"));
        inserisci.Parameters.AddWithValue("$attore", Guid.NewGuid());
        Assert.Equal(1, inserisci.ExecuteNonQuery());
    }

    private void InserisciVoceMinima(
        Guid id,
        Guid tenantId,
        Guid cedolinoId,
        string datiVoceProtetti = "voce-protetta")
    {
        using var inserisci = _connection.CreateCommand();
        inserisci.CommandText =
            """
            INSERT INTO "CedolinoVoceEntity" (
                "Id", "TenantId", "CedolinoId", "Ordine", "DatiVoceProtetti")
            VALUES ($id, $tenant, $cedolino, 0, $datiVoceProtetti);
            """;
        inserisci.Parameters.AddWithValue("$id", id);
        inserisci.Parameters.AddWithValue("$tenant", tenantId);
        inserisci.Parameters.AddWithValue("$cedolino", cedolinoId);
        inserisci.Parameters.AddWithValue("$datiVoceProtetti", datiVoceProtetti);
        Assert.Equal(1, inserisci.ExecuteNonQuery());
    }

    private Guid LeggiTenantVoce(Guid id)
    {
        using var leggi = _connection.CreateCommand();
        leggi.CommandText =
            "SELECT \"TenantId\" FROM \"CedolinoVoceEntity\" WHERE \"Id\" = $id;";
        leggi.Parameters.AddWithValue("$id", id);
        return Guid.Parse(Assert.IsType<string>(leggi.ExecuteScalar()));
    }

    private bool HaVincoloCompositoTenantVoce()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT COUNT(*)
            FROM (
                SELECT id
                FROM pragma_foreign_key_list('CedolinoVoceEntity')
                WHERE "table" = 'Cedolini'
                GROUP BY id
                HAVING COUNT(*) = 2
                   AND SUM(CASE WHEN "from" = 'TenantId' AND "to" = 'TenantId' THEN 1 ELSE 0 END) = 1
                   AND SUM(CASE WHEN "from" = 'CedolinoId' AND "to" = 'Id' THEN 1 ELSE 0 END) = 1
            );
            """;
        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    private bool HaChiaveTenantCedolinoNonParziale()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT COUNT(*)
            FROM (
                SELECT il.name
                FROM pragma_index_list('Cedolini') AS il
                JOIN pragma_index_info(il.name) AS ii
                WHERE il."unique" = 1 AND il."partial" = 0
                GROUP BY il.name
                HAVING COUNT(*) = 2
                   AND SUM(CASE WHEN ii.name = 'TenantId' THEN 1 ELSE 0 END) = 1
                   AND SUM(CASE WHEN ii.name = 'Id' THEN 1 ELSE 0 END) = 1
            );
            """;
        return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
    }

    private void RimuoviVincoliVociCedolino()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            PRAGMA foreign_keys = 0;
            CREATE TABLE "CedolinoVoceEntitySenzaFk" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CedolinoVoceEntitySenzaFk" PRIMARY KEY,
                "CedolinoId" TEXT NOT NULL,
                "DatiVoceProtetti" TEXT NOT NULL,
                "Ordine" INTEGER NOT NULL,
                "TenantId" TEXT NOT NULL);
            INSERT INTO "CedolinoVoceEntitySenzaFk" (
                "Id", "CedolinoId", "DatiVoceProtetti", "Ordine", "TenantId")
            SELECT "Id", "CedolinoId", "DatiVoceProtetti", "Ordine", "TenantId"
            FROM "CedolinoVoceEntity";
            DROP TABLE "CedolinoVoceEntity";
            ALTER TABLE "CedolinoVoceEntitySenzaFk" RENAME TO "CedolinoVoceEntity";
            PRAGMA foreign_keys = 1;
            """;
        cmd.ExecuteNonQuery();
    }

    private Guid InserisciCartellinoLegacyMinimo(Guid tenantId, int mese)
    {
        var id = Guid.NewGuid();
        using var schema = _connection.CreateCommand();
        schema.CommandText = "SELECT name, type FROM pragma_table_info('Cartellini') ORDER BY cid;";
        using var reader = schema.ExecuteReader();
        var colonne = new List<(string Nome, string Tipo)>();
        while (reader.Read()) colonne.Add((reader.GetString(0), reader.GetString(1)));
        reader.Close();

        using var inserisci = _connection.CreateCommand();
        inserisci.CommandText =
            $"INSERT INTO \"Cartellini\" ({string.Join(", ", colonne.Select(c => $"\"{c.Nome}\""))}) "
            + $"VALUES ({string.Join(", ", colonne.Select((_, i) => $"$p{i}"))});";
        for (var i = 0; i < colonne.Count; i++)
        {
            var (nome, tipo) = colonne[i];
            object valore = nome switch
            {
                "Id" => id,
                "TenantId" => tenantId,
                "RapportoLavoroId" => Guid.NewGuid(),
                "Anno" => 2026,
                "Mese" => mese,
                _ when tipo.Equals("INTEGER", StringComparison.OrdinalIgnoreCase) => 0,
                _ => "0",
            };
            inserisci.Parameters.AddWithValue($"$p{i}", valore);
        }
        Assert.Equal(1, inserisci.ExecuteNonQuery());
        return id;
    }

    private void AggiungiColonneResiduiAnfComeBootstrapLegacy(bool ferieNullable = false)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            $"""
            ALTER TABLE "Rapporti" ADD COLUMN "FerieResidueIniziali" TEXT
                {(ferieNullable ? "NULL" : "NOT NULL")} DEFAULT 0;
            ALTER TABLE "Rapporti" ADD COLUMN "PermessiResiduiIniziali" TEXT NOT NULL DEFAULT 0;
            ALTER TABLE "Cartellini" ADD COLUMN "ImportoAssegniFamiliari" TEXT NOT NULL DEFAULT 0;
            ALTER TABLE "Cartellini" ADD COLUMN "StatoAssegniFamiliari" TEXT NOT NULL DEFAULT '';
            """;
        cmd.ExecuteNonQuery();
    }

    private long ContaCartelliniConStatoAnf(string stato)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM \"Cartellini\" WHERE \"StatoAssegniFamiliari\" = $stato;";
        cmd.Parameters.AddWithValue("$stato", stato);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private long LeggiVersioneCartellino(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT \"Versione\" FROM \"Cartellini\" WHERE \"Id\" = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private void RimuoviChiaveAlternativaCedoliniComeBootstrapLegacy()
    {
        string ddl;
        using (var leggiDdl = _connection.CreateCommand())
        {
            leggiDdl.CommandText =
                "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'Cedolini';";
            ddl = Assert.IsType<string>(leggiDdl.ExecuteScalar());
        }

        const string vincolo =
            ",\n    CONSTRAINT \"AK_Cedolini_TenantId_Id\" UNIQUE (\"TenantId\", \"Id\")";
        var ddlSenzaChiave = ddl.Replace(vincolo, "", StringComparison.Ordinal);
        Assert.NotEqual(ddl, ddlSenzaChiave);

        var colonne = new List<string>();
        using (var leggiColonne = _connection.CreateCommand())
        {
            leggiColonne.CommandText = "SELECT name FROM pragma_table_info('Cedolini') ORDER BY cid;";
            using var reader = leggiColonne.ExecuteReader();
            while (reader.Read()) colonne.Add(reader.GetString(0));
        }
        var elenco = string.Join(", ", colonne.Select(c => $"\"{c}\""));

        using var ricostruisci = _connection.CreateCommand();
        ricostruisci.CommandText =
            $"""
            PRAGMA foreign_keys = 0;
            PRAGMA legacy_alter_table = 1;
            ALTER TABLE "Cedolini" RENAME TO "CedoliniConChiaveLegacy";
            {ddlSenzaChiave};
            INSERT INTO "Cedolini" ({elenco})
                SELECT {elenco} FROM "CedoliniConChiaveLegacy";
            DROP TABLE "CedoliniConChiaveLegacy";
            CREATE UNIQUE INDEX "IX_Cedolini_TenantId_RapportoLavoroId_Anno_Mese"
                ON "Cedolini" ("TenantId", "RapportoLavoroId", "Anno", "Mese");
            PRAGMA legacy_alter_table = 0;
            PRAGMA foreign_keys = 1;
            """;
        ricostruisci.ExecuteNonQuery();
    }

    private long ContaViolazioniForeignKey()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_key_check;";
        using var reader = cmd.ExecuteReader();
        long righe = 0;
        while (reader.Read()) righe++;
        return righe;
    }

    private string? DefaultColonna(string tabella, string colonna)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT dflt_value FROM pragma_table_info('{tabella}') WHERE name = $nome;";
        cmd.Parameters.AddWithValue("$nome", colonna);
        return cmd.ExecuteScalar() as string;
    }

    private (string Stato, long Versione, string Hash, string Parametri) LeggiCedolino(Guid id)
    {
        using var leggi = _connection.CreateCommand();
        leggi.CommandText =
            "SELECT \"Stato\", \"Versione\", \"HashParametriSha256\", "
            + "\"ParametriApplicatiProtetti\" FROM \"Cedolini\" WHERE \"Id\" = $id;";
        leggi.Parameters.AddWithValue("$id", id);
        using var riga = leggi.ExecuteReader();
        Assert.True(riga.Read());
        return (riga.GetString(0), riga.GetInt64(1), riga.GetString(2), riga.GetString(3));
    }

    private long ContaCedolini(Guid tenantId, string stato)
    {
        using var conta = _connection.CreateCommand();
        conta.CommandText =
            "SELECT COUNT(*) FROM \"Cedolini\" WHERE \"TenantId\" = $tenant AND \"Stato\" = $stato;";
        conta.Parameters.AddWithValue("$tenant", tenantId);
        conta.Parameters.AddWithValue("$stato", stato);
        return Convert.ToInt64(conta.ExecuteScalar());
    }

    private static CedolinoEntity CreaCedolinoCorrente(
        Guid id,
        Guid tenantId,
        int mese,
        string stato,
        string parametri = "",
        string hash = "") => new()
    {
        Id = id,
        TenantId = tenantId,
        AziendaId = Guid.NewGuid(),
        RapportoLavoroId = Guid.NewGuid(),
        Anno = 2026,
        Mese = mese,
        Stato = stato,
        Versione = 1,
        ParametriApplicatiProtetti = parametri,
        HashParametriSha256 = hash,
        CalcolatoIl = DateTime.UtcNow,
    };

    [Fact]
    public void DatabaseVuoto_LeMigrazioniCreanoLoSchemaELoRegistrano()
    {
        using var db = NuovoContesto();

        SchemaDatabase.Applica(db);

        Assert.Contains("Aziende", Tabelle());
        Assert.Contains("PolicyRetention", Tabelle());
        AssertSchemaAllineato();
    }

    [Fact]
    public void MigrazioneVersioneCartellino_BackfillaLeBozzeEsistentiAUno()
    {
        var tenant = Guid.NewGuid();
        Guid cartellinoId;
        using (var db = NuovoContesto())
        {
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260822152630_RuntimeAurora");
            cartellinoId = InserisciCartellinoLegacyMinimo(tenant, 7);

            migrator.Migrate();
        }

        Assert.Equal(1L, LeggiVersioneCartellino(cartellinoId));
        Assert.Equal("1", DefaultColonna("Cartellini", "Versione"));
        AssertSchemaAllineato();
    }

    [Fact]
    public void AdozioneSenzaStorico_AggiungeVersioneCartellinoConDefaultUno()
    {
        var tenant = Guid.NewGuid();
        Guid cartellinoId;
        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>().Migrate("20260822152630_RuntimeAurora");
            cartellinoId = InserisciCartellinoLegacyMinimo(tenant, 7);
            db.Database.ExecuteSqlRaw("DROP TABLE \"__EFMigrationsHistory\";");
        }

        using (var db = NuovoContesto())
            Assert.Contains("adottato", SchemaDatabase.Applica(db));

        Assert.Equal(1L, LeggiVersioneCartellino(cartellinoId));
        Assert.Equal("1", DefaultColonna("Cartellini", "Versione"));
        AssertSchemaAllineato();
    }

    [Fact]
    public void MigrazioneWorkflow_BackfillaGliStoriciPreWorkflowENuoviRestanoBozza()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var storicoA = Guid.NewGuid();
        var storicoB = Guid.NewGuid();
        var nuovo = Guid.NewGuid();
        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>().Migrate("20260810055043_OrdineVociCedolino");
            InserisciCedolinoMinimo(storicoA, tenantA, 1);
            InserisciCedolinoMinimo(storicoB, tenantB, 2);

            db.Database.GetService<IMigrator>().Migrate();
            InserisciCedolinoDopoWorkflow(nuovo, tenantA, 3);
        }

        Assert.Equal(("PreWorkflow", 1L, "", ""), LeggiCedolino(storicoA));
        Assert.Equal(("PreWorkflow", 1L, "", ""), LeggiCedolino(storicoB));
        Assert.Equal(("Bozza", 1L, "", ""), LeggiCedolino(nuovo));
        Assert.Equal(1L, ContaCedolini(tenantA, "PreWorkflow"));
        Assert.Equal(1L, ContaCedolini(tenantB, "PreWorkflow"));
    }

    [Fact]
    public void MigrazioneCorrettiva_WorkflowGiaRegistrato_RiclassificaSoloLeRigheLegacy()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var storicoVuoto = Guid.NewGuid();
        var bozzaValida = Guid.NewGuid();
        var bozzaConSnapshot = Guid.NewGuid();
        var bozzaConDocumento = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            // Riproduce il database locale osservato: workflow gia nello storico, ma righe adottate
            // in precedenza con i default neutri Stato="" e Versione=0.
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260810061248_WorkflowCedolinoImmutabile");
            InserisciCedolinoDopoWorkflow(
                storicoVuoto, tenantA, 1, stato: "", versione: 0);
            InserisciCedolinoDopoWorkflow(
                bozzaValida, tenantA, 2, parametri: "enc:v2:parametri", hash: "HASH");
            InserisciCedolinoDopoWorkflow(bozzaConSnapshot, tenantA, 3);
            InserisciCedolinoDopoWorkflow(bozzaConDocumento, tenantB, 4);
            InserisciSnapshotMinimo(tenantA, bozzaConSnapshot);
            InserisciDocumentoMinimo(tenantB, bozzaConDocumento);

            Assert.Contains(
                "20260810061248_WorkflowCedolinoImmutabile",
                db.Database.GetAppliedMigrations());
            Assert.DoesNotContain(
                "20260817222143_ClassificazioneCedoliniPreWorkflow",
                db.Database.GetAppliedMigrations());

            migrator.Migrate();
        }

        Assert.Equal(("PreWorkflow", 1L, "", ""), LeggiCedolino(storicoVuoto));
        Assert.Equal(("Bozza", 1L, "HASH", "enc:v2:parametri"), LeggiCedolino(bozzaValida));
        Assert.Equal("Bozza", LeggiCedolino(bozzaConSnapshot).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaConDocumento).Stato);
        Assert.Equal(1L, ContaCedolini(tenantA, "PreWorkflow"));
        Assert.Equal(0L, ContaCedolini(tenantB, "PreWorkflow"));
        Assert.Contains(
            "20260817222143_ClassificazioneCedoliniPreWorkflow",
            MigrazioniApplicate());
        AssertSchemaAllineato();
    }

    [Fact]
    public void SchemaIbridoConStoricoWorkflowEColonneResiduiGiaPresenti_ConvergeSenzaSaltareDatiOVincoli()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var cedolini = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>().Migrate("20260810061248_WorkflowCedolinoImmutabile");
            // Riproduce il vecchio bootstrap: Workflow risulta applicata, tabelle figlie e colonne
            // esistono, ma la sua AK (TenantId, Id) non era stata realmente installata.
            RimuoviChiaveAlternativaCedoliniComeBootstrapLegacy();
            InserisciCartellinoLegacyMinimo(tenantA, 1);
            InserisciCartellinoLegacyMinimo(tenantA, 2);
            InserisciCartellinoLegacyMinimo(tenantB, 3);
            AggiungiColonneResiduiAnfComeBootstrapLegacy();

            InserisciCedolinoDopoWorkflow(cedolini[0], tenantA, 1, stato: "", versione: 0);
            InserisciCedolinoDopoWorkflow(cedolini[1], tenantA, 2, stato: "", versione: 0);
            InserisciCedolinoDopoWorkflow(cedolini[2], tenantB, 3, stato: "", versione: 0);

            int migrazioniPendenti = db.Database.GetPendingMigrations().Count();
            Assert.True(migrazioniPendenti >= 1);
            Assert.Equal(3L, ContaCartelliniConStatoAnf(""));

            var esito = SchemaDatabase.Applica(db);
            Assert.Contains($"applicate {migrazioniPendenti} migrazioni", esito);
        }

        Assert.Equal(3L, ContaCartelliniConStatoAnf("DaVerificare"));
        Assert.Equal(0L, ContaCartelliniConStatoAnf(""));
        Assert.Equal("'0.0'", DefaultColonna("Rapporti", "FerieResidueIniziali"));
        Assert.Equal("'0.0'", DefaultColonna("Cartellini", "ImportoAssegniFamiliari"));
        Assert.Equal("'DaVerificare'", DefaultColonna("Cartellini", "StatoAssegniFamiliari"));
        Assert.All(cedolini, id => Assert.Equal("PreWorkflow", LeggiCedolino(id).Stato));
        Assert.True(HaVincoloCompositoTenantVoce());
        Assert.Equal(0L, ContaViolazioniForeignKey());
        AssertSchemaAllineato();

        var erroreCrossTenant = Assert.Throws<SqliteException>(() =>
            InserisciVoceMinima(Guid.NewGuid(), Guid.NewGuid(), cedolini[0]));
        Assert.Equal(19, erroreCrossTenant.SqliteErrorCode);
    }

    [Fact]
    public void SchemaIbridoResiduiNonEquivalente_FallisceSenzaRegistrareMigrazioni()
    {
        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>().Migrate("20260810061248_WorkflowCedolinoImmutabile");
            AggiungiColonneResiduiAnfComeBootstrapLegacy(ferieNullable: true);

            var errore = Assert.Throws<InvalidOperationException>(() => SchemaDatabase.Applica(db));
            Assert.Contains("non equivalente", errore.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "20260817211612_ResiduiRapportoEAssegniFamiliari",
            MigrazioniApplicate());
        Assert.Equal(8, MigrazioniApplicate().Count);
    }

    [Fact]
    public void MigrazioneVincoloTenantVoci_RiallineaLeRigheLegacyAlCedolino()
    {
        var tenantCedolino = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var voceId = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");
            InserisciCedolinoDopoWorkflow(
                cedolinoId,
                tenantCedolino,
                mese: 5,
                parametri: "parametri-protetti",
                hash: "HASH");

            // Guid.Empty è la sentinella delle righe legacy non ancora associate al tenant.
            InserisciVoceMinima(voceId, Guid.Empty, cedolinoId, datiVoceProtetti: "");
            Assert.Equal(Guid.Empty, LeggiTenantVoce(voceId));

            migrator.Migrate();
        }

        Assert.Equal(tenantCedolino, LeggiTenantVoce(voceId));
        AssertSchemaAllineato();
    }

    [Fact]
    public void MigrazioneVincoloTenantVoci_NonRiscriveUnCifratoAssociatoAlTenantErrato()
    {
        var tenantCedolino = Guid.NewGuid();
        var tenantDiscordante = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var voceId = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");
            InserisciCedolinoDopoWorkflow(
                cedolinoId,
                tenantCedolino,
                mese: 7,
                parametri: "parametri-protetti",
                hash: "HASH");
            InserisciVoceMinima(voceId, tenantDiscordante, cedolinoId);

            var errore = Assert.Throws<SqliteException>(() => migrator.Migrate());
            Assert.Equal(19, errore.SqliteErrorCode);
        }

        Assert.Equal(tenantDiscordante, LeggiTenantVoce(voceId));
        Assert.DoesNotContain(
            "20260817222655_VincoloTenantVociCedolino",
            MigrazioniApplicate());
    }

    [Fact]
    public void MigrazioneVincoloTenantVoci_RifiutaUnaVoceOrfanaSenzaRegistrarsi()
    {
        var voceId = Guid.NewGuid();
        using (var db = NuovoContesto())
        {
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");

            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = 0;");
            InserisciVoceMinima(voceId, Guid.NewGuid(), Guid.NewGuid(), "enc:v2:orfana");
            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = 1;");

            var errore = Assert.Throws<SqliteException>(() => migrator.Migrate());
            Assert.Equal(19, errore.SqliteErrorCode);
        }

        using (var verifica = _connection.CreateCommand())
        {
            verifica.CommandText =
                "SELECT COUNT(*) FROM \"CedolinoVoceEntity\" WHERE \"Id\" = $id;";
            verifica.Parameters.AddWithValue("$id", voceId);
            Assert.Equal(1L, Convert.ToInt64(verifica.ExecuteScalar()));
        }
        Assert.DoesNotContain(
            "20260817222655_VincoloTenantVociCedolino",
            MigrazioniApplicate());
    }

    [Fact]
    public void VincoloTenantVoci_RifiutaUnaVoceCheAttraversaIlTenant()
    {
        var tenantCedolino = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            SchemaDatabase.Applica(db);
            InserisciCedolinoDopoWorkflow(
                cedolinoId,
                tenantCedolino,
                mese: 6,
                parametri: "parametri-protetti",
                hash: "HASH");
        }

        var errore = Assert.Throws<SqliteException>(() =>
            InserisciVoceMinima(Guid.NewGuid(), Guid.NewGuid(), cedolinoId));

        Assert.Equal(19, errore.SqliteErrorCode);
        Assert.Contains("FOREIGN KEY constraint failed", errore.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AdozioneSchemaLegacy_BackfillPreWorkflowETabelleAssentiSonoGestiti()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var storicoA = Guid.NewGuid();
        var storicoB = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>().Migrate("20260810055043_OrdineVociCedolino");
            InserisciCedolinoMinimo(storicoA, tenantA, 1);
            InserisciCedolinoMinimo(storicoB, tenantB, 2);
            db.Database.ExecuteSqlRaw("DROP TABLE \"__EFMigrationsHistory\";");
        }
        Assert.DoesNotContain("CedoliniSnapshot", Tabelle());
        Assert.DoesNotContain("CedoliniDocumenti", Tabelle());

        using (var db = NuovoContesto())
        {
            var esito = SchemaDatabase.Applica(db);
            Assert.Contains("cedolini pre-workflow riclassificati: 2", esito);
        }

        Assert.Equal(("PreWorkflow", 1L, "", ""), LeggiCedolino(storicoA));
        Assert.Equal(("PreWorkflow", 1L, "", ""), LeggiCedolino(storicoB));
        Assert.Equal(1L, ContaCedolini(tenantA, "PreWorkflow"));
        Assert.Equal(1L, ContaCedolini(tenantB, "PreWorkflow"));
        Assert.Contains("CedoliniSnapshot", Tabelle());
        Assert.Contains("CedoliniDocumenti", Tabelle());
        Assert.True(HaVincoloCompositoTenantVoce());

        // La chiave candidata (TenantId, Id) mancava nello schema precedente al workflow: dopo
        // l'adozione deve sostenere sia il nuovo FK delle voci sia quelli delle tabelle create ora.
        InserisciVoceMinima(Guid.NewGuid(), tenantA, storicoA);
        InserisciSnapshotMinimo(tenantA, storicoA);
        InserisciDocumentoMinimo(tenantB, storicoB);
        var erroreCrossTenant = Assert.Throws<SqliteException>(() =>
            InserisciVoceMinima(Guid.NewGuid(), tenantB, storicoA));
        Assert.Equal(19, erroreCrossTenant.SqliteErrorCode);
        Assert.Contains(
            "FOREIGN KEY constraint failed",
            erroreCrossTenant.Message,
            StringComparison.Ordinal);
        AssertSchemaAllineato();
    }

    [Fact]
    public void AdozioneSchemaParzialeSenzaTabellaVoci_LaRicreaConVincoloTenantComposito()
    {
        var tenantCedolino = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>().Migrate("20260810055043_OrdineVociCedolino");
            InserisciCedolinoMinimo(cedolinoId, tenantCedolino, 10);
            db.Database.ExecuteSqlRaw("DROP TABLE \"CedolinoVoceEntity\";");
            db.Database.ExecuteSqlRaw("DROP TABLE \"__EFMigrationsHistory\";");
        }
        Assert.DoesNotContain("CedolinoVoceEntity", Tabelle());

        using (var db = NuovoContesto())
        {
            var esito = SchemaDatabase.Applica(db);
            Assert.Contains("CedolinoVoceEntity", esito);
        }

        Assert.Contains("CedolinoVoceEntity", Tabelle());
        Assert.True(HaVincoloCompositoTenantVoce());
        Assert.Equal(0L, ContaViolazioniForeignKey());
        AssertSchemaAllineato();

        InserisciVoceMinima(Guid.NewGuid(), tenantCedolino, cedolinoId);
        var erroreCrossTenant = Assert.Throws<SqliteException>(() =>
            InserisciVoceMinima(Guid.NewGuid(), Guid.NewGuid(), cedolinoId));
        Assert.Equal(19, erroreCrossTenant.SqliteErrorCode);
        Assert.Contains("FOREIGN KEY constraint failed", erroreCrossTenant.Message);
    }

    [Fact]
    public void AdozioneSenzaStorico_InstallaDavveroIlVincoloTenantVociERiparaSoloLaSentinella()
    {
        var tenantCedolino = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var voceId = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");
            InserisciCedolinoDopoWorkflow(
                cedolinoId,
                tenantCedolino,
                mese: 8,
                parametri: "parametri-protetti",
                hash: "HASH");
            InserisciVoceMinima(voceId, Guid.Empty, cedolinoId, datiVoceProtetti: "");
            // Copre anche uno schema importato privo del vecchio FK, non soltanto la sostituzione
            // del vincolo storico sul solo CedolinoId.
            RimuoviVincoliVociCedolino();
            Assert.False(HaVincoloCompositoTenantVoce());
            db.Database.ExecuteSqlRaw("DROP TABLE \"__EFMigrationsHistory\";");
        }

        using (var db = NuovoContesto())
            Assert.Contains("adottato", SchemaDatabase.Applica(db));

        Assert.Equal(tenantCedolino, LeggiTenantVoce(voceId));
        Assert.True(HaVincoloCompositoTenantVoce());
        AssertSchemaAllineato();

        var errore = Assert.Throws<SqliteException>(() =>
            InserisciVoceMinima(Guid.NewGuid(), Guid.NewGuid(), cedolinoId));
        Assert.Equal(19, errore.SqliteErrorCode);
        Assert.Contains("FOREIGN KEY constraint failed", errore.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AdozioneVociConColonnaExtra_FalliscePrimaDelRebuildEPreservaIlDato()
    {
        var tenant = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var voceId = Guid.NewGuid();
        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>()
                .Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");
            InserisciCedolinoDopoWorkflow(
                cedolinoId,
                tenant,
                mese: 11,
                parametri: "parametri-protetti",
                hash: "HASH");
            InserisciVoceMinima(voceId, tenant, cedolinoId, "voce-da-preservare");
            RimuoviVincoliVociCedolino();
            db.Database.ExecuteSqlInterpolated(
                $"""
                ALTER TABLE "CedolinoVoceEntity" ADD COLUMN "NotaLegacy" TEXT;
                UPDATE "CedolinoVoceEntity"
                SET "NotaLegacy" = {"nota-da-preservare"}
                WHERE "Id" = {voceId};
                DROP TABLE "__EFMigrationsHistory";
                """);

            var errore = Assert.Throws<InvalidOperationException>(() => SchemaDatabase.Applica(db));
            Assert.Contains("colonne extra", errore.Message, StringComparison.OrdinalIgnoreCase);
            // Se la mappatura di CedolinoVoceEntity non fosse stata risolta, l'elenco conterrebbe
            // anche le cinque colonne ordinarie invece della sola estensione legacy.
            Assert.Contains("(NotaLegacy)", errore.Message, StringComparison.Ordinal);
        }

        Assert.Contains("CedolinoVoceEntity", Tabelle());
        Assert.DoesNotContain(HistoryRepository.DefaultTableName, Tabelle());
        Assert.Equal(tenant, LeggiTenantVoce(voceId));
        using var verifica = _connection.CreateCommand();
        verifica.CommandText =
            """
            SELECT "DatiVoceProtetti", "NotaLegacy"
            FROM "CedolinoVoceEntity"
            WHERE "Id" = $id;
            """;
        verifica.Parameters.AddWithValue("$id", voceId);
        using var riga = verifica.ExecuteReader();
        Assert.True(riga.Read());
        Assert.Equal("voce-da-preservare", riga.GetString(0));
        Assert.Equal("nota-da-preservare", riga.GetString(1));
    }

    [Fact]
    public void AdozioneCedolini_UniqueParzialeNonValeComeChiaveReferenziata()
    {
        using (var db = NuovoContesto())
        {
            db.Database.GetService<IMigrator>()
                .Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");
            RimuoviChiaveAlternativaCedoliniComeBootstrapLegacy();
            RimuoviVincoliVociCedolino();
            db.Database.ExecuteSqlRaw(
                """
                CREATE UNIQUE INDEX "UX_Cedolini_TenantId_Id_Parziale"
                    ON "Cedolini" ("TenantId", "Id")
                    WHERE "Anno" >= 0;
                DROP TABLE "__EFMigrationsHistory";
                """);
            Assert.False(HaChiaveTenantCedolinoNonParziale());
        }

        using (var db = NuovoContesto())
            Assert.Contains("adottato", SchemaDatabase.Applica(db));

        Assert.True(HaChiaveTenantCedolinoNonParziale());
        Assert.True(HaVincoloCompositoTenantVoce());
        AssertSchemaAllineato();
    }

    [Fact]
    public void AdozioneSenzaStorico_MismatchCifratoFallisceSenzaFalsoCompletamento()
    {
        var tenantCedolino = Guid.NewGuid();
        var tenantDiscordante = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var voceId = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            var migrator = db.Database.GetService<IMigrator>();
            migrator.Migrate("20260817222143_ClassificazioneCedoliniPreWorkflow");
            InserisciCedolinoDopoWorkflow(
                cedolinoId,
                tenantCedolino,
                mese: 9,
                parametri: "parametri-protetti",
                hash: "HASH");
            InserisciVoceMinima(voceId, tenantDiscordante, cedolinoId, "enc:v2:voce");
            db.Database.ExecuteSqlRaw("DROP TABLE \"__EFMigrationsHistory\";");
        }

        using (var db = NuovoContesto())
        {
            var errore = Assert.Throws<InvalidOperationException>(() => SchemaDatabase.Applica(db));
            Assert.Contains("Adozione interrotta", errore.Message, StringComparison.Ordinal);
            Assert.Contains("AEAD", errore.Message, StringComparison.Ordinal);
        }

        Assert.Equal(tenantDiscordante, LeggiTenantVoce(voceId));
        Assert.False(HaVincoloCompositoTenantVoce());
        Assert.DoesNotContain(HistoryRepository.DefaultTableName, Tabelle());
    }

    [Fact]
    public void SchemaPreesistenteSenzaStorico_VieneAdottatoSenzaPerdereDati()
    {
        // Database "storico": creato con EnsureCreated, quindi senza tabella delle migrazioni.
        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            db.Aziende.Add(new AziendaEntity
            {
                TenantId = Guid.NewGuid(),
                RagioneSociale = "Alfa Srl",
                PartitaIva = "12345678903",
            });
            db.SaveChanges();
        }
        Assert.DoesNotContain(HistoryRepository.DefaultTableName, Tabelle());

        using (var db = NuovoContesto())
        {
            var esito = SchemaDatabase.Applica(db);
            Assert.Contains("adottato", esito);
        }

        AssertSchemaAllineato();
        using var verifica = NuovoContesto();
        Assert.Equal("Alfa Srl", Assert.Single(verifica.Aziende).RagioneSociale);
    }

    [Fact]
    public void AdozioneSchemaCorrente_UsaSentinelleENonConverteBozzeNuoveOSnapshot()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var storicoVuoto = Guid.NewGuid();
        var storicoBozza = Guid.NewGuid();
        var bozzaNuova = Guid.NewGuid();
        var bozzaConSnapshot = Guid.NewGuid();
        var bozzaConDocumento = Guid.NewGuid();

        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            db.Cedolini.AddRange(
                CreaCedolinoCorrente(storicoVuoto, tenantA, 1, "Bozza"),
                CreaCedolinoCorrente(storicoBozza, tenantB, 2, "Bozza"),
                CreaCedolinoCorrente(bozzaNuova, tenantA, 3, "Bozza", "parametri-protetti", "HASH"),
                CreaCedolinoCorrente(bozzaConSnapshot, tenantA, 4, "Bozza"),
                CreaCedolinoCorrente(bozzaConDocumento, tenantB, 5, "Bozza"));
            db.SaveChanges();

            // Lo stato neutro esiste soltanto nei database legacy: il codice corrente vieta
            // correttamente di creare via EF un nuovo cedolino fuori dallo stato Bozza.
            db.Database.ExecuteSqlInterpolated(
                $"UPDATE Cedolini SET Stato = '' WHERE Id = {storicoVuoto} AND TenantId = {tenantA}");

            // Fixture volutamente incoerenti che simulano dati legacy già presenti prima
            // della guardia applicativa: SQL diretto evita di indebolire l'interceptor corrente.
            db.Database.ExecuteSqlInterpolated($"""
                INSERT INTO CedoliniSnapshot
                    (Id, TenantId, CedolinoId, VersioneSchema, DatiProtetti,
                     HashDatiSha256, CreatoIl, CreatoDa)
                VALUES
                    ({Guid.NewGuid()}, {tenantA}, {bozzaConSnapshot}, {1},
                     {"snapshot-protetto"}, {"HASH-SNAPSHOT"},
                     {DateTime.UtcNow}, {Guid.NewGuid()})
                """);
            db.Database.ExecuteSqlInterpolated($"""
                INSERT INTO CedoliniDocumenti
                    (Id, TenantId, CedolinoId, PdfProtetto, HashPdfSha256,
                     HashFirmaSiglaTimbroSha256, VersioneRenderer, CreatoIl, CreatoDa)
                VALUES
                    ({Guid.NewGuid()}, {tenantB}, {bozzaConDocumento},
                     {"pdf-protetto"}, {"HASH-PDF"}, {"HASH-FIRMA"},
                     {"test-v1"}, {DateTime.UtcNow}, {Guid.NewGuid()})
                """);
        }

        using (var db = NuovoContesto())
        {
            var esito = SchemaDatabase.Applica(db);
            Assert.Contains("cedolini pre-workflow riclassificati: 2", esito);
        }

        Assert.Equal("PreWorkflow", LeggiCedolino(storicoVuoto).Stato);
        Assert.Equal("PreWorkflow", LeggiCedolino(storicoBozza).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaNuova).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaConSnapshot).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaConDocumento).Stato);
        Assert.Equal(1L, ContaCedolini(tenantA, "PreWorkflow"));
        Assert.Equal(1L, ContaCedolini(tenantB, "PreWorkflow"));

        // Simula la ripresa dell'adozione dopo la perdita dello storico: il predicato è
        // idempotente e non riclassifica né i record già marcati né le bozze correnti.
        using (var eliminaStorico = _connection.CreateCommand())
        {
            eliminaStorico.CommandText = "DROP TABLE \"__EFMigrationsHistory\";";
            eliminaStorico.ExecuteNonQuery();
        }
        using (var db = NuovoContesto())
        {
            var esito = SchemaDatabase.Applica(db);
            Assert.Contains("cedolini pre-workflow riclassificati: 0", esito);
        }

        Assert.Equal("PreWorkflow", LeggiCedolino(storicoVuoto).Stato);
        Assert.Equal("PreWorkflow", LeggiCedolino(storicoBozza).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaNuova).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaConSnapshot).Stato);
        Assert.Equal("Bozza", LeggiCedolino(bozzaConDocumento).Stato);
        AssertSchemaAllineato();
    }

    [Fact]
    public void SchemaPreesistenteIncompleto_LeTabelleMancantiVengonoRicreate()
    {
        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            // Simula un database fermo a una versione precedente del modello.
            db.Database.ExecuteSqlRaw("DROP TABLE \"PraticheInail\";");
            db.Database.ExecuteSqlRaw("DROP INDEX \"IX_Persone_TenantId_CodiceFiscale\";");
        }
        Assert.DoesNotContain("PraticheInail", Tabelle());

        using (var db = NuovoContesto())
            Assert.Contains("PraticheInail", SchemaDatabase.Applica(db));

        Assert.Contains("PraticheInail", Tabelle());
        AssertSchemaAllineato();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' " +
            "AND name = 'IX_Persone_TenantId_CodiceFiscale';";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public void AdozioneSchemaSenzaAuditLog_LaCreaPrimaDiEseguireIlBackfill()
    {
        using (var db = NuovoContesto())
        {
            db.Database.EnsureCreated();
            db.Database.ExecuteSqlRaw("DROP TABLE \"AuditLog\";");
        }
        Assert.DoesNotContain("AuditLog", Tabelle());
        Assert.DoesNotContain(HistoryRepository.DefaultTableName, Tabelle());

        using (var db = NuovoContesto())
            Assert.Contains("AuditLog", SchemaDatabase.Applica(db));

        Assert.Contains("AuditLog", Tabelle());
        AssertSchemaAllineato();
    }

    [Fact]
    public void AdozioneInterrottaConStoricoPrefisso_RiprendeSenzaDuplicareDdl()
    {
        using (var db = NuovoContesto())
        {
            db.Database.Migrate();
            var migrazioni = db.Database.GetMigrations().ToList();
            Assert.True(migrazioni.Count >= 2);
            var primaMancante = migrazioni[^2];
            var testa = migrazioni[^1];
            db.Database.ExecuteSqlInterpolated(
                $"DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" >= {primaMancante};");
            db.Database.ExecuteSqlRaw(
                """
                CREATE TABLE "__PagheAdozioneSchema" (
                    "Id" INTEGER NOT NULL PRIMARY KEY,
                    "MigrazioneTesta" TEXT NOT NULL
                );
                """);
            db.Database.ExecuteSqlInterpolated(
                $"""
                INSERT INTO "__PagheAdozioneSchema" ("Id", "MigrazioneTesta")
                VALUES (1, {testa});
                """);
            Assert.Equal(2, db.Database.GetPendingMigrations().Count());
        }

        using (var db = NuovoContesto())
            Assert.Contains("adottato", SchemaDatabase.Applica(db));

        Assert.DoesNotContain("__PagheAdozioneSchema", Tabelle());
        AssertSchemaAllineato();
        using (var db = NuovoContesto())
            Assert.Equal("schema già aggiornato", SchemaDatabase.Applica(db));
    }

    [Fact]
    public void SecondaEsecuzione_NonRipeteNulla()
    {
        using (var db = NuovoContesto()) SchemaDatabase.Applica(db);

        using (var db = NuovoContesto())
            Assert.Equal("schema già aggiornato", SchemaDatabase.Applica(db));

        AssertSchemaAllineato();
    }

    public void Dispose() => _connection.Dispose();
}
