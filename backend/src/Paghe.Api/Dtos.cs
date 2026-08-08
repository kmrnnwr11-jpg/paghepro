using System.Text.Json.Serialization;
using Paghe.Core.AdeTelematico;
using Paghe.Core.Contabilita;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.PartiteIva;

namespace Paghe.Api;

/// <summary>Registrazione di un nuovo account (tenant) con il primo utente amministratore.</summary>
public sealed record RegistraRequest(string RagioneSociale, string Email, string Password);

/// <summary>Credenziali di accesso (con codice 2FA opzionale per chi ha il secondo fattore attivo).</summary>
public sealed record LoginRequest(string Email, string Password, string? Codice2fa = null);

/// <summary>Refresh opaco inviato soltanto dai client mobili e custodito nel portachiavi nativo.</summary>
public sealed record RefreshMobileRequest(string RefreshToken);

public sealed record AccessoResponse(
    string Token,
    DateTimeOffset ScadeUtc,
    Guid TenantId,
    string Ruolo);

public sealed record AccessoMobileResponse(
    string Token,
    DateTimeOffset ScadeUtc,
    Guid TenantId,
    string Ruolo,
    string RefreshToken,
    DateTimeOffset RefreshScadeUtc);

/// <summary>Codice TOTP a 6 cifre per attivare/disattivare il secondo fattore.</summary>
public sealed record Codice2faRequest(string Codice);

/// <summary>Richiesta di bootstrap del super-admin: token segreto di piattaforma.</summary>
public sealed record BootstrapSuperAdminRequest(string Token);

/// <summary>Accesso con Google: "credential" è l'ID token restituito da Google Identity Services.</summary>
public sealed record GoogleLoginRequest(string Credential);

