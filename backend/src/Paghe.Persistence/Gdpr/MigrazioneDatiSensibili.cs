using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Paghe.Persistence.Gdpr;

/// <summary>
/// Runner esplicito e idempotente per il cutover GDPR. Aggiunge/backfilla le colonne protette già
/// presenti nel modello, con batch tenant-scoped, verifica tutti gli envelope e solo alla fine
/// elimina le colonne legacy in chiaro. Non va eseguito automaticamente da ogni replica.
/// </summary>
public sealed class MigrazioneDatiSensibili
{
    private const int DimensioneBatch = 200;
    private const string ValoreSentinella = "PAGHE-DATA-KEY-OK";
    private const string ContestoSentinella = "paghe-gdpr:v1|sentinella-chiave-dati";
    private const long LockPostgres = 0x5041474845474450; // "PAGHEGDP"

    private static readonly ColonnaLegacy[] ColonneLegacy =
    [
        new("Aziende", "Iban"),
        new("Dipendenti", "Iban"),
        new("AvvisiPagamento", "Iban"),
        new("Cartellini", "GiorniMalattia"),
        new("Cartellini", "NumeroEventoMalattiaAnno"),
        new("Cartellini", "GiorniInfortunio"),
        new("CedolinoVoceEntity", "Codice"),
        new("CedolinoVoceEntity", "Descrizione"),
        new("CedolinoVoceEntity", "Tipo"),
        new("CedolinoVoceEntity", "Quantita"),
        new("CedolinoVoceEntity", "Base"),
        new("CedolinoVoceEntity", "Importo"),
    ];

    private readonly PagheDbContext _db;
    private readonly ProtettoreDati _protettore;

    public MigrazioneDatiSensibili(PagheDbContext db, ProtettoreDati protettore)
    {
        _db = db;
        _protettore = protettore;
    }

    /// <summary>
    /// Esegue il cutover completo. In produzione va lanciato come comando one-shot, con traffico
    /// applicativo fermo e backup cifrato/restore verificato.
    /// </summary>
    public async Task<EsitoMigrazioneDatiSensibili> EseguiTuttiAsync(
        CancellationToken cancellationToken = default)
    {
        await VerificaProviderAsync(cancellationToken);
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        var lockPreso = await PrendiLockPostgresAsync(connessione, cancellationToken);

        try
        {
            // Il cutover e il DROP sono una sola unità atomica. Il lock esclusivo impedisce a una
            // vecchia replica di scrivere colonne legacy tra l'ultimo batch e la loro rimozione.
            await using var transazioneCutover = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await AcquisisciFencingScrittoriAsync(cancellationToken);
            await VerificaSentinellaPreMigrazioneAsync(cancellationToken);
            await VerificaColonneNuoveAsync(cancellationToken);
            await AllineaTenantVociLegacyAsync(cancellationToken);

            var tenantIds = await LeggiTenantIdsAsync(cancellationToken);
            var valoriMigrati = 0;
            foreach (var tenantId in tenantIds)
                valoriMigrati += await EseguiPerTenantInternoAsync(tenantId, cancellationToken);

            await VerificaEnvelopeCorrentiAsync(cancellationToken);
            await EliminaColonneLegacyAsync(cancellationToken);
            // La sentinella viene creata/ruotata per ultima. Un fallimento precedente lascia
            // invariata K1 e l'intera transazione (inclusi i DROP) viene annullata.
            await ScriviSentinellaCorrenteAsync(cancellationToken);
            await VerificaCutoverCompletatoAsync(cancellationToken);
            await transazioneCutover.CommitAsync(cancellationToken);

            return new EsitoMigrazioneDatiSensibili(tenantIds.Count, valoriMigrati);
        }
        finally
        {
            if (lockPreso)
                await RilasciaLockPostgresAsync(connessione, cancellationToken);
        }
    }

    /// <summary>
    /// Migra un solo tenant senza eliminare le colonne legacy. Serve per ripresa/diagnostica e
    /// rende verificabile che ogni SELECT/UPDATE di dati applicativi mantenga il filtro TenantId.
    /// </summary>
    public async Task<int> EseguiPerTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Il tenant della migrazione non può essere vuoto.", nameof(tenantId));
        await VerificaProviderAsync(cancellationToken);
        await VerificaColonneNuoveAsync(cancellationToken);
        await AllineaTenantVociLegacyAsync(cancellationToken);
        return await EseguiPerTenantInternoAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// Gate di startup in sola lettura: schema strict, nessuna colonna legacy, envelope con kid
    /// corrente e sentinella decifrabile. Ferma l'app prima del traffico se la chiave è errata.
    /// </summary>
    public async Task VerificaCutoverCompletatoAsync(
        CancellationToken cancellationToken = default)
    {
        await VerificaProviderAsync(cancellationToken);
        await VerificaColonneNuoveAsync(cancellationToken);

        foreach (var legacy in ColonneLegacy)
            if (await ColonnaEsisteAsync(legacy.Tabella, legacy.Colonna, cancellationToken))
                throw new InvalidOperationException(
                    $"Migrazione GDPR richiesta: esiste ancora {legacy.Tabella}.{legacy.Colonna}. " +
                    "Eseguire il comando --migra-dati-sensibili prima di avviare il servizio.");

        await VerificaTenantVociAsync(cancellationToken);
        await VerificaSentinellaCorrenteAsync(cancellationToken);
        await VerificaEnvelopeCorrentiAsync(cancellationToken);
    }

