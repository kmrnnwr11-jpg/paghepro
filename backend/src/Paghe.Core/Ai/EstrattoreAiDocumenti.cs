namespace Paghe.Core.Ai;

/// <summary>Un campo proposto dall'AI: valore letto dal documento e confidenza 0-1.</summary>
public sealed record CampoProposto(string Nome, string? Valore, decimal Confidenza);

/// <summary>Proposta di estrazione prodotta dall'AI per un documento.</summary>
public sealed record PropostaAi(IReadOnlyList<CampoProposto> Campi);

/// <summary>
/// Lettore AI di documenti: dato il testo di un documento, PROPONE i valori dei campi
/// richiesti. Regole architetturali del prodotto: l'AI legge e propone soltanto — la
/// conferma resta sempre all'utente e i calcoli restano nel codice deterministico.
/// Il default è <see cref="EstrattoreAiSpento"/>: nessuna chiamata esterna.
/// </summary>
public interface IEstrattoreAiDocumenti
{
    /// <summary>False = il livello AI non è configurato: il flusso resta solo euristico.</summary>
    bool Attivo { get; }

    /// <summary>True = il provider può leggere direttamente foto/scansioni (modello con "vista").</summary>
    bool VedeImmagini { get; }

    /// <summary>
    /// Propone i valori dei campi indicati leggendo il testo del documento.
    /// Null se l'AI è spenta o non produce una proposta utilizzabile.
    /// </summary>
    Task<PropostaAi?> EstraiAsync(
        string tipoDocumento,
        string testoDocumento,
        IReadOnlyList<string> campiRichiesti,
        CancellationToken ct = default);

    /// <summary>
    /// Propone i valori dei campi leggendo direttamente l'immagine del documento
    /// (foto/scansione): il modello con la vista vede anche il layout, quindi rende
    /// meglio dell'OCR sulle foto difficili. Null se la vista non è disponibile.
    /// </summary>
    Task<PropostaAi?> EstraiDaImmagineAsync(
        string tipoDocumento,
        byte[] immagine,
        string mimeType,
        IReadOnlyList<string> campiRichiesti,
        CancellationToken ct = default);
}

/// <summary>Implementazione di default: AI disattivata, nessuna rete, nessuna proposta.</summary>
public sealed class EstrattoreAiSpento : IEstrattoreAiDocumenti
{
    public bool Attivo => false;

    public bool VedeImmagini => false;

    public Task<PropostaAi?> EstraiAsync(
        string tipoDocumento, string testoDocumento,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default) =>
        Task.FromResult<PropostaAi?>(null);

    public Task<PropostaAi?> EstraiDaImmagineAsync(
        string tipoDocumento, byte[] immagine, string mimeType,
        IReadOnlyList<string> campiRichiesti, CancellationToken ct = default) =>
        Task.FromResult<PropostaAi?>(null);
}
