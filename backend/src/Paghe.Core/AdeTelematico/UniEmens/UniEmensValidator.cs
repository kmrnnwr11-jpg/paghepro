using System.Xml;
using System.Xml.Schema;

namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Valida un flusso UniEmens contro lo schema XSD ufficiale INPS incorporato come risorsa
/// (<c>UniEMENSind.xsd</c>, schema di validazione 4.32.0, allegato tecnico 4.32.3 del
/// 31/07/2026). La validazione è completamente offline, come per la FatturaPA.
///
/// Superare l'XSD è la condizione minima, non la prova di correttezza: il software di
/// controllo ufficiale INPS applica centinaia di controlli di merito in più (quadrature,
/// coerenze fra qualifiche, minimali). Il criterio di consegna resta l'esito di quello.
/// </summary>
public static class UniEmensValidator
{
    private const string RisorsaSchema = "Paghe.Core.AdeTelematico.UniEmens.Schema.UniEMENSind.xsd";

    private static readonly XmlSchemaSet Schemi = CaricaSchema();

    private static XmlSchemaSet CaricaSchema()
    {
        var set = new XmlSchemaSet { XmlResolver = null };
        var asm = typeof(UniEmensValidator).Assembly;
        using var stream = asm.GetManifestResourceStream(RisorsaSchema)
            ?? throw new InvalidOperationException($"Risorsa XSD non trovata: {RisorsaSchema}");
        using var reader = XmlReader.Create(
            stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        set.Add(null, reader);
        set.Compile();
        return set;
    }

    /// <summary>Elenco degli errori di validazione (vuoto = conforme allo schema).</summary>
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
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, e) => errori.Add($"[{e.Severity}] {e.Message}");

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

    /// <summary>True se il flusso è conforme allo schema ufficiale INPS.</summary>
    public static bool EValido(string xml) => Valida(xml).Count == 0;
}
