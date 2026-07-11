using System.Text;

namespace Paghe.Core.PartiteIva;

/// <summary>
/// Produce la distinta di riepilogo della pratica ComUnica: tutti i dati raccolti,
/// gli enti coinvolti e gli adempimenti con il loro stato, in un testo pronto da
/// consegnare all'intermediario o da ricopiare su ComunicaStarweb/DIRE.
/// Non è il tracciato telematico ufficiale (che richiede XSD/firma/Telemaco).
/// </summary>
public static class DistintaComUnica
{
    public static string Genera(
        PraticaAperturaPartitaIva pratica,
        PianoAperturaPartitaIva piano,
        Guid praticaId,
        string statoPratica,
        DateTime creataIl)
    {
        var sb = new StringBuilder();

        sb.AppendLine("====================================================================");
        sb.AppendLine(" COMUNICAZIONE UNICA D'IMPRESA (ComUnica) — DISTINTA DI RIEPILOGO");
        sb.AppendLine("====================================================================");
        sb.AppendLine($"Pratica:       {praticaId}");
        sb.AppendLine($"Creata il:     {creataIl:dd/MM/yyyy}");
        sb.AppendLine($"Stato:         {statoPratica}");
        sb.AppendLine($"Tipo pratica:  {DescriviTipo(pratica.TipoPratica)}");
        sb.AppendLine();

        sb.AppendLine("--- 1. SOGGETTO ----------------------------------------------------");
        sb.AppendLine($"Denominazione:    {pratica.Denominazione}");
        sb.AppendLine($"Codice fiscale:   {pratica.CodiceFiscale}");
        sb.AppendLine($"Partita IVA:      {(string.IsNullOrWhiteSpace(pratica.PartitaIva) ? "(da attribuire)" : pratica.PartitaIva)}");
        sb.AppendLine($"Forma giuridica:  {DescriviForma(pratica.FormaGiuridica)}");
        sb.AppendLine($"Regime contabile: {pratica.RegimeContabile}");
        sb.AppendLine($"Sede:             {pratica.IndirizzoSede}, {pratica.Cap} {pratica.Comune} ({pratica.Provincia})");
        sb.AppendLine($"PEC:              {(string.IsNullOrWhiteSpace(pratica.Pec) ? "(mancante)" : pratica.Pec)}");
        sb.AppendLine();

        sb.AppendLine("--- 2. ATTIVITÀ ----------------------------------------------------");
        sb.AppendLine($"Codice ATECO:     {pratica.CodiceAteco}");
        sb.AppendLine($"Descrizione:      {pratica.DescrizioneAttivita}");
        sb.AppendLine($"Data decorrenza:  {pratica.DataInizioAttivita:dd/MM/yyyy}");
        sb.AppendLine($"Caratteristiche:  {Caratteristiche(pratica)}");
        sb.AppendLine();

        sb.AppendLine("--- 3. ENTI COINVOLTI ----------------------------------------------");
        sb.AppendLine($"Modello Agenzia Entrate:  {piano.ModelloAgenziaEntrate}");
        sb.AppendLine($"Comunicazione Unica:      {SiNo(piano.RichiedeComunicazioneUnica)}");
        sb.AppendLine($"Registro Imprese / REA:   {SiNo(piano.RichiedeRegistroImprese)}");
        sb.AppendLine($"Camera di Commercio:      {SiNo(piano.RichiedeCameraCommercio)}");
        sb.AppendLine();

        sb.AppendLine("--- 4. ADEMPIMENTI -------------------------------------------------");
        foreach (var a in piano.Adempimenti)
            sb.AppendLine($"[{a.Stato,-14}] {a.Ente} — {a.Descrizione}{(a.Obbligatorio ? "" : " (facoltativo)")}");
        sb.AppendLine();

        sb.AppendLine("--- 5. AVVERTENZE --------------------------------------------------");
        foreach (var avvertenza in piano.Avvertenze)
            sb.AppendLine($"- {avvertenza}");
        sb.AppendLine();

        sb.AppendLine("--------------------------------------------------------------------");
        sb.AppendLine("NOTA: questo documento riepiloga i dati per compilare la pratica.");
        sb.AppendLine("L'invio telematico reale della Comunicazione Unica avviene tramite i");
        sb.AppendLine("canali ufficiali InfoCamere (ComunicaStarweb/DIRE con utenza Telemaco)");
        sb.AppendLine("o tramite un intermediario abilitato (commercialista, consulente).");

        return sb.ToString();
    }

    private static string DescriviTipo(TipoPraticaComUnica tipo) => tipo switch
    {
        TipoPraticaComUnica.Variazione => "Variazione dati",
        TipoPraticaComUnica.Cessazione => "Cessazione attività",
        _ => "Nuova impresa / inizio attività",
    };

    private static string DescriviForma(FormaGiuridicaApertura forma) => forma switch
    {
        FormaGiuridicaApertura.ProfessionistaPersonaFisica => "Professionista / persona fisica",
        FormaGiuridicaApertura.ImpresaIndividuale => "Impresa individuale",
        FormaGiuridicaApertura.SocietaPersone => "Società di persone",
        FormaGiuridicaApertura.SocietaCapitali => "Società di capitali",
        _ => forma.ToString(),
    };

    private static string Caratteristiche(PraticaAperturaPartitaIva p)
    {
        var attive = new List<string>();
        if (p.AttivitaCommercio) attive.Add("commercio");
        if (p.AttivitaArtigiana) attive.Add("artigiana");
        if (p.ImpresaAgricola) attive.Add("agricola");
        if (p.HaDipendentiPrevisti) attive.Add("dipendenti previsti");
        if (p.SoggettaInail) attive.Add("soggetta INAIL");
        return attive.Count > 0 ? string.Join(", ", attive) : "nessuna specifica";
    }

    private static string SiNo(bool v) => v ? "SÌ" : "NO";
}
