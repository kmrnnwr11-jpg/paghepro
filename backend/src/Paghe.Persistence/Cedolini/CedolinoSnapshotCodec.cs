using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paghe.Core.Domain;
using Paghe.Core.Parametri;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Cedolini;

/// <summary>
/// Serializzazione deterministica della fotografia paga. I record sono espliciti e le raccolte
/// vengono ordinate: l'hash deve dipendere dai dati, non dall'ordine casuale di un dizionario.
/// </summary>
internal static class CedolinoSnapshotCodec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string SerializzaParametri(
        ParametriNormativi p,
        Ccnl ccnl,
        RapportoLavoroEntity rapporto,
        AziendaEntity? azienda)
    {
        var qualifica = Enum.Parse<Qualifica>(rapporto.Qualifica);
        p.AliquoteInps.TryGetValue(qualifica, out var aliquotaInps);
        p.AddizionaleRegionale.TryGetValue(rapporto.Regione, out var aliquotaRegionale);
        p.AddizionaleComunale.TryGetValue(rapporto.CodiceCatastaleComune, out var aliquotaComunale);
        p.RegimiAddizionaleRegionale.TryGetValue(rapporto.Regione, out var regimeRegionale);
        p.RegimiAddizionaleComunale.TryGetValue(rapporto.CodiceCatastaleComune, out var regimeComunale);
        p.RegoleCcnl.TryGetValue(ccnl.Codice, out var regoleCcnl);
        var livello = ccnl.Livello(rapporto.Livello);
        var provinciaLavoro = string.IsNullOrWhiteSpace(rapporto.ProvinciaLavoro)
            ? azienda?.Provincia
            : rapporto.ProvinciaLavoro;

        var payload = new
        {
            versioneSchema = 1,
            anno = p.Anno,
            qualifica,
            aliquotaInps,
            scaglioniIrpef = p.ScaglioniIrpef
                .OrderBy(x => x.DaImporto)
                .Select(x => new { x.DaImporto, x.AImporto, x.Aliquota })
                .ToList(),
            addizionaleRegionale = new
            {
                territorio = rapporto.Regione,
                aliquotaPiatta = aliquotaRegionale,
                regime = regimeRegionale,
            },
            addizionaleComunale = new
            {
                territorio = rapporto.CodiceCatastaleComune,
                aliquotaPiatta = aliquotaComunale,
                regime = regimeComunale,
            },
            p.DivisoreOrario,
            p.DivisoreGiornaliero,
            p.MaggiorazioneStraordinario,
            p.MaggiorazioneStraordinarioNotturno,
            p.MaggiorazioneStraordinarioFestivo,
            p.GiorniFerieMese,
            p.OrePermessiMese,
            p.TfrDivisore,
            p.TfrContributoFondo,
            p.CuneoUlterioreDetrazioneAnnua,
            p.CuneoRedditoOltre,
            p.CuneoRedditoFinoA,
            p.CuneoRedditoAzzeramento,
            p.TrasfertaItaliaEsenteGiorno,
            p.TrasfertaEsteroEsenteGiorno,
            p.FringeBenefitSogliaAnnua,
            p.FringeBenefitSogliaAnnuaConFigli,
            p.BuonoPastoElettronicoEsenteGiorno,
            p.BuonoPastoCartaceoEsenteGiorno,
            p.PremioRisultatoLimiteAnnuo,
            p.PremioRisultatoAliquota,
            p.PremioRisultatoRedditoMassimo,
            ccnl = new
            {
                ccnl.Codice,
                ccnl.Descrizione,
                ccnl.CodiceCnel,
                ccnl.Fonte,
                livello,
                regole = regoleCcnl,
                terzoElementoApplicato = ccnl.TerzoElemento(provinciaLavoro),
                terzoElementoProvinciale = ccnl.TerzoElementoProvinciale
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new { provincia = x.Key, x.Value })
                    .ToList(),
            },
            rapporto = new
            {
                rapporto.PartTime,
                rapporto.PercentualePartTime,
                rapporto.NumeroMensilita,
                rapporto.ScattiAnzianita,
                rapporto.Superminimo,
                rapporto.DataAssunzione,
                ProvinciaLavoro = provinciaLavoro,
                rapporto.DetrazioniLavoroDipendente,
                rapporto.GiorniDetrazioneAnno,
                rapporto.DataDecorrenzaDomicilioFiscale,
                rapporto.RiferimentoProgressiviFiscaliPregressi,
            },
            azienda = new
            {
                OltreQuindiciDipendenti = azienda?.OltreQuindiciDipendenti ?? false,
                ContributiFondiSettore = azienda?.ContributiFondiSettore ?? 0m,
                azienda?.CscInps,
                azienda?.CodiciAutorizzazioneInps,
                azienda?.CodiceFondoIntegrazioneSalariale,
                azienda?.CodiceAgevolazioneContributiva,
                azienda?.AliquotaInpsDipendente,
                azienda?.AliquotaInpsDatore,
                azienda?.FonteProfiloContributivo,
            },
        };

        return JsonSerializer.Serialize(payload, Json);
    }

    internal static string SerializzaSnapshot(CedolinoPerPdf dati, string parametriApplicatiJson)
    {
        var c = dati.Cedolino;
        var payload = new SnapshotCedolinoV1(
            VersioneSchema: 1,
            Cedolino: new DatiCedolinoV1(
                c.Anno, c.Mese, c.Righe.ToList(), c.TotaleCompetenze, c.TotaleTrattenute,
                c.ImponibileInps, c.ContributiDipendente, c.ContributiDatore,
                c.ImponibileIrpef, c.IrpefLorda, c.DetrazioniLavoroDipendente, c.IrpefNetta,
                c.AddizionaleRegionale, c.AddizionaleComunale, c.NettoInBusta,
                c.TotaleEsenti, c.PremioRisultatoAgevolato, c.ImpostaSostitutivaPremio,
                c.QuotaTfr, c.CostoAzienda, c.FerieMaturate, c.FerieGodute, c.FerieResidue,
                c.PermessiMaturati, c.PermessiGoduti, c.PermessiResidui,
                c.CostoMedioAnnualizzato,
                c.UlterioreDetrazioneCuneo,
                c.RetribuzioneFissa, c.TariffaOraria, c.TariffaGiornaliera,
                c.PrestazioniRecuperabili,
                c.RolSaldoPrecedente, c.RolMaturati, c.RolGoduti, c.RolResidui,
                c.ExFestivitaSaldoPrecedente, c.ExFestivitaMaturate,
                c.ExFestivitaGodute, c.ExFestivitaResidue),
            dati.Intestazione,
            dati.Cartellino,
            parametriApplicatiJson,
            dati.Progressivi);
        return JsonSerializer.Serialize(payload, Json);
    }

    internal static CedolinoPerPdf DeserializzaSnapshot(string json)
    {
        var payload = JsonSerializer.Deserialize<SnapshotCedolinoV1>(json, Json)
            ?? throw new InvalidOperationException("Snapshot cedolino vuoto o non leggibile.");
        if (payload.VersioneSchema != 1)
            throw new InvalidOperationException(
                $"Versione snapshot cedolino non supportata: {payload.VersioneSchema}.");

        var d = payload.Cedolino;
        var cedolino = new Cedolino
        {
            Anno = d.Anno,
            Mese = d.Mese,
            RetribuzioneFissa = d.RetribuzioneFissa ?? 0m,
            TariffaOraria = d.TariffaOraria ?? 0m,
            TariffaGiornaliera = d.TariffaGiornaliera ?? 0m,
            TotaleCompetenze = d.TotaleCompetenze,
            TotaleTrattenute = d.TotaleTrattenute,
            ImponibileInps = d.ImponibileInps,
            ContributiDipendente = d.ContributiDipendente,
            ContributiDatore = d.ContributiDatore,
            ImponibileIrpef = d.ImponibileIrpef,
            IrpefLorda = d.IrpefLorda,
            DetrazioniLavoroDipendente = d.DetrazioniLavoroDipendente,
            IrpefNetta = d.IrpefNetta,
            AddizionaleRegionale = d.AddizionaleRegionale,
            AddizionaleComunale = d.AddizionaleComunale,
            NettoInBusta = d.NettoInBusta,
            TotaleEsenti = d.TotaleEsenti,
            PremioRisultatoAgevolato = d.PremioRisultatoAgevolato,
            ImpostaSostitutivaPremio = d.ImpostaSostitutivaPremio,
            QuotaTfr = d.QuotaTfr,
            PrestazioniRecuperabili = d.PrestazioniRecuperabili ?? 0m,
            CostoAzienda = d.CostoAzienda,
            CostoMedioAnnualizzato = d.CostoMedioAnnualizzato ?? 0m,
            UlterioreDetrazioneCuneo = d.UlterioreDetrazioneCuneo ?? 0m,
            FerieMaturate = d.FerieMaturate,
            FerieGodute = d.FerieGodute,
            FerieResidue = d.FerieResidue,
            PermessiMaturati = d.PermessiMaturati,
            PermessiGoduti = d.PermessiGoduti,
            PermessiResidui = d.PermessiResidui,
            RolSaldoPrecedente = d.RolSaldoPrecedente ?? 0m,
            RolMaturati = d.RolMaturati ?? 0m,
            RolGoduti = d.RolGoduti ?? 0m,
            RolResidui = d.RolResidui ?? 0m,
            ExFestivitaSaldoPrecedente = d.ExFestivitaSaldoPrecedente ?? 0m,
            ExFestivitaMaturate = d.ExFestivitaMaturate ?? 0m,
            ExFestivitaGodute = d.ExFestivitaGodute ?? 0m,
            ExFestivitaResidue = d.ExFestivitaResidue ?? 0m,
        };
        cedolino.Righe.AddRange(d.Righe);
        return new CedolinoPerPdf(cedolino, payload.Intestazione, payload.Cartellino, payload.Progressivi);
    }

    internal static string HashTesto(string testo) => Hash(Encoding.UTF8.GetBytes(testo));

    internal static string Hash(byte[] dati) => Convert.ToHexString(SHA256.HashData(dati));

    private sealed record SnapshotCedolinoV1(
        int VersioneSchema,
        DatiCedolinoV1 Cedolino,
        IntestazioneCedolino Intestazione,
        Cartellino Cartellino,
        string ParametriApplicatiJson,
        // Congelati al controllo, come il resto della fotografia. Facoltativi: gli snapshot
        // creati prima restano leggibili e mostrano i progressivi come non disponibili.
        ProgressiviAnnui? Progressivi = null);

    private sealed record DatiCedolinoV1(
        int Anno,
        int Mese,
        List<CedolinoRiga> Righe,
        decimal TotaleCompetenze,
        decimal TotaleTrattenute,
        decimal ImponibileInps,
        decimal ContributiDipendente,
        decimal ContributiDatore,
        decimal ImponibileIrpef,
        decimal IrpefLorda,
        decimal DetrazioniLavoroDipendente,
        decimal IrpefNetta,
        decimal AddizionaleRegionale,
        decimal AddizionaleComunale,
        decimal NettoInBusta,
        decimal TotaleEsenti,
        decimal PremioRisultatoAgevolato,
        decimal ImpostaSostitutivaPremio,
        decimal QuotaTfr,
        decimal CostoAzienda,
        decimal FerieMaturate,
        decimal FerieGodute,
        decimal FerieResidue,
        decimal PermessiMaturati,
        decimal PermessiGoduti,
        decimal PermessiResidui,
        // Campi aggiunti in modo opzionale: gli snapshot V1 creati prima delle revisioni
        // restano deserializzabili e mostrano i dati mancanti come non disponibili.
        decimal? CostoMedioAnnualizzato = null,
        decimal? UlterioreDetrazioneCuneo = null,
        decimal? RetribuzioneFissa = null,
        decimal? TariffaOraria = null,
        decimal? TariffaGiornaliera = null,
        decimal? PrestazioniRecuperabili = null,
        decimal? RolSaldoPrecedente = null,
        decimal? RolMaturati = null,
        decimal? RolGoduti = null,
        decimal? RolResidui = null,
        decimal? ExFestivitaSaldoPrecedente = null,
        decimal? ExFestivitaMaturate = null,
        decimal? ExFestivitaGodute = null,
        decimal? ExFestivitaResidue = null);
}
