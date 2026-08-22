using System.Data;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Comando di salvataggio della sola bozza presenze. <paramref name="VersioneAttesa"/> vale
/// zero soltanto quando il mese non ha ancora un cartellino; in aggiornamento ID e versione
/// devono coincidere con ciò che l'utente ha riletto.
/// </summary>
public sealed record SalvaCartellinoBozza(
    Cartellino Cartellino,
    Guid? CartellinoIdAtteso,
    int VersioneAttesa);

/// <summary>Risultato autorevole riletto dopo il salvataggio della bozza.</summary>
public sealed record CartellinoBozzaSalvata(
    Guid Id,
    Guid TenantId,
    Guid AziendaId,
    Guid RapportoId,
    int Anno,
    int Mese,
    int Versione,
    IReadOnlyList<GiornataCartellino> Giornate);

/// <summary>La bozza è cambiata tra lettura e salvataggio: nessun dato viene sovrascritto.</summary>
public sealed class ConflittoCartellinoException : InvalidOperationException
{
    public ConflittoCartellinoException(string messaggio) : base(messaggio) { }
    public ConflittoCartellinoException(string messaggio, Exception causa) : base(messaggio, causa) { }
}

/// <summary>Il rapporto richiesto non appartiene al tenant corrente (risposta indistinguibile).</summary>
public sealed class RapportoCartellinoNonTrovatoException : InvalidOperationException
{
    public RapportoCartellinoNonTrovatoException()
        : base("Rapporto di lavoro non trovato.") { }
}

/// <summary>
/// Salva un cartellino come bozza autonoma, senza calcolare né archiviare un cedolino. È il
/// confine di dominio comune alla pagina Presenze e alla futura conferma Aurora A3.
/// </summary>
public sealed class CartellinoBozzaService
{
    private readonly PagheDbContext _db;
    private readonly TimeProvider _orologio;

    public CartellinoBozzaService(PagheDbContext db, TimeProvider? orologio = null)
    {
        _db = db;
        _orologio = orologio ?? TimeProvider.System;
    }

    public async Task<CartellinoBozzaSalvata> SalvaAsync(
        Guid tenantId,
        Guid rapportoId,
        SalvaCartellinoBozza richiesta,
        CancellationToken ct = default)
    {
        // La pagina Presenze usa questo metodo direttamente; Aurora A3 lo invocherà invece
        // dentro la propria transazione che comprende anche proposta, grant e audit. In entrambi
        // i casi controlli e scrittura devono condividere lo stesso confine atomico.
        if (_db.Database.CurrentTransaction is not null)
            return await SalvaNelContestoAsync(tenantId, rapportoId, richiesta, ct);

        await using var transazione = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            var salvata = await SalvaNelContestoAsync(tenantId, rapportoId, richiesta, ct);
            await transazione.CommitAsync(ct);
            return salvata;
        }
        catch
        {
            await transazione.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<CartellinoBozzaSalvata> SalvaNelContestoAsync(
        Guid tenantId,
        Guid rapportoId,
        SalvaCartellinoBozza richiesta,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty || rapportoId == Guid.Empty)
            throw new ArgumentException("Tenant e rapporto di lavoro sono obbligatori.");
        if (richiesta.VersioneAttesa < 0)
            throw new ArgumentException("La versione attesa del cartellino non è valida.");

        var cartellino = richiesta.Cartellino
            ?? throw new ArgumentException("Il cartellino è obbligatorio.");
        RegoleCartellino.Valida(cartellino);
        RegoleCartellino.BloccaPeriodoFuturo(cartellino, _orologio);
        if (cartellino.Giornate.Count == 0)
            throw new ArgumentException(
                "La bozza presenze richiede il calendario completo giorno per giorno.");

        var rapporto = await _db.Rapporti.AsNoTracking().FirstOrDefaultAsync(r =>
            r.Id == rapportoId && r.TenantId == tenantId, ct)
            ?? throw new RapportoCartellinoNonTrovatoException();
        RegoleCartellino.VerificaPeriodoRapporto(rapporto, cartellino);

        // Il cartellino e il cedolino devono raccontare lo stesso mese. Una volta calcolato un
        // cedolino, anche se ancora Bozza, la correzione passa dal ricalcolo manuale esistente.
        bool cedolinoEsistente = await _db.Cedolini.AsNoTracking().AnyAsync(c =>
            c.TenantId == tenantId
            && c.RapportoLavoroId == rapportoId
            && c.Anno == cartellino.Anno
            && c.Mese == cartellino.Mese, ct);
        if (cedolinoEsistente)
            throw new ArgumentException(
                $"Il cedolino {cartellino.Mese:00}/{cartellino.Anno} esiste già: "
                + "la bozza presenze non può renderlo incoerente. Usa il ricalcolo manuale.");

        bool f24Finale = await _db.PraticheF24.AsNoTracking().AnyAsync(p =>
            p.TenantId == tenantId
            && p.AziendaId == rapporto.AziendaId
            && p.Anno == cartellino.Anno
            && p.Mese == cartellino.Mese
            && (p.Stato == nameof(StatoPraticaTelematica.InviataManualmente)
                || p.Stato == nameof(StatoPraticaTelematica.RicevutaAcquisita)), ct);
        if (f24Finale)
            throw new ArgumentException(
                $"Il periodo {cartellino.Mese:00}/{cartellino.Anno} ha un F24 già inviato o "
                + "quietanzato: la bozza presenze resta bloccata.");

        // Le giornate sono la fonte: i totali del mese vengono sempre ricalcolati dal dominio.
        var normalizzato = CalendarioCartellino.Compila(
            cartellino, cartellino.Giornate, rapporto.OreSettimanali / 5m);
        // Alcune regole (per esempio il numero progressivo dell'evento di malattia) dipendono
        // dai totali derivati dal calendario e quindi possono essere verificate soltanto ora.
        RegoleCartellino.Valida(normalizzato);

        var esistente = await _db.Cartellini.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId
            && c.RapportoLavoroId == rapportoId
            && c.Anno == normalizzato.Anno
            && c.Mese == normalizzato.Mese, ct);

