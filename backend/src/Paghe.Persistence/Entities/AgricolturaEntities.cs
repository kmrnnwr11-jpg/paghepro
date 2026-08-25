namespace Paghe.Persistence.Entities;

// Parametri del settore agricolo, versionati per anno E per provincia: in agricoltura le
// tariffe sono fissate dal contratto provinciale (CPL), quindi la stessa qualifica è pagata
// diversamente a Cuneo e a Piacenza. Come per il resto dei parametri, aggiornare significa
// inserire righe per il nuovo anno, mai riscrivere quelle storiche.

/// <summary>Parametri agricoli di un anno e di una provincia.</summary>
public class ParametriAgricoliEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }

    /// <summary>Sigla della provincia del CPL (es. "CN").</summary>
    public string Provincia { get; set; } = "";

    /// <summary>Quota contributiva che riduce l'imponibile fiscale (vedi ParametriAgricoli).</summary>
    public decimal AliquotaContributiDeducibili { get; set; }

    /// <summary>Da dove vengono i valori: senza fonte una tabella di paghe non è verificabile.</summary>
    public string Fonte { get; set; } = "";
}

/// <summary>Riga della tabella salariale provinciale: paga oraria di un livello.</summary>
public class TariffaAgricolaEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }
    public string Provincia { get; set; } = "";
    public string Livello { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public decimal SalarioProvincialeOrario { get; set; }

    /// <summary>Terzo elemento orario: paga ora per ora ferie, festività e mensilità aggiuntive.</summary>
    public decimal TerzoElementoOrario { get; set; }
}

/// <summary>Contributo agricolo di un anno e di una provincia (FAP, CAC, casse provinciali).</summary>
public class ContributoAgricoloEntity
{
    public Guid Id { get; set; }
    public int Anno { get; set; }
    public string Provincia { get; set; } = "";
    public string Codice { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public decimal Aliquota { get; set; }
    public bool ACaricoDipendente { get; set; }

    /// <summary>Ordine di esposizione in busta: le buste reali hanno un ordine preciso.</summary>
    public int Ordine { get; set; }
}
