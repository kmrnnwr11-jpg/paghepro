using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico.UniEmens;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Gestione di dipendenti e rapporti di lavoro, isolata per tenant.</summary>
public sealed class AnagraficheLavoroService
{
    private readonly PagheDbContext _db;
    private readonly TimeProvider _clock;

    public AnagraficheLavoroService(PagheDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Crea un dipendente e il relativo rapporto di lavoro per un'azienda.</summary>
    public async Task<RapportoLavoroEntity> CreaDipendenteERapportoAsync(
        Guid tenantId, Guid aziendaId, DipendenteEntity dipendente, RapportoLavoroEntity rapporto,
        CancellationToken ct = default)
    {
        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");

        // Regola anti-doppione: lo stesso codice fiscale non può risultare assunto due volte
        // nella stessa azienda (un doppio rapporto produrrebbe una doppia busta paga per sbaglio).
        // Se la persona esiste già nell'archivio (stesso CF nel tenant) si riusa la sua
        // anagrafica e si crea solo il nuovo rapporto: può lavorare per due clienti dello
        // studio, e ogni busta paga nasce dal rapporto della propria azienda di assunzione.
        // Il CF può mancare (import guidato: finisce tra i dati da completare) o essere il
        // numerico provvisorio a 11 cifre; se presente, però, il checksum deve tornare.
        string cf = dipendente.CodiceFiscale?.Trim().ToUpperInvariant() ?? "";
        if (cf.Length > 0
            && !Paghe.Core.AdeTelematico.ValidatoreFiscale.CodiceFiscaleValido(cf))
            throw new ArgumentException(
                "Codice fiscale del lavoratore formalmente errato: il checksum non coincide.");
        dipendente.CodiceFiscale = cf;
        dipendente.Sesso = SessoCoerente(dipendente.Sesso, cf);
        var anagrafica = dipendente;
        if (cf.Length > 0)
        {
            var esistente = await _db.Dipendenti
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.CodiceFiscale == cf, ct);
            if (esistente is not null)
            {
                bool giaAssuntoQui = await _db.Rapporti.AnyAsync(r =>
                    r.TenantId == tenantId && r.AziendaId == aziendaId
                    && r.DipendenteId == esistente.Id, ct);
                if (giaAssuntoQui)
                    throw new ArgumentException(
                        $"Il codice fiscale {cf} risulta già assunto in «{azienda.RagioneSociale}»: "
                        + "non creo un doppione. Per correggere il rapporto esistente usare la scheda dipendente.");
                anagrafica = esistente;
            }
        }

        if (anagrafica == dipendente)
        {
            anagrafica.Id = anagrafica.Id == Guid.Empty ? Guid.NewGuid() : anagrafica.Id;
            anagrafica.TenantId = tenantId;
            _db.Dipendenti.Add(anagrafica);
        }

        rapporto.Id = Guid.NewGuid();
        rapporto.TenantId = tenantId;
        rapporto.AziendaId = aziendaId;
        rapporto.DipendenteId = anagrafica.Id;

        _db.Rapporti.Add(rapporto);
        await _db.SaveChangesAsync(ct);
        return rapporto;
    }

