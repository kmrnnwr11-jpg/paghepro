using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.Contabilita;
using Paghe.Documenti;
using Paghe.Persistence;
using Paghe.Persistence.Entities;
using Xunit;

namespace Paghe.Tests;

/// <summary>
/// Verifica il fascicolo PDF del bilancio: dal bilancio d'esempio archiviato (BilancioJson)
/// si ricostruisce il dominio e si genera un PDF valido — lo stesso percorso
/// dell'endpoint GET /api/bilanci/{id}/pdf.
/// </summary>
public sealed class BilancioPdfTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public BilancioPdfTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NuovoContesto();
        db.Database.EnsureCreated();
    }

    private PagheDbContext NuovoContesto() =>
        new(new DbContextOptionsBuilder<PagheDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Genera_DaBilancioArchiviato_ProduceUnPdfValido()
    {
        var tenant = Guid.NewGuid();
        Guid aziendaId;
        await using (var db = NuovoContesto())
            aziendaId = (await new AziendeService(db).CreaAsync(tenant, new AziendaEntity
            {
                RagioneSociale = "Alfa Srls", PartitaIva = "01234561007",
                Indirizzo = "Via Roma 1", Cap = "20100", Comune = "Milano", Provincia = "MI",
            })).Id;

        await using var db2 = NuovoContesto();
        var entity = await new BilancioService(db2).CreaDaEsempioAsync(tenant, aziendaId, 2025);

        // Stesso percorso dell'endpoint: BilancioJson → dominio → PDF.
        var bilancio = JsonSerializer.Deserialize<Bilancio>(entity.BilancioJson)!;
        Assert.Equal(entity.UtilePerditaEsercizio, bilancio.UtilePerditaEsercizio);
        Assert.True(bilancio.Quadra);

        byte[] pdf = BilancioPdf.Genera(
            bilancio, entity.Anno, "Alfa Srls", "01234561007", entity.Stato,
            entity.Righe.Select(r => new RigaBilancioPdf(r.Codice, r.Descrizione, r.Voce, r.Importo)).ToList());

        Assert.True(pdf.Length > 2000, $"PDF sospettosamente piccolo: {pdf.Length} byte");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    public void Dispose() => _connection.Dispose();
}