/// <summary>Risposta dell'endpoint tokeninfo di Google (campi in snake_case).</summary>
public sealed class GoogleTokenInfo
{
    [JsonPropertyName("aud")] public string? Aud { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("email_verified")] public string? EmailVerified { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

/// <summary>Dati per il calcolo del ravvedimento operoso su un versamento tardivo.</summary>
public sealed record RavvedimentoRequest(
    decimal Importo, DateOnly Scadenza, DateOnly Versamento, decimal TassoLegaleAnnuo);

/// <summary>Richiesta di calcolo di un cedolino: rapporto di lavoro + cartellino presenze.</summary>
public sealed record CalcolaCedolinoRequest(RapportoLavoro Rapporto, Cartellino Cartellino);

/// <summary>Calcolo inverso: netto voluto in busta, con rapporto e cartellino di riferimento.</summary>
public sealed record CalcolaLordoPerNettoRequest(
    RapportoLavoro Rapporto, Cartellino Cartellino, decimal NettoDesiderato);

/// <summary>Richiesta di generazione PDF del cedolino (con intestazioni azienda/dipendente).</summary>
public sealed record CedolinoPdfRequest(
    RapportoLavoro Rapporto,
    Cartellino Cartellino,
    string Azienda = "Azienda demo",
    string Dipendente = "Dipendente demo");

/// <summary>Creazione di un dipendente e del relativo rapporto di lavoro.</summary>
public sealed record CreaDipendenteRequest(
    string CodiceFiscale,
    string Nome,
    string Cognome,
    string CodiceCcnl,
    string Livello,
    string? Iban = null,
    string Qualifica = "Impiegato",
    int NumeroMensilita = 14,
    bool PartTime = false,
    decimal PercentualePartTime = 100m,
    int ScattiAnzianita = 0,
    decimal Superminimo = 0m,
    string Regione = "",
    string CodiceCatastaleComune = "",
    bool DetrazioniLavoroDipendente = true,
    int GiorniDetrazioneAnno = 365,
    DateOnly? DataAssunzione = null,
    int OreSettimanali = 40,
    string TipoContratto = "Tempo indeterminato");

/// <summary>Richiesta di riclassificazione bilancio: piano dei conti + saldi.</summary>
public sealed record RiclassificaRequest(
    IReadOnlyList<ContoBilancio> PianoConti,
    IReadOnlyList<SaldoConto> Saldi);

/// <summary>Voce di bilancio in ingresso: conto, classificazione CEE e saldo di fine esercizio.</summary>
public sealed record VoceBilancioRequest(string Codice, string Descrizione, MacroVoce Voce, decimal Importo);

/// <summary>Creazione/archiviazione di un bilancio d'esercizio per azienda e anno.</summary>
public sealed record CreaBilancioRequest(int Anno, IReadOnlyList<VoceBilancioRequest> Voci);

/// <summary>Creazione di un bilancio d'esempio (piano dei conti CEE di base) per un anno.</summary>
public sealed record CreaBilancioEsempioRequest(int Anno);

/// <summary>Cambio di stato di un bilancio (valore di StatoBilancio: Bozza/Approvato/Depositato/Annullato).</summary>
public sealed record CambiaStatoBilancioRequest(string Stato);

/// <summary>Richiesta di liquidazione IVA vendite/acquisti per un periodo.</summary>
public sealed record LiquidazioneIvaRequest(
    DateOnly Dal,
    DateOnly Al,
    PeriodicitaIva Periodicita,
    IReadOnlyList<Fattura> FattureAttive,
    IReadOnlyList<Spesa> Spese,
    decimal CreditoPeriodoPrecedente = 0m);

/// <summary>Registrazione di una fattura passiva/spesa nel registro IVA acquisti.</summary>
public sealed record SalvaSpesaRequest(
    DateOnly Data,
    string Descrizione,
    string ContoCosto,
    decimal Imponibile,
    decimal AliquotaIva = 22m,
    decimal PercentualeDetraibilita = 100m,
    string? Fornitore = null,
    string? NumeroDocumento = null);

/// <summary>Creazione di una liquidazione IVA usando fatture e spese persistite.</summary>
public sealed record CreaLiquidazioneIvaRequest(
    DateOnly Dal,
    DateOnly Al,
    PeriodicitaIva Periodicita,
    decimal CreditoPeriodoPrecedente = 0m);

/// <summary>Dati per preparare una pratica di apertura partita IVA / Comunicazione Unica.</summary>
public sealed record CreaPraticaAperturaPartitaIvaRequest(
    FormaGiuridicaApertura FormaGiuridica,
    RegimeContabilePartitaIva RegimeContabile,
    DateOnly DataInizioAttivita,
    string Denominazione,
    string CodiceFiscale,
    string CodiceAteco,
    string DescrizioneAttivita,
    string IndirizzoSede,
    string Cap,
    string Comune,
    string Provincia,
    string? PartitaIva = null,
    string? Pec = null,
    bool AttivitaCommercio = false,
    bool AttivitaArtigiana = false,
    bool HaDipendentiPrevisti = false,
    bool SoggettaInail = false,
    Guid? AziendaId = null,
    TipoPraticaComUnica TipoPratica = TipoPraticaComUnica.NuovaImpresa,
    bool ImpresaAgricola = false);

/// <summary>Aggiornamento stato di un adempimento dentro una pratica.</summary>
public sealed record AggiornaAdempimentoPraticaRequest(string Stato, string? Note = null);

/// <summary>Transizione di stato della pratica ComUnica.</summary>
public sealed record CambiaStatoPraticaComUnicaRequest(string Stato);

/// <summary>Testo libero incollato dall'utente da cui estrarre i dati della pratica.</summary>
public sealed record AnalizzaTestoPraticaRequest(string Testo);

/// <summary>Richiesta di creazione anagrafica azienda.</summary>
public sealed record CreaAziendaRequest(
    string RagioneSociale,
    string Indirizzo,
    string Cap,
    string Comune,
    string Provincia,
    string? PartitaIva = null,
    string? CodiceFiscale = null,
    string Nazione = "IT",
    string RegimeFiscale = "RF01",
    string? Telefono = null,
    string? Email = null,
    string? Pec = null,
    string? CodiceDestinatarioSdi = null,
    string? CodiceCatastale = null,
    string? Settore = null,
    string? CodiceAteco = null,
    string? MatricolaInps = null,
    string? CodiceSedeInps = null,
    string? CscInps = null,
    string? PatInail = null,
    string? CcnlDefault = null,
    string? LivelloDefault = null,
    string? Iban = null,
    string? LegaleRappresentanteNome = null,
    string? LegaleRappresentanteCognome = null,
    string? LegaleRappresentanteCodiceFiscale = null,
    string? CanalePredefinito = null,
    bool HaDipendenti = false,
    string? TipoContribuente = null,
    bool AssoggettataIva = true);

/// <summary>Correzione anagrafica azienda: solo i campi presenti (non null) vengono cambiati.</summary>
public sealed record CorreggiAziendaRequest(
    string? RagioneSociale = null,
    string? PartitaIva = null,
    string? CodiceFiscale = null,
    string? Indirizzo = null,
    string? Cap = null,
    string? Comune = null,
    string? Provincia = null,
    string? Telefono = null,
    string? Email = null,
    string? Pec = null,
    string? CodiceDestinatarioSdi = null,
    string? CodiceCatastale = null,
    string? Settore = null,
    string? CodiceAteco = null,
    string? MatricolaInps = null,
    string? CodiceSedeInps = null,
    string? CscInps = null,
    string? PatInail = null,
    string? CcnlDefault = null,
    string? LivelloDefault = null,
    string? Iban = null,
    string? LegaleRappresentanteNome = null,
    string? LegaleRappresentanteCognome = null,
    string? LegaleRappresentanteCodiceFiscale = null,
    string? CanalePredefinito = null,
    bool? HaDipendenti = null,
    string? TipoContribuente = null,
    bool? AssoggettataIva = null);

public sealed record EliminaAziendeBulkRequest(List<Guid> Ids);

/// <summary>Richiesta di generazione dell'F24 del mese pescando i cedolini archiviati dell'azienda.</summary>
public sealed record F24MeseRequest(
    int Anno,
    int Mese,
    string MatricolaInps,
    string? CodiceSedeInps = null);

/// <summary>Cambio di stato di una pratica F24 (valore di StatoPraticaTelematica).</summary>
public sealed record CambiaStatoF24Request(string Stato);

/// <summary>Richiesta di generazione dell'F24 da versare a partire dagli importi delle paghe.</summary>
public sealed record F24DaPagheRequest(
    DatiSostitutoImposta Sostituto,
    IReadOnlyList<RigaPagaPerF24> Righe,
    int AnnoPeriodo,
    int MesePeriodo,
    DateOnly? DataVersamento = null);

/// <summary>Richiesta di salvataggio fattura (il numero, se omesso, è assegnato in automatico).</summary>
public sealed record SalvaFatturaRequest(
    DateOnly Data,
    DatiAnagrafici Cessionario,
    IReadOnlyList<RigaFattura> Righe,
    string? Numero = null,
    string TipoDocumento = "TD01",
    string CodiceDestinatario = "0000000");

/// <summary>Registrazione di una ricevuta/notifica SdI su una trasmissione (tipi: RC, MC, NS, NE, DT).</summary>
public sealed record RegistraRicevutaSdiRequest(
    string Tipo,
    string? CodiceEsito = null,
    string? Descrizione = null,
    string? ContenutoGrezzo = null);

/// <summary>Registrazione di una delega di un cliente verso l'intermediario per un adempimento.</summary>
public sealed record CreaDelegaRequest(
    Guid AziendaId,
    string TipoAdempimento,
    DateOnly DataInizio,
    DateOnly? DataFine = null,
    string? Riferimento = null,
    string? Note = null);

/// <summary>Creazione di un avviso di pagamento mensile dovuto dal cliente allo studio.</summary>
public sealed record CreaAvvisoPagamentoRequest(
    int Anno,
    int Mese,
    decimal Importo,
    DateOnly DataScadenza,
    string? Descrizione = null,
    DateOnly? DataEmissione = null,
    string MetodoPagamento = "Bonifico",
    string? Iban = null,
    string? Causale = null,
    string? Note = null);

/// <summary>Creazione massiva dello stesso avviso mensile per tutte le aziende clienti.</summary>
public sealed record CreaAvvisiPagamentoMeseRequest(
    int Anno,
    int Mese,
    decimal Importo,
    DateOnly DataScadenza,
    string? Descrizione = null,
    DateOnly? DataEmissione = null,
    string MetodoPagamento = "Bonifico",
    string? Iban = null,
    string? Causale = null,
    string? Note = null);

/// <summary>Cambio stato dell'avviso: DaPagare, Sollecitato, Pagato o Annullato.</summary>
public sealed record CambiaStatoAvvisoPagamentoRequest(
    string Stato,
    DateOnly? PagatoIl = null,
    string? RiferimentoPagamento = null,
    string? Note = null);

/// <summary>Registrazione di un DURC di un'azienda (scadenza usata per il promemoria di rinnovo).</summary>
public sealed record RegistraDurcRequest(
    string Numero,
    DateOnly DataEmissione,
    DateOnly? DataScadenza = null,
    bool Regolare = true,
    string? Note = null);

/// <summary>Inserimento di un promemoria/scadenza a mano nello scadenzario.</summary>
public sealed record CreaPromemoriaRequest(
    string Titolo,
    DateOnly DataScadenza,
    Guid? AziendaId = null,
    string? Descrizione = null,
    int GiorniPreavviso = 7);

/// <summary>Cambio stato di un promemoria: Aperto, Inviato, Completato o Annullato.</summary>
public sealed record CambiaStatoPromemoriaRequest(string Stato);

/// <summary>Invio di un promemoria su un canale (Email/Sms/WhatsApp); se omesso usa il canale di default.</summary>
public sealed record InviaPromemoriaRequest(string? Canale = null, string? Destinatario = null);
