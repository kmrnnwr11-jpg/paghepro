using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Paghe.Core.AdeTelematico.UniEmens;

namespace Paghe.Persistence;

/// <summary>
/// Codici della posizione agricola che il tracciato pretende e che non stanno (ancora) in
/// anagrafica: li fornisce chi conosce la posizione INPS dell'azienda, dalle tabelle
/// dell'allegato tecnico UniEmens. Il CIDA è il codice dell'azienda agricola negli archivi
/// INPS; gli altri qualificano contratto e inquadramento con i codici del tracciato, che non
/// coincidono con le sigle del CPL (es. il livello «COMUNE» ha un suo codice a 2 caratteri).
/// </summary>
public sealed record CodiciPosAgri(
    string Cida,
    string TipoManodopera,
    string ZonaTariffaria,
    string CodiceContratto,
    string CodiceRetribuzione,
    string CodiceIstatFondo,
    string CategoriaContrattuale,
    string Area,
    string Livello,
    string TipoDichiarazione = "P");

/// <summary>Riga di sintesi per il controllo umano della denuncia.</summary>
public sealed record SintesiLavoratorePosAgri(
    string Nominativo, string CodiceFiscale, int Giornate, decimal Retribuzione);

/// <summary>Esito della generazione: XML, esito schema, dati mancanti e sintesi.</summary>
public sealed record EsitoPosAgri(
    string Xml,
    IReadOnlyList<string> ErroriSchema,
    IReadOnlyList<CampoUniEmensMancante> DaCompletare,
    IReadOnlyList<SintesiLavoratorePosAgri> Lavoratori);

/// <summary>
/// Costruisce la sezione PosAgri del flusso UniEmens (denuncia mensile della manodopera
/// agricola, erede della DMAG) dalle buste agricole archiviate del periodo. Le giornate e le
/// retribuzioni escono dall'archivio, mai da una stima; ciò che manca viene dichiarato in
/// <c>daCompletare</c>. Il file va comunque passato dal software di controllo ufficiale INPS
/// e l'invio resta sui canali ufficiali (cassetto previdenziale o intermediario): questo
/// servizio prepara la bozza, non trasmette. Isolato per tenant.
/// </summary>
public sealed partial class PosAgriService
{
    private readonly PagheDbContext _db;

    public PosAgriService(PagheDbContext db) => _db = db;

    [GeneratedRegex("^[1-9][0-9]{0,7}$")]
    private static partial Regex FormatoCida();

