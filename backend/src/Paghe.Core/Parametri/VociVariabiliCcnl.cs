using System.Text.Json;

namespace Paghe.Core.Parametri;

/// <summary>Maggiorazioni del CCNL come frazione (0,30 = 30%). 0 se non previste.</summary>
public sealed record MaggiorazioniCcnl(
    decimal StraordinarioDiurno = 0m,
    decimal StraordinarioNotturno = 0m,
    decimal LavoroFestivo = 0m,
    decimal LavoroNotturno = 0m,
    decimal LavoroDomenicale = 0m,
    decimal StraordinarioFestivo = 0m);

/// <summary>Indennità del CCNL: nome, tipo ("giornaliera" | "oraria" | "mensile") e importo in euro.</summary>
public sealed record IndennitaCcnl(string Nome, string Tipo, decimal Importo);

/// <summary>Scatti di anzianità: numero massimo e periodicità ("biennale" | "triennale").</summary>
public sealed record ScattiCcnl(int NumeroMassimo, string Periodicita);

/// <summary>
/// Voci variabili di un CCNL (oltre ai minimi tabellari): orario, mensilità, maggiorazioni e
/// indennità. Servono al motore per calcolare il <b>lordo</b> reale (straordinari, festivi, turni,
/// trasferte…). Caricabili dal JSON ufficiale raccolto dalle fonti (CNEL / associazioni).
/// </summary>
public sealed record VociVariabiliCcnl(
    string Codice,
    int Anno,
    string? Fonte = null,
    decimal OrarioSettimanale = 40m,
    decimal DivisoreOrario = 168m,
    int NumeroMensilita = 14,
    MaggiorazioniCcnl? Maggiorazioni = null,
    IReadOnlyList<IndennitaCcnl>? Indennita = null,
    ScattiCcnl? Scatti = null);

/// <summary>Carica le voci variabili dei CCNL dal JSON (formato del "Prompt 2" di raccolta dati).</summary>
public static class VociVariabiliCcnlJson
{
    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<VociVariabiliCcnl> Carica(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        return JsonSerializer.Deserialize<List<VociVariabiliCcnl>>(json, Opzioni) ?? [];
    }
}
