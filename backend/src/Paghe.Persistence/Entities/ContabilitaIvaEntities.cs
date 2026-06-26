namespace Paghe.Persistence.Entities;

/// <summary>Fattura passiva/spesa registrata nel registro IVA acquisti.</summary>
public class SpesaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public AziendaEntity? Azienda { get; set; }

    public DateOnly Data { get; set; }
    public string Descrizione { get; set; } = "";
    public string ContoCosto { get; set; } = "";
    public string? Fornitore { get; set; }
    public string? NumeroDocumento { get; set; }

    public decimal Imponibile { get; set; }
    public decimal AliquotaIva { get; set; }
    public decimal PercentualeDetraibilita { get; set; } = 100m;
    public decimal Iva { get; set; }
    public decimal IvaDetraibile { get; set; }
    public decimal Totale { get; set; }
}

/// <summary>Liquidazione IVA calcolata e archiviata per azienda e periodo.</summary>
public class LiquidazioneIvaEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public AziendaEntity? Azienda { get; set; }

    public DateOnly Dal { get; set; }
    public DateOnly Al { get; set; }
    public string Periodicita { get; set; } = "";

    public decimal IvaVendite { get; set; }
    public decimal IvaAcquistiDetraibile { get; set; }
    public decimal CreditoPeriodoPrecedente { get; set; }
    public decimal Saldo { get; set; }
    public decimal IvaDaVersare { get; set; }
    public decimal CreditoDaRiportare { get; set; }
    public DateTime CreataIl { get; set; } = DateTime.UtcNow;

    public List<RigaLiquidazioneIvaEntity> Righe { get; set; } = new();
}

/// <summary>Riga del registro usata per ricostruire la liquidazione IVA archiviata.</summary>
public class RigaLiquidazioneIvaEntity
{
    public Guid Id { get; set; }
    public Guid LiquidazioneIvaId { get; set; }

    public DateOnly Data { get; set; }
    public string Descrizione { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal Imponibile { get; set; }
    public decimal AliquotaIva { get; set; }
    public decimal Iva { get; set; }
    public decimal IvaDetraibile { get; set; }
}
