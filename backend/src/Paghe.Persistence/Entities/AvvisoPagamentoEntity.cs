namespace Paghe.Persistence.Entities;

/// <summary>Avviso di pagamento mensile dovuto da un cliente allo studio/commercialista.</summary>
public class AvvisoPagamentoEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AziendaId { get; set; }
    public AziendaEntity? Azienda { get; set; }

    public int Anno { get; set; }
    public int Mese { get; set; }
    public string Descrizione { get; set; } = "";
    public decimal Importo { get; set; }

    public DateOnly DataEmissione { get; set; }
    public DateOnly DataScadenza { get; set; }
    public string Stato { get; set; } = "DaPagare";

    public string MetodoPagamento { get; set; } = "Bonifico";
    public string? Iban { get; set; }
    public string? Causale { get; set; }
    public string? Note { get; set; }

    public DateOnly? PagatoIl { get; set; }
    public string? RiferimentoPagamento { get; set; }
    public int NumeroPromemoria { get; set; }
    public DateTime? UltimoPromemoriaIl { get; set; }

    public DateTime CreataIl { get; set; }
    public DateTime UltimoAggiornamento { get; set; }
}
