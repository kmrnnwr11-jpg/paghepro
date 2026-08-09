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

/// <summary>
/// Che cosa è stata quella giornata per il lavoratore. È lo stato prevalente del giorno:
/// le quantità parziali (permessi, straordinari, uscite anticipate) stanno nelle ore della
/// <see cref="GiornataCartellino"/>, non in una seconda causale.
/// </summary>
public enum CausaleGiornata
{
    /// <summary>Giornata di lavoro (anche parziale, se ci sono ore di permesso o assenza).</summary>
    Lavorata,

    Ferie,

    /// <summary>Malattia certificata: va collegata al numero dell'evento nell'anno.</summary>
    Malattia,

    /// <summary>Inabilità temporanea per infortunio sul lavoro (INAIL).</summary>
    Infortunio,

    /// <summary>Giornata intera di permesso retribuito (ROL).</summary>
    PermessoRetribuito,

    /// <summary>Giornata intera goduta a titolo di ex festività.</summary>
    ExFestivita,

    /// <summary>Assenza non retribuita per l'intera giornata.</summary>
    AssenzaNonRetribuita,

    /// <summary>Riposo settimanale (di norma la domenica).</summary>
    RiposoSettimanale,

    /// <summary>Festività nazionale o infrasettimanale.</summary>
    Festivita
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
