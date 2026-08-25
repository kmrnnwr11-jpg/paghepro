namespace Paghe.Core.Parametri;

/// <summary>
/// Le regole di calcolo che cambiano da contratto a contratto: divisori, maggiorazioni per lo
/// straordinario, maturazione di ferie e permessi. Prima erano numeri scritti dentro il motore
/// con il commento «es. CCNL Commercio»: applicati a tutti, giusti per uno solo.
/// Sono versionate per anno insieme al resto dei parametri.
/// </summary>
public sealed record RegoleCcnl(
    /// <summary>Divide la retribuzione mensile per ottenere la paga oraria (es. 168, 169 in agricoltura).</summary>
    decimal DivisoreOrario,
    /// <summary>Divide la retribuzione mensile per ottenere la paga giornaliera (di norma 26).</summary>
    decimal DivisoreGiornaliero,
    decimal MaggiorazioneStraordinario,
    decimal MaggiorazioneStraordinarioNotturno,
    decimal MaggiorazioneStraordinarioFestivo,
    /// <summary>Giorni di ferie maturati ogni mese a tempo pieno.</summary>
    decimal GiorniFerieMese,
    /// <summary>Ore di permesso (ROL + ex-festività) maturate ogni mese a tempo pieno.</summary>
    decimal OrePermessiMese,
    /// <summary>Ore di permesso mensili nelle aziende oltre i 15 dipendenti, se il CCNL le differenzia.</summary>
    decimal? OrePermessiMeseOltre15 = null,
    /// <summary>Periodicità degli scatti di anzianità in mesi (36 = triennale). Se indicata insieme
    /// alla data di assunzione, il motore matura gli scatti da solo.</summary>
    int? ScattiOgniMesi = null,
    /// <summary>Numero massimo di scatti maturabili.</summary>
    int? ScattiMassimo = null,
    /// <summary>Mese in cui il contratto eroga la tredicesima (di norma 12).</summary>
    int? MeseErogazioneTredicesima = null,
    /// <summary>Mese in cui il contratto eroga la quattordicesima (Terziario: luglio).</summary>
    int? MeseErogazioneQuattordicesima = null,
    /// <summary>Quota mensile del fondo di assistenza sanitaria a carico del lavoratore (Fondo Est: 2 €).</summary>
    decimal? AssistenzaSanitariaLavoratoreMese = null,
    /// <summary>Quota mensile del fondo di assistenza sanitaria a carico dell'azienda (Fondo Est: 13 €).</summary>
    decimal? AssistenzaSanitariaDatoreMese = null,
    /// <summary>Aliquota ente bilaterale a carico del lavoratore su paga base + contingenza (EBiTer: 0,05%).</summary>
    decimal? EnteBilateraleAliquotaLavoratore = null,
    /// <summary>Aliquota ente bilaterale a carico dell'azienda su paga base + contingenza (EBiTer: 0,10%).</summary>
    decimal? EnteBilateraleAliquotaDatore = null,
    /// <summary>Monte ROL annuo a regime fino a 15 dipendenti. Separato dalle ex festivita.</summary>
    decimal? OreRolAnno = null,
    /// <summary>Monte ROL annuo a regime oltre 15 dipendenti.</summary>
    decimal? OreRolAnnoOltre15 = null,
    /// <summary>Monte annuo delle ex festivita, distinto dai ROL.</summary>
    decimal? OreExFestivitaAnno = null,
    /// <summary>Mesi iniziali di anzianita nei quali il lavoratore non matura ROL.</summary>
    int? MesiAnzianitaSenzaRol = null,
    /// <summary>Mesi successivi nei quali il lavoratore matura la percentuale ridotta di ROL.</summary>
    int? MesiAnzianitaRolRidotto = null,
    /// <summary>Percentuale di maturazione ROL nel periodo ridotto (0,50 = meta).</summary>
    decimal? PercentualeRolRidotta = null);
