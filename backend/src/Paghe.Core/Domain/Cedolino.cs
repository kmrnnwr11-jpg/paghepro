namespace Paghe.Core.Domain;

/// <summary>Singola riga (voce) del cedolino.</summary>
public sealed class CedolinoRiga
{
    public required string Codice { get; init; }
    public required string Descrizione { get; init; }
    public required TipoVoce Tipo { get; init; }

    /// <summary>Quantità (ore, giorni, ratei). Facoltativa.</summary>
    public decimal? Quantita { get; init; }

    /// <summary>Base di calcolo della voce. Facoltativa.</summary>
    public decimal? Base { get; init; }

    /// <summary>Importo con segno: positivo per competenze, negativo per trattenute.</summary>
    public required decimal Importo { get; init; }
}

/// <summary>
/// Somme progressive dell'anno fino al mese della busta (mese compreso), lette dai cedolini
/// elaborati in archivio: rispondono alla fascia «progressivi annui» del prospetto e sono la
/// base dei conguagli. <see cref="Detrazioni"/> comprende anche l'ulteriore detrazione cuneo.
/// </summary>
public sealed record ProgressiviAnnui(
    int MesiCompresi,
    decimal ImponibileInps,
    decimal ImponibileIrpef,
    decimal IrpefNetta,
    decimal Detrazioni);

/// <summary>
/// Risultato del calcolo: testata del cedolino con i totali e l'elenco delle voci.
/// Tutti gli importi sono in euro, arrotondati al centesimo.
/// </summary>
public sealed class Cedolino
{
    public required int Anno { get; init; }
    public required int Mese { get; init; }

    public List<CedolinoRiga> Righe { get; } = new();

    /// <summary>Retribuzione fissa mensile effettiva usata nel calcolo.</summary>
    public decimal RetribuzioneFissa { get; set; }

    /// <summary>Tariffa oraria non arrotondata prematuramente; la stampa mostra almeno 4 decimali.</summary>
    public decimal TariffaOraria { get; set; }

    /// <summary>Tariffa giornaliera non arrotondata prematuramente; la stampa mostra almeno 4 decimali.</summary>
    public decimal TariffaGiornaliera { get; set; }

    public decimal TotaleCompetenze { get; set; }
    public decimal TotaleTrattenute { get; set; }

    public decimal ImponibileInps { get; set; }
    public decimal ContributiDipendente { get; set; }
    public decimal ContributiDatore { get; set; }

    public decimal ImponibileIrpef { get; set; }
    public decimal IrpefLorda { get; set; }
    public decimal DetrazioniLavoroDipendente { get; set; }

    /// <summary>
    /// Ulteriore detrazione «cuneo fiscale» (L. 207/2024) del mese: casella distinta dalla
    /// detrazione da lavoro dipendente, non vanno sommate nella stessa voce.
    /// </summary>
    public decimal UlterioreDetrazioneCuneo { get; set; }
    public decimal IrpefNetta { get; set; }

    public decimal AddizionaleRegionale { get; set; }
    public decimal AddizionaleComunale { get; set; }

    public decimal NettoInBusta { get; set; }

    /// <summary>
    /// Somme erogate in esenzione (trasferta entro soglia, rimborsi documentati, fringe benefit
    /// e buoni pasto entro i limiti): entrano nel netto ma non nell'imponibile.
    /// </summary>
    public decimal TotaleEsenti { get; set; }

    /// <summary>Quota di premio di risultato assoggettata a imposta sostitutiva.</summary>
    public decimal PremioRisultatoAgevolato { get; set; }

    /// <summary>Imposta sostitutiva trattenuta sul premio di risultato.</summary>
    public decimal ImpostaSostitutivaPremio { get; set; }

    public decimal QuotaTfr { get; set; }

    /// <summary>Prestazioni INPS/INAIL anticipate e recuperabili, escluse dal costo aziendale.</summary>
    public decimal PrestazioniRecuperabili { get; set; }

    /// <summary>Costo aziendale totale del lavoratore (lordo + contributi c/datore + quota TFR).</summary>
    public decimal CostoAzienda { get; set; }

    /// <summary>
    /// Costo medio mensile annualizzato: retribuzione contrattuale annua (13ª/14ª comprese) con
    /// contributi c/datore e TFR, divisa per 12. Da non confondere con <see cref="CostoAzienda"/>,
    /// che è il costo del singolo mese e non contiene i ratei delle mensilità aggiuntive
    /// (distinzione chiesta dalla revisione del consulente, 24/08/2026).
    /// </summary>
    public decimal CostoMedioAnnualizzato { get; set; }

    // Saldi Ferie e Permessi ROL
    public decimal FerieMaturate { get; set; }
    public decimal FerieGodute { get; set; }
    public decimal FerieResidue { get; set; }

    public decimal PermessiMaturati { get; set; }
    public decimal PermessiGoduti { get; set; }
    public decimal PermessiResidui { get; set; }

    public decimal RolSaldoPrecedente { get; set; }
    public decimal RolMaturati { get; set; }
    public decimal RolGoduti { get; set; }
    public decimal RolResidui { get; set; }

    public decimal ExFestivitaSaldoPrecedente { get; set; }
    public decimal ExFestivitaMaturate { get; set; }
    public decimal ExFestivitaGodute { get; set; }
    public decimal ExFestivitaResidue { get; set; }
}
