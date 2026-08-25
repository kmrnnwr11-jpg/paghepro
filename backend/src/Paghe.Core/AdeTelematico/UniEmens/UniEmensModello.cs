namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Modello del flusso UniEmens: la denuncia mensile che il datore o l'intermediario trasmette
/// all'INPS con retribuzioni e contributi dei lavoratori dipendenti.
///
/// La struttura segue il tracciato ufficiale (schema di validazione INPS 4.32.0, allegato
/// tecnico 4.32.3 del 31/07/2026): radice <c>DenunceMensili</c>, un mittente, una o più
/// aziende, ciascuna con la propria posizione contributiva (matricola INPS), le denunce
/// individuali dei lavoratori e la denuncia aziendale di quadratura.
///
/// Il modello copre il caso ordinario del lavoro dipendente. Le gestioni speciali del
/// tracciato (CIG, collaboratori, sport e spettacolo, pubblica amministrazione, agricoltura)
/// non sono rappresentate: vanno aggiunte quando servono, non simulate.
/// </summary>
public sealed record FlussoUniEmens(
    MittenteUniEmens Mittente,
    AziendaUniEmens Azienda);

/// <summary>
/// Chi trasmette il flusso. <paramref name="Tipo"/> 1 = azienda che invia per sé,
/// 2 = intermediario/consulente, 4 = associazione di categoria (appendice A).
/// I codici fiscali sono quelli della persona fisica che firma, del soggetto mittente e
/// della software house che ha prodotto il file.
/// </summary>
public sealed record MittenteUniEmens(
    string CfPersonaMittente,
    string RagioneSociale,
    string CfMittente,
    string CfSoftwarehouse,
    string? SedeInps = null,
    int Tipo = 1);

/// <summary>
/// Testata dell'azienda denunciante: quel che serve per aprire il flusso, prima di conoscere
/// i lavoratori. La usa <see cref="UniEmensDaCedolini"/> per costruire il resto.
/// </summary>
public sealed record TestataAziendaUniEmens(
    int Anno,
    int Mese,
    string CfAzienda,
    string RagioneSociale,
    string Matricola,
    string Composizione = "FP");

/// <summary>Azienda denunciante nel mese di competenza.</summary>
public sealed record AziendaUniEmens(
    int Anno,
    int Mese,
    string CfAzienda,
    string RagioneSociale,
    PosizioneContributivaUniEmens Posizione);

/// <summary>
/// Posizione contributiva INPS (matricola a 10 cifre). <paramref name="Composizione"/> indica
/// come è composto il flusso della posizione: "FP" = flusso pieno, cioè tutti i lavoratori
/// del mese.
/// </summary>
public sealed record PosizioneContributivaUniEmens(
    string Matricola,
    IReadOnlyList<DenunciaIndividualeUniEmens> Lavoratori,
    DenunciaAziendaleUniEmens Aziendale,
    string Composizione = "FP");

/// <summary>
/// Denuncia individuale di un lavoratore: inquadramento e dati retributivi del mese.
/// I campi opzionali restano <c>null</c> quando il dato non è disponibile in anagrafica:
/// l'elemento non viene scritto affatto, non viene inventato un valore plausibile.
/// </summary>
public sealed record DenunciaIndividualeUniEmens(
    string CodiceFiscale,
    string Cognome,
    string Nome,
    string Qualifica1,
    DatiRetributiviUniEmens Retribuzione,
    string? Qualifica2 = null,
    string? Qualifica3 = null,
    string? TipoContribuzione = null,
    string? RegimePost95 = null,
    string? Cittadinanza = null,
    // Obbligatorie per i controlli INPS DR01923: in assenza di unità operativa sulla
    // posizione contributiva l'elemento va comunque esposto a zero (circ. n. 197/2015).
    int UnitaOperativa = 0,
    int UnitaProduttiva = 0,
    string? CodiceComune = null,
    string? CodiceContratto = null,
    string? TipoCodiceContratto = null,
    string? QualProf = null,
    string? TipoPaga = null,
    int? DivisoreOrarioContr = null,
    int? OrarioContrattuale = null,
    int? PercPartTime = null,
    int? PercPartTimeMese = null,
    int? NumMensilita = null);