    /// <summary>
    /// Bootstrap limitato a un database senza dati sensibili e senza sentinella. Permette il
    /// primo avvio locale/cloud; non modifica mai un database popolato o già inizializzato.
    /// </summary>
    public async Task<bool> InizializzaSeDatabaseVuotoAsync(
        CancellationToken cancellationToken = default)
    {
        await VerificaProviderAsync(cancellationToken);
        await VerificaColonneNuoveAsync(cancellationToken);
        if (await LeggiSentinellaAsync(consentiAssente: true, cancellationToken) is not null)
            return false;
        if (!await DatabaseApplicativoVuotoAsync(cancellationToken))
            return false;

        await EseguiTuttiAsync(cancellationToken);
        return true;
    }

    private async Task<int> EseguiPerTenantInternoAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var migrati = 0;
        migrati += await MigraIbanAsync(
            "Aziende", "IbanProtetto", "Iban", tenantId,
            ContestoDatiProtetti.IbanAzienda, cancellationToken);
        migrati += await MigraIbanAsync(
            "Dipendenti", "IbanProtetto", "Iban", tenantId,
            ContestoDatiProtetti.IbanDipendente, cancellationToken);
        migrati += await MigraIbanAsync(
            "AvvisiPagamento", "IbanProtetto", "Iban", tenantId,
            ContestoDatiProtetti.IbanAvviso, cancellationToken);
        migrati += await MigraSaluteAsync(tenantId, cancellationToken);
        migrati += await MigraVociCedolinoAsync(tenantId, cancellationToken);
        migrati += await MigraConContestoAsync(
            "Aziende", "FirmaSiglaTimbroProtetta", tenantId,
            ContestoDatiProtetti.FirmaSiglaTimbroAzienda, cancellationToken);
        migrati += await MigraConContestoAsync(
            "Cedolini", "ParametriApplicatiProtetti", tenantId,
            ContestoDatiProtetti.ParametriCedolino, cancellationToken);
        migrati += await MigraConContestoAsync(
            "Cedolini", "DatiValidazioneProtetti", tenantId,
            ContestoDatiProtetti.ValidazioneCedolino, cancellationToken);
        migrati += await MigraSnapshotCedolinoAsync(
            "CedoliniSnapshot", "DatiProtetti", tenantId,
            ContestoDatiProtetti.DatiSnapshotCedolino, cancellationToken);
        migrati += await MigraSnapshotCedolinoAsync(
            "CedoliniDocumenti", "PdfProtetto", tenantId,
            ContestoDatiProtetti.PdfSnapshotCedolino, cancellationToken);
        migrati += await MigraInPlaceAsync("Utenti", "SegretoTotp", tenantId, cancellationToken);
        migrati += await MigraInPlaceAsync("ConfigurazioniSdi", "ApiKey", tenantId, cancellationToken);
        return migrati;
    }

    private async Task<int> MigraConContestoAsync(
        string tabella,
        string colonna,
        Guid tenantId,
        Func<Guid, Guid, string> creaContesto,
        CancellationToken cancellationToken)
    {
        var migrati = 0;
        await PerOgniBatchTenantAsync(tabella, [colonna], tenantId, async (righe, tx) =>
        {
            foreach (var riga in righe)
            {
                var corrente = Stringa(riga.Valori[0]);
                if (string.IsNullOrWhiteSpace(corrente)) continue;
                var nuovo = _protettore.RicifraConContesto(
                    corrente, creaContesto(tenantId, riga.Id))!;
                if (string.Equals(corrente, nuovo, StringComparison.Ordinal)) continue;
                await AggiornaCampoAsync(
                    tabella, colonna, riga.Id, tenantId, nuovo, tx, cancellationToken);
                migrati++;
            }
        }, cancellationToken);
        return migrati;
    }

    private async Task<int> MigraSnapshotCedolinoAsync(
        string tabella,
        string colonna,
        Guid tenantId,
        Func<Guid, Guid, Guid, string> creaContesto,
        CancellationToken cancellationToken)
    {
        var migrati = 0;
        await PerOgniBatchTenantAsync(tabella, [colonna, "CedolinoId"], tenantId, async (righe, tx) =>
        {
            foreach (var riga in righe)
            {
                var corrente = Stringa(riga.Valori[0]);
                if (string.IsNullOrWhiteSpace(corrente)) continue;
                var cedolinoId = GuidDaDb(riga.Valori[1]
                    ?? throw new InvalidOperationException($"{tabella} {riga.Id} senza CedolinoId."));
                var nuovo = _protettore.RicifraConContesto(
                    corrente, creaContesto(tenantId, riga.Id, cedolinoId))!;
                if (string.Equals(corrente, nuovo, StringComparison.Ordinal)) continue;
                await AggiornaCampoAsync(
                    tabella, colonna, riga.Id, tenantId, nuovo, tx, cancellationToken);
                migrati++;
            }
        }, cancellationToken);
        return migrati;
    }

    private async Task<int> MigraIbanAsync(
        string tabella,
        string colonnaNuova,
        string colonnaLegacy,
        Guid tenantId,
        Func<Guid, Guid, string> creaContesto,
        CancellationToken cancellationToken)
    {
        var haLegacy = await ColonnaEsisteAsync(tabella, colonnaLegacy, cancellationToken);
        var colonne = haLegacy ? new[] { colonnaNuova, colonnaLegacy } : new[] { colonnaNuova };
        var migrati = 0;

        await PerOgniBatchTenantAsync(tabella, colonne, tenantId, async (righe, tx) =>
        {
            foreach (var riga in righe)
            {
                var corrente = Stringa(riga.Valori[0]);
                string? nuovo;
                if (!string.IsNullOrWhiteSpace(corrente))
                {
                    nuovo = _protettore.RicifraConContesto(
                        corrente,
                        creaContesto(tenantId, riga.Id));
                }
                else if (haLegacy && !string.IsNullOrWhiteSpace(Stringa(riga.Valori[1])))
                {
                    var chiaro = _protettore.Svela(Stringa(riga.Valori[1]))!;
                    nuovo = _protettore.ProteggiConContesto(
                        chiaro,
                        creaContesto(tenantId, riga.Id));
                }
                else
                {
                    nuovo = null;
                }

                if (!string.Equals(corrente, nuovo, StringComparison.Ordinal))
                {
                    await AggiornaCampoAsync(
                        tabella, colonnaNuova, riga.Id, tenantId, nuovo, tx, cancellationToken);
                    migrati++;
                }
            }
        }, cancellationToken);

        return migrati;
    }

    private async Task<int> MigraSaluteAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string tabella = "Cartellini";
        var legacyPresenti = new List<string>();
        foreach (var colonna in new[]
        {
            "GiorniMalattia", "NumeroEventoMalattiaAnno", "GiorniInfortunio",
        })
            if (await ColonnaEsisteAsync(tabella, colonna, cancellationToken))
                legacyPresenti.Add(colonna);
        var haLegacy = legacyPresenti.Count > 0;
        // Il calendario giornaliero è protetto quanto la terna e va ruotato con lei: in coda,
        // così gli indici delle colonne legacy qui sotto restano quelli.
        var colonne = new[] { "DatiSaluteProtetti" }
            .Concat(legacyPresenti)
            .Append("GiornateProtette")
            .ToArray();
        var indiceGiornate = colonne.Length - 1;
        var migrati = 0;

        await PerOgniBatchTenantAsync(tabella, colonne, tenantId, async (righe, tx) =>
        {
            foreach (var riga in righe)
            {
                var corrente = Stringa(riga.Valori[0]);
                string nuovo;
                if (!string.IsNullOrWhiteSpace(corrente))
                {
                    nuovo = _protettore.RicifraConContesto(
                        corrente,
                        ContestoDatiProtetti.SaluteCartellino(tenantId, riga.Id))!;
                    _ = PayloadDatiProtetti.DeserializzaSalute(
                        _protettore.SvelaConContesto(
                            nuovo,
                            ContestoDatiProtetti.SaluteCartellino(tenantId, riga.Id))!);
                }
                else
                {
                    if (!haLegacy)
                        throw new InvalidOperationException(
                            $"Cartellino {riga.Id} del tenant {tenantId} privo di payload e colonne legacy.");
                    var valoriLegacy = legacyPresenti
                        .Select((nome, indice) => (nome, valore: riga.Valori[indice + 1]))
                        .ToDictionary(x => x.nome, x => x.valore, StringComparer.Ordinal);
                    var malattia = valoriLegacy.TryGetValue("GiorniMalattia", out var rawMalattia)
                        ? DecimaleStorico(rawMalattia, "GiorniMalattia")
                        : 0m;
                    var evento = valoriLegacy.TryGetValue("NumeroEventoMalattiaAnno", out var rawEvento)
                        ? InteroStorico(rawEvento, "NumeroEventoMalattiaAnno")
                        : 1;
                    var infortunio = valoriLegacy.TryGetValue("GiorniInfortunio", out var rawInfortunio)
                        ? DecimaleStorico(rawInfortunio, "GiorniInfortunio")
                        : 0m;
                    nuovo = _protettore.ProteggiConContesto(
                        PayloadDatiProtetti.SerializzaSalute(malattia, evento, infortunio),
                        ContestoDatiProtetti.SaluteCartellino(tenantId, riga.Id))!;
                }

                if (!string.Equals(corrente, nuovo, StringComparison.Ordinal))
                {
                    await AggiornaCampoAsync(
                        tabella, "DatiSaluteProtetti", riga.Id, tenantId, nuovo, tx, cancellationToken);
                    migrati++;
                }

                // Calendario: esiste solo sui cartellini compilati giorno per giorno, quindi
                // vuoto è legittimo e si salta. Quando c'è, si ricifra con la chiave corrente.
                var calendario = Stringa(riga.Valori[indiceGiornate]);
                if (string.IsNullOrWhiteSpace(calendario)) continue;

                var calendarioNuovo = _protettore.RicifraConContesto(
                    calendario,
                    ContestoDatiProtetti.GiornateCartellino(tenantId, riga.Id))!;
                if (string.Equals(calendario, calendarioNuovo, StringComparison.Ordinal)) continue;

                await AggiornaCampoAsync(
                    tabella, "GiornateProtette", riga.Id, tenantId, calendarioNuovo, tx, cancellationToken);
                migrati++;
            }
        }, cancellationToken);

        return migrati;
    }

    private async Task<int> MigraVociCedolinoAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string tabella = "CedolinoVoceEntity";
        var haLegacy = await ColonnaEsisteAsync(tabella, "Codice", cancellationToken);
        var colonne = haLegacy
            ? new[] { "DatiVoceProtetti", "Codice", "Descrizione", "Tipo", "Quantita", "Base", "Importo" }
            : new[] { "DatiVoceProtetti" };
        var migrati = 0;

        await PerOgniBatchTenantAsync(tabella, colonne, tenantId, async (righe, tx) =>
        {
            foreach (var riga in righe)
            {
                var corrente = Stringa(riga.Valori[0]);
                string nuovo;
                if (!string.IsNullOrWhiteSpace(corrente))
                {
                    nuovo = _protettore.RicifraConContesto(
                        corrente,
                        ContestoDatiProtetti.VoceCedolino(tenantId, riga.Id))!;
                    _ = PayloadDatiProtetti.DeserializzaVoce(
                        _protettore.SvelaConContesto(
                            nuovo,
                            ContestoDatiProtetti.VoceCedolino(tenantId, riga.Id))!);
                }
                else
                {
                    if (!haLegacy)
                        throw new InvalidOperationException(
                            $"Voce cedolino {riga.Id} del tenant {tenantId} priva di payload e colonne legacy.");
                    nuovo = _protettore.ProteggiConContesto(
                        PayloadDatiProtetti.SerializzaVoce(
                            Stringa(riga.Valori[1]) ?? "",
                            Stringa(riga.Valori[2]) ?? "",
                            Stringa(riga.Valori[3]) ?? "",
                            DecimaleNullableStorico(riga.Valori[4], "Quantita"),
                            DecimaleNullableStorico(riga.Valori[5], "Base"),
                            DecimaleStorico(riga.Valori[6], "Importo")),
                        ContestoDatiProtetti.VoceCedolino(tenantId, riga.Id))!;
                }

                if (!string.Equals(corrente, nuovo, StringComparison.Ordinal))
                {
                    await AggiornaCampoAsync(
                        tabella, "DatiVoceProtetti", riga.Id, tenantId, nuovo, tx, cancellationToken);
                    migrati++;
                }
            }
        }, cancellationToken);

        return migrati;
    }

    private async Task<int> MigraInPlaceAsync(
        string tabella,
        string colonna,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var migrati = 0;
        await PerOgniBatchTenantAsync(tabella, [colonna], tenantId, async (righe, tx) =>
        {
            foreach (var riga in righe)
            {
                var corrente = Stringa(riga.Valori[0]);
                if (string.IsNullOrWhiteSpace(corrente)) continue;
                var nuovo = _protettore.Ricifra(corrente)!;
                if (string.Equals(corrente, nuovo, StringComparison.Ordinal)) continue;
                await AggiornaCampoAsync(tabella, colonna, riga.Id, tenantId, nuovo, tx, cancellationToken);
                migrati++;
            }
        }, cancellationToken);
        return migrati;
    }

    private async Task PerOgniBatchTenantAsync(
        string tabella,
        IReadOnlyList<string> colonne,
        Guid tenantId,
        Func<IReadOnlyList<RigaGrezza>, DbTransaction, Task> elabora,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is { } transazioneCutover)
        {
            await ElaboraBatchNellaTransazioneAsync(
                tabella,
                colonne,
                tenantId,
                transazioneCutover.GetDbTransaction(),
                elabora,
                cancellationToken);
            return;
        }

        // La variante per singolo tenant non esegue il cutover/DROP e può quindi mantenere
        // commit piccoli. Il runner completo entra sempre nel ramo atomico qui sopra.
        var ultimoId = Guid.Empty;
        while (true)
        {
            await using var txEf = await _db.Database.BeginTransactionAsync(cancellationToken);
            var tx = txEf.GetDbTransaction();
            var righe = await LeggiBatchAsync(
                tabella, colonne, tenantId, ultimoId, tx, cancellationToken);
            if (righe.Count == 0)
            {
                await txEf.RollbackAsync(cancellationToken);
                break;
            }

            await elabora(righe, tx);
            await txEf.CommitAsync(cancellationToken);
            ultimoId = righe[^1].Id;
        }
    }

    private async Task ElaboraBatchNellaTransazioneAsync(
        string tabella,
        IReadOnlyList<string> colonne,
        Guid tenantId,
        DbTransaction transazione,
        Func<IReadOnlyList<RigaGrezza>, DbTransaction, Task> elabora,
        CancellationToken cancellationToken)
    {
        var ultimoId = Guid.Empty;
        while (true)
        {
            var righe = await LeggiBatchAsync(
                tabella, colonne, tenantId, ultimoId, transazione, cancellationToken);
            if (righe.Count == 0) break;

            await elabora(righe, transazione);
            ultimoId = righe[^1].Id;
        }
    }

    private async Task<List<RigaGrezza>> LeggiBatchAsync(
        string tabella,
        IReadOnlyList<string> colonne,
        Guid tenantId,
        Guid ultimoId,
        DbTransaction transazione,
        CancellationToken cancellationToken)
    {
        var connessione = _db.Database.GetDbConnection();
        await using var cmd = connessione.CreateCommand();
        cmd.Transaction = transazione;
        cmd.CommandText =
            $"SELECT \"Id\", {string.Join(", ", colonne.Select(c => $"\"{c}\""))} " +
            $"FROM \"{tabella}\" WHERE \"TenantId\" = @tenant AND \"Id\" > @ultimo " +
            "ORDER BY \"Id\" LIMIT @limite;";
        AggiungiParametro(cmd, "@tenant", tenantId);
        AggiungiParametro(cmd, "@ultimo", ultimoId);
        AggiungiParametro(cmd, "@limite", DimensioneBatch);

        var righe = new List<RigaGrezza>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = GuidDaDb(reader.GetValue(0));
            var valori = new object?[colonne.Count];
            for (var i = 0; i < colonne.Count; i++)
                valori[i] = reader.IsDBNull(i + 1) ? null : reader.GetValue(i + 1);
            righe.Add(new RigaGrezza(id, valori));
        }
        return righe;
    }

    private async Task AggiornaCampoAsync(
        string tabella,
        string colonna,
        Guid id,
        Guid tenantId,
        object? valore,
        DbTransaction transazione,
        CancellationToken cancellationToken)
    {
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transazione;
        cmd.CommandText =
            $"UPDATE \"{tabella}\" SET \"{colonna}\" = @valore " +
            "WHERE \"Id\" = @id AND \"TenantId\" = @tenant;";
        AggiungiParametro(cmd, "@valore", valore ?? DBNull.Value);
        AggiungiParametro(cmd, "@id", id);
        AggiungiParametro(cmd, "@tenant", tenantId);
        var aggiornate = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (aggiornate != 1)
            throw new InvalidOperationException(
                $"Backfill non deterministico su {tabella}.{colonna}: aggiornate {aggiornate} righe.");
    }

    private async Task AllineaTenantVociLegacyAsync(CancellationToken cancellationToken)
    {
        if (!await ColonnaEsisteAsync("CedolinoVoceEntity", "TenantId", cancellationToken))
            throw new InvalidOperationException("Manca CedolinoVoceEntity.TenantId: eseguire l'allineamento schema.");

        var sql = _db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? """
              UPDATE "CedolinoVoceEntity" AS v
              SET "TenantId" = c."TenantId"
              FROM "Cedolini" AS c
              WHERE v."CedolinoId" = c."Id"
                AND v."TenantId" = '00000000-0000-0000-0000-000000000000';
              """
            : """
              UPDATE "CedolinoVoceEntity"
              SET "TenantId" = (
                  SELECT c."TenantId" FROM "Cedolini" AS c
                  WHERE c."Id" = "CedolinoVoceEntity"."CedolinoId")
              WHERE "TenantId" = '00000000-0000-0000-0000-000000000000';
              """;
        await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        var senzaTenant = await ContaAsync(
            "SELECT COUNT(*) FROM \"CedolinoVoceEntity\" " +
            "WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';",
            cancellationToken);
        if (senzaTenant > 0)
            throw new InvalidOperationException(
                $"{senzaTenant} voci cedolino non sono associabili a un tenant.");

        await VerificaTenantVociAsync(cancellationToken);
    }

    private async Task VerificaTenantVociAsync(CancellationToken cancellationToken)
    {
        var orfane = await ContaAsync(
            "SELECT COUNT(*) FROM \"CedolinoVoceEntity\" AS v " +
            "LEFT JOIN \"Cedolini\" AS c ON c.\"Id\" = v.\"CedolinoId\" " +
            "WHERE c.\"Id\" IS NULL;",
            cancellationToken);
        if (orfane > 0)
            throw new InvalidOperationException(
                $"{orfane} voci cedolino sono orfane: il cutover GDPR è stato annullato.");

        var tenantDiscordanti = await ContaAsync(
            "SELECT COUNT(*) FROM \"CedolinoVoceEntity\" AS v " +
            "INNER JOIN \"Cedolini\" AS c ON c.\"Id\" = v.\"CedolinoId\" " +
            "WHERE v.\"TenantId\" <> c.\"TenantId\";",
            cancellationToken);
        if (tenantDiscordanti > 0)
            throw new InvalidOperationException(
                $"{tenantDiscordanti} voci cedolino appartengono a un tenant diverso dalla testata: " +
                "il cutover GDPR è stato annullato.");
    }

    private async Task AcquisisciFencingScrittoriAsync(CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            // ACCESS EXCLUSIVE resta attivo fino al COMMIT e blocca anche applicazioni legacy che
            // non cooperano con l'advisory lock del runner.
            await EseguiNonQueryAsync(
                "LOCK TABLE \"Tenants\", \"Aziende\", \"Dipendenti\", \"AvvisiPagamento\", " +
                "\"Cartellini\", \"Cedolini\", \"CedolinoVoceEntity\", \"Utenti\", " +
                "\"ConfigurazioniSdi\", \"CedoliniSnapshot\", \"CedoliniDocumenti\" " +
                "IN ACCESS EXCLUSIVE MODE;",
                cancellationToken);
            return;
        }

        // In SQLite una scrittura innocua forza subito il lock di scrittura della transazione,
        // prima di leggere/backfillare le colonne legacy. Nessun altro writer può superare il DROP.
        await EseguiNonQueryAsync(
            "UPDATE \"Tenants\" SET \"Id\" = \"Id\" WHERE 0 = 1;",
            cancellationToken);
    }

    private async Task<List<Guid>> LeggiTenantIdsAsync(CancellationToken cancellationToken)
    {
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var cmd = connessione.CreateCommand();
        CollegaTransazioneCorrente(cmd);
        cmd.CommandText =
            """
            SELECT "Id" AS "TenantId" FROM "Tenants"
            UNION SELECT "TenantId" FROM "Aziende"
            UNION SELECT "TenantId" FROM "Dipendenti"
            UNION SELECT "TenantId" FROM "AvvisiPagamento"
            UNION SELECT "TenantId" FROM "Utenti"
            UNION SELECT "TenantId" FROM "ConfigurazioniSdi"
            UNION SELECT "TenantId" FROM "Cartellini"
            UNION SELECT "TenantId" FROM "Cedolini"
            UNION SELECT "TenantId" FROM "CedolinoVoceEntity"
            UNION SELECT "TenantId" FROM "CedoliniSnapshot"
            UNION SELECT "TenantId" FROM "CedoliniDocumenti"
            """;
        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0)) continue;
            var id = GuidDaDb(reader.GetValue(0));
            if (id != Guid.Empty) ids.Add(id);
        }
        ids.Sort();
        return ids;
    }

    private async Task VerificaColonneNuoveAsync(CancellationToken cancellationToken)
    {
        foreach (var richiesta in new[]
        {
            new ColonnaLegacy("Aziende", "IbanProtetto"),
            new ColonnaLegacy("Dipendenti", "IbanProtetto"),
            new ColonnaLegacy("AvvisiPagamento", "IbanProtetto"),
            new ColonnaLegacy("Cartellini", "DatiSaluteProtetti"),
            new ColonnaLegacy("CedolinoVoceEntity", "TenantId"),
            new ColonnaLegacy("CedolinoVoceEntity", "DatiVoceProtetti"),
            new ColonnaLegacy("Aziende", "FirmaSiglaTimbroProtetta"),
            new ColonnaLegacy("Cedolini", "ParametriApplicatiProtetti"),
            new ColonnaLegacy("Cedolini", "DatiValidazioneProtetti"),
            new ColonnaLegacy("CedoliniSnapshot", "DatiProtetti"),
            new ColonnaLegacy("CedoliniDocumenti", "PdfProtetto"),
        })
            if (!await ColonnaEsisteAsync(richiesta.Tabella, richiesta.Colonna, cancellationToken))
                throw new InvalidOperationException(
                    $"Schema GDPR incompleto: manca {richiesta.Tabella}.{richiesta.Colonna}.");
    }

    private async Task VerificaEnvelopeCorrentiAsync(CancellationToken cancellationToken)
    {
        foreach (var campo in new[]
        {
            new CampoCorrente("Aziende", "IbanProtetto", false),
            new CampoCorrente("Dipendenti", "IbanProtetto", false),
            new CampoCorrente("AvvisiPagamento", "IbanProtetto", false),
            new CampoCorrente("Cartellini", "DatiSaluteProtetti", true),
            // Facoltativo: i cartellini compilati a totali non hanno calendario.
            new CampoCorrente("Cartellini", "GiornateProtette", false),
            new CampoCorrente("CedolinoVoceEntity", "DatiVoceProtetti", true),
            new CampoCorrente("Aziende", "FirmaSiglaTimbroProtetta", false),
            // I cedolini storici antecedenti al workflow possono non avere i nuovi payload.
            new CampoCorrente("Cedolini", "ParametriApplicatiProtetti", false),
            new CampoCorrente("Cedolini", "DatiValidazioneProtetti", false),
            new CampoCorrente("CedoliniSnapshot", "DatiProtetti", true),
            new CampoCorrente("CedoliniDocumenti", "PdfProtetto", true),
            new CampoCorrente("Utenti", "SegretoTotp", false),
            new CampoCorrente("ConfigurazioniSdi", "ApiKey", false),
        })
        {
            if (campo.Obbligatorio)
            {
                var mancanti = await ContaAsync(
                    $"SELECT COUNT(*) FROM \"{campo.Tabella}\" " +
                    $"WHERE \"{campo.Colonna}\" IS NULL OR \"{campo.Colonna}\" = '';",
                    cancellationToken);
                if (mancanti > 0)
                    throw new InvalidOperationException(
                        $"{mancanti} righe senza payload in {campo.Tabella}.{campo.Colonna}.");
            }

            var nonCorrenti = await ContaConPrefissoAsync(campo, cancellationToken);
            if (nonCorrenti > 0)
                throw new InvalidOperationException(
                    $"{nonCorrenti} envelope non allineati in {campo.Tabella}.{campo.Colonna}.");
        }
    }

    private async Task<long> ContaConPrefissoAsync(
        CampoCorrente campo,
        CancellationToken cancellationToken)
    {
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var cmd = connessione.CreateCommand();
        CollegaTransazioneCorrente(cmd);
        cmd.CommandText =
            $"SELECT COUNT(*) FROM \"{campo.Tabella}\" " +
            $"WHERE \"{campo.Colonna}\" IS NOT NULL AND \"{campo.Colonna}\" <> '' " +
            $"AND \"{campo.Colonna}\" NOT LIKE @prefisso;";
        AggiungiParametro(cmd, "@prefisso", _protettore.PrefissoChiaveCorrente + "%");
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private async Task EliminaColonneLegacyAsync(CancellationToken cancellationToken)
    {
        foreach (var legacy in ColonneLegacy)
        {
            if (!await ColonnaEsisteAsync(legacy.Tabella, legacy.Colonna, cancellationToken)) continue;
            await EseguiNonQueryAsync(
                $"ALTER TABLE \"{legacy.Tabella}\" DROP COLUMN \"{legacy.Colonna}\";",
                cancellationToken);
        }
    }

    private async Task VerificaSentinellaPreMigrazioneAsync(CancellationToken cancellationToken)
    {
        var valore = await LeggiSentinellaAsync(consentiAssente: true, cancellationToken);
        if (valore is null) return;

        // In rotazione può essere ancora K1, purché la chiave sia nel portachiavi decrypt-only.
        if (_protettore.SvelaConContesto(valore, ContestoSentinella) != ValoreSentinella)
            throw new InvalidOperationException("Sentinella PAGHE_DATA_KEY non valida.");
    }

    private async Task VerificaSentinellaCorrenteAsync(CancellationToken cancellationToken)
    {
        var valore = await LeggiSentinellaAsync(consentiAssente: false, cancellationToken)
            ?? throw new InvalidOperationException("Sentinella PAGHE_DATA_KEY assente.");
        if (!valore.StartsWith(_protettore.PrefissoChiaveCorrente, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Sentinella non allineata alla chiave corrente: eseguire il runner GDPR.");
        if (_protettore.SvelaConContesto(valore, ContestoSentinella) != ValoreSentinella)
            throw new InvalidOperationException("Sentinella PAGHE_DATA_KEY non valida.");
    }

    private async Task<string?> LeggiSentinellaAsync(
        bool consentiAssente,
        CancellationToken cancellationToken)
    {
        if (!await ColonnaEsisteAsync(
                "MetadatiProtezioneDati",
                "ValoreProtetto",
                cancellationToken))
        {
            if (consentiAssente) return null;
            throw new InvalidOperationException(
                "Sentinella della chiave assente: eseguire --migra-dati-sensibili.");
        }

        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var leggi = connessione.CreateCommand();
        CollegaTransazioneCorrente(leggi);
        leggi.CommandText =
            "SELECT \"ValoreProtetto\" FROM \"MetadatiProtezioneDati\" WHERE \"Id\" = 'master';";
        var valore = await leggi.ExecuteScalarAsync(cancellationToken) as string;
        if (valore is null && !consentiAssente)
            throw new InvalidOperationException(
                "Sentinella della chiave assente: eseguire --migra-dati-sensibili.");
        return valore;
    }

    private async Task ScriviSentinellaCorrenteAsync(CancellationToken cancellationToken)
    {
        var tipoTesto = _db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? "text"
            : "TEXT";
        await EseguiNonQueryAsync(
            $"CREATE TABLE IF NOT EXISTS \"MetadatiProtezioneDati\" (" +
            $"\"Id\" {tipoTesto} NOT NULL PRIMARY KEY, \"ValoreProtetto\" {tipoTesto} NOT NULL);",
            cancellationToken);

        var valore = _protettore.ProteggiConContesto(ValoreSentinella, ContestoSentinella)!;
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var scrivi = connessione.CreateCommand();
        CollegaTransazioneCorrente(scrivi);
        scrivi.CommandText =
            "INSERT INTO \"MetadatiProtezioneDati\" (\"Id\", \"ValoreProtetto\") " +
            "VALUES ('master', @valore) " +
            "ON CONFLICT (\"Id\") DO UPDATE SET \"ValoreProtetto\" = excluded.\"ValoreProtetto\";";
        AggiungiParametro(scrivi, "@valore", valore);
        await scrivi.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> DatabaseApplicativoVuotoAsync(CancellationToken cancellationToken)
    {
        foreach (var tabella in new[]
        {
            "Aziende", "Dipendenti", "AvvisiPagamento", "Cartellini", "CedolinoVoceEntity",
            "Cedolini", "CedoliniSnapshot", "CedoliniDocumenti", "Utenti", "ConfigurazioniSdi",
        })
            if (await ContaAsync($"SELECT COUNT(*) FROM \"{tabella}\";", cancellationToken) > 0)
                return false;
        return true;
    }

    private async Task<bool> ColonnaEsisteAsync(
        string tabella,
        string colonna,
        CancellationToken cancellationToken)
    {
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var cmd = connessione.CreateCommand();
        CollegaTransazioneCorrente(cmd);
        if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabella}') WHERE name = @colonna;";
            AggiungiParametro(cmd, "@colonna", colonna);
        }
        else
        {
            cmd.CommandText =
                "SELECT COUNT(*) FROM information_schema.columns " +
                "WHERE table_schema = 'public' AND table_name = @tabella AND column_name = @colonna;";
            AggiungiParametro(cmd, "@tabella", tabella);
            AggiungiParametro(cmd, "@colonna", colonna);
        }
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }

    private async Task<long> ContaAsync(string sql, CancellationToken cancellationToken)
    {
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var cmd = connessione.CreateCommand();
        CollegaTransazioneCorrente(cmd);
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private async Task EseguiNonQueryAsync(string sql, CancellationToken cancellationToken)
    {
        var connessione = _db.Database.GetDbConnection();
        await ApriSeNecessarioAsync(connessione, cancellationToken);
        await using var cmd = connessione.CreateCommand();
        CollegaTransazioneCorrente(cmd);
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private void CollegaTransazioneCorrente(DbCommand comando)
    {
        if (_db.Database.CurrentTransaction is { } transazione)
            comando.Transaction = transazione.GetDbTransaction();
    }

    private async Task VerificaProviderAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (_db.Database.ProviderName is not "Microsoft.EntityFrameworkCore.Sqlite"
            and not "Npgsql.EntityFrameworkCore.PostgreSQL")
            throw new NotSupportedException(
                $"Provider non supportato dalla migrazione GDPR: {_db.Database.ProviderName}.");
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<bool> PrendiLockPostgresAsync(
        DbConnection connessione,
        CancellationToken cancellationToken)
    {
        if (_db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return false;
        await using var cmd = connessione.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_lock(@chiave);";
        AggiungiParametro(cmd, "@chiave", LockPostgres);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static async Task RilasciaLockPostgresAsync(
        DbConnection connessione,
        CancellationToken cancellationToken)
    {
        await using var cmd = connessione.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(@chiave);";
        AggiungiParametro(cmd, "@chiave", LockPostgres);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private decimal DecimaleStorico(object? valore, string campo) =>
        DecimaleNullableStorico(valore, campo)
        ?? throw new InvalidOperationException($"{campo} legacy è nullo.");

    private decimal? DecimaleNullableStorico(object? valore, string campo)
    {
        if (valore is null or DBNull) return null;
        var testo = _protettore.Svela(Stringa(valore));
        if (!decimal.TryParse(testo, NumberStyles.Number, CultureInfo.InvariantCulture, out var risultato))
            throw new InvalidOperationException($"{campo} legacy non è un decimal valido.");
        return risultato;
    }

    private int InteroStorico(object? valore, string campo)
    {
        if (valore is null or DBNull)
            throw new InvalidOperationException($"{campo} legacy è nullo.");
        var testo = Stringa(valore);
        testo = _protettore.Svela(testo)!;
        if (!int.TryParse(testo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var risultato))
            throw new InvalidOperationException($"{campo} legacy non è un intero valido.");
        return risultato;
    }

    private static string? Stringa(object? valore) =>
        valore is null or DBNull
            ? null
            : Convert.ToString(valore, CultureInfo.InvariantCulture);

    private static Guid GuidDaDb(object valore) =>
        valore is Guid guid ? guid : Guid.Parse(Stringa(valore)!);

    private static void AggiungiParametro(DbCommand comando, string nome, object valore)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valore;
        comando.Parameters.Add(parametro);
    }

    private static async Task ApriSeNecessarioAsync(
        DbConnection connessione,
        CancellationToken cancellationToken)
    {
        if (connessione.State != ConnectionState.Open)
            await connessione.OpenAsync(cancellationToken);
    }

    private sealed record RigaGrezza(Guid Id, object?[] Valori);
    private sealed record ColonnaLegacy(string Tabella, string Colonna);
    private sealed record CampoCorrente(string Tabella, string Colonna, bool Obbligatorio);
}

public sealed record EsitoMigrazioneDatiSensibili(int TenantEsaminati, int ValoriMigrati);
