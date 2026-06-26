using Paghe.Core.Calcolo;

namespace Paghe.Core.Fatturazione;

/// <summary>Esito di un controllo semantico SdI: codice errore ufficiale e descrizione.</summary>
public sealed record ErroreSdi(string Codice, string Descrizione);

/// <summary>
/// Controlli semantici del Sistema di Interscambio (SdI) sulla fattura, ulteriori rispetto
/// alla validazione XSD. Replicano alcuni dei controlli ufficiali (codici di scarto 00xxx)
/// che fanno respingere le fatture pur formalmente valide a schema.
///
/// Coperti: coerenza Aliquota IVA / Natura (00400, 00401), identificativo del cessionario
/// (00417), presenza di righe e coerenza degli importi del riepilogo (00422).
/// Non coperti (fuori scope): validità formale della partita IVA tramite controllo del
/// carattere di controllo, esterometro, e i controlli legati alla trasmissione effettiva.
/// </summary>
public static class ControlliSdi
{
    public static IReadOnlyList<ErroreSdi> Verifica(Fattura fattura)
    {
        var errori = new List<ErroreSdi>();

        if (fattura.Righe.Count == 0)
            errori.Add(new ErroreSdi("00423", "La fattura non contiene righe di dettaglio."));

        // 00417 — il cessionario/committente deve avere Partita IVA o Codice Fiscale.
        if (string.IsNullOrWhiteSpace(fattura.Cessionario.PartitaIva) &&
            string.IsNullOrWhiteSpace(fattura.Cessionario.CodiceFiscale))
            errori.Add(new ErroreSdi("00417",
                "CessionarioCommittente: IdFiscaleIVA e CodiceFiscale non valorizzati."));

        // 00400 / 00401 — coerenza tra Aliquota IVA e Natura sulle righe.
        int numeroLinea = 0;
        foreach (var r in fattura.Righe)
        {
            numeroLinea++;
            bool haNatura = !string.IsNullOrWhiteSpace(r.NaturaIva);

            if (r.AliquotaIva == 0m && !haNatura)
                errori.Add(new ErroreSdi("00400",
                    $"Riga {numeroLinea}: Natura non presente a fronte di AliquotaIVA pari a zero."));

            if (r.AliquotaIva != 0m && haNatura)
                errori.Add(new ErroreSdi("00401",
                    $"Riga {numeroLinea}: Natura presente a fronte di AliquotaIVA diversa da zero."));
        }

        // 00422 — l'imposta di ogni riepilogo deve essere coerente con imponibile e aliquota.
        var totali = CalcoloFattura.Calcola(fattura);
        foreach (var rip in totali.Riepiloghi)
        {
            decimal impostaAttesa = Arrotondamento.Euro(rip.Imponibile * rip.Aliquota / 100m);
            if (Math.Abs(rip.Imposta - impostaAttesa) > 0.01m)
                errori.Add(new ErroreSdi("00422",
                    $"Aliquota {rip.Aliquota}%: imposta {rip.Imposta} non coerente con imponibile {rip.Imponibile} (attesa {impostaAttesa})."));
        }

        return errori;
    }

    public static bool EConforme(Fattura fattura) => Verifica(fattura).Count == 0;
}
