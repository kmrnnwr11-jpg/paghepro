using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Sicurezza;

public enum EsitoRotazioneRefresh
{
    Ok,
    NonValido,
    Scaduto,
    Collisione,
    Replay,
    AccountNonAttivo,
}

public sealed record SessioneRefreshEmessa(
    string RefreshToken,
    Guid FamigliaId,
    UtenteEntity Utente,
    DateTime ScadeUtc);

public sealed record RisultatoRotazioneRefresh(
    EsitoRotazioneRefresh Esito,
    SessioneRefreshEmessa? Sessione = null);

public sealed record SessioneRevocata(Guid TenantId, Guid UtenteId);

/// <summary>
/// Sessioni refresh opache, monouso e ruotate. Il primo lookup per selettore è l'unica eccezione
/// auth-bootstrap al filtro tenant: il tenant non viene mai accettato dal client e viene derivato
/// dalla riga trovata; ogni query successiva include quel TenantId.
/// </summary>
public sealed class SessioniRefreshService
{
    public static readonly TimeSpan DurataAssoluta = TimeSpan.FromDays(30);
    public static readonly TimeSpan DurataInattivita = TimeSpan.FromDays(7);
    private static readonly TimeSpan FinestraCollisione = TimeSpan.FromSeconds(10);

    private readonly PagheDbContext _db;
    private readonly TimeProvider _orologio;

    public SessioniRefreshService(PagheDbContext db, TimeProvider? orologio = null)
    {
        _db = db;
        _orologio = orologio ?? TimeProvider.System;
    }

    public async Task<SessioneRefreshEmessa> CreaAsync(
        UtenteEntity utente,
        string client,
        CancellationToken ct = default)
    {
        var ora = OraUtc();
        var famiglia = Guid.NewGuid();
        var (riga, token) = NuovaRiga(utente, famiglia, client, ora, ora + DurataAssoluta);
        _db.SessioniRefresh.Add(riga);
        await _db.SaveChangesAsync(ct);
        return new SessioneRefreshEmessa(token, famiglia, utente, riga.ScadeAssolutaUtc);
    }

    public async Task<RisultatoRotazioneRefresh> RuotaAsync(
        string? token,
        string client,
        CancellationToken ct = default)
    {
        if (!ProvaLeggereToken(token, out var selettore, out var segreto))
            return new(EsitoRotazioneRefresh.NonValido);

        // Eccezione confinata: il selettore casuale individua la riga; il tenant arriva dalla riga.
        var esistente = await _db.SessioniRefresh.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == selettore, ct);
        if (esistente is null || !HashCorrisponde(segreto, esistente.HashSegreto))
            return new(EsitoRotazioneRefresh.NonValido);

        var ora = OraUtc();
        if (esistente.RevocataUtc is not null)
            return new(EsitoRotazioneRefresh.NonValido);
        if (esistente.UsataUtc is { } usata)
            return await GestisciRiusoAsync(esistente, usata, ora, ct);
        if (ora >= esistente.ScadeAssolutaUtc || ora >= esistente.ScadeInattivitaUtc)
        {
            await RevocaFamigliaAsync(esistente.TenantId, esistente.FamigliaId, "Scaduta", ct);
            return new(EsitoRotazioneRefresh.Scaduto);
        }