        CartellinoEntity entita;
        if (esistente is null)
        {
            if (richiesta.CartellinoIdAtteso is not null || richiesta.VersioneAttesa != 0)
                throw new ConflittoCartellinoException(
                    "La bozza letta non esiste più: ricarica il mese prima di salvare.");
            entita = new CartellinoEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RapportoLavoroId = rapportoId,
                Anno = normalizzato.Anno,
                Mese = normalizzato.Mese,
                Versione = 1,
            };
            Copia(normalizzato, entita);
            _db.Cartellini.Add(entita);
        }
        else
        {
            if (richiesta.CartellinoIdAtteso != esistente.Id
                || richiesta.VersioneAttesa != esistente.Versione)
                throw new ConflittoCartellinoException(
                    "Il cartellino è cambiato dopo la lettura: nessun dato è stato sovrascritto. "
                    + "Ricarica il mese e controlla di nuovo le modifiche.");
            if (esistente.Versione == int.MaxValue)
                throw new ConflittoCartellinoException(
                    "La versione del cartellino non è più incrementabile: serve assistenza tecnica.");

            Copia(normalizzato, esistente);
            esistente.Versione++;
            entita = esistente;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflittoCartellinoException(
                "Il cartellino è stato modificato contemporaneamente: ricarica il mese.", ex);
        }
        catch (DbUpdateException ex) when (esistente is null)
        {
            // Non trasformare qualunque errore di persistenza (schema, cifratura, FK) in un
            // innocuo 409. È una corsa di creazione solo se, dopo il rollback/savepoint EF,
            // esiste davvero la stessa bozza tenant-scoped.
            _db.Entry(entita).State = EntityState.Detached;
            bool creatoDaAltroSalvataggio = await _db.Cartellini.AsNoTracking().AnyAsync(c =>
                c.TenantId == tenantId
                && c.RapportoLavoroId == rapportoId
                && c.Anno == normalizzato.Anno
                && c.Mese == normalizzato.Mese, ct);
            if (creatoDaAltroSalvataggio)
                throw new ConflittoCartellinoException(
                    "Un altro salvataggio ha già creato il cartellino: ricarica il mese.", ex);
            throw;
        }

        // Rilettura esplicita: il chiamante non si fida del payload che ha inviato.
        _db.Entry(entita).State = EntityState.Detached;
        var salvata = await _db.Cartellini.AsNoTracking().FirstAsync(c =>
            c.Id == entita.Id && c.TenantId == tenantId, ct);
        return Proietta(salvata, rapporto.AziendaId);
    }

    public async Task<CartellinoBozzaSalvata?> LeggiAsync(
        Guid tenantId, Guid rapportoId, int anno, int mese, CancellationToken ct = default)
    {
        var rapporto = await _db.Rapporti.AsNoTracking().FirstOrDefaultAsync(r =>
            r.Id == rapportoId && r.TenantId == tenantId, ct);
        if (rapporto is null) return null;

        var entita = await _db.Cartellini.AsNoTracking().FirstOrDefaultAsync(c =>
            c.TenantId == tenantId
            && c.RapportoLavoroId == rapportoId
            && c.Anno == anno
            && c.Mese == mese, ct);
        return entita is null ? null : Proietta(entita, rapporto.AziendaId);
    }

    private static CartellinoBozzaSalvata Proietta(CartellinoEntity e, Guid aziendaId) => new(
        e.Id, e.TenantId, aziendaId, e.RapportoLavoroId,
        e.Anno, e.Mese, e.Versione, e.Giornate);

    private static void Copia(Cartellino c, CartellinoEntity e)
    {
        e.OreStraordinario = c.OreStraordinario;
        e.OreStraordinarioNotturno = c.OreStraordinarioNotturno;
        e.OreStraordinarioFestivo = c.OreStraordinarioFestivo;
        e.GiorniFerie = c.GiorniFerie;
        e.OrePermessi = c.OrePermessi;
        e.OreExFestivita = c.OreExFestivita;
        e.GiorniMalattia = c.GiorniMalattia;
        e.NumeroEventoMalattiaAnno = c.NumeroEventoMalattiaAnno;
        e.GiorniInfortunio = c.GiorniInfortunio;
        e.OreAssenzaNonRetribuita = c.OreAssenzaNonRetribuita;
        e.StatoAssegniFamiliari = c.StatoAssegniFamiliari.ToString();
        e.ImportoAssegniFamiliari = c.ImportoAssegniFamiliari;
        e.GiorniTrasfertaItalia = c.GiorniTrasfertaItalia;
        e.GiorniTrasfertaEstero = c.GiorniTrasfertaEstero;
        e.IndennitaTrasfertaGiornaliera = c.IndennitaTrasfertaGiornaliera;
        e.RimborsoTrasferta = c.RimborsoTrasferta.ToString();
        e.RimborsiSpeseDocumentate = c.RimborsiSpeseDocumentate;
        e.FringeBenefit = c.FringeBenefit;
        e.FringeBenefitGiaErogatoAnno = c.FringeBenefitGiaErogatoAnno;
        e.FigliACarico = c.FigliACarico;
        e.GiorniBuonoPasto = c.GiorniBuonoPasto;
        e.ValoreBuonoPasto = c.ValoreBuonoPasto;
        e.BuonoPastoElettronico = c.BuonoPastoElettronico;
        e.PremioRisultato = c.PremioRisultato;
        e.RedditoAnnoPrecedente = c.RedditoAnnoPrecedente;
        e.Giornate = c.Giornate;
    }

    /// <summary>
    /// Ricostruzione interna completa usata dal patcher A3. Non è un DTO HTTP/AI: può
    /// contenere dati sanitari e deve restare dentro il confine di persistenza.
    /// </summary>
    internal static Cartellino DaEntita(CartellinoEntity e) => new()
    {
        Anno = e.Anno,
        Mese = e.Mese,
        OreStraordinario = e.OreStraordinario,
        OreStraordinarioNotturno = e.OreStraordinarioNotturno,
        OreStraordinarioFestivo = e.OreStraordinarioFestivo,
        GiorniFerie = e.GiorniFerie,
        OrePermessi = e.OrePermessi,
        OreExFestivita = e.OreExFestivita,
        GiorniMalattia = e.GiorniMalattia,
        NumeroEventoMalattiaAnno = Math.Max(1, e.NumeroEventoMalattiaAnno),
        GiorniInfortunio = e.GiorniInfortunio,
        OreAssenzaNonRetribuita = e.OreAssenzaNonRetribuita,
        StatoAssegniFamiliari = Enum.TryParse<StatoAssegniFamiliari>(
            e.StatoAssegniFamiliari, out var anf) ? anf : StatoAssegniFamiliari.DaVerificare,
        ImportoAssegniFamiliari = e.ImportoAssegniFamiliari,
        GiorniTrasfertaItalia = e.GiorniTrasfertaItalia,
        GiorniTrasfertaEstero = e.GiorniTrasfertaEstero,
        IndennitaTrasfertaGiornaliera = e.IndennitaTrasfertaGiornaliera,
        RimborsoTrasferta = Enum.TryParse<TipoRimborsoTrasferta>(e.RimborsoTrasferta, out var rimborso)
            ? rimborso : TipoRimborsoTrasferta.Forfettario,
        RimborsiSpeseDocumentate = e.RimborsiSpeseDocumentate,
        FringeBenefit = e.FringeBenefit,
        FringeBenefitGiaErogatoAnno = e.FringeBenefitGiaErogatoAnno,
        FigliACarico = e.FigliACarico,
        GiorniBuonoPasto = e.GiorniBuonoPasto,
        ValoreBuonoPasto = e.ValoreBuonoPasto,
        BuonoPastoElettronico = e.BuonoPastoElettronico,
        PremioRisultato = e.PremioRisultato,
        RedditoAnnoPrecedente = e.RedditoAnnoPrecedente,
        Giornate = [.. e.Giornate.OrderBy(g => g.Data)],
    };
}

