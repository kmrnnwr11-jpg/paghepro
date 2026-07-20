namespace Paghe.Persistence.Ai;

/// <summary>Classe di dato del catalogo G0 (ordine crescente di sensibilità dove sensato).</summary>
public enum ClasseDati
{
    /// <summary>Normativo/pubblico: parametri, scaglioni, CCNL.</summary>
    D0,

    /// <summary>Aziendale: fatture, scadenze, contabilità, dati societari.</summary>
    D1,

    /// <summary>Personale di lavoro: anagrafiche, rapporto, paga, presenze.</summary>
    D2,

    /// <summary>Particolare art. 9 (salute, sindacato): richiede grant GS, non concedibile in G2.</summary>
    D3A,

    /// <summary>Penale art. 10: disciplina separata, non concedibile in G2.</summary>
    D3B,

    /// <summary>Segreto operativo (password, chiavi, token): mai nel contesto del modello.</summary>
    D4,

    /// <summary>Control plane (audit, policy, grant, consumi): non è una risorsa AI.</summary>
    ControlPlane,
}

public sealed record RisorsaCatalogata(string Nome, string Modulo, ClasseDati Classe, string Note);

/// <summary>
/// Catalogo delle risorse professionali raggiungibili dall'AI (G0 §risorse): ogni tabella/vista
/// nota è censita con modulo e classe dato; qualunque risorsa NON censita è negata per
/// costruzione (default DENY, piano §3.4). Le classi D3A/D3B/D4/ControlPlane sono sempre
/// negate in G2: i grant GS e il control plane restano fuori dal perimetro del modello.
/// </summary>
public static class ProfessionalResourceCatalog
{
    private static readonly Dictionary<string, RisorsaCatalogata> Risorse =
        new(StringComparer.OrdinalIgnoreCase);

    static ProfessionalResourceCatalog()
    {
        // --- D0: normativo/pubblico ---
        Aggiungi("ParametriNormativi", "Parametri", ClasseDati.D0, "Aliquote, scaglioni, addizionali per anno.");
        Aggiungi("Ccnl", "Parametri", ClasseDati.D0, "Contratti collettivi e livelli.");

        // --- D1: aziendale ---
        Aggiungi("Aziende", "Anagrafiche", ClasseDati.D1, "Anagrafica datori (l'IBAN resta cifrato e fuori manifest).");
        Aggiungi("Fatture", "Fatturazione", ClasseDati.D1, "Fatture attive con righe.");
        Aggiungi("Spese", "Contabilita", ClasseDati.D1, "Spese/acquisti registrati.");
        Aggiungi("LiquidazioniIva", "Contabilita", ClasseDati.D1, "Liquidazioni IVA di periodo.");
        Aggiungi("Bilanci", "Contabilita", ClasseDati.D1, "Bilanci riclassificati e righe.");
        Aggiungi("Scadenzario", "Adempimenti", ClasseDati.D1, "Promemoria e scadenze generate.");
        Aggiungi("Durc", "Adempimenti", ClasseDati.D1, "DURC registrati con scadenza.");
        Aggiungi("PraticheF24", "Adempimenti", ClasseDati.D1, "Pratiche F24 con stati.");
        Aggiungi("AvvisiPagamento", "Fatturazione", ClasseDati.D1, "Solleciti clienti (IBAN cifrato escluso).");
        Aggiungi("PraticheComUnica", "Pratiche", ClasseDati.D1, "Pratiche apertura/variazione P.IVA.");
        Aggiungi("Immobili", "Fiscale", ClasseDati.D1, "Immobili per IMU.");
        Aggiungi("PosizioniInail", "Adempimenti", ClasseDati.D1, "Posizioni INAIL aziendali.");
        Aggiungi("Deleghe", "Trasmissione", ClasseDati.D1, "Deleghe all'intermediario.");
        Aggiungi("TrasmissioniFatture", "Trasmissione", ClasseDati.D1, "Stati e ricevute SdI.");

        // --- D2: personale di lavoro ---
        Aggiungi("Persone", "Anagrafiche", ClasseDati.D2, "Persone fisiche (rappresentanti, soci).");
        Aggiungi("Dipendenti", "Paghe", ClasseDati.D2, "Anagrafica lavoratori (IBAN cifrato escluso).");
        Aggiungi("Rapporti", "Paghe", ClasseDati.D2, "Rapporti di lavoro e inquadramento.");
        Aggiungi("Cartellini", "Paghe", ClasseDati.D2, "Presenze e variabili mensili SENZA la terna malattia/infortunio.");
        Aggiungi("Cedolini", "Paghe", ClasseDati.D2, "Cedolini archiviati (voci cifrate lette via servizio).");
        Aggiungi("DichiarazioniRedditiPf", "Fiscale", ClasseDati.D2, "Dichiarazioni redditi persone fisiche.");

        // --- D3A: particolare art. 9 — censita ma NON concedibile in G2 (serve grant GS, fase G5+) ---
        Aggiungi("Cartellini.Malattia", "Paghe", ClasseDati.D3A, "Terna malattia/infortunio: cifrata, esclusa dai grant G2.");

        // --- D4 / control plane: censiti per completezza, sempre negati al modello ---
        Aggiungi("Utenti", "Sicurezza", ClasseDati.D4, "Credenziali, hash password, TOTP: mai al modello.");
        Aggiungi("Tenants", "Sicurezza", ClasseDati.ControlPlane, "Anagrafica studi: control plane.");
        Aggiungi("ConfigurazioniSdi", "Trasmissione", ClasseDati.D4, "ApiKey provider (cifrata): mai al modello.");
        Aggiungi("AuditLog", "Sicurezza", ClasseDati.ControlPlane, "Registro attività GDPR.");
        Aggiungi("AiAudit", "Sicurezza", ClasseDati.ControlPlane, "Audit del Governor stesso.");
        Aggiungi("AiScopeGrants", "Sicurezza", ClasseDati.ControlPlane, "I mandati non sono risorse AI.");
        Aggiungi("ConsumiAi", "Sicurezza", ClasseDati.ControlPlane, "Contatori d'uso AI.");
    }

    private static void Aggiungi(string nome, string modulo, ClasseDati classe, string note) =>
        Risorse[nome] = new RisorsaCatalogata(nome, modulo, classe, note);

    /// <summary>Null se la risorsa non è censita: per il piano equivale a negata.</summary>
    public static RisorsaCatalogata? Classifica(string risorsa) =>
        string.IsNullOrWhiteSpace(risorsa) ? null : Risorse.GetValueOrDefault(risorsa.Trim());

    /// <summary>Le sole classi concedibili a un grant G2 (S0-S2, A1-A2, senza scritture).</summary>
    public static bool ConcedibileInG2(ClasseDati classe) =>
        classe is ClasseDati.D0 or ClasseDati.D1 or ClasseDati.D2;

    public static IReadOnlyCollection<RisorsaCatalogata> Tutte => Risorse.Values;
}
