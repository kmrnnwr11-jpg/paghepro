using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Ai;

public sealed record ImpostazioneCronologiaAurora(bool Attiva, int OreRetention = 24);

public sealed record CreaConversazioneAurora(
    string? Titolo = null,
    Guid? AziendaId = null,
    Guid? RapportoId = null,
    string? Progetto = null,
    string? Periodo = null);

public sealed record ModificaConversazioneAurora(string? Titolo = null, bool? Archiviata = null);

public sealed record AuroraConversazioneRiepilogo(
    Guid Id,
    string Titolo,
    string TipoContesto,
    Guid? AziendaId,
    Guid? RapportoId,
    string? Progetto,
    string? Periodo,
    DateTime CreataUtc,
    DateTime AggiornataUtc,
    DateTime ScadeUtc,
    DateTime? ArchiviataUtc,
    int Versione);

public sealed record AuroraMessaggioWorkspace(
    Guid Id,
    Guid TurnoId,
    int Sequenza,
    string Ruolo,
    string Contenuto,
    string StatoTurno,
    DateTime CreatoUtc);

public sealed record AuroraConversazioneDettaglio(
    AuroraConversazioneRiepilogo Conversazione,
    IReadOnlyList<AuroraMessaggioWorkspace> Messaggi);

public sealed record AuroraTurnoAvviato(Guid TurnoId, int VersioneConversazione);
public sealed record AuroraTurnoCompletato(int VersioneConversazione, bool RispostaTroncata);

public sealed class CronologiaAuroraDisattivataException : InvalidOperationException
{
    public CronologiaAuroraDisattivataException()
        : base("Attiva la cronologia Aurora per conservare e riaprire questa conversazione.") { }
}

public sealed class ConversazioneAuroraConflittoException : InvalidOperationException
{
    public ConversazioneAuroraConflittoException(string messaggio) : base(messaggio) { }
}

public sealed class LimiteAuroraRaggiuntoException(string messaggio)
    : InvalidOperationException(messaggio);

public sealed class TurnoAuroraDuplicatoException(string stato) : InvalidOperationException(
    "Questo messaggio è già stato inviato. Riapri la conversazione per vedere lo stato aggiornato.")
{
    public string Stato { get; } = stato;
}

/// <summary>
/// Workspace recente di Aurora. Tutte le query applicano insieme tenant e utente; i contenuti
/// restano ciphertext nel modello EF e vengono aperti soltanto dopo il filtro di proprietà.
/// </summary>
public sealed partial class AuroraConversazioniService
{
    public const int OreRetention = 24;
    public const int MassimoCaratteriTitolo = 120;
    public const int MassimoCaratteriProgetto = 80;
    public const int MassimoCaratteriMessaggio = 8_000;
    public const int MassimoCaratteriRisposta = 16_000;
    public const int MassimoConversazioniPerUtente = 50;
    public const int MassimoTurniPerConversazione = 100;

    private readonly PagheDbContext _db;
    private readonly TimeProvider _orologio;

    public AuroraConversazioniService(PagheDbContext db, TimeProvider? orologio = null)
    {
        _db = db;
        _orologio = orologio ?? TimeProvider.System;
    }

