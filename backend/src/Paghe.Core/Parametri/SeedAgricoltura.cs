namespace Paghe.Core.Parametri;

/// <summary>
/// Tabelle agricole di partenza. Come il resto del <see cref="Seed"/> servono a inizializzare
/// il database: qui però i valori NON sono inventati, sono quelli letti su buste paga reali e
/// verificati al centesimo (vedi docs/buste-reali/). Restano da validare con il Consulente del
/// Lavoro e da estendere alle altre province: il CPL cambia in ognuna.
/// </summary>
public static class SeedAgricoltura
{
    /// <summary>
    /// Provincia di Cuneo, 2025 — CCNL operai agricoli e florovivaisti, OTD.
    /// Ricavata dalle buste paga reali di ottobre, novembre e dicembre 2025 (software INAZ).
    /// </summary>
    public static ParametriAgricoli Cuneo2025() => new()
    {
        Anno = 2025,
        Provincia = "CN",
        Fonte = "Buste paga reali OTD Cuneo ott/nov/dic 2025 (INAZ) — docs/buste-reali/busta-05-agricoltura-otd-cuneo-2025.md",

        Tariffe = new[]
        {
            new TariffaAgricola(
                "COMUNE", "Operaio comune OTD (tariffa osservata in busta)",
                SalarioProvincialeOrario: 6.16000m,
                TerzoElementoOrario: 2.40671m),
        },

        // Ordine come sul cedolino reale. Il CAC provinciale è dovuto due volte con aliquote
        // diverse: 0,20% trattenuto al lavoratore e 0,44% a carico dell'azienda.
        Contributi = new[]
        {
            new ContributoAgricolo("32300", "Contributo FAVLA Cuneo", 0.0030m, ACaricoDipendente: true),
            new ContributoAgricolo("32301", "CAC provinciale Cuneo", 0.0020m, ACaricoDipendente: true),
            new ContributoAgricolo("40000", "Contributo FAP (IVS) zona normale", 0.0884m,
                ACaricoDipendente: true, DeducibileIrpef: true),
            new ContributoAgricolo("45000", "CAC nazionale", 0.0020m, ACaricoDipendente: true),
            new ContributoAgricolo("32301A", "CAC provinciale Cuneo c/azienda", 0.0044m, ACaricoDipendente: false),
        },

        // Valore OSSERVATO sulle buste reali (7,80%): finché il consulente non spiega lo
        // scarto dal FAP 8,84%, vince sui flag DeducibileIrpef (vedi ParametriAgricoli).
        AliquotaContributiDeducibili = 0.0780m,
    };
}
