namespace Paghe.Persistence.Entities;

// Parametri normativi versionati per anno. Aggiornarli = inserire righe per il nuovo anno,
// mai modificare quelle storiche, così i cedolini passati restano ricalcolabili.

/// <summary>Parametri scalari dell'anno (divisori, maggiorazioni, parametri TFR).</summary>
public class ParametriAnnoEntity
{
    public int Anno { get; set; } // chiave

    public decimal DivisoreOrario { get; set; }
    public decimal MaggiorazioneStraordinario { get; set; }
    public decimal MaggiorazioneStraordinarioNotturno { get; set; }
    public decimal MaggiorazioneStraordinarioFestivo { get; set; }
    public decimal TfrDivisore { get; set; }
    public decimal TfrContributoFondo { get; set; }

    /// <summary>Variazione ISTAT per la rivalutazione TFR dell'anno (es. 0,01 = +1%). Null se non ancora inserita.</summary>
    public decimal? VariazioneIstatTfr { get; set; }

    // ---- Limiti degli elementi retributivi agevolati -------------------------------
    // Soglie di legge, quindi versionate per anno come tutto il resto. Nullable perché i
    // database creati prima di questa colonna non le hanno: in quel caso valgono i default
    // di ParametriNormativi, e restano modificabili senza toccare il codice.
    public decimal? TrasfertaItaliaEsenteGiorno { get; set; }
    public decimal? TrasfertaEsteroEsenteGiorno { get; set; }
    public decimal? FringeBenefitSogliaAnnua { get; set; }
    public decimal? FringeBenefitSogliaAnnuaConFigli { get; set; }
    public decimal? BuonoPastoElettronicoEsenteGiorno { get; set; }
    public decimal? BuonoPastoCartaceoEsenteGiorno { get; set; }
    public decimal? PremioRisultatoLimiteAnnuo { get; set; }
    public decimal? PremioRisultatoAliquota { get; set; }
    public decimal? PremioRisultatoRedditoMassimo { get; set; }
}

/// <summary>Aliquota contributiva INPS per anno e qualifica.</summary>
public class InpsAliquotaEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }
    public string Qualifica { get; set; } = ""; // nome dell'enum Qualifica
    public decimal Dipendente { get; set; }
    public decimal Datore { get; set; }
}

/// <summary>Scaglione IRPEF per anno.</summary>
public class IrpefScaglioneEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }
    public decimal DaImporto { get; set; }
    public decimal? AImporto { get; set; }
    public decimal Aliquota { get; set; }
}

/// <summary>Aliquota addizionale regionale IRPEF per anno e regione.</summary>
public class AddizionaleRegionaleEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }
    public string Regione { get; set; } = "";
    public decimal Aliquota { get; set; }
}

/// <summary>Aliquota addizionale comunale IRPEF per anno e codice catastale.</summary>
public class AddizionaleComunaleEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }
    public string CodiceCatastale { get; set; } = "";
    public decimal Aliquota { get; set; }
}

/// <summary>Anagrafica di un CCNL.</summary>
public class CcnlEntity
{
    public string Codice { get; set; } = ""; // chiave
    public string Descrizione { get; set; } = "";
}

/// <summary>Livello di un CCNL con i minimi, versionato per anno.</summary>
public class CcnlLivelloEntity
{
    public Guid Id { get; set; }
    public string CcnlCodice { get; set; } = "";
    public int Anno { get; set; }
    public string Livello { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public decimal MinimoTabellare { get; set; }
    public decimal Edr { get; set; }
    public decimal ImportoScatto { get; set; }
}