/// <summary>Regole condivise fra bozza presenze e calcolo del cedolino.</summary>
public static class RegoleCartellino
{
    public static void BloccaPeriodoFuturo(Cartellino c, TimeProvider orologio)
    {
        var oggi = DateOnly.FromDateTime(orologio.GetLocalNow().DateTime);
        if (c.Anno > oggi.Year || (c.Anno == oggi.Year && c.Mese > oggi.Month))
            throw new ArgumentException(
                $"Il periodo {c.Mese:00}/{c.Anno} è nel futuro: si può lavorare al massimo "
                + $"sul mese corrente ({oggi.Month:00}/{oggi.Year}).");
    }

    public static void VerificaPeriodoRapporto(RapportoLavoroEntity rapporto, Cartellino c)
    {
        var inizioMese = new DateOnly(c.Anno, c.Mese, 1);
        var fineMese = new DateOnly(c.Anno, c.Mese, DateTime.DaysInMonth(c.Anno, c.Mese));
        if (rapporto.DataAssunzione != default && fineMese < rapporto.DataAssunzione)
            throw new ArgumentException(
                $"Il periodo {c.Mese:00}/{c.Anno} precede la data di assunzione "
                + $"({rapporto.DataAssunzione:dd/MM/yyyy}).");
        if (rapporto.DataCessazione is { } cessazione && inizioMese > cessazione)
            throw new ArgumentException(
                $"Il periodo {c.Mese:00}/{c.Anno} è successivo alla cessazione "
                + $"({cessazione:dd/MM/yyyy}).");
    }

