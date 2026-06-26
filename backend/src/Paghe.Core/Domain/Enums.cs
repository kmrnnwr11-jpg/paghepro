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
