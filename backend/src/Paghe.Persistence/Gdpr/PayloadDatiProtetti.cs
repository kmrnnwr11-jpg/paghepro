using System.Buffers.Binary;
using System.Text.Json;
using Paghe.Core.Domain;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence.Gdpr;

/// <summary>Serializzazioni interne a lunghezza fissa prima della cifratura AEAD.</summary>
internal static class PayloadDatiProtetti
{
    private const int DimensioneSalute = 36; // 2 decimal (16 byte ciascuno) + 1 int (4 byte)
    private const int DimensioneVoce = 2048;

    /// <summary>
    /// Calendario di un mese: 31 giornate compatte stanno abbondantemente qui dentro. La
    /// dimensione è fissa come per le voci, così la lunghezza del cifrato non lascia intuire
    /// quante assenze contenga il mese.
    /// </summary>
    private const int DimensioneGiornateLegacy = 8192;
    private const int DimensioneGiornate = 32768;

    public static string SerializzaSalute(decimal malattia, int numeroEvento, decimal infortunio)
    {
        Span<byte> payload = stackalloc byte[DimensioneSalute];
        ScriviDecimal(payload[..16], malattia);
        BinaryPrimitives.WriteInt32LittleEndian(payload.Slice(16, 4), numeroEvento);
        ScriviDecimal(payload.Slice(20, 16), infortunio);
        return Convert.ToBase64String(payload);
    }

