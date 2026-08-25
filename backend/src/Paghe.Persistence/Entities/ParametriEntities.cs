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

    // Ulteriore detrazione "cuneo fiscale" (L. 207/2024): importo annuo e soglie di reddito.
    public decimal? CuneoUlterioreDetrazioneAnnua { get; set; }
    public decimal? CuneoRedditoOltre { get; set; }
    public decimal? CuneoRedditoFinoA { get; set; }
    public decimal? CuneoRedditoAzzeramento { get; set; }

    /// <summary>Contributo di solidarietà sui fondi di assistenza sanitaria c/datore (10%).</summary>
    public decimal? SolidarietaFondiAliquota { get; set; }
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

/// <summary>
/// Identità ufficiale di una versione annuale del contratto. Il codice CNEL non è ricavato
/// dal nome del CCNL: arriva esplicitamente dalla fonte e resta storicizzato con l'anno.
/// </summary>
public class CcnlVersioneEntity
{
    public Guid Id { get; set; }
    public string CcnlCodice { get; set; } = "";
    public int Anno { get; set; }
    public string? CodiceCnel { get; set; }
    public string? Fonte { get; set; }
}

/// <summary>
/// Regole di calcolo di un CCNL per un anno: divisori, maggiorazioni dello straordinario e
/// maturazione di ferie e permessi. Le colonne facoltative restano nulle quando il dato del
/// contratto non lo abbiamo: in quel caso vale il default generale dell'anno, dichiarato in
/// ParametriNormativi, invece di un numero inventato.
/// </summary>
public class RegoleCcnlEntity
{
    public Guid Id { get; set; }
    public string CcnlCodice { get; set; } = "";
    public int Anno { get; set; }

    public decimal DivisoreOrario { get; set; }
    public decimal? DivisoreGiornaliero { get; set; }

    public decimal MaggiorazioneStraordinario { get; set; }
    public decimal MaggiorazioneStraordinarioNotturno { get; set; }
    public decimal MaggiorazioneStraordinarioFestivo { get; set; }

    public decimal? GiorniFerieMese { get; set; }
    public decimal? OrePermessiMese { get; set; }

    /// <summary>Ore di permesso mensili nelle aziende oltre i 15 dipendenti, se il CCNL le differenzia.</summary>
    public decimal? OrePermessiMeseOltre15 { get; set; }

    /// <summary>Periodicità degli scatti di anzianità in mesi (36 = triennale): abilita la maturazione automatica.</summary>
    public int? ScattiOgniMesi { get; set; }

    /// <summary>Numero massimo di scatti maturabili.</summary>
    public int? ScattiMassimo { get; set; }

    /// <summary>Mese in cui il contratto eroga la tredicesima (di norma 12).</summary>
    public int? MeseErogazioneTredicesima { get; set; }

    /// <summary>Mese in cui il contratto eroga la quattordicesima (Terziario: 7).</summary>
    public int? MeseErogazioneQuattordicesima { get; set; }

    /// <summary>Quota mensile del fondo di assistenza sanitaria a carico del lavoratore (Fondo Est: 2 €).</summary>
    public decimal? AssistenzaSanitariaLavoratoreMese { get; set; }

    /// <summary>Quota mensile del fondo di assistenza sanitaria a carico dell'azienda (Fondo Est: 13 €).</summary>
    public decimal? AssistenzaSanitariaDatoreMese { get; set; }

    /// <summary>Aliquota ente bilaterale a carico del lavoratore su paga base + contingenza (EBiTer: 0,0005).</summary>
    public decimal? EnteBilateraleAliquotaLavoratore { get; set; }

    /// <summary>Aliquota ente bilaterale a carico dell'azienda su paga base + contingenza (EBiTer: 0,0010).</summary>
    public decimal? EnteBilateraleAliquotaDatore { get; set; }

    /// <summary>Monte ROL annuo a regime fino a 15 dipendenti, separato dalle ex festivita.</summary>
    public decimal? OreRolAnno { get; set; }

    /// <summary>Monte ROL annuo a regime oltre 15 dipendenti.</summary>
    public decimal? OreRolAnnoOltre15 { get; set; }

    /// <summary>Monte annuo delle ex festivita.</summary>
    public decimal? OreExFestivitaAnno { get; set; }

    public int? MesiAnzianitaSenzaRol { get; set; }
    public int? MesiAnzianitaRolRidotto { get; set; }
    public decimal? PercentualeRolRidotta { get; set; }

    /// <summary>Da dove vengono i numeri (accordo CNEL, tabella…): serve a chi li rivede.</summary>
    public string? Fonte { get; set; }
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

    /// <summary>
    /// Quota di contingenza (EDR storico incluso) già compresa nel minimo tabellare, quando la
    /// scomposizione è nota: in busta va esposta come voce distinta dalla paga base. Null =
    /// scomposizione non ancora fornita, il minimo resta un'unica voce.
    /// </summary>
    public decimal? Contingenza { get; set; }
}

/// <summary>
/// Terzo elemento provinciale di un CCNL (Terziario), versionato per anno: importo mensile per
/// sigla provincia dell'unità operativa; la riga con provincia "*" è il valore nazionale.
/// Una provincia senza riga usa il nazionale, mai la tabella di un'altra provincia.
/// </summary>
public class CcnlTerzoElementoEntity
{
    public Guid Id { get; set; }
    public string CcnlCodice { get; set; } = "";
    public int Anno { get; set; }
    public string Provincia { get; set; } = "";
    public decimal Importo { get; set; }
}
