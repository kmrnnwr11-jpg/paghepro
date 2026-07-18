using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Paghe.Core.Ai;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dati di collegamento di una casella email/PEC inviati dall'utente (self-service).</summary>
public sealed record CasellaEmailInput(
    string Etichetta, string Tipo, string Host, int Porta,
    string Utente, string? Password, bool Ssl, bool Attiva);

/// <summary>Esito di una sincronizzazione IMAP: nuovi messaggi importati oppure un errore da mostrare.</summary>
public sealed record EsitoSincronizzazione(int NuoviMessaggi, string? Errore);

/// <summary>Filtri di elenco per la casella di posta unificata.</summary>
public sealed record FiltriMessaggiEmail(
    Guid? CasellaId = null, bool? Letto = null, bool? Urgente = null, bool? Pubblicita = null,
    bool? SoloConPropostaInAttesa = null);

/// <summary>
/// Gestisce caselle email/PEC (collegamento IMAP self-service) e i messaggi importati, isolati
/// per tenant. La sincronizzazione è on-demand (nessun polling in background): l'utente la avvia
/// da un pulsante. Il corpo del messaggio si legge solo quando l'utente lo apre, per non
/// scaricare/cifrare testo che non verrà mai consultato.
/// </summary>
public sealed class CasellaEmailService
{
    private readonly PagheDbContext _db;

    public CasellaEmailService(PagheDbContext db) => _db = db;

    public Task<List<CasellaEmailEntity>> ElencoCaselleAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.CaselleEmail.Where(c => c.TenantId == tenantId).OrderBy(c => c.Etichetta).ToListAsync(ct);

    public async Task<CasellaEmailEntity> CreaCasellaAsync(
        Guid tenantId, CasellaEmailInput input, CancellationToken ct = default)
    {
        var c = new CasellaEmailEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Etichetta = input.Etichetta,
            Tipo = input.Tipo,
            Host = input.Host,
            Porta = input.Porta,
            Utente = input.Utente,
            Password = input.Password,
            Ssl = input.Ssl,
            Attiva = input.Attiva,
            CreataIl = DateTime.UtcNow,
        };
        _db.CaselleEmail.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<bool> EliminaCasellaAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var c = await _db.CaselleEmail.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        if (c is null) return false;
        _db.CaselleEmail.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<MessaggioEmailEntity>> ElencoMessaggiAsync(
        Guid tenantId, FiltriMessaggiEmail filtri, CancellationToken ct = default)
    {
        var query = _db.MessaggiEmail.Where(m => m.TenantId == tenantId);
        if (filtri.CasellaId is { } casellaId) query = query.Where(m => m.CasellaId == casellaId);
        if (filtri.Letto is { } letto) query = query.Where(m => m.Letto == letto);
        if (filtri.Urgente is { } urgente) query = query.Where(m => m.Urgente == urgente);
        if (filtri.Pubblicita is { } pubblicita) query = query.Where(m => m.Pubblicita == pubblicita);
        if (filtri.SoloConPropostaInAttesa == true)
            query = query.Where(m =>
                m.Urgente == null && m.Pubblicita == null
                && (m.UrgenteProposto != null || m.PubblicitaProposto != null));
        return query.OrderByDescending(m => m.DataRicezione).ToListAsync(ct);
    }

    public Task<MessaggioEmailEntity?> OttieniMessaggioAsync(
        Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.MessaggiEmail.FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, ct);

