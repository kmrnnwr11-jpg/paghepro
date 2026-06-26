using Paghe.Core.Calcolo;
using Paghe.Core.Fatturazione;

namespace Paghe.Core.Contabilita;

public enum PeriodicitaIva
{
    Mensile,
    Trimestrale,
}

public sealed record RigaRegistroIva(
    DateOnly Data,
    string Descrizione,
    string Tipo,
    decimal Imponibile,
    decimal AliquotaIva,
    decimal Iva,
    decimal IvaDetraibile);

public sealed class LiquidazioneIva
{
    public required DateOnly Dal { get; init; }
    public required DateOnly Al { get; init; }
    public required PeriodicitaIva Periodicita { get; init; }
    public required decimal IvaVendite { get; init; }
    public required decimal IvaAcquistiDetraibile { get; init; }
    public required decimal CreditoPeriodoPrecedente { get; init; }
    public required decimal Saldo { get; init; }
    public decimal IvaDaVersare => Saldo > 0m ? Saldo : 0m;
    public decimal CreditoDaRiportare => Saldo < 0m ? Math.Abs(Saldo) : 0m;
    public required IReadOnlyList<RigaRegistroIva> Registri { get; init; }
}

/// <summary>Calcolo IVA vendite/acquisti per periodo, pronto per registri e LIPE.</summary>
public static class CalcoloLiquidazioneIva
{
    public static LiquidazioneIva Calcola(
        DateOnly dal,
        DateOnly al,
        PeriodicitaIva periodicita,
        IEnumerable<Fattura> fattureAttive,
        IEnumerable<Spesa> spese,
        decimal creditoPeriodoPrecedente = 0m)
    {
        if (al < dal)
            throw new ArgumentException("La data finale non può precedere la data iniziale.");

        var registri = new List<RigaRegistroIva>();

        foreach (var fattura in fattureAttive.Where(f => f.Data >= dal && f.Data <= al))
        {
            foreach (var rip in CalcoloFattura.Calcola(fattura).Riepiloghi)
            {
                registri.Add(new RigaRegistroIva(
                    fattura.Data,
                    $"Fattura attiva {fattura.Numero}",
                    "Vendita",
                    rip.Imponibile,
                    rip.Aliquota,
                    rip.Imposta,
                    0m));
            }
        }

        foreach (var spesa in spese.Where(s => s.Data >= dal && s.Data <= al))
        {
            var (iva, ivaDetraibile, _) = CalcoloFattura.CalcolaSpesa(spesa);
            registri.Add(new RigaRegistroIva(
                spesa.Data,
                spesa.Descrizione,
                "Acquisto",
                spesa.Imponibile,
                spesa.AliquotaIva,
                iva,
                ivaDetraibile));
        }

        decimal ivaVendite = Arrotondamento.Euro(registri.Where(r => r.Tipo == "Vendita").Sum(r => r.Iva));
        decimal ivaAcquisti = Arrotondamento.Euro(registri.Where(r => r.Tipo == "Acquisto").Sum(r => r.IvaDetraibile));
        decimal saldo = Arrotondamento.Euro(ivaVendite - ivaAcquisti - creditoPeriodoPrecedente);

        return new LiquidazioneIva
        {
            Dal = dal,
            Al = al,
            Periodicita = periodicita,
            IvaVendite = ivaVendite,
            IvaAcquistiDetraibile = ivaAcquisti,
            CreditoPeriodoPrecedente = creditoPeriodoPrecedente,
            Saldo = saldo,
            Registri = registri.OrderBy(r => r.Data).ThenBy(r => r.Tipo).ToList(),
        };
    }
}