    public static (decimal Malattia, int NumeroEvento, decimal Infortunio) DeserializzaSalute(
        string payloadBase64)
    {
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(payloadBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Payload salute Base64 non valido.", ex);
        }

        if (payload.Length != DimensioneSalute)
            throw new InvalidOperationException(
                $"Payload salute di lunghezza non valida: {payload.Length} byte.");

        try
        {
            return (
                LeggiDecimal(payload.AsSpan(0, 16)),
                BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(16, 4)),
                LeggiDecimal(payload.AsSpan(20, 16)));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Payload salute contiene un decimal non valido.", ex);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    public static string SerializzaVoce(CedolinoVoceEntity voce) =>
        SerializzaVoce(
            voce.Codice,
            voce.Descrizione,
            voce.Tipo,
            voce.Quantita,
            voce.Base,
            voce.Importo);

    public static string SerializzaVoce(
        string codice,
        string descrizione,
        string tipo,
        decimal? quantita,
        decimal? @base,
        decimal importo)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(
            new DatiVoce(codice, descrizione, tipo, quantita, @base, importo));
        if (json.Length > DimensioneVoce - sizeof(int))
            throw new InvalidOperationException(
                $"La voce cedolino supera il payload protetto massimo di {DimensioneVoce - sizeof(int)} byte.");

        var payload = new byte[DimensioneVoce];
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), json.Length);
            json.CopyTo(payload.AsSpan(sizeof(int)));
            return Convert.ToBase64String(payload);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(json);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    public static DatiVoce DeserializzaVoce(string payloadBase64)
    {
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(payloadBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Payload voce cedolino Base64 non valido.", ex);
        }

        if (payload.Length != DimensioneVoce)
            throw new InvalidOperationException(
                $"Payload voce cedolino di lunghezza non valida: {payload.Length} byte.");

        try
        {
            var lunghezza = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            if (lunghezza <= 0 || lunghezza > DimensioneVoce - sizeof(int))
                throw new InvalidOperationException("Lunghezza interna del payload voce non valida.");

            return JsonSerializer.Deserialize<DatiVoce>(
                    payload.AsSpan(sizeof(int), lunghezza))
                ?? throw new InvalidOperationException("Payload voce cedolino vuoto.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Payload voce cedolino non valido.", ex);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static void ScriviDecimal(Span<byte> destinazione, decimal valore)
    {
        var bits = decimal.GetBits(valore);
        for (var i = 0; i < bits.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destinazione.Slice(i * 4, 4), bits[i]);
    }

    private static decimal LeggiDecimal(ReadOnlySpan<byte> sorgente)
    {
        Span<int> bits = stackalloc int[4];
        for (var i = 0; i < bits.Length; i++)
            bits[i] = BinaryPrimitives.ReadInt32LittleEndian(sorgente.Slice(i * 4, 4));
        return new decimal(bits);
    }

    internal sealed record DatiVoce(
        string Codice,
        string Descrizione,
        string Tipo,
        decimal? Quantita,
        decimal? Base,
        decimal Importo);

    // ---- Calendario giornaliero del cartellino ----------------------------------------
    // Dice quali giorni sono stati malattia o infortunio: è un dato sanitario, quindi viaggia
    // cifrato come la terna salute, non in colonne leggibili. Anno e mese non si serializzano:
    // stanno già sul cartellino, qui basta il giorno.

    /// <summary>Una giornata nella forma compatta che finisce dentro il payload cifrato.</summary>
    private sealed record GiornataProtetta(
        int G, string C, decimal Ord, decimal Per, decimal Exf, decimal Ass,
        decimal Str, decimal StrN, decimal StrF,
        decimal? Prg = null,
        string? IdE = null,
        DateOnly? IniE = null,
        decimal? RmgP = null,
        decimal? Rat = null,
        decimal? Lor = null);

    public static string SerializzaGiornate(IReadOnlyList<GiornataCartellino> giornate)
    {
        var compatte = giornate
            .OrderBy(g => g.Data)
            .Select(g => new GiornataProtetta(
                g.Data.Day, g.Causale.ToString(), g.OreOrdinarie, g.OrePermessi,
                g.OreExFestivita, g.OreAssenzaNonRetribuita,
                g.OreStraordinario, g.OreStraordinarioNotturno, g.OreStraordinarioFestivo,
                g.ProgressivoOreSettimanali, g.IdentificativoEvento,
                g.DataInizioEvento, g.RetribuzioneMediaGiornalieraPrecedente,
                g.RateiGiornalieri, g.CoefficienteLordizzazione))
            .ToList();

        var json = JsonSerializer.SerializeToUtf8Bytes(compatte);
        if (json.Length > DimensioneGiornate - sizeof(int))
            throw new InvalidOperationException(
                $"Il calendario del mese supera il payload protetto massimo di {DimensioneGiornate - sizeof(int)} byte.");

        var payload = new byte[DimensioneGiornate];
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), json.Length);
            json.CopyTo(payload.AsSpan(sizeof(int)));
            return Convert.ToBase64String(payload);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(json);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    /// <summary>Rilegge il calendario. Payload vuoto = cartellino compilato a totali.</summary>
    public static IReadOnlyList<GiornataCartellino> DeserializzaGiornate(
        string? payloadBase64, int anno, int mese)
    {
        if (string.IsNullOrWhiteSpace(payloadBase64)) return [];

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(payloadBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Payload calendario Base64 non valido.", ex);
        }

        if (payload.Length is not DimensioneGiornate and not DimensioneGiornateLegacy)
            throw new InvalidOperationException(
                $"Payload calendario di lunghezza non valida: {payload.Length} byte.");

        try
        {
            var lunghezza = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            if (lunghezza <= 0 || lunghezza > payload.Length - sizeof(int))
                throw new InvalidOperationException("Lunghezza interna del payload calendario non valida.");

            var compatte = JsonSerializer.Deserialize<List<GiornataProtetta>>(
                    payload.AsSpan(sizeof(int), lunghezza))
                ?? throw new InvalidOperationException("Payload calendario vuoto.");

            return [.. compatte.Select(g => new GiornataCartellino
            {
                Data = new DateOnly(anno, mese, g.G),
                Causale = LeggiCausaleGiornata(g.C),
                OreOrdinarie = g.Ord,
                OrePermessi = g.Per,
                OreExFestivita = g.Exf,
                OreAssenzaNonRetribuita = g.Ass,
                OreStraordinario = g.Str,
                OreStraordinarioNotturno = g.StrN,
                OreStraordinarioFestivo = g.StrF,
                ProgressivoOreSettimanali = g.Prg,
                IdentificativoEvento = g.IdE,
                DataInizioEvento = g.IniE,
                RetribuzioneMediaGiornalieraPrecedente = g.RmgP,
                RateiGiornalieri = g.Rat,
                CoefficienteLordizzazione = g.Lor,
            })];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Payload calendario non valido.", ex);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static CausaleGiornata LeggiCausaleGiornata(string valore)
    {
        if (!Enum.TryParse<CausaleGiornata>(valore, out var causale)
            || !Enum.IsDefined(causale))
            throw new InvalidOperationException(
                $"Il payload calendario contiene una causale non riconosciuta: '{valore}'.");
        return causale;
    }
}
