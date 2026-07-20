namespace Paghe.Persistence.Entities;

/// <summary>Avviso di pagamento mensile dovuto da un cliente allo studio/commercialista.</summary>
public class AvvisoPagamentoEntity
{
    private string? _iban;

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
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Iban
    {
        get => _iban;
        set
        {
            if (string.Equals(_iban, value, StringComparison.Ordinal)) return;
            _iban = value;
            IbanModificato = true;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? IbanProtetto { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    internal bool IbanModificato { get; private set; }

    internal void CaricaIbanDecifrato(string? valore)
    {
        _iban = valore;
        IbanModificato = false;
    }

    internal void ConfermaProtezioneIban() => IbanModificato = false;
    public string? Causale { get; set; }
    public string? Note { get; set; }

    public DateOnly? PagatoIl { get; set; }
    public string? RiferimentoPagamento { get; set; }
    public int NumeroPromemoria { get; set; }
    public DateTime? UltimoPromemoriaIl { get; set; }

    public DateTime CreataIl { get; set; }
    public DateTime UltimoAggiornamento { get; set; }
}
