using Paghe.Core.AdeTelematico;
using Paghe.Core.Contabilita;
using Paghe.Core.Domain;
using Paghe.Core.Fatturazione;
using Paghe.Core.PartiteIva;

namespace Paghe.Api;

/// <summary>Registrazione di un nuovo account (tenant) con il primo utente amministratore.</summary>
public sealed record RegistraRequest(string RagioneSociale, string Email, string Password);

/// <summary>Credenziali di accesso.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Richiesta di calcolo di un cedolino: rapporto di lavoro + cartellino presenze.</summary>
public sealed record CalcolaCedolinoRequest(RapportoLavoro Rapporto, Cartellino Cartellino);

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
    DateOnly? DataAssunzione = null);

/// <summary>Richiesta di riclassificazione bilancio: piano dei conti + saldi.</summary>
public sealed record RiclassificaRequest(
    IReadOnlyList<ContoBilancio> PianoConti,
    IReadOnlyList<SaldoConto> Saldi);

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
    Guid? AziendaId = null);

/// <summary>Aggiornamento stato di un adempimento dentro una pratica.</summary>
public sealed record AggiornaAdempimentoPraticaRequest(string Stato, string? Note = null);

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
    string RegimeFiscale = "RF01");

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
