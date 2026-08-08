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
/// Risultato del calcolo: testata del cedolino con i totali e l'elenco delle voci.
/// Tutti gli importi sono in euro, arrotondati al centesimo.
/// </summary>
public sealed class Cedolino
{
    public required int Anno { get; init; }
    public required int Mese { get; init; }

    public List<CedolinoRiga> Righe { get; } = new();

    public decimal TotaleCompetenze { get; set; }
    public decimal TotaleTrattenute { get; set; }

    public decimal ImponibileInps { get; set; }
    public decimal ContributiDipendente { get; set; }
    public decimal ContributiDatore { get; set; }

    public decimal ImponibileIrpef { get; set; }
    public decimal IrpefLorda { get; set; }
    public decimal DetrazioniLavoroDipendente { get; set; }
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

    /// <summary>Costo aziendale totale del lavoratore (lordo + contributi c/datore + quota TFR).</summary>
    public decimal CostoAzienda { get; set; }

    // Saldi Ferie e Permessi ROL
    public decimal FerieMaturate { get; set; }
    public decimal FerieGodute { get; set; }
    public decimal FerieResidue { get; set; }

    public decimal PermessiMaturati { get; set; }
    public decimal PermessiGoduti { get; set; }
    public decimal PermessiResidui { get; set; }
}