/// <summary>
/// Dati retributivi del mese. Attenzione ai formati del tracciato, diversi fra loro:
/// l'imponibile è in euro interi, il contributo ha i centesimi con la virgola, le ore sono
/// in centesimi di ora (4600 = 46 ore).
/// </summary>
public sealed record DatiRetributiviUniEmens(
    decimal Imponibile,
    decimal Contributo,
    int GiorniRetribuiti,
    IReadOnlyList<SettimanaUniEmens> Settimane,
    IReadOnlyList<GiornoUniEmens> Giorni,
    string? TipoLavoratore = null,
    decimal? RetribTeorica = null,
    int? OreLavorabili = null,
    /// <summary>
    /// Giorni con contribuzione versata nel mese. Il controllo INPS DR00085 lo pretende
    /// ogni volta che c'è un contributo; per i part-time si usa invece OreContribuite.
    /// </summary>
    int? GiorniContribuiti = null,
    int? OreContribuite = null,
    string? RispettoMinimale = null,
    int? SettimaneUtili = null)
{
    /// <summary>
    /// Retribuzione persa per gli eventi figurativi del mese. Le quote per identificativo
    /// evento sono esposte solo quando il tracciato ufficiale le richiede e il dato è noto.
    /// </summary>
    public IReadOnlyList<DifferenzaAccreditoUniEmens> DifferenzeAccredito { get; init; } = [];
}

/// <summary>
/// Importo non retribuito associato a un evento figurativo. Il codice e l'importo seguono
/// <c>DifferenzeAccredito</c> dello schema ufficiale incluso nel progetto.
/// </summary>
public sealed record DifferenzaAccreditoUniEmens(string CodiceEvento, decimal Importo)
{
    public IReadOnlyList<QuotaDifferenzaAccreditoUniEmens> QuoteEvento { get; init; } = [];
}

/// <summary>
/// Ripartizione della differenza per il vero identificativo dell'evento, prevista da
/// <c>InfoEvento</c>. Non contiene RMG, ratei o lordizzazione, che non sono campi XML.
/// </summary>
public sealed record QuotaDifferenzaAccreditoUniEmens(
    string MotivoEvento,
    string TipoMotivoEvento,
    decimal Importo);

/// <summary>
/// Settimana di calendario INPS coperta dalla contribuzione. <paramref name="TipoCopertura"/>:
/// "X" = interamente retribuita, "1"/"2" = parziale, "0" = non retribuita, "D" = differita.
/// </summary>
public sealed record SettimanaUniEmens(int Id, string TipoCopertura = "X")
{
    /// <summary>
    /// Eventi figurativi che hanno determinato una copertura settimanale "1" o "2".
    /// Il tracciato ammette più codici, purché distinti.
    /// </summary>
    public IReadOnlyList<string> CodiciEvento { get; init; } = [];
}

/// <summary>
/// Dettaglio di un evento esposto nel calendario giornaliero. I soli dati serializzati sono
/// quelli previsti da <c>EventoGiorn</c> nello XSD incorporato: codice, eventuali ore e una
/// eventuale informazione aggiuntiva tipizzata. RMG, ratei e lordizzazione sono prerequisiti
/// del calcolo paghe, non elementi del tracciato UniEmens.
/// </summary>
public sealed record EventoGiornalieroUniEmens(string CodiceEvento)
{
    /// <summary>Ore dell'evento in centesimi; null per un evento che copre l'intera giornata.</summary>
    public int? NumeroOreEvento { get; init; }

    /// <summary>Codice a due caratteri dell'attributo TipoInfoAggEvento (es. CM).</summary>
    public string? TipoInformazioneAggiuntiva { get; init; }

    public string? InformazioneAggiuntiva { get; init; }
}

/// <summary>
/// Giorno reale del cartellino riportato nel calendario UniEmens. La copertura giornaliera e
/// gli eventi seguono esattamente la struttura di <c>Giorno</c> nello XSD ufficiale incluso.
/// </summary>
public sealed record GiornoUniEmens(int Giorno, bool Lavorato)
{
    public string? TipoCopertura { get; init; }
    public IReadOnlyList<EventoGiornalieroUniEmens> Eventi { get; init; } = [];
}

/// <summary>
/// Denuncia aziendale: totali del mese e quadratura. <paramref name="TrattQuotaLav"/> "S"
/// indica che il datore trattiene la quota a carico del lavoratore.
/// </summary>
public sealed record DenunciaAziendaleUniEmens(
    int NumLavoratori,
    int ForzaAziendale,
    int NumDenIndiv,
    decimal TotaleADebito,
    decimal TotaleACredito,
    string TrattQuotaLav = "S");
