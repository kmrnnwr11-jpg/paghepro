namespace Paghe.Core.AdeTelematico.UniEmens;

/// <summary>
/// Sezione <c>PosAgri</c> del flusso UniEmens: la denuncia mensile della manodopera agricola
/// (l'erede della DMAG), con cui l'INPS calcola i contributi che poi chiede al datore.
/// Vive dentro l'elemento <c>Azienda</c> accanto alle posizioni ordinarie; il datore agricolo
/// è identificato dal CIDA (codice dell'azienda agricola negli archivi INPS), non dalla
/// matricola. Attenzione al formato: in questa sezione gli importi sono <b>centesimi di euro
/// interi</b> (10,50 € → 1050), diversamente dal resto del flusso.
/// <paramref name="TipoDichiarazione"/>: "P" principale, "V" sostitutiva/di variazione.
/// <paramref name="TipoManodopera"/>: codice a una cifra (1/2) dell'allegato tecnico.
/// </summary>
public sealed record PosAgriUniEmens(
    string Cida,
    string TipoManodopera,
    IReadOnlyList<DenunciaAgriIndividualeUniEmens> Lavoratori,
    DenunciaAgriAziendaleUniEmens Aziendale,
    string TipoDichiarazione = "P");

/// <summary>Denuncia individuale agricola: il lavoratore e i suoi periodi retributivi.</summary>
public sealed record DenunciaAgriIndividualeUniEmens(
    string CodiceFiscale,
    string Cognome,
    string Nome,
    IReadOnlyList<DatiAgriRetribuzioneUniEmens> Retribuzioni);

/// <summary>
/// Periodo retributivo agricolo del mese. I codici (zona tariffaria, contratto a 3 caratteri,
/// retribuzione a 1 carattere, categoria, area, livello) seguono le tabelle dell'allegato
/// tecnico INPS: non si deducono dal CPL, li fornisce chi conosce la posizione aziendale.
/// <paramref name="Retribuzione"/> è in euro e viene scritta in centesimi nel file.
/// I flag "N" dichiarano l'assenza della situazione speciale (riallineamento, emersione,
/// dimissioni), non un dato ignoto.
/// </summary>
public sealed record DatiAgriRetribuzioneUniEmens(
    string ZonaTariffaria,
    string CodiceContratto,
    string CodiceRetribuzione,
    string CodiceIstatFondo,
    DateOnly DataAssunzione,
    string CategoriaContrattuale,
    string Area,
    string Livello,
    int? NumeroGiornate = null,
    decimal? Retribuzione = null,
    DateOnly? DataCessazione = null,
    string FlagRiallineamento = "N",
    string FlagEmersioneLav = "N",
    string FlagDimissioni = "N");

/// <summary>
/// Denuncia aziendale agricola di quadratura. <paramref name="DichTrattQuotaLav"/> "S" indica
/// che il datore trattiene la quota a carico del lavoratore (come nella busta agricola).
/// </summary>
public sealed record DenunciaAgriAziendaleUniEmens(
    int QuadraturaNumLav,
    string FlagAutRisPreInt = "N",
    string DichBeneficiContributivi = "N",
    string DichTrattQuotaLav = "S");