    public Task<List<RapportoLavoroEntity>> ElencoRapportiAsync(
        Guid tenantId, Guid aziendaId, CancellationToken ct = default) =>
        _db.Rapporti.Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && r.AziendaId == aziendaId)
            .ToListAsync(ct);

    public Task<RapportoLavoroEntity?> GetRapportoAsync(
        Guid tenantId, Guid rapportoId, CancellationToken ct = default) =>
        _db.Rapporti.Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.Id == rapportoId && r.TenantId == tenantId, ct);

    /// <summary>Campi correggibili di dipendente + rapporto (es. dopo un import UNILAV/OCR impreciso). Null = non cambiare.</summary>
    public sealed record CorrezioneRapporto(
        string? Nome = null, string? Cognome = null, string? CodiceFiscale = null, string? Iban = null,
        string? CodiceCcnl = null, string? Livello = null, string? Qualifica = null,
        int? NumeroMensilita = null, bool? PartTime = null, decimal? PercentualePartTime = null,
        int? ScattiAnzianita = null, decimal? Superminimo = null,
        string? Regione = null, string? CodiceCatastaleComune = null, DateOnly? DataAssunzione = null,
        decimal? FondoTfrIniziale = null,
        decimal? FerieResidueIniziali = null, decimal? PermessiResiduiIniziali = null,
        DateOnly? DataCessazione = null, bool? RimuoviDataCessazione = null,
        string? TipoPartTimeInps = null, string? CittadinanzaInps = null,
        string? QualificaProfessionaleIstat = null, string? TipoContribuzioneInps = null,
        string? ProvinciaLavoro = null,
        decimal? AddizionaleRegionaleConguaglioAnnuo = null,
        decimal? AddizionaleComunaleSaldoAnnuo = null,
        decimal? AddizionaleComunaleAccontoAnnuo = null,
        string? Sesso = null,
        DateOnly? DataDecorrenzaDomicilioFiscale = null,
        string? RiferimentoProgressiviFiscaliPregressi = null,
        decimal? RolResiduiIniziali = null,
        decimal? ExFestivitaResidueIniziali = null,
        decimal? NettoConcordatoMensile = null,
        DateOnly? NettoConcordatoDecorrenza = null,
        bool? RimuoviNettoConcordato = null);

    /// <summary>
    /// Corregge i dati di un dipendente/rapporto (isolato per tenant). Pensato per rivedere ciò che
    /// l'import automatico (UNILAV, OCR) ha letto male. Restituisce il rapporto aggiornato,
    /// null se non esiste per il tenant. Un dipendente anonimizzato (GDPR) non è modificabile.
    /// </summary>
    public async Task<RapportoLavoroEntity?> CorreggiRapportoAsync(
        Guid tenantId, Guid rapportoId, CorrezioneRapporto c, CancellationToken ct = default)
    {
        var r = await _db.Rapporti.Include(x => x.Dipendente)
            .FirstOrDefaultAsync(x => x.Id == rapportoId && x.TenantId == tenantId, ct);
        if (r is null) return null;
        if (r.Dipendente is { Anonimizzato: true })
            throw new InvalidOperationException("Dipendente anonimizzato (GDPR): non è modificabile.");

        var d = r.Dipendente!;
        if (c.Nome is not null) d.Nome = c.Nome.Trim();
        if (c.Cognome is not null) d.Cognome = c.Cognome.Trim();
        if (c.CodiceFiscale is not null)
        {
            var nuovoCf = c.CodiceFiscale.Trim().ToUpperInvariant();
            if (!Paghe.Core.AdeTelematico.ValidatoreFiscale.CodiceFiscalePersonaFisicaValido(nuovoCf))
                throw new ArgumentException(
                    "Codice fiscale del lavoratore formalmente errato: il checksum non coincide.");
            d.CodiceFiscale = nuovoCf;
        }
        if (c.Iban is not null) d.Iban = c.Iban.Trim().Length == 0 ? null : c.Iban.Trim();
        if (c.Sesso is not null) d.Sesso = c.Sesso;
        // Riverifica anche quando cambia il solo codice fiscale: la coerenza vale sulla coppia.
        d.Sesso = SessoCoerente(d.Sesso, d.CodiceFiscale);

        if (c.CodiceCcnl is not null) r.CodiceCcnl = c.CodiceCcnl.Trim();
        if (c.Livello is not null) r.Livello = c.Livello.Trim();
        if (c.Qualifica is not null) r.Qualifica = c.Qualifica.Trim();
        if (c.NumeroMensilita is { } nm) r.NumeroMensilita = nm;
        if (c.PartTime is { } pt) r.PartTime = pt;
        if (c.PercentualePartTime is { } ppt) r.PercentualePartTime = ppt;
        if (c.ScattiAnzianita is { } sa) r.ScattiAnzianita = sa;
        if (c.Superminimo is { } sm) r.Superminimo = sm;
        // Netto concordato: vale solo dal mese corrente in avanti. Le buste dei mesi passati
        // non si ricostruiscono da un netto pattuito (stessa famiglia del blocco mesi futuri).
        if (c.RimuoviNettoConcordato == true)
        {
            r.NettoConcordatoMensile = null;
            r.NettoConcordatoDecorrenza = null;
        }
        else if (c.NettoConcordatoMensile is { } nettoConcordato)
        {
            if (nettoConcordato <= 0m)
                throw new ArgumentException("Il netto concordato deve essere un importo positivo.");
            var oggi = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var meseCorrente = new DateOnly(oggi.Year, oggi.Month, 1);
            var decorrenza = c.NettoConcordatoDecorrenza is { } dec
                ? new DateOnly(dec.Year, dec.Month, 1)
                : meseCorrente;
            if (decorrenza < meseCorrente)
                throw new ArgumentException(
                    "Il netto concordato vale dal mese corrente in avanti e non puo essere "
                    + "retroattivo: le buste dei mesi passati restano calcolate dal lordo.");
            r.NettoConcordatoMensile = nettoConcordato;
            r.NettoConcordatoDecorrenza = decorrenza;
        }
        if (c.Regione is not null) r.Regione = c.Regione.Trim();
        if (c.CodiceCatastaleComune is not null) r.CodiceCatastaleComune = c.CodiceCatastaleComune.Trim().ToUpperInvariant();
        // Provincia dell'unità operativa: stringa vuota = torna alla sede aziendale.
        if (c.ProvinciaLavoro is not null)
            r.ProvinciaLavoro = c.ProvinciaLavoro.Trim().Length == 0
                ? null
                : c.ProvinciaLavoro.Trim().ToUpperInvariant();
        if (c.DataAssunzione is { } da) r.DataAssunzione = da;

        // Addizionali da conguaglio dell'anno precedente: un importo negativo non ha senso;
        // zero è un dato vero («il conguaglio dice zero») e disattiva l'anteprima presuntiva.
        if (c.AddizionaleRegionaleConguaglioAnnuo is { } addReg)
            r.AddizionaleRegionaleConguaglioAnnuo = ImportoNonNegativo(addReg,
                "L'addizionale regionale da conguaglio non può essere negativa.");
        if (c.AddizionaleComunaleSaldoAnnuo is { } addComSaldo)
            r.AddizionaleComunaleSaldoAnnuo = ImportoNonNegativo(addComSaldo,
                "Il saldo dell'addizionale comunale non può essere negativo.");
        if (c.AddizionaleComunaleAccontoAnnuo is { } addComAcconto)
            r.AddizionaleComunaleAccontoAnnuo = ImportoNonNegativo(addComAcconto,
                "L'acconto dell'addizionale comunale non può essere negativo.");

        if (c.FondoTfrIniziale is { } ft) r.FondoTfrIniziale = ft;
        if (c.FerieResidueIniziali is { } ferie) r.FerieResidueIniziali = ferie;
        if (c.PermessiResiduiIniziali is { } permessi) r.PermessiResiduiIniziali = permessi;
        if (c.RolResiduiIniziali is { } rol) r.RolResiduiIniziali = rol;
        if (c.ExFestivitaResidueIniziali is { } exFest) r.ExFestivitaResidueIniziali = exFest;
        if (c.DataDecorrenzaDomicilioFiscale is { } domicilio)
            r.DataDecorrenzaDomicilioFiscale = domicilio;
        if (c.RiferimentoProgressiviFiscaliPregressi is not null)
            r.RiferimentoProgressiviFiscaliPregressi =
                string.IsNullOrWhiteSpace(c.RiferimentoProgressiviFiscaliPregressi)
                    ? null : c.RiferimentoProgressiviFiscaliPregressi.Trim();
        if (c.RimuoviDataCessazione == true) r.DataCessazione = null;
        else if (c.DataCessazione is { } dc) r.DataCessazione = dc;

        // Inquadramento contributivo UniEmens: stringa vuota = «togli il dato», un codice
        // scritto male viene rifiutato subito invece di far scartare la denuncia dall'INPS.
        if (c.TipoPartTimeInps is not null)
            r.TipoPartTimeInps = Codice(c.TipoPartTimeInps,
                AnagraficaContributiva.TipoPartTime,
                "Tipo di part-time non valido: usare P (orizzontale), V (verticale) o M (misto).");
        if (c.CittadinanzaInps is not null)
            r.CittadinanzaInps = Codice(c.CittadinanzaInps,
                AnagraficaContributiva.Cittadinanza,
                "Cittadinanza non valida: servono le tre cifre della codifica delle Comunicazioni Obbligatorie.");
        if (c.QualificaProfessionaleIstat is not null)
            r.QualificaProfessionaleIstat = Codice(c.QualificaProfessionaleIstat,
                AnagraficaContributiva.QualificaProfessionaleIstat,
                "Qualifica professionale ISTAT non valida: il formato è del tipo 8.1.3.3.0.2.");
        if (c.TipoContribuzioneInps is not null)
            r.TipoContribuzioneInps = Codice(c.TipoContribuzioneInps,
                AnagraficaContributiva.TipoContribuzione,
                "Tipo di contribuzione non valido: sono due caratteri della tabella INPS.");

        if (r.DataCessazione is { } cessazione && r.DataAssunzione != default && cessazione < r.DataAssunzione)
            throw new ArgumentException(
                $"La data di cessazione ({cessazione:dd/MM/yyyy}) non può precedere "
                + $"l'assunzione ({r.DataAssunzione:dd/MM/yyyy}).");

        await _db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>
    /// Normalizza un codice contributivo: vuoto lo cancella, valido lo tiene, sbagliato
    /// interrompe con un messaggio comprensibile a chi sta compilando la scheda.
    /// </summary>
    private static string? Codice(string valore, Func<string?, string?> normalizza, string errore)
    {
        if (valore.Trim().Length == 0) return null;
        return normalizza(valore) ?? throw new ArgumentException(errore);
    }

    private static decimal ImportoNonNegativo(decimal valore, string errore) =>
        valore < 0m ? throw new ArgumentException(errore) : valore;

    /// <summary>
    /// Normalizza il sesso ("M"/"F"; vuoto = non indicato) e lo verifica contro il codice
    /// fiscale: il giorno di nascita oltre 40 identifica una donna. Un'anagrafica incoerente
    /// va fermata qui, prima di finire su LUL, CU e denunce (revisione consulente 25/08/2026).
    /// Il secolo di nascita non incide sulla regola, quindi l'anno di riferimento è quello corrente.
    /// </summary>
    private static string? SessoCoerente(string? sesso, string? codiceFiscale)
    {
        var s = sesso?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(s))
            return null;
        if (s is not ("M" or "F"))
            throw new ArgumentException("Sesso non valido: usare M o F (vuoto = non indicato).");

        var dalCf = Paghe.Core.AdeTelematico.AnagraficaCodiceFiscale
            .Decodifica(codiceFiscale, DateTime.UtcNow.Year);
        if (dalCf is not null && dalCf.Sesso != s)
            throw new ArgumentException(
                "Il codice fiscale indica una persona di sesso "
                + (dalCf.Sesso == "F" ? "femminile" : "maschile")
                + $" e non corrisponde al sesso indicato ({s}): correggere l'uno o l'altro.");
        return s;
    }

    /// <summary>
    /// GDPR — diritto all'oblio (art. 17): anonimizza i dati personali del dipendente (nome, cognome,
    /// codice fiscale, IBAN) <b>mantenendo</b> i record contabili collegati (cedolini), soggetti a
    /// conservazione fiscale obbligatoria. È idempotente e isolato per tenant; l'audit log registra
    /// automaticamente l'operazione. Restituisce false se il dipendente non esiste.
    /// </summary>
    public async Task<bool> AnonimizzaDipendenteAsync(
        Guid tenantId, Guid dipendenteId, CancellationToken ct = default)
    {
        var dip = await _db.Dipendenti
            .FirstOrDefaultAsync(d => d.Id == dipendenteId && d.TenantId == tenantId, ct);
        if (dip is null) return false;
        if (dip.Anonimizzato) return true; // già anonimizzato: idempotente

        dip.Nome = "(anonimizzato)";
        dip.Cognome = "(anonimizzato)";
        dip.CodiceFiscale = $"ANON-{Guid.NewGuid():N}"; // resta univoco per (tenant, codice fiscale)
        dip.Iban = null;
        dip.Anonimizzato = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
