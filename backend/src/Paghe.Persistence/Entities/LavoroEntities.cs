namespace Paghe.Persistence.Entities;

/// <summary>Lavoratore (persona fisica), persistito.</summary>
public class DipendenteEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CodiceFiscale { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Cognome { get; set; } = "";
    public string? Iban { get; set; }

    public List<RapportoLavoroEntity> Rapporti { get; set; } = new();
}

/// <summary>Rapporto di lavoro persistito: collega dipendente e azienda con l'inquadramento.</summary>
public class RapportoLavoroEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public Guid DipendenteId { get; set; }
    public DipendenteEntity? Dipendente { get; set; }

    public string CodiceCcnl { get; set; } = "";
    public string Livello { get; set; } = "";
    public string Qualifica { get; set; } = "Impiegato"; // nome enum Qualifica
    public int NumeroMensilita { get; set; } = 14;
    public bool PartTime { get; set; }
    public decimal PercentualePartTime { get; set; } = 100m;
    public int ScattiAnzianita { get; set; }
    public decimal Superminimo { get; set; }
    public string Regione { get; set; } = "";
    public string CodiceCatastaleComune { get; set; } = "";
    public bool DetrazioniLavoroDipendente { get; set; } = true;
    public int GiorniDetrazioneAnno { get; set; } = 365;
    public DateOnly DataAssunzione { get; set; }
}

/// <summary>Cartellino presenze mensile persistito.</summary>
public class CartellinoEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RapportoLavoroId { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }
    public decimal OreStraordinario { get; set; }
    public decimal OreStraordinarioNotturno { get; set; }
    public decimal OreStraordinarioFestivo { get; set; }
    public decimal GiorniFerie { get; set; }
    public decimal OrePermessi { get; set; }
    public decimal GiorniMalattia { get; set; }
    public decimal OreAssenzaNonRetribuita { get; set; }
}

/// <summary>Cedolino calcolato e archiviato (testata con i totali).</summary>
public class CedolinoEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public Guid RapportoLavoroId { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }

    public decimal TotaleCompetenze { get; set; }
    public decimal TotaleTrattenute { get; set; }
    public decimal ImponibileInps { get; set; }
    public decimal ContributiDipendente { get; set; }
    public decimal ContributiDatore { get; set; }
    public decimal ImponibileIrpef { get; set; }
    public decimal IrpefNetta { get; set; }
    public decimal AddizionaleRegionale { get; set; }
    public decimal AddizionaleComunale { get; set; }
    public decimal NettoInBusta { get; set; }
    public decimal QuotaTfr { get; set; }
    public decimal CostoAzienda { get; set; }

    public DateTime CalcolatoIl { get; set; }
    public List<CedolinoVoceEntity> Voci { get; set; } = new();
}

/// <summary>Riga (voce) di un cedolino archiviato.</summary>
public class CedolinoVoceEntity
{
    public Guid Id { get; set; }
    public Guid CedolinoId { get; set; }
    public string Codice { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public string Tipo { get; set; } = ""; // nome enum TipoVoce
    public decimal? Quantita { get; set; }
    public decimal? Base { get; set; }
    public decimal Importo { get; set; }
}
