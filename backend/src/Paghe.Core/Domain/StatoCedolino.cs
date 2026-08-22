namespace Paghe.Core.Domain;

/// <summary>
/// Stato operativo del cedolino archiviato. Solo una bozza può essere sostituita da un nuovo
/// calcolo; validazione e chiusura lavorano sulla stessa fotografia immutabile. I documenti
/// anteriori all'introduzione del workflow restano consultabili come archivio separato.
/// </summary>
public enum StatoCedolino
{
    Bozza,
    Controllata,
    Validata,
    Chiusa,

    /// <summary>
    /// Cedolino storico nato prima della macchina a stati: immutabile, non transitabile e mai
    /// presentato come definitivo perché non possiede le attestazioni del nuovo workflow.
    /// </summary>
    PreWorkflow,
}
