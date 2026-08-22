using Paghe.Persistence.Ai;
using Paghe.Persistence.Sicurezza;

namespace Paghe.Api;

/// <summary>
/// Contratto di uno strumento Aurora (ToolDescriptor del piano Aurora §3.1): ogni strumento
/// dichiara effetto, resa in UI, ruolo minimo, risorsa del catalogo, operazione del Governor
/// e il gate di piano che lo attiva. Uno strumento non registrato non esiste per il modello.
/// </summary>
public sealed record DescrittoreStrumento(
    string Nome,
    string DescrizioneUtente,
    string DescrizioneModello,
    object Schema,
    string Effetto,          // lettura | scrittura_reversibile | navigazione
    string ResaUi,           // testo | tabella | diff | navigazione
    string RuoloMinimo,      // sola_lettura | operatore | admin
    string Risorsa,          // nome nel ProfessionalResourceCatalog
    string Operazione,       // read | search | draft
    string Gate,             // A2 (attivo oggi) | A3 | A4 (fasi future)
    bool Idempotente,
    string Undo);

/// <summary>Contesto server-side di un turno: identità dal token, mandato dal Governor.</summary>
public sealed record ContestoAurora(
    Guid TenantId, Guid UtenteId, string? Ruolo, string? Lease, ManifestGrant? Manifest);

/// <summary>Risultato di una lettura: il JSON per il modello più i dati per il SessionLog.</summary>
public sealed record RisultatoStrumentoAurora(
    string ContenutoJson, bool Negato, string? Motivo, int Righe);

/// <summary>
/// Unico esecutore ammesso per le letture A2. I plugin dichiarano il contratto, ma non
/// ricevono accesso diretto ai dati: tenant, scope, quota e revoca restano nel broker.
/// </summary>
public interface IEsecutoreLettureAurora
{
    Task<RisultatoStrumentoAurora> EseguiAsync(
        ContestoAurora contesto, DescrittoreStrumento descrittore,
        string argomentiJson, CancellationToken ct);
}

/// <summary>Proposta preparata da uno strumento di scrittura, prima della persistenza.</summary>
public sealed record PropostaAurora(
    string Titolo, IReadOnlyList<RigaDiffAurora> Diff, bool PuoProcedere, string? MotivoBlocco);

/// <summary>Proposta firmata come arriva alla SPA: il browser rimanda soltanto il token.</summary>
public sealed record PropostaAzioneAurora(
    string Token,
    string Strumento,
    string Titolo,
    IReadOnlyList<RigaDiffAurora> Diff,
    string ResaUi,
    DateTime ScadeUtc);

/// <summary>
/// Uno strumento del runtime. Le letture dichiarano soltanto il descrittore e passano
/// dall'esecutore centrale; le scritture usano <see cref="PreparaAsync"/> (diff per l'utente)
/// e <see cref="ApplicaAsync"/>
/// (eseguita solo dopo la conferma col token, dentro i servizi normali del programma).
/// </summary>
public interface IStrumentoAurora
{
    DescrittoreStrumento Descrittore { get; }

    Task<PropostaAurora> PreparaAsync(
        ContestoAurora contesto, string argomentiJson, CancellationToken ct) =>
        throw new InvalidOperationException($"Lo strumento {Descrittore.Nome} non prepara scritture.");

    Task<(bool Riuscita, string EsitoJson)> ApplicaAsync(
        ContestoAurora contesto, string argomentiJson, CancellationToken ct) =>
        throw new InvalidOperationException($"Lo strumento {Descrittore.Nome} non applica scritture.");
}

/// <summary>
/// AiToolRegistry del piano §7.1, prima versione dinamica: gli strumenti sono plugin
/// registrati nel contenitore, default DENY. Il catalogo offerto al modello è filtrato per
/// gate di piano, ruolo dell'operatore e mandato attivo (risorse e operazioni del manifest):
/// uno strumento fuori mandato non viene nemmeno dichiarato.
/// </summary>
public sealed class AiToolRegistry(IEnumerable<IStrumentoAurora> strumenti)
{
    /// <summary>L'unico gate di strumenti attivo oggi: A3/A4 restano dichiarati ma spenti.</summary>
    public const string GateAttivo = "A2";

    public IReadOnlyList<IStrumentoAurora> Tutti { get; } = strumenti.ToArray();

    public IStrumentoAurora? Trova(string nome) =>
        Tutti.FirstOrDefault(s => string.Equals(s.Descrittore.Nome, nome, StringComparison.Ordinal));

    public IReadOnlyList<IStrumentoAurora> PerMandato(string? ruolo, ManifestGrant manifest) =>
        [.. Tutti.Where(s =>
            string.Equals(s.Descrittore.Gate, GateAttivo, StringComparison.Ordinal)
            && RuoloAmmesso(ruolo, s.Descrittore)
            && manifest.Risorse.Contains(s.Descrittore.Risorsa, StringComparer.OrdinalIgnoreCase)
            && manifest.Operazioni.Contains(s.Descrittore.Operazione, StringComparer.OrdinalIgnoreCase))];

    /// <summary>Traduzione del descrittore nel formato function-calling del provider.</summary>
    public static StrumentoAi PerModello(DescrittoreStrumento d) =>
        new(d.Nome, d.DescrizioneModello, d.Schema, Scrittura: d.Effetto != "lettura");

    /// <summary>
    /// Aurora agisce col ruolo dell'utente, mai oltre: sola_lettura non raggiunge le
    /// scritture, le azioni da admin restano da admin (stessa scala di RuoloPolicy).
    /// </summary>
    public static bool RuoloAmmesso(string? ruolo, DescrittoreStrumento d)
    {
        bool amministratore = string.Equals(ruolo, RuoloPolicy.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ruolo, "superadmin", StringComparison.OrdinalIgnoreCase);
        bool operatore = amministratore
            || string.Equals(ruolo, RuoloPolicy.Operatore, StringComparison.OrdinalIgnoreCase);
        bool solaLettura = string.Equals(ruolo, RuoloPolicy.SolaLettura, StringComparison.OrdinalIgnoreCase);

        if (d.Effetto != "lettura" && !operatore) return false;
        return d.RuoloMinimo.ToLowerInvariant() switch
        {
            "admin" => amministratore,
            "operatore" => operatore,
            "sola_lettura" => operatore || solaLettura,
            _ => false,
        };
    }
}
