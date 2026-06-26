using Paghe.Core.Calcolo;

namespace Paghe.Core.Fatturazione;

/// <summary>Calcolo di imponibili, IVA e totali di una fattura.</summary>
public static class CalcoloFattura
{
    /// <summary>Imponibile di una riga = quantità × prezzo unitario, arrotondato.</summary>
    public static decimal ImponibileRiga(RigaFattura riga) =>
        Arrotondamento.Euro(riga.Quantita * riga.PrezzoUnitario);

    /// <summary>
    /// Calcola i riepiloghi IVA (uno per aliquota) e i totali del documento.
    /// L'IVA è calcolata sull'imponibile aggregato per aliquota, come richiesto dal
    /// tracciato FatturaPA (un riepilogo per ogni aliquota/natura).
    /// </summary>
    public static TotaliFattura Calcola(Fattura fattura)
    {
        var riepiloghi = fattura.Righe
            .GroupBy(r => (r.AliquotaIva, r.NaturaIva))
            .Select(g =>
            {
                decimal imponibile = Arrotondamento.Euro(g.Sum(ImponibileRiga));
                decimal imposta = Arrotondamento.Euro(imponibile * g.Key.AliquotaIva / 100m);
                return new RiepilogoIva
                {
                    Aliquota = g.Key.AliquotaIva,
                    NaturaIva = g.Key.NaturaIva,
                    Imponibile = imponibile,
                    Imposta = imposta
                };
            })
            .OrderBy(r => r.Aliquota)
            .ToList();

        decimal totaleImponibile = riepiloghi.Sum(r => r.Imponibile);
        decimal totaleImposta = riepiloghi.Sum(r => r.Imposta);

        return new TotaliFattura
        {
            Riepiloghi = riepiloghi,
            TotaleImponibile = totaleImponibile,
            TotaleImposta = totaleImposta,
            TotaleDocumento = Arrotondamento.Euro(totaleImponibile + totaleImposta)
        };
    }

    /// <summary>IVA (potenzialmente parzialmente detraibile) di una spesa.</summary>
    public static (decimal iva, decimal ivaDetraibile, decimal totale) CalcolaSpesa(Spesa spesa)
    {
        decimal iva = Arrotondamento.Euro(spesa.Imponibile * spesa.AliquotaIva / 100m);
        decimal ivaDetraibile = Arrotondamento.Euro(iva * spesa.PercentualeDetraibilita / 100m);
        decimal totale = Arrotondamento.Euro(spesa.Imponibile + iva);
        return (iva, ivaDetraibile, totale);
    }
}