    public static void Valida(Cartellino c)
    {
        if (c.Anno is < 2000 or > 2100)
            throw new ArgumentException("L'anno del cartellino deve essere compreso tra 2000 e 2100.");
        if (c.Mese is < 1 or > 12)
            throw new ArgumentException("Il mese del cartellino deve essere compreso tra 1 e 12.");
        if (!Enum.IsDefined(c.StatoAssegniFamiliari))
            throw new ArgumentException("Lo stato degli assegni familiari non è riconosciuto.");
        if (!Enum.IsDefined(c.RimborsoTrasferta))
            throw new ArgumentException("Il tipo di rimborso trasferta non è riconosciuto.");
        if (c.NumeroEventoMalattiaAnno < 1)
            throw new ArgumentException(
                "Il numero progressivo dell'evento di malattia deve essere almeno uno.");
        if (c.GiorniBuonoPasto < 0)
            throw new ArgumentException("I giorni di buono pasto non possono essere negativi.");

        var quantita = new (string Nome, decimal Valore)[]
        {
            ("ore di straordinario", c.OreStraordinario),
            ("ore di straordinario notturno", c.OreStraordinarioNotturno),
            ("ore di straordinario festivo", c.OreStraordinarioFestivo),
            ("giorni di ferie", c.GiorniFerie),
            ("ore di permesso", c.OrePermessi),
            ("ore di ex-festivita", c.OreExFestivita),
            ("giorni di malattia", c.GiorniMalattia),
            ("giorni di infortunio", c.GiorniInfortunio),
            ("ore di assenza non retribuita", c.OreAssenzaNonRetribuita),
            ("assegni familiari", c.ImportoAssegniFamiliari),
            ("giorni di trasferta in Italia", c.GiorniTrasfertaItalia),
            ("giorni di trasferta all'estero", c.GiorniTrasfertaEstero),
            ("indennità di trasferta", c.IndennitaTrasfertaGiornaliera),
            ("rimborsi di spese documentate", c.RimborsiSpeseDocumentate),
            ("fringe benefit", c.FringeBenefit),
            ("fringe benefit già erogati", c.FringeBenefitGiaErogatoAnno),
            ("valore del buono pasto", c.ValoreBuonoPasto),
            ("premio di risultato", c.PremioRisultato),
            ("reddito dell'anno precedente", c.RedditoAnnoPrecedente),
        };
        var negativa = quantita.FirstOrDefault(x => x.Valore < 0m);
        if (negativa.Valore < 0m)
            throw new ArgumentException($"Il valore '{negativa.Nome}' non puo essere negativo.");

        int giorniMese = DateTime.DaysInMonth(c.Anno, c.Mese);
        decimal giorniConCausale = c.GiorniFerie + c.GiorniMalattia + c.GiorniInfortunio;
        if (giorniConCausale > giorniMese)
            throw new ArgumentException(
                $"Ferie, malattia e infortunio totalizzano {giorniConCausale:0.##} giorni, "
                + $"ma il mese ne contiene {giorniMese}.");
        if (c.StatoAssegniFamiliari == StatoAssegniFamiliari.NonSpettanti
            && c.ImportoAssegniFamiliari != 0m)
            throw new ArgumentException(
                "Gli assegni familiari sono non spettanti: l'importo deve essere zero.");
        if (c.StatoAssegniFamiliari == StatoAssegniFamiliari.Spettanti
            && c.ImportoAssegniFamiliari <= 0m)
            throw new ArgumentException(
                "Per gli assegni familiari spettanti indica un importo maggiore di zero.");
    }
}
