using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Cedolini;

/// <summary>Singolo dato mancante o incoerente che impedisce un'elaborazione archiviabile.</summary>
public sealed record BloccoCedolino(string Codice, string Messaggio);

/// <summary>
/// Gate fail-closed del prospetto paga. Il motore puro resta utilizzabile per simulazioni, ma
/// archivio e workflow non possono trasformare dati incompleti in un netto apparentemente
/// definitivo. Ogni query e vincolata al tenant.
/// </summary>
public sealed class RequisitiCedolinoService
{
    private readonly PagheDbContext _db;

    public RequisitiCedolinoService(PagheDbContext db) => _db = db;

    public async Task<IReadOnlyList<BloccoCedolino>> VerificaAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti.AsNoTracking()
            .Include(r => r.Dipendente)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == rapportoId, ct);
        if (rapporto is null)
            return [new("RAPPORTO_NON_TROVATO", "Rapporto di lavoro non trovato nel tenant corrente.")];

        var azienda = await _db.Aziende.AsNoTracking().FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Id == rapporto.AziendaId, ct);
        if (azienda is null)
            return [new("AZIENDA_NON_TROVATA", "Azienda del rapporto non trovata nel tenant corrente.")];

        var blocchi = new List<BloccoCedolino>();
        AggiungiBlocchiIdentificativi(blocchi, azienda, rapporto.Dipendente);
        AggiungiBlocchiDomicilio(blocchi, rapporto, cartellino);
        AggiungiBlocchiProfiloContributivo(blocchi, azienda);

        decimal oreGiornaliere = rapporto.OreSettimanali > 0m
            ? rapporto.OreSettimanali / 5m
            : 0m;
        decimal? riportoPrimaSettimana = await RiportoOreSettimanaliService.CalcolaAsync(
            _db, tenantId, rapportoId, cartellino.Anno, cartellino.Mese,
            rapporto.DataAssunzione, ct);
        foreach (var problema in CalendarioCartellino.Verifica(
                     cartellino.Anno, cartellino.Mese, cartellino.Giornate, oreGiornaliere,
                     oreLavoratePregressePrimaSettimana: riportoPrimaSettimana)
                 .Where(p => p.Bloccante))
            blocchi.Add(new("CALENDARIO", problema.Messaggio));

        if (cartellino.StatoAssegniFamiliari == StatoAssegniFamiliari.DaVerificare)
            blocchi.Add(new("ANF_NON_VERIFICATO",
                "Indicare esplicitamente se gli assegni familiari spettano nel mese."));

        await AggiungiBlocchiProgressiviAsync(blocchi, tenantId, rapporto, cartellino, ct);

        string? codiceCnel = await _db.CcnlVersioni.AsNoTracking()
            .Where(v => v.CcnlCodice == rapporto.CodiceCcnl && v.Anno == cartellino.Anno)
            .Select(v => v.CodiceCnel)
            .FirstOrDefaultAsync(ct);
        if (string.Equals(codiceCnel, "H011", StringComparison.OrdinalIgnoreCase))
            AggiungiBlocchiH011(blocchi, azienda, rapporto, cartellino);

        return blocchi;
    }

    public async Task RichiediCompletiAsync(
        Guid tenantId, Guid rapportoId, Cartellino cartellino, CancellationToken ct = default)
    {
        var blocchi = await VerificaAsync(tenantId, rapportoId, cartellino, ct);
        if (blocchi.Count == 0)
            return;

        throw new ArgumentException(
            "Elaborazione archiviabile bloccata; il risultato resta soltanto una simulazione. "
            + string.Join(" ", blocchi.Select(b => $"[{b.Codice}] {b.Messaggio}")));
    }

    public async Task RichiediCedolinoCompletoAsync(
        Guid tenantId, Guid cedolinoId, CancellationToken ct = default)
    {
        var cedolino = await _db.Cedolini.AsNoTracking().FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Id == cedolinoId, ct)
            ?? throw new CedolinoNonTrovatoException();
        var cartellino = await _db.Cartellini.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.RapportoLavoroId == cedolino.RapportoLavoroId
            && c.Anno == cedolino.Anno && c.Mese == cedolino.Mese, ct)
            ?? throw new ArgumentException("Cartellino del prospetto non trovato.");
        await RichiediCompletiAsync(
            tenantId, cedolino.RapportoLavoroId, CedoliniService.ToDominioCartellino(cartellino), ct);
    }

    private static void AggiungiBlocchiIdentificativi(
        ICollection<BloccoCedolino> blocchi, AziendaEntity azienda, DipendenteEntity? dipendente)
    {
        if (string.IsNullOrWhiteSpace(azienda.PartitaIva)
            || !ValidatoreFiscale.PartitaIvaValida(azienda.PartitaIva))
            blocchi.Add(new("PIVA_CHECKSUM", "La Partita IVA aziendale manca o ha checksum errato."));
        if (string.IsNullOrWhiteSpace(azienda.CodiceFiscale)
            || !ValidatoreFiscale.CodiceFiscaleValido(azienda.CodiceFiscale))
            blocchi.Add(new("CF_AZIENDA_CHECKSUM", "Il codice fiscale aziendale manca o ha checksum errato."));
        if (dipendente is null || !ValidatoreFiscale.CodiceFiscalePersonaFisicaValido(dipendente.CodiceFiscale))
            blocchi.Add(new("CF_DIPENDENTE_CHECKSUM", "Il codice fiscale del lavoratore manca o ha checksum errato."));
    }

    private static void AggiungiBlocchiDomicilio(
        ICollection<BloccoCedolino> blocchi, RapportoLavoroEntity rapporto, Cartellino cartellino)
    {
        if (string.IsNullOrWhiteSpace(rapporto.Regione)
            || string.IsNullOrWhiteSpace(rapporto.CodiceCatastaleComune))
            blocchi.Add(new("DOMICILIO_FISCALE",
                "Mancano regione o comune del domicilio fiscale per le addizionali."));
        var ultimoGiorno = new DateOnly(
            cartellino.Anno, cartellino.Mese, DateTime.DaysInMonth(cartellino.Anno, cartellino.Mese));
        if (rapporto.DataDecorrenzaDomicilioFiscale is null
            || rapporto.DataDecorrenzaDomicilioFiscale > ultimoGiorno)
            blocchi.Add(new("DECORRENZA_DOMICILIO",
                "Manca una decorrenza valida del domicilio fiscale."));
        if (rapporto.AddizionaleRegionaleConguaglioAnnuo is null)
            blocchi.Add(new("CONGUAGLIO_REGIONALE", "Manca il saldo annuale dell'addizionale regionale."));
        if (rapporto.AddizionaleComunaleSaldoAnnuo is null)
            blocchi.Add(new("SALDO_COMUNALE", "Manca il saldo annuale dell'addizionale comunale."));
        if (rapporto.AddizionaleComunaleAccontoAnnuo is null)
            blocchi.Add(new("ACCONTO_COMUNALE", "Manca l'acconto annuale dell'addizionale comunale."));
    }

    private static void AggiungiBlocchiProfiloContributivo(
        ICollection<BloccoCedolino> blocchi, AziendaEntity azienda)
    {
        void Richiedi(string? valore, string codice, string messaggio)
        {
            if (string.IsNullOrWhiteSpace(valore)) blocchi.Add(new(codice, messaggio));
        }

        Richiedi(azienda.MatricolaInps, "MATRICOLA_INPS", "Manca la matricola INPS.");
        Richiedi(azienda.CodiceSedeInps, "SEDE_INPS", "Manca il codice sede INPS.");
        Richiedi(azienda.CscInps, "CSC_INPS", "Manca il CSC INPS.");
        Richiedi(azienda.CodiciAutorizzazioneInps, "CA_INPS",
            "Indicare i codici di autorizzazione INPS oppure NESSUNO.");
        Richiedi(azienda.CodiceFondoIntegrazioneSalariale, "FONDO_INTEGRAZIONE",
            "Indicare FIS/altro fondo di integrazione oppure NESSUNO.");
        Richiedi(azienda.CodiceAgevolazioneContributiva, "AGEVOLAZIONE_CONTRIBUTIVA",
            "Indicare l'agevolazione contributiva oppure NESSUNA.");
        Richiedi(azienda.FonteProfiloContributivo, "FONTE_PROFILO_INPS",
            "Manca la fonte del profilo contributivo risolto da CSC, CA, organico, FIS e agevolazioni.");
        if (azienda.AliquotaInpsDipendente is not > 0m)
            blocchi.Add(new("ALIQUOTA_INPS_DIPENDENTE", "Manca l'aliquota c/dipendente del profilo reale."));
        if (azienda.AliquotaInpsDatore is not > 0m)
            blocchi.Add(new("ALIQUOTA_INPS_DATORE", "Manca l'aliquota c/datore del profilo reale."));
    }

    private async Task AggiungiBlocchiProgressiviAsync(
        ICollection<BloccoCedolino> blocchi,
        Guid tenantId,
        RapportoLavoroEntity rapporto,
        Cartellino cartellino,
        CancellationToken ct)
    {
        if (cartellino.Mese <= 1)
            return;

        int primoMeseAtteso = rapporto.DataAssunzione.Year == cartellino.Anno
            ? rapporto.DataAssunzione.Month
            : 1;
        int attesi = Math.Max(0, cartellino.Mese - primoMeseAtteso);
        int presenti = await _db.Cedolini.UtilizzabiliAValle()
            .Where(c => c.TenantId == tenantId && c.RapportoLavoroId == rapporto.Id
                && c.Anno == cartellino.Anno && c.Mese >= primoMeseAtteso
                && c.Mese < cartellino.Mese)
            .Select(c => c.Mese)
            .Distinct()
            .CountAsync(ct);
        if (presenti < attesi && string.IsNullOrWhiteSpace(rapporto.RiferimentoProgressiviFiscaliPregressi))
            blocchi.Add(new("PROGRESSIVI_FISCALI",
                $"Progressivi fiscali incompleti: presenti {presenti} mesi su {attesi}; "
                + "caricare i mesi precedenti o il riferimento certificato di importazione."));
    }

    private static void AggiungiBlocchiH011(
        ICollection<BloccoCedolino> blocchi,
        AziendaEntity azienda,
        RapportoLavoroEntity rapporto,
        Cartellino cartellino)
    {
        if (string.IsNullOrWhiteSpace(azienda.CodiceAteco))
            blocchi.Add(new("H011_ATECO", "H011: manca il codice ATECO dell'attivita prevalente."));
        if (string.IsNullOrWhiteSpace(azienda.RiferimentoVisuraCamerale))
            blocchi.Add(new("H011_VISURA", "H011: manca il riferimento della visura camerale verificata."));
        if (!string.Equals(azienda.StatoAlboArtigiani, "NonIscritta", StringComparison.OrdinalIgnoreCase))
            blocchi.Add(new("H011_ALBO_ARTIGIANI",
                "H011 per autoriparazione richiede la verifica di non iscrizione all'Albo artigiani."));
        if (string.IsNullOrWhiteSpace(azienda.RiferimentoVerificaApplicabilitaCcnl))
            blocchi.Add(new("H011_APPLICABILITA",
                "Manca il riferimento della verifica tra attivita prevalente e campo H011."));
        string provincia = string.IsNullOrWhiteSpace(rapporto.ProvinciaLavoro)
            ? azienda.Provincia
            : rapporto.ProvinciaLavoro;
        if (string.Equals(provincia, "MI", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(azienda.RiferimentoTerzoElementoTerritoriale))
            blocchi.Add(new("H011_TERZO_ELEMENTO_MI",
                "Il terzo elemento Milano richiede una fonte territoriale documentata."));

        if (cartellino.Mese == 7 && rapporto.NumeroMensilita >= 14)
        {
            if (string.IsNullOrWhiteSpace(cartellino.RiferimentoQuattordicesimaSeparata))
                blocchi.Add(new("QUATTORDICESIMA_SEPARATA",
                    "Manca il riferimento del prospetto separato della quattordicesima."));
            if (cartellino.DataPagamentoQuattordicesima
                != new DateOnly(cartellino.Anno, 7, 1))
                blocchi.Add(new("DATA_QUATTORDICESIMA",
                    "La quattordicesima H011 deve essere documentata con pagamento al 1 luglio."));
        }
    }
}
