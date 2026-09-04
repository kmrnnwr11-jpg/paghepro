using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>Dataset dimostrativo isolato: disponibile solo per gli account @paghe.local.</summary>
public sealed class DemoDatasetService
{
    private readonly PagheDbContext _db;
    private readonly AziendeService _aziende;
    private readonly AnagraficheLavoroService _anagrafiche;

    public DemoDatasetService(
        PagheDbContext db,
        AziendeService aziende,
        AnagraficheLavoroService anagrafiche)
    {
        _db = db;
        _aziende = aziende;
        _anagrafiche = anagrafiche;
    }

    public sealed record Esito(int Aziende, int Dipendenti);

    private sealed record Scheda(
        string NomeAzienda, string PartitaIva, string Comune, string Provincia, string Cap,
        string Catastale, string Settore, string Ateco, string Ccnl, string Livello,
        string Nome, string Cognome, string CodiceFiscale, string Qualifica,
        string Regione, int Mensilita);

    private static readonly Scheda[] Schede =
    {
        new("DEMO Trasporti Nord Srl", "99000000101", "Milano", "MI", "20121", "F205",
            "Trasporti e logistica", "49.41.00", "TRASPORTI_LOGISTICA", "C3",
            "Luca", "Bianchi", "BNCLCU90A01F205V", "Operaio", "Lombardia", 14),
        new("DEMO Pulizie Italia Srl", "99000000119", "Bologna", "BO", "40121", "A944",
            "Pulizie e multiservizi", "81.21.00", "MULTISERVIZI_PULIZIE", "3",
            "Sara", "Verdi", "VRDSRA92B41A944R", "Operaio", "Emilia-Romagna", 14),
        new("DEMO Edil Casa Srl", "99000000127", "Torino", "TO", "10121", "L219",
            "Edilizia", "41.20.00", "EDILIZIA_INDUSTRIA", "3",
            "Paolo", "Romano", "RMNPLA88C15L219N", "Operaio", "Piemonte", 13),
        new("DEMO Metal Tech Srl", "99000000135", "Brescia", "BS", "25121", "B157",
            "Metalmeccanica", "25.62.00", "METALMECCANICI_INDUSTRIA", "C2",
            "Elena", "Gallo", "GLLLNE95D52B157B", "Operaio", "Lombardia", 13),
        new("DEMO Hotel Mare Srl", "99000000143", "Rimini", "RN", "47921", "H294",
            "Turismo e ospitalita", "55.10.00", "TURISMO_PUBBLICI_ESERCIZI", "4",
            "Marco", "Conti", "CNTMRC87E10H294R", "Impiegato", "Emilia-Romagna", 14),
    };

    public async Task<Esito> CreaSeMancanteAsync(Guid tenantId, CancellationToken ct = default)
    {
        var aziende = await _db.Aziende
            .Where(a => a.TenantId == tenantId && a.RagioneSociale.StartsWith("DEMO "))
            .ToListAsync(ct);

        foreach (var scheda in Schede)
        {
            var azienda = aziende.FirstOrDefault(a => a.RagioneSociale == scheda.NomeAzienda);
            if (azienda is null)
            {
                azienda = await _aziende.CreaAsync(tenantId, new AziendaEntity
                {
                    RagioneSociale = scheda.NomeAzienda,
                    PartitaIva = scheda.PartitaIva,
                    CodiceFiscale = scheda.PartitaIva,
                    Indirizzo = "Via Dimostrazione 10",
                    Cap = scheda.Cap,
                    Comune = scheda.Comune,
                    Provincia = scheda.Provincia,
                    CodiceCatastale = scheda.Catastale,
                    Nazione = "IT",
                    RegimeFiscale = "RF01",
                    CodiceDestinatarioSdi = "0000000",
                    Settore = scheda.Settore,
                    CodiceAteco = scheda.Ateco,
                    MatricolaInps = "8" + scheda.PartitaIva[1..],
                    CodiceSedeInps = "0001",
                    CscInps = "70701",
                    PatInail = "8" + scheda.PartitaIva[4..],
                    CcnlDefault = scheda.Ccnl,
                    LivelloDefault = scheda.Livello,
                    HaDipendenti = true,
                    TipoContribuente = "Societa di capitali",
                    AssoggettataIva = true,
                }, ct);
                aziende.Add(azienda);
            }

            bool haDipendente = await _db.Rapporti.AnyAsync(
                r => r.TenantId == tenantId && r.AziendaId == azienda.Id, ct);
            if (!haDipendente)
            {
                await _anagrafiche.CreaDipendenteERapportoAsync(tenantId, azienda.Id,
                    new DipendenteEntity
                    {
                        CodiceFiscale = scheda.CodiceFiscale,
                        Nome = scheda.Nome,
                        Cognome = scheda.Cognome,
                    },
                    new RapportoLavoroEntity
                    {
                        CodiceCcnl = scheda.Ccnl,
                        Livello = scheda.Livello,
                        Qualifica = scheda.Qualifica,
                        NumeroMensilita = scheda.Mensilita,
                        Regione = scheda.Regione,
                        CodiceCatastaleComune = scheda.Catastale,
                        DataAssunzione = new DateOnly(2025, 1, 15),
                    }, ct);
            }
        }

        var aziendeDemo = await _db.Aziende
            .Where(a => a.TenantId == tenantId && a.RagioneSociale.StartsWith("DEMO "))
            .Select(a => a.Id)
            .ToListAsync(ct);
        var aziendeFinali = aziendeDemo.Count;
        var dipendentiFinali = await _db.Rapporti.CountAsync(
            r => r.TenantId == tenantId && aziendeDemo.Contains(r.AziendaId), ct);
        return new Esito(aziendeFinali, dipendentiFinali);
    }
}
