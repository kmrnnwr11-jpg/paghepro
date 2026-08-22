using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;

namespace Paghe.Persistence;

/// <summary>
/// Contatore dei consumi AI per tenant/mese, per il modello "AI inclusa nel canone":
/// la chiave del provider è centrale, il cliente non configura nulla, e ogni studio ha
/// un limite mensile di letture AI. Superato il limite il software continua a funzionare
/// con la sola estrazione euristica (l'AI è un acceleratore, mai una dipendenza).
/// </summary>
public sealed class ConsumoAiService
{
    private readonly PagheDbContext _db;

    public ConsumoAiService(PagheDbContext db) => _db = db;

    /// <summary>
    /// Tenta di consumare una chiamata AI per il tenant nel mese corrente.
    /// True = sotto il limite, chiamata registrata; false = limite raggiunto, non chiamare l'AI.
    /// Limite &lt;= 0 = illimitato (viene comunque conteggiato).
    /// </summary>
    public async Task<bool> ProvaConsumaAsync(Guid tenantId, int limiteMensile, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant non valido per il consumo AI.", nameof(tenantId));
        var ambient = AmbientContestoAudit.Corrente;
        if (ambient.TenantId is { } tenantAutenticato && tenantAutenticato != tenantId)
            throw new InvalidOperationException(
                "Operazione bloccata: il tenant del consumo AI non coincide con quello autenticato.");

        var oggi = DateTime.UtcNow;
        var idProposto = Guid.NewGuid();
        var provider = _db.Database.ProviderName;
        if (provider is not "Microsoft.EntityFrameworkCore.Sqlite"
            and not "Npgsql.EntityFrameworkCore.PostgreSQL")
            throw new NotSupportedException(
                $"Contatore AI atomico non disponibile per il provider '{provider}'.");

        IDbContextTransaction? transazionePropria = null;
        if (_db.Database.CurrentTransaction is null)
            transazionePropria = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);

        try
        {
            await using var comando = _db.Database.GetDbConnection().CreateCommand();
            comando.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            comando.CommandText = """
                INSERT INTO "ConsumiAi"
                    ("Id", "TenantId", "Anno", "Mese", "Chiamate", "AggiornatoIl")
                VALUES
                    (@id, @tenantId, @anno, @mese, 1, @aggiornatoIl)
                ON CONFLICT ("TenantId", "Anno", "Mese") DO UPDATE SET
                    "Chiamate" = "ConsumiAi"."Chiamate" + 1,
                    "AggiornatoIl" = excluded."AggiornatoIl"
                WHERE @limiteMensile <= 0
                   OR "ConsumiAi"."Chiamate" < @limiteMensile
                RETURNING "Id", "Chiamate";
                """;
            AggiungiParametro(comando, "@id", idProposto);
            AggiungiParametro(comando, "@tenantId", tenantId);
            AggiungiParametro(comando, "@anno", oggi.Year);
            AggiungiParametro(comando, "@mese", oggi.Month);
            AggiungiParametro(comando, "@aggiornatoIl", oggi);
            AggiungiParametro(comando, "@limiteMensile", limiteMensile);

            await using var lettore = await comando.ExecuteReaderAsync(ct);
            if (!await lettore.ReadAsync(ct))
            {
                await lettore.DisposeAsync();
                if (transazionePropria is not null)
                    await transazionePropria.CommitAsync(ct);
                return false;
            }

            var idPersistito = LeggiGuid(lettore.GetValue(0));
            await lettore.DisposeAsync();

            // Il precedente SaveChanges generava una riga di audit per ogni consumo. La
            // manteniamo esplicitamente e nella stessa transazione dell'UPSERT atomico. Una
            // categoria dedicata evita che l'interceptor la confonda con una propria riga
            // residua di un retry fallito.
            _db.AuditLog.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UtenteId = ambient.UtenteId,
                Categoria = "UsoAi",
                Azione = idPersistito == idProposto ? "Inserimento" : "Modifica",
                Entita = "ConsumiAi",
                ChiaveEntita = idPersistito.ToString(),
                CampiModificati = idPersistito == idProposto ? null : "AggiornatoIl,Chiamate",
                Esito = "Riuscito",
                CorrelationId = ambient.CorrelationId,
                QuandoUtc = oggi,
            });
            await _db.SaveChangesAsync(ct);

            if (transazionePropria is not null)
                await transazionePropria.CommitAsync(ct);
            return true;
        }
        catch
        {
            if (transazionePropria is not null)
                await transazionePropria.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (transazionePropria is not null)
                await transazionePropria.DisposeAsync();
        }
    }

    /// <summary>Chiamate AI del tenant nel mese corrente (0 se nessuna).</summary>
    public async Task<int> ConsumoDelMeseAsync(Guid tenantId, CancellationToken ct = default)
    {
        var oggi = DateTime.UtcNow;
        var riga = await _db.ConsumiAi.AsNoTracking().FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Anno == oggi.Year && c.Mese == oggi.Month, ct);
        return riga?.Chiamate ?? 0;
    }

    private static void AggiungiParametro(DbCommand comando, string nome, object valore)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valore;
        comando.Parameters.Add(parametro);
    }

    private static Guid LeggiGuid(object valore) => valore switch
    {
        Guid id => id,
        string testo when Guid.TryParse(testo, out var id) => id,
        byte[] bytes when bytes.Length == 16 => new Guid(bytes),
        _ => throw new InvalidOperationException("Identificatore consumo AI non valido nel database."),
    };
}
