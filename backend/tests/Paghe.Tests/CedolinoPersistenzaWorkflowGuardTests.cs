using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Domain;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

public sealed class CedolinoPersistenzaWorkflowGuardTests : IDisposable
{
    private const string Modifica = "modifica";
    private const string Aggiunta = "aggiunta";
    private const string Cancellazione = "cancellazione";

    private readonly SqliteConnection _connessione = new("DataSource=:memory:");

    public CedolinoPersistenzaWorkflowGuardTests()
    {
        _connessione.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connessione).Options);

    [Theory]
    [InlineData(nameof(StatoCedolino.PreWorkflow), Modifica)]
    [InlineData(nameof(StatoCedolino.PreWorkflow), Aggiunta)]
    [InlineData(nameof(StatoCedolino.PreWorkflow), Cancellazione)]
    [InlineData(nameof(StatoCedolino.Controllata), Modifica)]
    [InlineData(nameof(StatoCedolino.Controllata), Aggiunta)]
    [InlineData(nameof(StatoCedolino.Controllata), Cancellazione)]
    [InlineData(nameof(StatoCedolino.Validata), Modifica)]
    [InlineData(nameof(StatoCedolino.Validata), Aggiunta)]
    [InlineData(nameof(StatoCedolino.Validata), Cancellazione)]
    [InlineData(nameof(StatoCedolino.Chiusa), Modifica)]
    [InlineData(nameof(StatoCedolino.Chiusa), Aggiunta)]
    [InlineData(nameof(StatoCedolino.Chiusa), Cancellazione)]
    public async Task Voci_SonoImmutabiliDopoLaBozza(string stato, string operazione)
    {
        var dati = await InserisciBozzaConVoceAsync();
        await ImpostaStatoFuoriInterceptorAsync(dati, stato);

        await using (var db = NuovoContesto())
        {
            if (operazione == Modifica)
            {
                // Carica anche la testata: la guardia deve considerare sia DB sia tracker.
                _ = await db.Cedolini.SingleAsync(c =>
                    c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
                var voce = await db.Set<CedolinoVoceEntity>().SingleAsync(v =>
                    v.TenantId == dati.TenantId && v.Id == dati.VoceId);
                voce.Ordine = 99;
            }
            else if (operazione == Aggiunta)
            {
                db.Set<CedolinoVoceEntity>().Add(NuovaVoce(
                    dati.TenantId, dati.CedolinoId, Guid.NewGuid(), ordine: 2));
            }
            else
            {
                var voce = await db.Set<CedolinoVoceEntity>().SingleAsync(v =>
                    v.TenantId == dati.TenantId && v.Id == dati.VoceId);
                db.Remove(voce);
            }

            var errore = await Assert.ThrowsAsync<InvalidOperationException>(
                () => db.SaveChangesAsync());
            Assert.Contains("voci", errore.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bozza", errore.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var verifica = NuovoContesto())
        {
            Assert.Equal(stato, await verifica.Cedolini
                .Where(c => c.TenantId == dati.TenantId && c.Id == dati.CedolinoId)
                .Select(c => c.Stato)
                .SingleAsync());
            Assert.Equal(1, await verifica.Set<CedolinoVoceEntity>().CountAsync(v =>
                v.TenantId == dati.TenantId && v.CedolinoId == dati.CedolinoId));
            Assert.Equal(1, await verifica.Set<CedolinoVoceEntity>()
                .Where(v => v.TenantId == dati.TenantId && v.Id == dati.VoceId)
                .Select(v => v.Ordine)
                .SingleAsync());
        }
    }

    [Fact]
    public async Task Voci_InBozzaPossonoEssereCreateModificateEdEliminate()
    {
        var tenantId = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var primaVoceId = Guid.NewGuid();
        var secondaVoceId = Guid.NewGuid();

        await using (var db = NuovoContesto())
        {
            var cedolino = NuovoCedolino(tenantId, cedolinoId, StatoCedolino.Bozza);
            cedolino.Voci.Add(NuovaVoce(tenantId, cedolinoId, primaVoceId, ordine: 1));
            db.Cedolini.Add(cedolino);
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var prima = await db.Set<CedolinoVoceEntity>().SingleAsync(v =>
                v.TenantId == tenantId && v.Id == primaVoceId);
            prima.Ordine = 3;
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            db.Set<CedolinoVoceEntity>().Add(
                NuovaVoce(tenantId, cedolinoId, secondaVoceId, ordine: 2));
            await db.SaveChangesAsync();
        }

        await using (var db = NuovoContesto())
        {
            var seconda = await db.Set<CedolinoVoceEntity>().SingleAsync(v =>
                v.TenantId == tenantId && v.Id == secondaVoceId);
            db.Remove(seconda);
            await db.SaveChangesAsync();
        }

        await using (var verifica = NuovoContesto())
        {
            Assert.Equal(nameof(StatoCedolino.Bozza), await verifica.Cedolini
                .Where(c => c.TenantId == tenantId && c.Id == cedolinoId)
                .Select(c => c.Stato)
                .SingleAsync());
            Assert.Equal(1, await verifica.Set<CedolinoVoceEntity>().CountAsync(v =>
                v.TenantId == tenantId && v.CedolinoId == cedolinoId));
            Assert.Equal(3, await verifica.Set<CedolinoVoceEntity>()
                .Where(v => v.TenantId == tenantId && v.Id == primaVoceId)
                .Select(v => v.Ordine)
                .SingleAsync());
        }
    }

    [Fact]
    public async Task Voce_NonPuoCollegarsiAllaTestataDiUnAltroTenant()
    {
        var dati = await InserisciBozzaConVoceAsync();
        await using var db = NuovoContesto();
        db.Set<CedolinoVoceEntity>().Add(NuovaVoce(
            Guid.NewGuid(), dati.CedolinoId, Guid.NewGuid(), ordine: 2));

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("stesso tenant", errore.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(StatoCedolino.Bozza), nameof(StatoCedolino.Chiusa))]
    [InlineData(nameof(StatoCedolino.Controllata), nameof(StatoCedolino.Chiusa))]
    public async Task Transizioni_NonConsentonoSalti(string origine, string destinazione)
    {
        var dati = await InserisciBozzaConVoceAsync();
        if (origine != nameof(StatoCedolino.Bozza))
            await ImpostaStatoFuoriInterceptorAsync(dati, origine);

        await using (var db = NuovoContesto())
        {
            var cedolino = await db.Cedolini.SingleAsync(c =>
                c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
            cedolino.Stato = destinazione;
            cedolino.Versione++;

            var errore = await Assert.ThrowsAsync<InvalidOperationException>(
                () => db.SaveChangesAsync());
            Assert.Contains("soltanto", errore.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var verifica = NuovoContesto();
        Assert.Equal(origine, await verifica.Cedolini
            .Where(c => c.TenantId == dati.TenantId && c.Id == dati.CedolinoId)
            .Select(c => c.Stato)
            .SingleAsync());
    }

    [Fact]
    public async Task Controllo_SenzaSnapshotAtomicoEVietato()
    {
        var dati = await InserisciBozzaConVoceAsync();
        await using var db = NuovoContesto();
        var cedolino = await db.Cedolini.SingleAsync(c =>
            c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
        cedolino.Stato = nameof(StatoCedolino.Controllata);
        cedolino.Versione = 2;
        cedolino.ControllatoIl = DateTime.UtcNow;
        cedolino.ControllatoDa = Guid.NewGuid();

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("snapshot", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(StatoCedolino.Bozza), await StatoPersistitoAsync(dati));
    }

    [Fact]
    public async Task Chiusura_SenzaDocumentoAtomicoEVietata()
    {
        var dati = await InserisciBozzaConVoceAsync();
        await ImpostaWorkflowFuoriInterceptorAsync(
            dati, StatoCedolino.Validata, versione: 3);
        await using var db = NuovoContesto();
        var cedolino = await db.Cedolini.SingleAsync(c =>
            c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
        cedolino.Stato = nameof(StatoCedolino.Chiusa);
        cedolino.Versione = 4;
        cedolino.ChiusoIl = DateTime.UtcNow;
        cedolino.ChiusoDa = Guid.NewGuid();

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("documento", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(StatoCedolino.Validata), await StatoPersistitoAsync(dati));
    }

    [Theory]
    [InlineData(nameof(StatoCedolino.Bozza), "snapshot", false)]
    [InlineData(nameof(StatoCedolino.Bozza), "documento", true)]
    [InlineData(nameof(StatoCedolino.PreWorkflow), "snapshot", true)]
    [InlineData(nameof(StatoCedolino.PreWorkflow), "documento", false)]
    public async Task EvidenzaAggiunta_SenzaTransizioneDellaTestataEVietata(
        string statoPersistito,
        string tipoEvidenza,
        bool caricaParentInvariato)
    {
        var dati = await InserisciBozzaConVoceAsync();
        if (statoPersistito != nameof(StatoCedolino.Bozza))
            await ImpostaStatoFuoriInterceptorAsync(dati, statoPersistito);

        await using (var db = NuovoContesto())
        {
            if (caricaParentInvariato)
            {
                // Anche avere la testata nel tracker non basta: deve essere realmente Modified
                // dalla transizione corretta all'interno dello stesso SaveChanges.
                _ = await db.Cedolini.SingleAsync(c =>
                    c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
            }

            var attoreId = Guid.NewGuid();
            var quando = DateTime.UtcNow;
            if (tipoEvidenza == "snapshot")
                db.CedoliniSnapshot.Add(NuovoSnapshot(
                    dati.TenantId, dati.CedolinoId, attoreId, quando));
            else
                db.CedoliniDocumenti.Add(NuovoDocumento(
                    dati.TenantId, dati.CedolinoId, attoreId, quando));

            var errore = await Assert.ThrowsAsync<InvalidOperationException>(
                () => db.SaveChangesAsync());

            Assert.Contains("transizione", errore.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("stesso SaveChanges", errore.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var verifica = NuovoContesto();
        Assert.Equal(0, await verifica.CedoliniSnapshot.CountAsync(s =>
            s.TenantId == dati.TenantId && s.CedolinoId == dati.CedolinoId));
        Assert.Equal(0, await verifica.CedoliniDocumenti.CountAsync(d =>
            d.TenantId == dati.TenantId && d.CedolinoId == dati.CedolinoId));
        Assert.Equal(statoPersistito, await verifica.Cedolini
            .Where(c => c.TenantId == dati.TenantId && c.Id == dati.CedolinoId)
            .Select(c => c.Stato)
            .SingleAsync());
    }

    [Fact]
    public async Task Transizione_ConVersioneSaltataEVietataAncheConSnapshot()
    {
        var dati = await InserisciBozzaConVoceAsync();
        await using var db = NuovoContesto();
        var cedolino = await db.Cedolini.SingleAsync(c =>
            c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
        var attoreId = Guid.NewGuid();
        var quando = DateTime.UtcNow;
        cedolino.Stato = nameof(StatoCedolino.Controllata);
        cedolino.Versione = 3;
        cedolino.ControllatoIl = quando;
        cedolino.ControllatoDa = attoreId;
        db.CedoliniSnapshot.Add(NuovoSnapshot(
            dati.TenantId, dati.CedolinoId, attoreId, quando));

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("versione", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uno", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(nameof(StatoCedolino.Bozza), await StatoPersistitoAsync(dati));
    }

    [Fact]
    public void NuovoCedolino_PreWorkflowENonBozzaSonoBloccati()
    {
        using var db = NuovoContesto();
        db.Cedolini.Add(NuovoCedolino(
            Guid.NewGuid(), Guid.NewGuid(), StatoCedolino.PreWorkflow));

        var errore = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        Assert.Contains("nascere", errore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bozza", errore.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(StatoCedolino.PreWorkflow), Modifica)]
    [InlineData(nameof(StatoCedolino.PreWorkflow), Cancellazione)]
    [InlineData(nameof(StatoCedolino.Chiusa), Modifica)]
    [InlineData(nameof(StatoCedolino.Chiusa), Cancellazione)]
    public async Task StatoOriginaleForgiato_NonSuperaLoStatoPersistito(
        string statoPersistito,
        string operazione)
    {
        var dati = await InserisciBozzaConVoceAsync();
        await ImpostaStatoFuoriInterceptorAsync(dati, statoPersistito);

        decimal nettoPrima;
        await using (var db = NuovoContesto())
        {
            var staccato = await db.Cedolini.AsNoTracking().SingleAsync(c =>
                c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
            nettoPrima = staccato.NettoInBusta;
            var entry = db.Attach(staccato);

            // Simula un writer detached ostile che prova a presentare come Bozza una riga storica.
            entry.Property(c => c.Stato).OriginalValue = nameof(StatoCedolino.Bozza);
            if (operazione == Modifica)
            {
                staccato.NettoInBusta += 123m;
                entry.Property(c => c.NettoInBusta).IsModified = true;
            }
            else
            {
                entry.State = EntityState.Deleted;
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using var verifica = NuovoContesto();
        var persistito = await verifica.Cedolini.AsNoTracking().SingleAsync(c =>
            c.TenantId == dati.TenantId && c.Id == dati.CedolinoId);
        Assert.Equal(statoPersistito, persistito.Stato);
        Assert.Equal(nettoPrima, persistito.NettoInBusta);
    }

    [Fact]
    public async Task Transizioni_EfConsentonoLaSequenzaEsatta()
    {
        var tenantId = Guid.NewGuid();
        var cedolinoId = Guid.NewGuid();
        var attoreId = Guid.NewGuid();
        await using var db = NuovoContesto();
        var cedolino = NuovoCedolino(tenantId, cedolinoId, StatoCedolino.Bozza);
        db.Cedolini.Add(cedolino);
        await db.SaveChangesAsync();

        cedolino.Stato = nameof(StatoCedolino.Controllata);
        cedolino.Versione = 2;
        cedolino.ControllatoIl = DateTime.UtcNow;
        cedolino.ControllatoDa = attoreId;
        db.CedoliniSnapshot.Add(NuovoSnapshot(
            tenantId, cedolinoId, attoreId, cedolino.ControllatoIl.Value));
        await db.SaveChangesAsync();

        cedolino.Stato = nameof(StatoCedolino.Validata);
        cedolino.Versione = 3;
        cedolino.ValidatoIl = DateTime.UtcNow;
        cedolino.ValidatoDa = attoreId;
        cedolino.DatiValidazioneProtetti = "fixture-cifrata";
        await db.SaveChangesAsync();

        cedolino.Stato = nameof(StatoCedolino.Chiusa);
        cedolino.Versione = 4;
        cedolino.ChiusoIl = DateTime.UtcNow;
        cedolino.ChiusoDa = attoreId;
        db.CedoliniDocumenti.Add(NuovoDocumento(
            tenantId, cedolinoId, attoreId, cedolino.ChiusoIl.Value));
        await db.SaveChangesAsync();

        Assert.Equal(nameof(StatoCedolino.Chiusa), cedolino.Stato);
        Assert.Equal(4, cedolino.Versione);
    }

    private async Task<DatiCedolino> InserisciBozzaConVoceAsync()
    {
        var dati = new DatiCedolino(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var db = NuovoContesto();
        var cedolino = NuovoCedolino(dati.TenantId, dati.CedolinoId, StatoCedolino.Bozza);
        cedolino.Voci.Add(NuovaVoce(
            dati.TenantId, dati.CedolinoId, dati.VoceId, ordine: 1));
        db.Cedolini.Add(cedolino);
        await db.SaveChangesAsync();
        return dati;
    }

    private async Task ImpostaStatoFuoriInterceptorAsync(DatiCedolino dati, string stato)
    {
        await using var db = NuovoContesto();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Cedolini
            SET Stato = {stato}
            WHERE TenantId = {dati.TenantId} AND Id = {dati.CedolinoId}
            """);
    }

    private async Task ImpostaWorkflowFuoriInterceptorAsync(
        DatiCedolino dati,
        StatoCedolino stato,
        int versione)
    {
        await using var db = NuovoContesto();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Cedolini
            SET Stato = {stato.ToString()}, Versione = {versione}
            WHERE TenantId = {dati.TenantId} AND Id = {dati.CedolinoId}
            """);
    }

    private async Task<string> StatoPersistitoAsync(DatiCedolino dati)
    {
        await using var db = NuovoContesto();
        return await db.Cedolini.AsNoTracking()
            .Where(c => c.TenantId == dati.TenantId && c.Id == dati.CedolinoId)
            .Select(c => c.Stato)
            .SingleAsync();
    }

    private static CedolinoSnapshotEntity NuovoSnapshot(
        Guid tenantId,
        Guid cedolinoId,
        Guid attoreId,
        DateTime quando) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CedolinoId = cedolinoId,
            VersioneSchema = 1,
            DatiProtetti = "fixture-snapshot-cifrata",
            HashDatiSha256 = "fixture-hash-snapshot",
            CreatoIl = quando,
            CreatoDa = attoreId,
        };

    private static CedolinoDocumentoEntity NuovoDocumento(
        Guid tenantId,
        Guid cedolinoId,
        Guid attoreId,
        DateTime quando) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CedolinoId = cedolinoId,
            PdfProtetto = "fixture-pdf-cifrato",
            HashPdfSha256 = "fixture-hash-pdf",
            HashFirmaSiglaTimbroSha256 = "fixture-hash-firma",
            CreatoIl = quando,
            CreatoDa = attoreId,
        };

    private static CedolinoEntity NuovoCedolino(
        Guid tenantId,
        Guid cedolinoId,
        StatoCedolino stato) =>
        new()
        {
            Id = cedolinoId,
            TenantId = tenantId,
            AziendaId = Guid.NewGuid(),
            RapportoLavoroId = Guid.NewGuid(),
            Anno = 2026,
            Mese = 8,
            Stato = stato.ToString(),
            Versione = 1,
            ParametriApplicatiProtetti = "fixture-cifrata",
            HashParametriSha256 = "fixture-hash",
            CalcolatoIl = DateTime.UtcNow,
        };

    private static CedolinoVoceEntity NuovaVoce(
        Guid tenantId,
        Guid cedolinoId,
        Guid voceId,
        int ordine) =>
        new()
        {
            Id = voceId,
            TenantId = tenantId,
            CedolinoId = cedolinoId,
            Ordine = ordine,
            Codice = "PAGA_BASE",
            Descrizione = "Paga base",
            Tipo = "Competenza",
            Quantita = 1m,
            Base = 1_000m,
            Importo = 1_000m,
        };

    private sealed record DatiCedolino(Guid TenantId, Guid CedolinoId, Guid VoceId);

    public void Dispose() => _connessione.Dispose();
}
