using System.Text.Json;

namespace Paghe.Core.Ai;

/// <summary>
/// Interpreta la risposta JSON di un modello AI nel contratto comune
/// {"campi":[{"nome":...,"valore":...,"confidenza":...}]}. Condiviso da tutti i
/// provider (Claude, DeepSeek, GLM, OpenAI-compatibili): se il JSON non è valido
/// restituisce null e il flusso resta su base euristica.
/// </summary>
public static class PropostaAiJson
{
    public static PropostaAi? Interpreta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(TogliRecinzioni(json));
            if (!doc.RootElement.TryGetProperty("campi", out var campi)
                || campi.ValueKind != JsonValueKind.Array)
                return null;

            var lista = new List<CampoProposto>();
            foreach (var c in campi.EnumerateArray())
            {
                string? nome = c.TryGetProperty("nome", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(nome)) continue;
                string? valore = c.TryGetProperty("valore", out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() : null;
                decimal confidenza = c.TryGetProperty("confidenza", out var f)
                    && f.ValueKind == JsonValueKind.Number ? f.GetDecimal() : 0m;
                lista.Add(new CampoProposto(nome, valore, Math.Clamp(confidenza, 0m, 1m)));
            }
            return lista.Count == 0 ? null : new PropostaAi(lista);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// I modelli con la vista spesso incorniciano il JSON in blocchi ```json ... ```:
    /// qui si toglie la cornice, se presente, prima del parsing.
    /// </summary>
    private static string TogliRecinzioni(string testo)
    {
        string t = testo.Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        int inizio = t.IndexOf('\n');
        int fine = t.LastIndexOf("```", StringComparison.Ordinal);
        return inizio >= 0 && fine > inizio ? t[(inizio + 1)..fine].Trim() : t;
    }
}
