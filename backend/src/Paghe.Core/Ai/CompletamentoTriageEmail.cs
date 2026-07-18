namespace Paghe.Core.Ai;

/// <summary>
/// Proposta di triage per un messaggio email/PEC. Non è mai applicata da sola: resta in attesa
/// finché l'utente non la conferma con un click (Email/PEC sono fuori dal perimetro AI diretto —
/// l'AI propone soltanto).
/// </summary>
public sealed record PropostaTriageEmail(
    bool? Urgente, decimal? ConfidenzaUrgente,
    bool? Pubblicita, decimal? ConfidenzaPubblicita,
    string? Fonte);

/// <summary>
/// Fonde il segnale euristico (header IMAP <c>List-Unsubscribe</c>/<c>List-Id</c> presenti = quasi
/// certamente pubblicità, gratis e senza AI) con la proposta AI sui campi che l'euristica lascia
/// indecisi. Come per la visura camerale: l'euristica vince sempre, l'AI riempie solo i dubbi e
/// solo sopra la soglia di confidenza.
/// </summary>
public static class CompletamentoTriageEmail
{
    /// <summary>Nomi dei campi come richiesti all'AI (stabili: fanno da contratto).</summary>
    public static readonly IReadOnlyList<string> CampiTriage = ["urgente", "pubblicita"];

    public static PropostaTriageEmail Interpreta(
        bool pubblicitaEuristica, PropostaAi? propostaAi, decimal sogliaConfidenza)
    {
        if (pubblicitaEuristica)
            return new PropostaTriageEmail(null, null, true, 0.95m, "Euristica");

        if (propostaAi is null)
            return new PropostaTriageEmail(null, null, null, null, null);

        var (urgente, confidenzaUrgente) = Leggi(propostaAi, "urgente", sogliaConfidenza);
        var (pubblicita, confidenzaPubblicita) = Leggi(propostaAi, "pubblicita", sogliaConfidenza);
        var fonte = urgente is not null || pubblicita is not null ? "Ai" : null;

        return new PropostaTriageEmail(urgente, confidenzaUrgente, pubblicita, confidenzaPubblicita, fonte);
    }

    private static (bool? Valore, decimal? Confidenza) Leggi(
        PropostaAi proposta, string nome, decimal soglia)
    {
        var campo = proposta.Campi.FirstOrDefault(c =>
            string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));
        if (campo is null || campo.Confidenza < soglia || !bool.TryParse(campo.Valore, out var valore))
            return (null, null);
        return (valore, campo.Confidenza);
    }
}
