using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Paghe.Persistence.Ai;

/// <summary>
/// Canone comune degli hash A3: gli hash di pre/postcondizione confrontano oggetti in memoria
/// con riletture dal database, e il roundtrip può cambiare la SCALA dei decimali (0 → 0.0 su
/// SQLite) senza cambiarne il valore. Qui ogni decimale viene normalizzato al valore puro.
/// </summary>
internal static class CanoneAurora
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DecimalCanonicoJson() },
    };

    public static string Hash(object valore) =>
        HashTesto(JsonSerializer.Serialize(valore, Json));

    public static string HashTesto(string testo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testo ?? "")));

    private sealed class DecimalCanonicoJson : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDecimal();

        public override void Write(
            Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(decimal.Parse(
                value.ToString("G29", CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture));
    }
}
