using System.Xml;
using System.Xml.Schema;

namespace Paghe.Documenti;

/// <summary>
/// Valida un XML FatturaPA contro lo schema XSD ufficiale dell'Agenzia delle Entrate
/// (Schema_VFPR12, versione 1.2.3), incluso lo schema xmldsig importato.
/// Gli XSD sono incorporati come risorse: la validazione avviene completamente offline.
/// </summary>
public static class FatturaPaValidator
{
    private const string ResSchema = "Paghe.Documenti.Schemi.Schema_VFPR12.xsd";
    private const string ResXmldsig = "Paghe.Documenti.Schemi.xmldsig-core-schema.xsd";

    private static readonly XmlSchemaSet Schemi = CaricaSchemi();

    private static XmlSchemaSet CaricaSchemi()
    {
        var set = new XmlSchemaSet { XmlResolver = null };
        var asm = typeof(FatturaPaValidator).Assembly;

        // Lo schema xmldsig ha un DOCTYPE (entità non usate nel corpo): lo ignoriamo.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

        // xmldsig va aggiunto per primo, così l'import nello schema FatturaPA si risolve
        // per namespace senza accedere alla rete.
        AggiungiSchema(set, asm, ResXmldsig, settings);
        AggiungiSchema(set, asm, ResSchema, settings);
        set.Compile();
        return set;
    }

    private static void AggiungiSchema(XmlSchemaSet set, System.Reflection.Assembly asm,
        string risorsa, XmlReaderSettings settings)
    {
        using var stream = asm.GetManifestResourceStream(risorsa)
            ?? throw new InvalidOperationException($"Risorsa XSD non trovata: {risorsa}");
        using var reader = XmlReader.Create(stream, settings);
        set.Add(null, reader);
    }

    /// <summary>Restituisce l'elenco degli errori di validazione (vuoto = XML valido).</summary>
    public static IReadOnlyList<string> Valida(string xml)
    {
        var errori = new List<string>();

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Schemi,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        settings.ValidationEventHandler += (_, e) =>
            errori.Add($"[{e.Severity}] {e.Message}");

        try
        {
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            errori.Add($"[XmlException] {ex.Message}");
        }

        return errori;
    }

    /// <summary>True se l'XML è conforme allo schema FatturaPA.</summary>
    public static bool EValido(string xml) => Valida(xml).Count == 0;
}
