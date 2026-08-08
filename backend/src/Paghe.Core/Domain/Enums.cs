namespace Paghe.Core.Domain;

/// <summary>Qualifica del lavoratore (incide su aliquote contributive e regole CCNL).</summary>
public enum Qualifica
{
    Operaio,
    Impiegato,
    Quadro,
    Dirigente,
    Apprendista
}

/// <summary>Natura di una voce del cedolino.</summary>
public enum TipoVoce
{
    /// <summary>Aumenta il lordo / il netto (es. paga base, straordinario).</summary>
    Competenza,

    /// <summary>Riduce il netto (es. contributi c/dipendente, IRPEF, addizionali).</summary>
    Trattenuta,

    /// <summary>Voce informativa, non confluisce nel netto (es. quota TFR, contributi c/datore).</summary>
    Figurativa
}

/// <summary>
/// Come sono trattate vitto e alloggio durante la trasferta. Con il rimborso misto la soglia
/// esente della diaria si riduce: di un terzo se è rimborsato uno dei due, di due terzi se
/// sono rimborsati entrambi (art. 51 c.5 TUIR).
/// </summary>
public enum TipoRimborsoTrasferta
{
    /// <summary>Solo diaria: soglia piena.</summary>
    Forfettario,

    /// <summary>Diaria più rimborso di vitto oppure alloggio: soglia ridotta di 1/3.</summary>
    MistoVittoOAlloggio,

    /// <summary>Diaria più rimborso di vitto e alloggio: soglia ridotta di 2/3.</summary>
    MistoVittoEAlloggio
}