    public async Task<ImpostazioneCronologiaAurora> ImpostazioneAsync(
        Guid tenantId, Guid utenteId, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        await EliminaScaduteAsync(tenantId, utenteId, ct);
        var attiva = await _db.AuroraCronologiaPreferenze.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UtenteId == utenteId)
            .Select(p => (bool?)p.Attiva)
            .SingleOrDefaultAsync(ct) ?? false;
        return new(attiva);
    }

    public async Task<ImpostazioneCronologiaAurora> ImpostaAsync(
        Guid tenantId, Guid utenteId, bool attiva, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        for (var tentativo = 0; tentativo < 5; tentativo++)
        {
            try
            {
                var preferenza = await _db.AuroraCronologiaPreferenze.SingleOrDefaultAsync(
                    p => p.TenantId == tenantId && p.UtenteId == utenteId, ct);
                if (preferenza is null)
                {
                    preferenza = new AuroraCronologiaPreferenzaEntity
                    {
                        TenantId = tenantId,
                        UtenteId = utenteId,
                        Versione = 1,
                    };
                    _db.AuroraCronologiaPreferenze.Add(preferenza);
                }
                else
                {
                    // Create e opt-out scrivono lo stesso token: una revoca concorrente non può
                    // terminare lasciando dietro una conversazione nata dalla versione vecchia.
                    preferenza.Versione++;
                }
                preferenza.Attiva = attiva;
                preferenza.AggiornataUtc = OraUtc();

                if (!attiva)
                {
                    var conversazioni = await _db.AuroraConversazioni
                        .Where(c => c.TenantId == tenantId && c.UtenteId == utenteId)
                        .ToListAsync(ct);
                    _db.AuroraConversazioni.RemoveRange(conversazioni);
                }

                await _db.SaveChangesAsync(ct);
                return new(attiva);
            }
            catch (DbUpdateException) when (tentativo < 4)
            {
                // Se una create ha vinto il primo giro, l'opt-out rilegge e al giro successivo
                // cancella anche quella conversazione. Se ha vinto l'opt-out, create fallirà.
                _db.ChangeTracker.Clear();
            }
        }

        throw new ConversazioneAuroraConflittoException(
            "La preferenza della cronologia è cambiata in parallelo: riprova.");
    }

    public async Task<AuroraConversazioneRiepilogo> CreaAsync(
        Guid tenantId, Guid utenteId, CreaConversazioneAurora richiesta,
        CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        ValidaMetadati(richiesta.Titolo, richiesta.Progetto);
        var contesto = await ValidaContestoAsync(tenantId, richiesta, ct);
        var titolo = NormalizzaTitolo(richiesta.Titolo, contesto.TitoloPredefinito);
        // Un payload ambiguo non deve lasciare un'etichetta progetto dentro uno scope
        // azienda/rapporto: il tipo validato è l'unica fonte autorevole del contesto.
        var progetto = contesto.Tipo == "Progetto"
            ? NormalizzaProgetto(richiesta.Progetto)
            : null;
        for (var tentativo = 0; tentativo < 5; tentativo++)
        {
            var preferenza = await _db.AuroraCronologiaPreferenze.SingleOrDefaultAsync(
                p => p.TenantId == tenantId && p.UtenteId == utenteId, ct);
            if (preferenza?.Attiva != true)
                throw new CronologiaAuroraDisattivataException();
            var numeroConversazioni = await _db.AuroraConversazioni.AsNoTracking()
                .CountAsync(c => c.TenantId == tenantId && c.UtenteId == utenteId
                    && c.ScadeUtc > OraUtc(), ct);
            if (numeroConversazioni >= MassimoConversazioniPerUtente)
                throw new LimiteAuroraRaggiuntoException(
                    $"Hai già {MassimoConversazioniPerUtente} conversazioni recenti: "
                    + "eliminane una o attendi la scadenza prima di aprirne un'altra.");

            var ora = OraUtc();
            var id = Guid.NewGuid();
            var entita = new AuroraConversazioneEntity
            {
                Id = id,
                TenantId = tenantId,
                UtenteId = utenteId,
                TipoContesto = contesto.Tipo,
                AziendaId = contesto.AziendaId,
                RapportoId = contesto.RapportoId,
                Periodo = contesto.Periodo,
                TitoloProtetto = Proteggi(titolo, ContestoTitolo(tenantId, utenteId, id)),
                ProgettoProtetto = progetto is null
                    ? null
                    : Proteggi(progetto, ContestoProgetto(tenantId, utenteId, id)),
                CreataUtc = ora,
                AggiornataUtc = ora,
                ScadeUtc = ora.AddHours(OreRetention),
                Versione = 1,
            };
            _db.AuroraConversazioni.Add(entita);
            preferenza.Versione++;
            try
            {
                await _db.SaveChangesAsync(ct);
                return ApriRiepilogo(entita);
            }
            catch (DbUpdateConcurrencyException) when (tentativo < 4)
            {
                _db.ChangeTracker.Clear();
            }
        }

        throw new ConversazioneAuroraConflittoException(
            "La cronologia è cambiata in parallelo: riaprila e riprova.");
    }

    public async Task<IReadOnlyList<AuroraConversazioneRiepilogo>> ElencoAsync(
        Guid tenantId, Guid utenteId, bool archiviate = false, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        await EliminaScaduteAsync(tenantId, utenteId, ct);
        if (!await CronologiaAttivaAsync(tenantId, utenteId, ct))
            return [];

        var righe = await _db.AuroraConversazioni.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UtenteId == utenteId
                && (archiviate ? c.ArchiviataUtc != null : c.ArchiviataUtc == null))
            .OrderByDescending(c => c.AggiornataUtc)
            .Take(MassimoConversazioniPerUtente)
            .ToListAsync(ct);
        return [.. righe.Select(ApriRiepilogo)];
    }

    public async Task<AuroraConversazioneDettaglio?> DettaglioAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var conversazione = await ConversazioneAsync(tenantId, utenteId, conversazioneId, ct);
        if (conversazione is null) return null;

        var turni = await _db.AuroraTurni.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.UtenteId == utenteId
                && t.ConversazioneId == conversazioneId)
            .ToDictionaryAsync(t => t.Id, t => t.Stato, ct);
        var messaggi = await _db.AuroraMessaggi.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UtenteId == utenteId
                && m.ConversazioneId == conversazioneId)
            .OrderBy(m => m.Sequenza)
            .ToListAsync(ct);

        return new(
            ApriRiepilogo(conversazione),
            [.. messaggi.Select(m => new AuroraMessaggioWorkspace(
                m.Id,
                m.TurnoId,
                m.Sequenza,
                m.Ruolo,
                Svela(m.ContenutoProtetto,
                    ContestoMessaggio(tenantId, utenteId, conversazioneId, m.Id)),
                turni.GetValueOrDefault(m.TurnoId, "Fallito"),
                m.CreatoUtc))]);
    }

    /// <summary>Restituisce solo turni completati, destinabili di nuovo al modello.</summary>
    public async Task<AuroraConversazioneDettaglio?> ContestoChatAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, CancellationToken ct = default)
    {
        var dettaglio = await DettaglioAsync(tenantId, utenteId, conversazioneId, ct);
        if (dettaglio is null) return null;
        return dettaglio with
        {
            Messaggi = [.. dettaglio.Messaggi.Where(m => m.StatoTurno == "Completato")],
        };
    }

    public async Task<AuroraConversazioneRiepilogo?> ModificaAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId,
        ModificaConversazioneAurora richiesta, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var conversazione = await ConversazioneAsync(tenantId, utenteId, conversazioneId, ct);
        if (conversazione is null) return null;
        var modificata = false;
        if (richiesta.Titolo is not null)
        {
            ValidaMetadati(richiesta.Titolo, null);
            var titolo = NormalizzaTitolo(richiesta.Titolo, "Nuova conversazione");
            conversazione.TitoloProtetto = Proteggi(
                titolo, ContestoTitolo(tenantId, utenteId, conversazioneId));
            modificata = true;
        }
        if (richiesta.Archiviata is { } archiviata)
        {
            conversazione.ArchiviataUtc = archiviata ? OraUtc() : null;
            modificata = true;
        }
        if (modificata)
        {
            conversazione.AggiornataUtc = OraUtc();
            conversazione.Versione++;
            await _db.SaveChangesAsync(ct);
        }
        return ApriRiepilogo(conversazione);
    }

    public async Task<bool> EliminaAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var conversazione = await _db.AuroraConversazioni
            .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.UtenteId == utenteId
                && c.Id == conversazioneId, ct);
        if (conversazione is null) return false;
        _db.AuroraConversazioni.Remove(conversazione);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AuroraTurnoAvviato> AvviaTurnoAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, Guid clientTurnId,
        string messaggio, int? versioneAttesa, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        if (clientTurnId == Guid.Empty)
            throw new ArgumentException("Identificativo del turno mancante.", nameof(clientTurnId));
        if (string.IsNullOrWhiteSpace(messaggio) || messaggio.Length > MassimoCaratteriMessaggio)
            throw new ArgumentException(
                $"Il messaggio deve contenere da 1 a {MassimoCaratteriMessaggio:N0} caratteri.",
                nameof(messaggio));
        await RichiediCronologiaAttivaAsync(tenantId, utenteId, ct);
        var conversazione = await ConversazioneAsync(tenantId, utenteId, conversazioneId, ct)
            ?? throw new KeyNotFoundException("Conversazione non trovata.");
        if (versioneAttesa is { } versione && versione != conversazione.Versione)
            throw new ConversazioneAuroraConflittoException(
                "La conversazione è cambiata in un'altra scheda. Riaprila prima di inviare.");

        var duplicato = await _db.AuroraTurni.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.UtenteId == utenteId
                && t.ConversazioneId == conversazioneId && t.ClientTurnId == clientTurnId)
            .Select(t => t.Stato)
            .SingleOrDefaultAsync(ct);
        if (duplicato is not null)
            throw new TurnoAuroraDuplicatoException(duplicato);

        var numeroTurni = await _db.AuroraTurni.AsNoTracking()
            .CountAsync(t => t.TenantId == tenantId && t.UtenteId == utenteId
                && t.ConversazioneId == conversazioneId, ct);
        if (numeroTurni >= MassimoTurniPerConversazione)
            throw new LimiteAuroraRaggiuntoException(
                $"Questa conversazione ha raggiunto {MassimoTurniPerConversazione} turni: "
                + "apri una nuova scheda per continuare.");

        var sequenza = (await _db.AuroraMessaggi.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UtenteId == utenteId
                && m.ConversazioneId == conversazioneId)
            .MaxAsync(m => (int?)m.Sequenza, ct) ?? 0) + 1;
        var ora = OraUtc();
        var turnoId = Guid.NewGuid();
        var messaggioId = Guid.NewGuid();
        _db.AuroraTurni.Add(new AuroraTurnoEntity
        {
            Id = turnoId,
            TenantId = tenantId,
            UtenteId = utenteId,
            ConversazioneId = conversazioneId,
            ClientTurnId = clientTurnId,
            Stato = "InCorso",
            CreatoUtc = ora,
            Versione = 1,
        });
        _db.AuroraMessaggi.Add(new AuroraMessaggioEntity
        {
            Id = messaggioId,
            TenantId = tenantId,
            UtenteId = utenteId,
            ConversazioneId = conversazioneId,
            TurnoId = turnoId,
            Sequenza = sequenza,
            Ruolo = "user",
            ContenutoProtetto = Proteggi(messaggio,
                ContestoMessaggio(tenantId, utenteId, conversazioneId, messaggioId)),
            CreatoUtc = ora,
        });
        conversazione.AggiornataUtc = ora;
        conversazione.Versione++;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var stato = await _db.AuroraTurni.AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.UtenteId == utenteId
                    && t.ConversazioneId == conversazioneId && t.ClientTurnId == clientTurnId)
                .Select(t => t.Stato)
                .SingleOrDefaultAsync(ct);
            if (stato is not null) throw new TurnoAuroraDuplicatoException(stato);
            throw new ConversazioneAuroraConflittoException(
                "La conversazione è stata aggiornata in parallelo. Riaprila e riprova.");
        }
        return new(turnoId, conversazione.Versione);
    }

    public async Task<AuroraTurnoCompletato?> CompletaTurnoAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, Guid turnoId,
        string risposta, CancellationToken ct = default)
    {
        ValidaIdentita(tenantId, utenteId);
        var conversazione = await _db.AuroraConversazioni
            .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.UtenteId == utenteId
                && c.Id == conversazioneId, ct);
        var turno = await _db.AuroraTurni
            .SingleOrDefaultAsync(t => t.TenantId == tenantId && t.UtenteId == utenteId
                && t.ConversazioneId == conversazioneId && t.Id == turnoId, ct);
        // Una cancellazione durante la risposta è definitiva: non ricreiamo alcuna riga.
        if (conversazione is null || turno is null || turno.Stato != "InCorso") return null;

        var rispostaTroncata = risposta.Length > MassimoCaratteriRisposta;
        var rispostaPersistita = rispostaTroncata
            ? risposta[..MassimoCaratteriRisposta]
                + "\n\n[Risposta accorciata nella cronologia: riapri una nuova domanda per il dettaglio.]"
            : risposta;
        var sequenza = (await _db.AuroraMessaggi.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.UtenteId == utenteId
                && m.ConversazioneId == conversazioneId)
            .MaxAsync(m => (int?)m.Sequenza, ct) ?? 0) + 1;
        var ora = OraUtc();
        var messaggioId = Guid.NewGuid();
        _db.AuroraMessaggi.Add(new AuroraMessaggioEntity
        {
            Id = messaggioId,
            TenantId = tenantId,
            UtenteId = utenteId,
            ConversazioneId = conversazioneId,
            TurnoId = turnoId,
            Sequenza = sequenza,
            Ruolo = "assistant",
            ContenutoProtetto = Proteggi(rispostaPersistita,
                ContestoMessaggio(tenantId, utenteId, conversazioneId, messaggioId)),
            CreatoUtc = ora,
        });
        turno.Stato = "Completato";
        turno.CompletatoUtc = ora;
        turno.Versione++;
        conversazione.AggiornataUtc = ora;
        conversazione.Versione++;
        try
        {
            await _db.SaveChangesAsync(ct);
            return new(conversazione.Versione, rispostaTroncata);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return null;
        }
    }

    public async Task SegnaTurnoFallitoAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, Guid turnoId,
        CancellationToken ct = default)
    {
        var turno = await _db.AuroraTurni.SingleOrDefaultAsync(t =>
            t.TenantId == tenantId && t.UtenteId == utenteId
            && t.ConversazioneId == conversazioneId && t.Id == turnoId, ct);
        if (turno is null || turno.Stato != "InCorso") return;
        turno.Stato = "Fallito";
        turno.CompletatoUtc = OraUtc();
        turno.Versione++;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Rimuove l'intero turno quando soltanto la risposta rivela dati esclusi. La cascata elimina
    /// anche il messaggio utente già prenotato, così non resta un transcript parziale.
    /// </summary>
    public async Task ScartaTurnoAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, Guid turnoId,
        CancellationToken ct = default)
    {
        var turno = await _db.AuroraTurni.SingleOrDefaultAsync(t =>
            t.TenantId == tenantId && t.UtenteId == utenteId
            && t.ConversazioneId == conversazioneId && t.Id == turnoId, ct);
        if (turno is null) return;
        _db.AuroraTurni.Remove(turno);
        var conversazione = await _db.AuroraConversazioni.SingleOrDefaultAsync(c =>
            c.TenantId == tenantId && c.UtenteId == utenteId && c.Id == conversazioneId, ct);
        if (conversazione is not null)
        {
            conversazione.AggiornataUtc = OraUtc();
            conversazione.Versione++;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>D3A/D3B restano nella sola working memory del turno e non nel transcript.</summary>
    public static bool ContieneDatiEsclusi(string? testo) =>
        !string.IsNullOrWhiteSpace(testo) && DatiEsclusiRegex().IsMatch(testo);

    /// <summary>
    /// Pulizia autonoma cross-tenant invocata dal worker applicativo. La query non restituisce
    /// contenuti e usa soltanto il TTL; l'interceptor registra ogni cancellazione sul tenant
    /// della riga. Il limite evita transazioni troppo grandi in installazioni affollate.
    /// </summary>
    public async Task<int> EliminaScaduteTenantAsync(
        Guid tenantId, int massimo = 500, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant della pulizia mancante.", nameof(tenantId));
        if (massimo is < 1 or > 5_000)
            throw new ArgumentOutOfRangeException(nameof(massimo));

        var scadute = await _db.AuroraConversazioni
            .Where(c => c.TenantId == tenantId && c.ScadeUtc <= OraUtc())
            .OrderBy(c => c.ScadeUtc)
            .Take(massimo)
            .ToListAsync(ct);
        if (scadute.Count == 0) return 0;
        _db.AuroraConversazioni.RemoveRange(scadute);
        await _db.SaveChangesAsync(ct);
        return scadute.Count;
    }

    private async Task<AuroraConversazioneEntity?> ConversazioneAsync(
        Guid tenantId, Guid utenteId, Guid conversazioneId, CancellationToken ct)
    {
        var conversazione = await _db.AuroraConversazioni.SingleOrDefaultAsync(c =>
            c.TenantId == tenantId && c.UtenteId == utenteId && c.Id == conversazioneId, ct);
        if (conversazione is null) return null;
        if (conversazione.ScadeUtc > OraUtc()) return conversazione;
        _db.AuroraConversazioni.Remove(conversazione);
        await _db.SaveChangesAsync(ct);
        return null;
    }

    private async Task EliminaScaduteAsync(Guid tenantId, Guid utenteId, CancellationToken ct)
    {
        var ora = OraUtc();
        var scadute = await _db.AuroraConversazioni
            .Where(c => c.TenantId == tenantId && c.UtenteId == utenteId && c.ScadeUtc <= ora)
            .ToListAsync(ct);
        if (scadute.Count == 0) return;
        _db.AuroraConversazioni.RemoveRange(scadute);
        await _db.SaveChangesAsync(ct);
    }

    private async Task RichiediCronologiaAttivaAsync(
        Guid tenantId, Guid utenteId, CancellationToken ct)
    {
        if (!await CronologiaAttivaAsync(tenantId, utenteId, ct))
            throw new CronologiaAuroraDisattivataException();
    }

    private Task<bool> CronologiaAttivaAsync(Guid tenantId, Guid utenteId, CancellationToken ct) =>
        _db.AuroraCronologiaPreferenze.AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.UtenteId == utenteId && p.Attiva, ct);

    private async Task<ContestoValidato> ValidaContestoAsync(
        Guid tenantId, CreaConversazioneAurora richiesta, CancellationToken ct)
    {
        var periodo = NormalizzaPeriodo(richiesta.Periodo);
        Guid? aziendaId = richiesta.AziendaId;
        string? aziendaNome = null;
        if (aziendaId is { } idAzienda)
        {
            aziendaNome = await _db.Aziende.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.Id == idAzienda)
                .Select(a => a.RagioneSociale)
                .SingleOrDefaultAsync(ct)
                ?? throw new ArgumentException("Azienda non trovata nel tenant corrente.");
        }

        if (richiesta.RapportoId is { } rapportoId)
        {
            var rapporto = await _db.Rapporti.AsNoTracking()
                .Where(r => r.TenantId == tenantId && r.Id == rapportoId)
                .Select(r => new
                {
                    r.AziendaId,
                    Nome = r.Dipendente!.Nome + " " + r.Dipendente.Cognome,
                })
                .SingleOrDefaultAsync(ct)
                ?? throw new ArgumentException("Rapporto di lavoro non trovato nel tenant corrente.");
            if (aziendaId is not null && aziendaId != rapporto.AziendaId)
                throw new ArgumentException("Il lavoratore non appartiene all'azienda indicata.");
            aziendaId = rapporto.AziendaId;
            return new("Rapporto", aziendaId, rapportoId, periodo,
                $"{rapporto.Nome.Trim()} · {periodo ?? "Aurora"}");
        }

        if (!string.IsNullOrWhiteSpace(richiesta.Progetto))
            return new("Progetto", aziendaId, null, periodo,
                NormalizzaProgetto(richiesta.Progetto)!);
        if (aziendaId is not null)
            return new("Azienda", aziendaId, null, periodo,
                $"{aziendaNome} · {periodo ?? "Aurora"}");
        return new("Generale", null, null, periodo, "Aurora · generale");
    }

    private AuroraConversazioneRiepilogo ApriRiepilogo(AuroraConversazioneEntity c) => new(
        c.Id,
        Svela(c.TitoloProtetto, ContestoTitolo(c.TenantId, c.UtenteId, c.Id)),
        c.TipoContesto,
        c.AziendaId,
        c.RapportoId,
        c.ProgettoProtetto is null
            ? null
            : Svela(c.ProgettoProtetto, ContestoProgetto(c.TenantId, c.UtenteId, c.Id)),
        c.Periodo,
        c.CreataUtc,
        c.AggiornataUtc,
        c.ScadeUtc,
        c.ArchiviataUtc,
        c.Versione);

    private string Proteggi(string testo, string contesto) =>
        _db.Protettore.ProteggiConContesto(testo, contesto)
        ?? throw new InvalidOperationException("Cifratura della conversazione non riuscita.");

    private string Svela(string testo, string contesto) =>
        _db.Protettore.SvelaConContesto(testo, contesto)
        ?? throw new InvalidOperationException("Decifratura della conversazione non riuscita.");

    private static string ContestoTitolo(Guid tenantId, Guid utenteId, Guid conversazioneId) =>
        $"AuroraConversazioni|{tenantId:D}|{utenteId:D}|{conversazioneId:D}|titolo";

    private static string ContestoProgetto(Guid tenantId, Guid utenteId, Guid conversazioneId) =>
        $"AuroraConversazioni|{tenantId:D}|{utenteId:D}|{conversazioneId:D}|progetto";

    private static string ContestoMessaggio(
        Guid tenantId, Guid utenteId, Guid conversazioneId, Guid messaggioId) =>
        $"AuroraMessaggi|{tenantId:D}|{utenteId:D}|{conversazioneId:D}|{messaggioId:D}|contenuto";

    private static string NormalizzaTitolo(string? titolo, string fallback)
    {
        var valore = string.IsNullOrWhiteSpace(titolo) ? fallback : titolo.Trim();
        return valore.Length <= MassimoCaratteriTitolo
            ? valore
            : valore[..MassimoCaratteriTitolo];
    }

    private static string? NormalizzaProgetto(string? progetto)
    {
        if (string.IsNullOrWhiteSpace(progetto)) return null;
        var valore = progetto.Trim();
        return valore.Length <= MassimoCaratteriProgetto
            ? valore
            : valore[..MassimoCaratteriProgetto];
    }

    private static string? NormalizzaPeriodo(string? periodo)
    {
        if (string.IsNullOrWhiteSpace(periodo)) return null;
        var valore = periodo.Trim();
        if (!Regex.IsMatch(valore, @"^\d{4}-(0[1-9]|1[0-2])$"))
            throw new ArgumentException("Periodo non valido: usare AAAA-MM.");
        return valore;
    }

    private static void ValidaMetadati(string? titolo, string? progetto)
    {
        if (FiltroSegretiAi.DeveBloccare(titolo) || FiltroSegretiAi.DeveBloccare(progetto))
            throw new ArgumentException(
                "Titolo o progetto sembra contenere una credenziale: rimuovila e riprova.");
        if (ContieneDatiEsclusi(titolo) || ContieneDatiEsclusi(progetto))
            throw new ArgumentException(
                "Titolo e progetto non possono contenere dati altamente sensibili.");
    }

    private DateTime OraUtc() => _orologio.GetUtcNow().UtcDateTime;

    private static void ValidaIdentita(Guid tenantId, Guid utenteId)
    {
        if (tenantId == Guid.Empty || utenteId == Guid.Empty)
            throw new UnauthorizedAccessException("Sessione non valida.");
    }

    private sealed record ContestoValidato(
        string Tipo, Guid? AziendaId, Guid? RapportoId, string? Periodo,
        string TitoloPredefinito);

    [GeneratedRegex(
        @"\b(malatti[ae]|infortuni[oa]?|salute|diagnos[ie]|terapi[ae]|farmac[io]|patologi[ae]|certificat[oa] medic[oa]|gravidanza|disabilit[aà]|handicap|tumor[ei]?|cancer[eo]|hiv|aids|diabet[ei]?|epilessia|sindacat[oi]|iscrizione sindacale|origine etnic[ao]|etni[ae]|origine razziale|dato razziale|opinion[ei] politic[ah]|orientamento politico|appartenenza politic[ah]|partito politico|convinzion[ei] religios[ae]|credo religioso|religion[ei]|musulman[oa]|islamic[oa]|cristian[oa]|ebraic[oa]|buddist[ao]|convinzion[ei] filosofic[ah]|dato genetic[oa]|dna|genoma|dato biometric[oa]|impronta digitale|riconoscimento facciale|scansione dell['’]iride|vita sessuale|orientamento sessuale|omosessual[ei]|eterosessual[ei]|bisessual[ei]|transessual[ei]|identit[aà] di genere|condann[ae]|reat[oi]|casellario giudiziale|procedimento penale|precedent[ei] penal[ei]|imputat[oa]|arrestat[oa]|misura cautelare)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DatiEsclusiRegex();
}
