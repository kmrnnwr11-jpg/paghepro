using Paghe.Core.PartiteIva;

namespace Paghe.Core.Ai;

/// <summary>
/// Fonde l'estrazione euristica della visura con la proposta dell'AI: il parser
/// deterministico VINCE sempre; l'AI riempie solo i campi rimasti vuoti, e solo se
/// la sua confidenza supera la soglia. Restituisce anche l'elenco dei campi riempiti
/// dall'AI, così l'interfaccia può segnalarli all'utente per il controllo finale.
/// </summary>
public static class CompletamentoVisuraAi
{
    /// <summary>Nomi dei campi della visura come richiesti all'AI (stabili: fanno da contratto).</summary>
    public static readonly IReadOnlyList<string> CampiVisura =
    [
        "ragioneSociale", "partitaIva", "codiceFiscale",
        "indirizzo", "cap", "comune", "provincia", "codiceAteco", "legaleRappresentante",
    ];

    /// <summary>Campi mancanti dopo il parser euristico (quelli da chiedere all'AI).</summary>
    public static IReadOnlyList<string> CampiMancanti(DatiAziendaDaVisura d)
    {
        var mancanti = new List<string>();
        void Controlla(string nome, string? valore)
        {
            if (string.IsNullOrWhiteSpace(valore)) mancanti.Add(nome);
        }

        Controlla("ragioneSociale", d.RagioneSociale);
        Controlla("partitaIva", d.PartitaIva);
        Controlla("codiceFiscale", d.CodiceFiscale);
        Controlla("indirizzo", d.Indirizzo);
        Controlla("cap", d.Cap);
        Controlla("comune", d.Comune);
        Controlla("provincia", d.Provincia);
        Controlla("codiceAteco", d.CodiceAteco);
        Controlla("legaleRappresentante", d.LegaleRappresentante);
        return mancanti;
    }

    /// <summary>Applica la proposta AI ai soli campi vuoti. Restituisce i dati fusi e i campi riempiti dall'AI.</summary>
    public static (DatiAziendaDaVisura Dati, IReadOnlyList<string> CampiDallAi) Completa(
        DatiAziendaDaVisura euristica, PropostaAi proposta, decimal sogliaConfidenza)
    {
        var dallAi = new List<string>();

        string? Scegli(string nome, string? valoreEuristico)
        {
            if (!string.IsNullOrWhiteSpace(valoreEuristico)) return valoreEuristico;
            var campo = proposta.Campi.FirstOrDefault(c =>
                string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));
            if (campo is null || string.IsNullOrWhiteSpace(campo.Valore)
                || campo.Confidenza < sogliaConfidenza)
                return valoreEuristico;
            dallAi.Add(nome);
            return campo.Valore.Trim();
        }

        var fusi = new DatiAziendaDaVisura(
            Scegli("ragioneSociale", euristica.RagioneSociale),
            Scegli("partitaIva", euristica.PartitaIva),
            Scegli("codiceFiscale", euristica.CodiceFiscale),
            Scegli("indirizzo", euristica.Indirizzo),
            Scegli("cap", euristica.Cap),
            Scegli("comune", euristica.Comune),
            Scegli("provincia", euristica.Provincia),
            Scegli("codiceAteco", euristica.CodiceAteco),
            Scegli("legaleRappresentante", euristica.LegaleRappresentante));

        return (fusi, dallAi);
    }
}
