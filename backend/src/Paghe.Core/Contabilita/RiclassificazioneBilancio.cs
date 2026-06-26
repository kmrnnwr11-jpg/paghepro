using Paghe.Core.Calcolo;

namespace Paghe.Core.Contabilita;

/// <summary>
/// Motore di riclassificazione del bilancio: somma i saldi dei conti nelle macro-voci
/// CEE e costruisce Stato Patrimoniale e Conto Economico, calcolando il risultato
/// d'esercizio. L'utile/perdita confluisce nel patrimonio netto, così che — se la
/// contabilità quadra — risulti Totale Attivo = Totale Passivo.
/// </summary>
public static class RiclassificazioneBilancio
{
    public static Bilancio Riclassifica(
        IEnumerable<ContoBilancio> pianoConti,
        IEnumerable<SaldoConto> saldi)
    {
        var voceByConto = pianoConti.ToDictionary(c => c.Codice, c => c.Voce);

        var totali = new Dictionary<MacroVoce, decimal>();
        foreach (var v in Enum.GetValues<MacroVoce>())
            totali[v] = 0m;

        foreach (var s in saldi)
        {
            if (!voceByConto.TryGetValue(s.CodiceConto, out var voce))
                throw new ArgumentException(
                    $"Conto '{s.CodiceConto}' non presente nel piano dei conti.");
            totali[voce] += s.Importo;
        }

        decimal G(MacroVoce v) => Arrotondamento.Euro(totali[v]);

        // Conto economico → utile da inserire nel patrimonio netto.
        decimal valoreProd = G(MacroVoce.ValoreDellaProduzione);
        decimal costiProd = G(MacroVoce.CostiDellaProduzione);
        decimal proventiFin = G(MacroVoce.ProventiFinanziari);
        decimal oneriFin = G(MacroVoce.OneriFinanziari);
        decimal imposte = G(MacroVoce.Imposte);
        decimal utile = valoreProd - costiProd + (proventiFin - oneriFin) - imposte;

        decimal patrimonioNetto = Arrotondamento.Euro(G(MacroVoce.PatrimonioNetto) + utile);

        return new Bilancio
        {
            ValoreDellaProduzione = valoreProd,
            CostiDellaProduzione = costiProd,
            ProventiFinanziari = proventiFin,
            OneriFinanziari = oneriFin,
            Imposte = imposte,

            CreditiVersoSoci = G(MacroVoce.CreditiVersoSoci),
            Immobilizzazioni = G(MacroVoce.Immobilizzazioni),
            AttivoCircolante = G(MacroVoce.AttivoCircolante),
            RateiRiscontiAttivi = G(MacroVoce.RateiRiscontiAttivi),

            PatrimonioNetto = patrimonioNetto,
            FondiRischiOneri = G(MacroVoce.FondiRischiOneri),
            TrattamentoFineRapporto = G(MacroVoce.TrattamentoFineRapporto),
            Debiti = G(MacroVoce.Debiti),
            RateiRiscontiPassivi = G(MacroVoce.RateiRiscontiPassivi),
        };
    }
}