    public async Task<EsitoPosAgri> GeneraDelMeseAsync(
        Guid tenantId, Guid aziendaId, int anno, int mese, CodiciPosAgri codici,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(codici);
        if (!FormatoCida().IsMatch(codici.Cida ?? ""))
            throw new ArgumentException(
                "CIDA non valido: è il codice numerico dell'azienda agricola negli archivi "
                + "INPS (da 1 a 8 cifre, senza zeri iniziali). Lo trovi sul cassetto "
                + "previdenziale o lo conosce il consulente.");
        if (codici.TipoManodopera is not ("1" or "2"))
            throw new ArgumentException("Tipo manodopera non valido: il tracciato ammette 1 o 2.");
        if (codici.TipoDichiarazione is not ("P" or "V"))
            throw new ArgumentException(
                "Tipo dichiarazione non valido: P (principale) oppure V (variazione).");

        var azienda = await _db.Aziende
            .FirstOrDefaultAsync(a => a.Id == aziendaId && a.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Azienda non trovata per il tenant indicato.");
        string codiceFiscaleAzienda = azienda.CodiceFiscale ?? azienda.PartitaIva ?? "";
        if (string.IsNullOrWhiteSpace(codiceFiscaleAzienda))
            throw new ArgumentException(
                "L'azienda non ha codice fiscale né partita IVA: il tracciato li richiede.");

        var buste = await _db.CedoliniAgricoli.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.AziendaId == aziendaId
                && c.Anno == anno && c.Mese == mese)
            .ToListAsync(ct);
        if (buste.Count == 0)
            throw new ArgumentException(
                $"Nessuna busta agricola archiviata per il periodo {mese:00}/{anno}: la "
                + "denuncia si costruisce dalle buste elaborate, non da dati inventati.");

        var rapportoIds = buste.Select(b => b.RapportoLavoroId).Distinct().ToList();
        var rapporti = await _db.Rapporti.AsNoTracking()
            .Include(r => r.Dipendente)
            .Where(r => r.TenantId == tenantId && rapportoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var daCompletare = new List<CampoUniEmensMancante>();
        var lavoratori = new List<DenunciaAgriIndividualeUniEmens>();
        var sintesi = new List<SintesiLavoratorePosAgri>();
        foreach (var busta in buste.OrderBy(b => b.ArchiviatoIl))
        {
            rapporti.TryGetValue(busta.RapportoLavoroId, out var rapporto);
            var dipendente = rapporto?.Dipendente;
            string nominativo = $"{dipendente?.Cognome} {dipendente?.Nome}".Trim();
            if (nominativo.Length == 0) nominativo = "(lavoratore senza anagrafica)";

            if (string.IsNullOrWhiteSpace(dipendente?.CodiceFiscale))
                daCompletare.Add(new CampoUniEmensMancante(
                    "Codice fiscale del lavoratore", nominativo,
                    "Senza codice fiscale la denuncia individuale viene scartata.",
                    "Dipendenti → scheda del lavoratore → Anagrafica"));
            // La data di assunzione è obbligatoria su ogni periodo retributivo e non si
            // inventa: senza, la denuncia di questo lavoratore non si può costruire.
            if (rapporto is null || rapporto.DataAssunzione == default)
                throw new InvalidOperationException(
                    $"PosAgri bloccato per {nominativo}: manca il rapporto o la data di "
                    + "assunzione della busta archiviata.");

            var retribuzione = new DatiAgriRetribuzioneUniEmens(
                ZonaTariffaria: codici.ZonaTariffaria,
                CodiceContratto: codici.CodiceContratto,
                CodiceRetribuzione: codici.CodiceRetribuzione,
                CodiceIstatFondo: codici.CodiceIstatFondo,
                DataAssunzione: rapporto.DataAssunzione,
                CategoriaContrattuale: codici.CategoriaContrattuale,
                Area: codici.Area,
                Livello: codici.Livello,
                NumeroGiornate: busta.GiornateLavorate,
                // Retribuzione denunciata = imponibile contributivo della busta archiviata
                // (in agricoltura è arrotondato all'euro): assunzione da validare col
                // consulente, dichiarata anche nella risposta dell'endpoint.
                Retribuzione: busta.ImponibileInps,
                DataCessazione: rapporto?.DataCessazione);

            lavoratori.Add(new DenunciaAgriIndividualeUniEmens(
                dipendente?.CodiceFiscale ?? "",
                dipendente?.Cognome ?? "",
                dipendente?.Nome ?? "",
                [retribuzione]));
            sintesi.Add(new SintesiLavoratorePosAgri(
                nominativo, dipendente?.CodiceFiscale ?? "",
                busta.GiornateLavorate, busta.ImponibileInps));
        }

        var mittente = new MittenteUniEmens(
            CfPersonaMittente: azienda.LegaleRappresentanteCodiceFiscale ?? "",
            RagioneSociale: azienda.RagioneSociale,
            CfMittente: codiceFiscaleAzienda,
            CfSoftwarehouse: codiceFiscaleAzienda,
            SedeInps: azienda.CodiceSedeInps);
        if (string.IsNullOrWhiteSpace(mittente.CfPersonaMittente))
            daCompletare.Add(new CampoUniEmensMancante(
                "Codice fiscale del legale rappresentante", "(azienda)",
                "Il tracciato INPS chiede la persona fisica che firma la denuncia.",
                "Aziende → Correggi azienda → Legale rappresentante"));

        var posAgri = new PosAgriUniEmens(
            codici.Cida!,
            codici.TipoManodopera,
            lavoratori,
            new DenunciaAgriAziendaleUniEmens(QuadraturaNumLav: lavoratori.Count),
            codici.TipoDichiarazione);
        var flusso = new FlussoUniEmens(
            mittente,
            new AziendaUniEmens(anno, mese, codiceFiscaleAzienda, azienda.RagioneSociale)
            {
                PosAgri = [posAgri],
            });

        string xml = UniEmensXmlWriter.GeneraStringa(flusso);
        return new EsitoPosAgri(xml, UniEmensValidator.Valida(xml), daCompletare, sintesi);
    }
}
