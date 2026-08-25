using Paghe.Core.Domain;

namespace Paghe.Core.Parametri;

/// <summary>
/// Dati di esempio per il CCNL Commercio/Terziario e i parametri normativi 2026.
///
/// ⚠️ ATTENZIONE: i valori qui riportati sono ESEMPI ILLUSTRATIVI a scopo di sviluppo
/// e test del motore di calcolo. NON sono dati ufficiali certificati. Minimi tabellari,
/// aliquote contributive, scaglioni e addizionali vanno verificati e aggiornati con un
/// Consulente del Lavoro prima di qualsiasi uso reale.
/// </summary>
public static class Seed
{
    /// <summary>
    /// Fallback di sviluppo H011 2026, allineato al dataset incorporato. Il runtime importa il
    /// file versionato; questa copia evita che endpoint/test stateless reintroducano l'EDR di
    /// 10,33 euro o i minimi precedenti.
    /// </summary>
    public static Ccnl CcnlCommercio2026() => new(
        codice: "COMMERCIO_TERZIARIO",
        descrizione: "CCNL Terziario, Distribuzione e Servizi (Confcommercio)",
        livelli: new[]
        {
            new LivelloCcnl("Quadro", "Quadro",          2723.46m, 0m, 25.46m),
            new LivelloCcnl("1",      "Primo livello",   2504.06m, 0m, 24.84m),
            new LivelloCcnl("2",      "Secondo livello", 2233.58m, 0m, 22.83m),
            new LivelloCcnl("3",      "Terzo livello",   1981.84m, 0m, 21.95m, 527.90m),
            new LivelloCcnl("4",      "Quarto livello",  1781.68m, 0m, 20.66m),
            new LivelloCcnl("5",      "Quinto livello",  1658.01m, 0m, 20.30m),
            new LivelloCcnl("6",      "Sesto livello",   1539.70m, 0m, 19.73m),
            new LivelloCcnl("7",      "Settimo livello", 1390.73m, 0m, 19.47m),
        },
        codiceCnel: "H011",
        fonte: "Archivio nazionale CNEL, accordo 242377; minimi dal 01/11/2025",
        terzoElemento: new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["*"] = 2.07m,
            ["MI"] = 11.36m,
        });

    // --- Altri CCNL (minimi tabellari di ESEMPIO, da validare con il Consulente del Lavoro) ---
    // Servono come destinazione della mappatura dei CCNL letti dagli UNILAV dei vari settori.

    private static Ccnl CcnlEsempio(string codice, string descrizione, params (string liv, string desc, decimal min)[] livelli) =>
        new(codice, descrizione, livelli.Select(l => new LivelloCcnl(l.liv, l.desc, l.min, 0m, 25m)));

    public static Ccnl CcnlAgricolturaOperai2026() => CcnlEsempio(
        "AGRICOLTURA_OPERAI", "CCNL Operai Agricoli e Florovivaisti — valori di esempio",
        ("1", "Specializzato super", 1480m), ("2", "Specializzato", 1390m),
        ("3", "Qualificato", 1300m), ("4", "Comune", 1220m));

    public static Ccnl CcnlTrasportiLogistica2026() => CcnlEsempio(
        "TRASPORTI_LOGISTICA", "CCNL Logistica, Trasporto e Spedizione — valori di esempio",
        ("1", "Primo livello", 1900m), ("2", "Secondo livello", 1750m), ("3", "Terzo livello", 1600m),
        ("4", "Quarto livello", 1480m), ("5", "Quinto livello", 1380m), ("6", "Sesto livello", 1300m));

    public static Ccnl CcnlEdilizia2026() => CcnlEsempio(
        "EDILIZIA_INDUSTRIA", "CCNL Edilizia Industria — valori di esempio",
        ("1", "Operaio comune", 1300m), ("2", "Operaio qualificato", 1420m),
        ("3", "Operaio specializzato", 1560m), ("4", "Quarto livello", 1700m));

    public static Ccnl CcnlMetalmeccanici2026() => CcnlEsempio(
        "METALMECCANICI_INDUSTRIA", "CCNL Metalmeccanici Industria — valori di esempio",
        ("1", "Primo livello", 1380m), ("2", "Secondo livello", 1520m), ("3", "Terzo livello", 1660m),
        ("4", "Quarto livello", 1780m), ("5", "Quinto livello", 1900m), ("6", "Sesto livello", 2050m),
        ("7", "Settimo livello", 2200m));

    public static Ccnl CcnlTurismo2026() => CcnlEsempio(
        "TURISMO_PUBBLICI_ESERCIZI", "CCNL Turismo / Pubblici Esercizi — valori di esempio",
        ("1", "Primo livello", 1700m), ("2", "Secondo livello", 1560m), ("3", "Terzo livello", 1430m),
        ("4", "Quarto livello", 1340m), ("5", "Quinto livello", 1270m), ("6", "Sesto livello", 1210m));

    public static Ccnl CcnlMultiservizi2026() => CcnlEsempio(
        "MULTISERVIZI_PULIZIE", "CCNL Multiservizi / Pulizie — valori di esempio",
        ("1", "Primo livello", 1180m), ("2", "Secondo livello", 1250m), ("3", "Terzo livello", 1330m),
        ("4", "Quarto livello", 1430m), ("5", "Quinto livello", 1540m), ("6", "Sesto livello", 1660m));

    public static Ccnl CcnlStudiProfessionali2026() => CcnlEsempio(
        "STUDI_PROFESSIONALI", "CCNL Studi Professionali — valori di esempio",
        ("1", "Primo livello", 1980m), ("2", "Secondo livello", 1720m), ("3", "Terzo livello", 1520m),
        ("4", "Quarto livello", 1360m), ("5", "Quinto livello", 1240m));

    public static Ccnl CcnlChimicoFarmaceutico2026() => CcnlEsempio(
        "CHIMICO_FARMACEUTICO", "CCNL Chimico-Farmaceutico — valori di esempio",
        ("1", "Categoria A", 1450m), ("2", "Categoria B", 1620m), ("3", "Categoria C", 1820m),
        ("4", "Categoria D", 2050m), ("5", "Categoria E", 2300m));

    public static Ccnl CcnlAlimentare2026() => CcnlEsempio(
        "ALIMENTARE_INDUSTRIA", "CCNL Industria Alimentare — valori di esempio",
        ("1", "Primo livello", 1360m), ("2", "Secondo livello", 1480m), ("3", "Terzo livello", 1610m),
        ("4", "Quarto livello", 1740m), ("5", "Quinto livello", 1880m), ("6", "Sesto livello", 2020m));

    /// <summary>
    /// Tutti i CCNL d'esempio caricati nel database. ⚠️ Minimi tabellari di ESEMPIO: per buste reali
    /// vanno sostituiti con i valori ufficiali (caricabili da file via l'import CCNL) e validati con
    /// un Consulente del Lavoro.
    /// </summary>
    public static IEnumerable<Ccnl> TuttiICcnl2026() => new[]
    {
        CcnlCommercio2026(), CcnlAgricolturaOperai2026(), CcnlTrasportiLogistica2026(),
        CcnlEdilizia2026(), CcnlMetalmeccanici2026(), CcnlTurismo2026(),
        CcnlMultiservizi2026(), CcnlStudiProfessionali2026(), CcnlChimicoFarmaceutico2026(),
        CcnlAlimentare2026(),
    };

    /// <summary>Parametri normativi nazionali 2026 (valori di esempio).</summary>
    public static ParametriNormativi Parametri2026() => new()
    {
        Anno = 2026,

        // Scaglioni IRPEF 2026: 23% / 33% / 43%. La seconda aliquota è scesa dal 35% (2024-2025)
        // al 33% (Legge di Bilancio 2026). Verifica: a 50.000 il cumulato è 13.700 = 28.000×23% + 22.000×33%.
        ScaglioniIrpef = new[]
        {
            new ScaglioneIrpef(0m,      28000m, 0.23m),
            new ScaglioneIrpef(28000m,  50000m, 0.33m),
            new ScaglioneIrpef(50000m,  null,   0.43m),
        },

        // Aliquote INPS di esempio (quota dipendente / quota datore). La quota dipendente 9,19%
        // è la sola IVS: CIG 0,30% e fondi di settore si aggiungono dai dati dell'azienda.
        // La quota datore NON è un valore certificato per nessun settore (revisione consulente
        // 24/08/2026: per il commercio il riferimento ordinario è 28,98% prima del FIS, ma
        // dipende da CSC, autorizzazioni e dimensione): il profilo reale si imposta
        // sull'azienda (AliquotaInpsDatore) e vince su questa tabella.
        AliquoteInps = new Dictionary<Qualifica, AliquotaInps>
        {
            [Qualifica.Operaio]     = new AliquotaInps(0.0919m, 0.2788m),
            [Qualifica.Impiegato]   = new AliquotaInps(0.0919m, 0.2788m),
            [Qualifica.Quadro]      = new AliquotaInps(0.0919m, 0.2888m),
            [Qualifica.Dirigente]   = new AliquotaInps(0.0919m, 0.3000m),
            [Qualifica.Apprendista] = new AliquotaInps(0.0584m, 0.1161m),
        },

        // Addizionale regionale IRPEF (aliquota piatta di esempio per tutte le 20 regioni;
        // i nomi coincidono con quelli del dataset comuni del frontend).
        AddizionaleRegionale = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Abruzzo"]                          = 0.0173m,
            ["Basilicata"]                       = 0.0123m,
            ["Calabria"]                         = 0.0173m,
            ["Campania"]                         = 0.0203m,
            ["Emilia-Romagna"]                   = 0.0143m,
            ["Friuli-Venezia Giulia"]            = 0.0123m,
            ["Lazio"]                            = 0.0173m,
            ["Liguria"]                          = 0.0123m,
            ["Lombardia"]                        = 0.0123m,
            ["Marche"]                           = 0.0123m,
            ["Molise"]                           = 0.0173m,
            ["Piemonte"]                         = 0.0162m,
            ["Puglia"]                           = 0.0133m,
            ["Sardegna"]                         = 0.0123m,
            ["Sicilia"]                          = 0.0123m,
            ["Toscana"]                          = 0.0142m,
            ["Trentino-Alto Adige/Südtirol"]     = 0.0123m,
            ["Umbria"]                           = 0.0123m,
            ["Valle d'Aosta/Vallée d'Aoste"]     = 0.0123m,
            ["Veneto"]                           = 0.0123m,
        },

        // Addizionale comunale IRPEF per codice catastale del comune (esempio).
        AddizionaleComunale = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["F205"] = 0.0080m, // Milano
            ["H501"] = 0.0090m, // Roma
            ["F839"] = 0.0080m, // Napoli
            ["L736"] = 0.0080m, // Venezia
        },

        DivisoreOrario = 168m,
        MaggiorazioneStraordinario = 0.15m,
        MaggiorazioneStraordinarioNotturno = 0.50m,
        MaggiorazioneStraordinarioFestivo = 0.30m,
        RegoleCcnl = new Dictionary<string, RegoleCcnl>(StringComparer.OrdinalIgnoreCase)
        {
            ["COMMERCIO_TERZIARIO"] = new RegoleCcnl(
                DivisoreOrario: 168m,
                DivisoreGiornaliero: 26m,
                MaggiorazioneStraordinario: 0.15m,
                MaggiorazioneStraordinarioNotturno: 0.50m,
                MaggiorazioneStraordinarioFestivo: 0.30m,
                GiorniFerieMese: 2.1667m,
                OrePermessiMese: 7.3333m,
                OrePermessiMeseOltre15: 8.6667m,
                ScattiOgniMesi: 36,
                ScattiMassimo: 10,
                MeseErogazioneTredicesima: 12,
                MeseErogazioneQuattordicesima: 7,
                AssistenzaSanitariaLavoratoreMese: 2m,
                AssistenzaSanitariaDatoreMese: 13m,
                EnteBilateraleAliquotaLavoratore: 0.0005m,
                EnteBilateraleAliquotaDatore: 0.001m,
                OreRolAnno: 56m,
                OreRolAnnoOltre15: 72m,
                OreExFestivitaAnno: 32m,
                MesiAnzianitaSenzaRol: 24,
                MesiAnzianitaRolRidotto: 24,
                PercentualeRolRidotta: 0.5m),
        },
        TfrDivisore = 13.5m,
        TfrContributoFondo = 0.005m,
    };
}
