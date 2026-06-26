namespace Paghe.Core.PartiteIva;

/// <summary>
/// Genera una checklist pratica per apertura partita IVA e, quando dovuta, Comunicazione
/// Unica/Camera di Commercio. Non effettua invii telematici: prepara dati e controlli.
/// </summary>
public static class PianoAperturaPartitaIvaBuilder
{
    public static PianoAperturaPartitaIva Crea(PraticaAperturaPartitaIva pratica)
    {
        Valida(pratica);

        bool personaFisica = pratica.FormaGiuridica is FormaGiuridicaApertura.ProfessionistaPersonaFisica
            or FormaGiuridicaApertura.ImpresaIndividuale;
        bool impresa = pratica.FormaGiuridica != FormaGiuridicaApertura.ProfessionistaPersonaFisica;
        string modelloAe = personaFisica ? "AA9/12" : "AA7/10";

        var adempimenti = new List<AdempimentoAperturaPartitaIva>
        {
            new("AE-INIZIO-IVA", "Agenzia Entrate",
                $"Dichiarazione di inizio attività IVA con modello {modelloAe}.", true),
        };

        if (impresa)
        {
            adempimenti.Add(new("COMUNICA", "Registro Imprese",
                "Preparazione e invio della Comunicazione Unica d'impresa.", true));
            adempimenti.Add(new("RI-REA", "Camera di Commercio",
                "Iscrizione al Registro Imprese e alimentazione posizione REA.", true));
        }
        else
        {
            adempimenti.Add(new("PREVIDENZA", "INPS / Cassa professionale",
                "Verifica gestione previdenziale applicabile al professionista.", true));
        }

        if (pratica.AttivitaCommercio)
        {
            adempimenti.Add(new("INPS-COM", "INPS",
                "Verifica iscrizione alla gestione commercianti.", true));
            adempimenti.Add(new("SUAP-SCIA", "SUAP",
                "Verifica eventuale SCIA/autorizzazione comunale per attività commerciale.", true));
        }

        if (pratica.AttivitaArtigiana)
        {
            adempimenti.Add(new("ALBO-ART", "Camera di Commercio",
                "Verifica iscrizione all'Albo delle Imprese Artigiane.", true));
            adempimenti.Add(new("INPS-ART", "INPS",
                "Verifica iscrizione alla gestione artigiani.", true));
        }

        if (pratica.HaDipendentiPrevisti)
            adempimenti.Add(new("INPS-DIP", "INPS",
                "Apertura/aggiornamento posizione contributiva per lavoratori dipendenti.", true));

        if (pratica.SoggettaInail)
            adempimenti.Add(new("INAIL", "INAIL",
                "Apertura posizione assicurativa territoriale e classificazione rischio.", true));

        adempimenti.Add(new("CONTABILITA", "Studio / Contabilità",
            pratica.RegimeContabile == RegimeContabilePartitaIva.Forfettario
                ? "Configurazione regime forfettario e conservazione fatture elettroniche."
                : "Configurazione registri IVA vendite/acquisti, liquidazioni periodiche e conservazione documenti.",
            true));

        var avvertenze = Avvertenze(pratica, impresa).ToList();

        return new PianoAperturaPartitaIva
        {
            ModelloAgenziaEntrate = modelloAe,
            RichiedeComunicazioneUnica = impresa,
            RichiedeRegistroImprese = impresa,
            RichiedeCameraCommercio = impresa,
            Adempimenti = adempimenti,
            Avvertenze = avvertenze,
        };
    }

    private static void Valida(PraticaAperturaPartitaIva p)
    {
        if (string.IsNullOrWhiteSpace(p.Denominazione))
            throw new ArgumentException("La denominazione/nominativo è obbligatoria.");
        if (string.IsNullOrWhiteSpace(p.CodiceFiscale))
            throw new ArgumentException("Il codice fiscale è obbligatorio.");
        if (string.IsNullOrWhiteSpace(p.CodiceAteco))
            throw new ArgumentException("Il codice ATECO è obbligatorio.");
        if (string.IsNullOrWhiteSpace(p.DescrizioneAttivita))
            throw new ArgumentException("La descrizione attività è obbligatoria.");
        if (string.IsNullOrWhiteSpace(p.IndirizzoSede) ||
            string.IsNullOrWhiteSpace(p.Cap) ||
            string.IsNullOrWhiteSpace(p.Comune) ||
            string.IsNullOrWhiteSpace(p.Provincia))
            throw new ArgumentException("Sede, CAP, comune e provincia sono obbligatori.");
        if (p.DataInizioAttivita == default)
            throw new ArgumentException("La data inizio attività è obbligatoria.");
    }

    private static IEnumerable<string> Avvertenze(PraticaAperturaPartitaIva p, bool impresa)
    {
        yield return "Il software prepara la pratica: l'invio telematico richiede canali/credenziali ufficiali o intermediario abilitato.";

        if (impresa && string.IsNullOrWhiteSpace(p.Pec))
            yield return "Per le pratiche al Registro Imprese serve normalmente un domicilio digitale/PEC dell'impresa.";

        if (p.RegimeContabile == RegimeContabilePartitaIva.Forfettario && (p.AttivitaCommercio || p.AttivitaArtigiana))
            yield return "Il regime forfettario va verificato con i requisiti soggettivi, cause di esclusione e limiti vigenti.";

        if (p.AttivitaCommercio)
            yield return "Le attività commerciali possono richiedere SCIA/autorizzazioni SUAP comunali prima dell'avvio effettivo.";

        if (!string.IsNullOrWhiteSpace(p.PartitaIva))
            yield return "La partita IVA indicata viene trattata come già attribuita: verificare coerenza con Agenzia Entrate.";
    }
}