        var utente = await _db.Utenti.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == esistente.UtenteId && x.TenantId == esistente.TenantId, ct);
        var tenant = await _db.Tenants.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == esistente.TenantId, ct);
        if (utente is null || !utente.Attivo || tenant is null || tenant.Sospeso)
        {
            await RevocaFamigliaAsync(esistente.TenantId, esistente.FamigliaId, "AccountNonAttivo", ct);
            return new(EsitoRotazioneRefresh.AccountNonAttivo);
        }

        await using var transazione = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var (nuova, nuovoToken) = NuovaRiga(
                utente,
                esistente.FamigliaId,
                client,
                ora,
                esistente.ScadeAssolutaUtc);
            _db.SessioniRefresh.Add(nuova);
            await _db.SaveChangesAsync(ct);

            var consumate = await _db.SessioniRefresh
                .Where(x => x.Id == esistente.Id
                    && x.TenantId == esistente.TenantId
                    && x.Versione == esistente.Versione
                    && x.UsataUtc == null
                    && x.RevocataUtc == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.UsataUtc, ora)
                    .SetProperty(x => x.SostituitaDaId, nuova.Id)
                    .SetProperty(x => x.Versione, x => x.Versione + 1), ct);

            if (consumate != 1)
            {
                await transazione.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                var aggiornata = await _db.SessioniRefresh.AsNoTracking()
                    .SingleAsync(x => x.Id == esistente.Id, ct);
                return aggiornata.UsataUtc is { } collisione
                    ? await GestisciRiusoAsync(aggiornata, collisione, ora, ct)
                    : new(EsitoRotazioneRefresh.NonValido);
            }

            await transazione.CommitAsync(ct);
            return new(EsitoRotazioneRefresh.Ok,
                new SessioneRefreshEmessa(
                    nuovoToken, esistente.FamigliaId, utente, nuova.ScadeAssolutaUtc));
        }
        catch (Exception ex) when (ex is DbUpdateException or DbException)
        {
            await transazione.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return new(EsitoRotazioneRefresh.Collisione);
        }
    }

    public async Task<bool> ValidaAccessoAsync(
        Guid tenantId,
        Guid utenteId,
        Guid famigliaId,
        string ruolo,
        CancellationToken ct = default)
    {
        var ora = OraUtc();
        var utente = await _db.Utenti.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == utenteId && x.TenantId == tenantId, ct);
        if (utente is null || !utente.Attivo || !string.Equals(utente.Ruolo, ruolo, StringComparison.Ordinal))
            return false;
        if (await _db.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenantId && x.Sospeso, ct))
            return false;

        return await _db.SessioniRefresh.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId
            && x.UtenteId == utenteId
            && x.FamigliaId == famigliaId
            && x.RevocataUtc == null
            && x.UsataUtc == null
            && x.ScadeAssolutaUtc > ora
            && x.ScadeInattivitaUtc > ora, ct);
    }

    public Task<int> RevocaFamigliaAsync(
        Guid tenantId,
        Guid famigliaId,
        string motivo,
        CancellationToken ct = default)
    {
        var ora = OraUtc();
        return _db.SessioniRefresh
            .Where(x => x.TenantId == tenantId
                && x.FamigliaId == famigliaId
                && x.RevocataUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevocataUtc, ora)
                .SetProperty(x => x.MotivoRevoca, motivo)
                .SetProperty(x => x.Versione, x => x.Versione + 1), ct);
    }

    public Task<int> RevocaUtenteAsync(
        Guid tenantId,
        Guid utenteId,
        string motivo,
        CancellationToken ct = default)
    {
        var ora = OraUtc();
        return _db.SessioniRefresh
            .Where(x => x.TenantId == tenantId
                && x.UtenteId == utenteId
                && x.RevocataUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevocataUtc, ora)
                .SetProperty(x => x.MotivoRevoca, motivo)
                .SetProperty(x => x.Versione, x => x.Versione + 1), ct);
    }

    public async Task<SessioneRevocata?> RevocaDaTokenAsync(
        string? token,
        string motivo,
        CancellationToken ct = default)
    {
        if (!ProvaLeggereToken(token, out var selettore, out var segreto))
            return null;

        // Come nel refresh, questa ricerca è il bootstrap: tenant e famiglia sono derivati
        // dalla riga autenticata tramite il segreto, mai da valori forniti separatamente.
        var sessione = await _db.SessioniRefresh.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == selettore, ct);
        if (sessione is null || !HashCorrisponde(segreto, sessione.HashSegreto))
            return null;

        await RevocaFamigliaAsync(sessione.TenantId, sessione.FamigliaId, motivo, ct);
        return new(sessione.TenantId, sessione.UtenteId);
    }

    /// <summary>Elimina materiale tecnico ormai inutilizzabile, senza toccare gli audit semantici.</summary>
    public async Task<int> EliminaScaduteAsync(CancellationToken ct = default)
    {
        // Sette giorni di margine permettono di diagnosticare replay recenti senza conservare
        // indefinitamente selettori e hash di sessioni ormai non utilizzabili.
        var soglia = OraUtc() - TimeSpan.FromDays(7);
        var candidate = _db.SessioniRefresh.Where(x =>
            x.ScadeAssolutaUtc < soglia || (x.RevocataUtc != null && x.RevocataUtc < soglia));
        await candidate.ExecuteUpdateAsync(
            s => s.SetProperty(x => x.SostituitaDaId, (Guid?)null), ct);
        return await candidate.ExecuteDeleteAsync(ct);
    }

    private async Task<RisultatoRotazioneRefresh> GestisciRiusoAsync(
        SessioneRefreshEntity sessione,
        DateTime usata,
        DateTime ora,
        CancellationToken ct)
    {
        if (ora - usata <= FinestraCollisione)
            return new(EsitoRotazioneRefresh.Collisione);
        await RevocaFamigliaAsync(sessione.TenantId, sessione.FamigliaId, "ReplayRilevato", ct);
        return new(EsitoRotazioneRefresh.Replay);
    }

    private (SessioneRefreshEntity Riga, string Token) NuovaRiga(
        UtenteEntity utente,
        Guid famiglia,
        string client,
        DateTime ora,
        DateTime scadenzaAssoluta)
    {
        var selettore = Guid.NewGuid();
        var segreto = RandomNumberGenerator.GetBytes(32);
        var token = $"{CodificaBase64Url(selettore.ToByteArray())}.{CodificaBase64Url(segreto)}";
        return (new SessioneRefreshEntity
        {
            Id = selettore,
            TenantId = utente.TenantId,
            UtenteId = utente.Id,
            FamigliaId = famiglia,
            HashSegreto = Hash(segreto),
            Client = client,
            CreataUtc = ora,
            ScadeAssolutaUtc = scadenzaAssoluta,
            ScadeInattivitaUtc = Min(ora + DurataInattivita, scadenzaAssoluta),
            Versione = 0,
        }, token);
    }

    private static bool ProvaLeggereToken(string? token, out Guid selettore, out byte[] segreto)
    {
        selettore = Guid.Empty;
        segreto = [];
        var parti = token?.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parti is not { Length: 2 }) return false;
        try
        {
            var id = DecodificaBase64Url(parti[0]);
            segreto = DecodificaBase64Url(parti[1]);
            return id.Length == 16 && segreto.Length == 32 && (selettore = new Guid(id)) != Guid.Empty;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Hash(byte[] segreto) =>
        CodificaBase64Url(SHA256.HashData(segreto));

    private static bool HashCorrisponde(byte[] segreto, string atteso)
    {
        var calcolato = Encoding.ASCII.GetBytes(Hash(segreto));
        var salvato = Encoding.ASCII.GetBytes(atteso);
        return calcolato.Length == salvato.Length
            && CryptographicOperations.FixedTimeEquals(calcolato, salvato);
    }

    private DateTime OraUtc() => _orologio.GetUtcNow().UtcDateTime;
    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

    private static string CodificaBase64Url(byte[] dati) =>
        Convert.ToBase64String(dati).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodificaBase64Url(string valore)
    {
        var base64 = valore.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => throw new FormatException() };
        return Convert.FromBase64String(base64);
    }
}