    public async Task<bool> MarcaLettoAsync(
        Guid tenantId, Guid id, bool letto, CancellationToken ct = default)
    {
        var m = await OttieniMessaggioAsync(tenantId, id, ct);
        if (m is null) return false;
        m.Letto = letto;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Salva una proposta di triage AI (in attesa di conferma): non tocca mai i campi definitivi.</summary>
    public async Task<bool> SalvaPropostaAsync(
        Guid tenantId, Guid id, PropostaTriageEmail proposta, CancellationToken ct = default)
    {
        var m = await OttieniMessaggioAsync(tenantId, id, ct);
        if (m is null) return false;
        if (proposta.Urgente is not null)
        {
            m.UrgenteProposto = proposta.Urgente;
            m.ConfidenzaUrgente = proposta.ConfidenzaUrgente;
        }
        if (proposta.Pubblicita is not null)
        {
            m.PubblicitaProposto = proposta.Pubblicita;
            m.ConfidenzaPubblicita = proposta.ConfidenzaPubblicita;
        }
        if (proposta.Fonte is not null) m.FonteProposta = proposta.Fonte;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>L'utente conferma (o corregge) il triage: diventa definitivo, la proposta pendente si azzera.</summary>
    public async Task<bool> ConfermaPropostaAsync(
        Guid tenantId, Guid id, bool? urgente, bool? pubblicita, CancellationToken ct = default)
    {
        var m = await OttieniMessaggioAsync(tenantId, id, ct);
        if (m is null) return false;
        if (urgente is not null) m.Urgente = urgente;
        if (pubblicita is not null) m.Pubblicita = pubblicita;
        m.UrgenteProposto = null;
        m.PubblicitaProposto = null;
        m.ConfidenzaUrgente = null;
        m.ConfidenzaPubblicita = null;
        m.FonteProposta = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Legge il corpo integrale via IMAP su richiesta (non scaricato/cifrato durante la sync) e marca letto.</summary>
    public async Task<(bool Trovato, string? Corpo, string? Errore)> LeggiCorpoAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var m = await OttieniMessaggioAsync(tenantId, id, ct);
        if (m is null) return (false, null, null);

        if (!string.IsNullOrEmpty(m.Corpo))
        {
            m.Letto = true;
            await _db.SaveChangesAsync(ct);
            return (true, m.Corpo, null);
        }

        var casella = await _db.CaselleEmail.FirstOrDefaultAsync(
            c => c.Id == m.CasellaId && c.TenantId == tenantId, ct);
        if (casella is null || string.IsNullOrWhiteSpace(casella.Password))
            return (true, null, "Casella non collegata o priva di credenziali.");

        try
        {
            using var client = new ImapClient();
            await ConnettiAsync(client, casella, ct);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
            var mime = await inbox.GetMessageAsync(new UniqueId((uint)m.UidImap), ct);
            var corpo = mime.TextBody ?? mime.HtmlBody ?? "";
            await client.DisconnectAsync(true, ct);

            m.Corpo = corpo;
            m.Letto = true;
            await _db.SaveChangesAsync(ct);
            return (true, corpo, null);
        }
        catch (Exception ex)
        {
            return (true, null, $"Impossibile leggere il messaggio: {ex.Message}");
        }
    }

    /// <summary>
    /// Sincronizza on-demand la INBOX via IMAP: importa solo i messaggi con UID successivo
    /// all'ultimo già visto. L'euristica pubblicità (header List-Unsubscribe/List-Id) è applicata
    /// subito, gratis; il resto del triage resta una proposta da chiedere con l'AI.
    /// </summary>
    public async Task<EsitoSincronizzazione> SincronizzaAsync(
        Guid tenantId, Guid casellaId, CancellationToken ct = default)
    {
        var casella = await _db.CaselleEmail.FirstOrDefaultAsync(
            c => c.Id == casellaId && c.TenantId == tenantId, ct);
        if (casella is null) return new EsitoSincronizzazione(0, "Casella non trovata.");
        if (!casella.Attiva) return new EsitoSincronizzazione(0, "Casella disattivata.");
        if (string.IsNullOrWhiteSpace(casella.Password))
            return new EsitoSincronizzazione(0, "Manca la password della casella.");

        try
        {
            using var client = new ImapClient();
            await ConnettiAsync(client, casella, ct);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var da = casella.UltimoUidSincronizzato is { } ultimo
                ? new UniqueId((uint)ultimo + 1)
                : new UniqueId(1);
            if (da.Id < 1) da = new UniqueId(1);
            var intervallo = new UniqueIdRange(da, UniqueId.MaxValue);

            var riepiloghi = await inbox.FetchAsync(intervallo,
                MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope
                | MessageSummaryItems.Flags | MessageSummaryItems.PreviewText
                | MessageSummaryItems.Headers,
                ct);

            int nuovi = 0;
            long? massimoUid = casella.UltimoUidSincronizzato;
            foreach (var r in riepiloghi)
            {
                if (casella.UltimoUidSincronizzato is { } visto && r.UniqueId.Id <= (uint)visto) continue;

                var giaImportato = await _db.MessaggiEmail.AnyAsync(
                    m => m.CasellaId == casella.Id && m.UidImap == r.UniqueId.Id, ct);
                if (giaImportato) continue;

                bool pubblicitaEuristica = r.Headers is not null
                    && (r.Headers.Contains(HeaderId.ListUnsubscribe) || r.Headers.Contains(HeaderId.ListId));
                var anteprima = r.PreviewText ?? "";
                if (anteprima.Length > 300) anteprima = anteprima[..300];

                _db.MessaggiEmail.Add(new MessaggioEmailEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CasellaId = casella.Id,
                    UidImap = r.UniqueId.Id,
                    Mittente = r.Envelope?.From?.Mailboxes?.FirstOrDefault()?.Address
                        ?? r.Envelope?.From?.ToString() ?? "",
                    Oggetto = r.Envelope?.Subject ?? "",
                    DataRicezione = r.Envelope?.Date?.UtcDateTime ?? DateTime.UtcNow,
                    Anteprima = anteprima,
                    Letto = r.Flags is { } flags && flags.HasFlag(MessageFlags.Seen),
                    PubblicitaProposto = pubblicitaEuristica ? true : null,
                    ConfidenzaPubblicita = pubblicitaEuristica ? 0.95m : null,
                    FonteProposta = pubblicitaEuristica ? "Euristica" : null,
                });
                nuovi++;
                if (massimoUid is null || r.UniqueId.Id > (uint)massimoUid) massimoUid = r.UniqueId.Id;
            }

            casella.UltimoUidSincronizzato = massimoUid;
            casella.UltimaSincronizzazioneIl = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await client.DisconnectAsync(true, ct);
            return new EsitoSincronizzazione(nuovi, null);
        }
        catch (Exception ex)
        {
            return new EsitoSincronizzazione(0, $"Impossibile sincronizzare: {ex.Message}");
        }
    }

    private static async Task ConnettiAsync(ImapClient client, CasellaEmailEntity casella, CancellationToken ct)
    {
        var opzioniSicurezza = casella.Ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(casella.Host, casella.Porta, opzioniSicurezza, ct);
        await client.AuthenticateAsync(casella.Utente, casella.Password!, ct);
    }
}
