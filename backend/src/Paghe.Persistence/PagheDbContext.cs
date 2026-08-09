using System.Data;
using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;
using Paghe.Persistence.Gdpr;

namespace Paghe.Persistence;

/// <summary>
/// Contesto EF Core dell'applicazione. È indipendente dal provider: in sviluppo/test
/// si usa SQLite, in produzione PostgreSQL (Npgsql) — cambia solo la configurazione
/// di <c>UseSqlite/UseNpgsql</c> nell'host, non questo contesto.
/// </summary>
public class PagheDbContext : DbContext
{
    private static readonly ProtezioneDatiInterceptor InterceptorProtezione = new();
    private static readonly AuditSaveChangesInterceptor InterceptorAudit = new();

    private readonly ProtettoreDati _protettore;

    /// <summary>Protettore associato a questa istanza, usato dall'interceptor condiviso.</summary>
    internal ProtettoreDati Protettore => _protettore;

    public PagheDbContext(
        DbContextOptions<PagheDbContext> options,
        ProtettoreDati? protettore = null) : base(options)
    {
        _protettore = protettore ?? ProtettoreDati.Corrente;
    }

    public DbSet<AziendaEntity> Aziende => Set<AziendaEntity>();
    public DbSet<FatturaEntity> Fatture => Set<FatturaEntity>();

    // Parametri normativi versionati
    public DbSet<ParametriAnnoEntity> ParametriAnno => Set<ParametriAnnoEntity>();
    public DbSet<InpsAliquotaEntity> InpsAliquote => Set<InpsAliquotaEntity>();
    public DbSet<IrpefScaglioneEntity> IrpefScaglioni => Set<IrpefScaglioneEntity>();
    public DbSet<AddizionaleRegionaleEntity> AddizionaliRegionali => Set<AddizionaleRegionaleEntity>();
    public DbSet<AddizionaleComunaleEntity> AddizionaliComunali => Set<AddizionaleComunaleEntity>();
    public DbSet<CcnlEntity> Ccnl => Set<CcnlEntity>();
    public DbSet<CcnlLivelloEntity> CcnlLivelli => Set<CcnlLivelloEntity>();
    public DbSet<RegoleCcnlEntity> RegoleCcnl => Set<RegoleCcnlEntity>();

    // Autenticazione
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UtenteEntity> Utenti => Set<UtenteEntity>();
    public DbSet<SessioneRefreshEntity> SessioniRefresh => Set<SessioneRefreshEntity>();

    // Anagrafiche e cedolini del lavoro
    public DbSet<DipendenteEntity> Dipendenti => Set<DipendenteEntity>();
    public DbSet<RapportoLavoroEntity> Rapporti => Set<RapportoLavoroEntity>();
    public DbSet<CartellinoEntity> Cartellini => Set<CartellinoEntity>();
    public DbSet<CedolinoEntity> Cedolini => Set<CedolinoEntity>();
    public DbSet<PraticaAperturaPartitaIvaEntity> PraticheAperturaPartitaIva => Set<PraticaAperturaPartitaIvaEntity>();
    public DbSet<SpesaEntity> Spese => Set<SpesaEntity>();
    public DbSet<LiquidazioneIvaEntity> LiquidazioniIva => Set<LiquidazioneIvaEntity>();

    // Bilancio d'esercizio riclassificato (CEE) per azienda/anno
    public DbSet<BilancioEntity> Bilanci => Set<BilancioEntity>();
    public DbSet<RigaBilancioEntity> RigheBilancio => Set<RigaBilancioEntity>();

    // Trasmissione fatture elettroniche (SdI) e deleghe intermediario
    public DbSet<TrasmissioneFatturaEntity> TrasmissioniFatture => Set<TrasmissioneFatturaEntity>();
    public DbSet<RicevutaSdiEntity> RicevuteSdi => Set<RicevutaSdiEntity>();
    public DbSet<DelegaEntity> Deleghe => Set<DelegaEntity>();
    public DbSet<PraticaF24Entity> PraticheF24 => Set<PraticaF24Entity>();
    public DbSet<PraticaInailEntity> PraticheInail => Set<PraticaInailEntity>();
    public DbSet<AvvisoPagamentoEntity> AvvisiPagamento => Set<AvvisoPagamentoEntity>();
    public DbSet<DurcEntity> Durc => Set<DurcEntity>();
    public DbSet<PromemoriaEntity> Promemoria => Set<PromemoriaEntity>();

    // Configurazione canale SdI self-service (un record per tenant)
    public DbSet<ConfigurazioneSdiEntity> ConfigurazioniSdi => Set<ConfigurazioneSdiEntity>();

    // Dichiarazione Redditi PF / 730 (input manuali per rapporto/anno)
    public DbSet<DichiarazioneRedditiPfEntity> DichiarazioniRedditiPf => Set<DichiarazioneRedditiPfEntity>();

    // Immobili per il calcolo IMU
    public DbSet<ImmobileEntity> Immobili => Set<ImmobileEntity>();

    // Posizioni assicurative INAIL (PAT + tasso) per l'autoliquidazione
    public DbSet<PosizioneInailEntity> PosizioniInail => Set<PosizioneInailEntity>();

    // Audit log (GDPR / tracciabilità): chi/cosa/quando, scritto dall'interceptor
    public DbSet<AuditLogEntity> AuditLog => Set<AuditLogEntity>();

    // Retention GDPR: policy versionate, blocchi, anteprime vincolanti e ricevute append-only.
    public DbSet<PolicyRetentionEntity> PolicyRetention => Set<PolicyRetentionEntity>();
    public DbSet<BloccoConservazioneEntity> BlocchiConservazione => Set<BloccoConservazioneEntity>();
    public DbSet<AnteprimaRetentionEntity> AnteprimeRetention => Set<AnteprimaRetentionEntity>();
    public DbSet<EsecuzioneRetentionEntity> EsecuzioniRetention => Set<EsecuzioneRetentionEntity>();

    // Governor AI (G2): mandati d'uso e audit concatenato — control plane, mai risorse AI.
    public DbSet<AiScopeGrantEntity> AiScopeGrants => Set<AiScopeGrantEntity>();
    public DbSet<AiAuditEntity> AiAudit => Set<AiAuditEntity>();

    // Archivio persone fisiche (rappresentanti, soci): riusate per compilare i moduli ufficiali
    public DbSet<PersonaEntity> Persone => Set<PersonaEntity>();
    public DbSet<ConsumoAiEntity> ConsumiAi => Set<ConsumoAiEntity>();

    // Posta: caselle email/PEC collegate self-service e messaggi importati (triage in proposta)
    public DbSet<CasellaEmailEntity> CaselleEmail => Set<CasellaEmailEntity>();
    public DbSet<MessaggioEmailEntity> MessaggiEmail => Set<MessaggioEmailEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // La protezione deve precedere l'audit: i valori sono cifrati prima che EF generi SQL e
        // i nomi tecnici dei payload protetti vengono esclusi dal dettaglio di audit.
        // Le istanze degli interceptor sono stabili: il protettore viene letto dal singolo
        // PagheDbContext, evitando la proliferazione degli internal service provider EF.
        optionsBuilder.AddInterceptors(InterceptorProtezione, InterceptorAudit);
        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>
    /// Allinea il database allo schema del modello EF aggiungendo le COLONNE mancanti
    /// (schema creato con EnsureCreated e poi evoluto nel codice). Le nuove tabelle restano
    /// gestite dai blocchi CREATE TABLE espliciti; le tabelle non ancora presenti vengono
    /// saltate. Supporta SQLite (sviluppo) e PostgreSQL/Supabase. Le colonne NOT NULL
    /// ricevono un DEFAULT neutro per riempire le righe esistenti. In produzione la strada
    /// maestra restano le migrazioni EF.
    /// </summary>
    public void AllineaColonneMancanti()
    {
        bool sqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        bool postgres = Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
        if (!sqlite && !postgres)
            return;

        var conn = Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        foreach (var entita in Model.GetEntityTypes())
        {
            var tabella = entita.GetTableName();
            if (tabella is null) continue;

            // Colonne già presenti nel database (tabella assente => la creano i blocchi dedicati).
            var esistenti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sqlite
                    ? $"SELECT name FROM pragma_table_info('{tabella}');"
                    : "SELECT column_name FROM information_schema.columns " +
                      $"WHERE table_schema = 'public' AND table_name = '{tabella}';";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) esistenti.Add(reader.GetString(0));
            }
            if (esistenti.Count == 0) continue;

            var idTabella = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier
                .Table(tabella, entita.GetSchema());
            foreach (var prop in entita.GetProperties())
            {
                var colonna = prop.GetColumnName(idTabella);
                if (colonna is null || esistenti.Contains(colonna)) continue;

                string tipo = prop.GetColumnType();
                string sql = $"ALTER TABLE \"{tabella}\" ADD COLUMN \"{colonna}\" {tipo}";
                if (!prop.IsColumnNullable(idTabella))
                    sql += $" NOT NULL DEFAULT {DefaultNeutro(prop.ClrType, postgres)}";
                // Tabella/colonna/tipo provengono esclusivamente dai metadata EF, non da input.
                // Il comando diretto evita che l'analizzatore interpreti gli identificatori
                // generati dal modello come concatenazione SQL di dati utente.
                using var aggiungiColonna = conn.CreateCommand();
                aggiungiColonna.CommandText = sql + ";";
                aggiungiColonna.ExecuteNonQuery();
            }
        }

        // Le righe create prima dell'introduzione delle categorie sono tutte modifiche dati.
        Database.ExecuteSqlRaw(
            "UPDATE \"AuditLog\" SET \"Categoria\" = 'ModificaDati' WHERE \"Categoria\" = '';");
    }

    /// <summary>Letterale SQL di riempimento per le colonne NOT NULL aggiunte a posteriori.</summary>
    private static string DefaultNeutro(Type clr, bool postgres)
    {
        var t = Nullable.GetUnderlyingType(clr) ?? clr;
        if (t == typeof(bool)) return postgres ? "false" : "0";
        if (t == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "'1970-01-01 00:00:00+00'";
        if (t == typeof(DateOnly)) return "'1970-01-01'";
        if (t.IsEnum || t == typeof(string) || t == typeof(char)) return "''";
        if (t.IsPrimitive || t == typeof(decimal)) return "0";
        return "''";
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // GDPR: cifratura applicativa a riposo di IBAN, segreti e quantità sanitarie.
        var protettore = _protettore;

        // --- Governor AI (G2): contatori atomici e catena audit unica per tenant ---
        mb.Entity<AiScopeGrantEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Stato });
            // La quota non si aggira in parallelo: aggiornamento con token di concorrenza.
            e.Property(x => x.Versione).IsConcurrencyToken();
        });

        mb.Entity<AiAuditEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Sequenza }).IsUnique();
        });

        mb.Entity<AziendaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale });
            e.Property(x => x.RagioneSociale).IsRequired();
            e.Ignore(x => x.Iban);
            e.Property(x => x.IbanProtetto);
            e.HasMany(x => x.Fatture)
                .WithOne(f => f.Azienda!)
                .HasForeignKey(f => f.AziendaId);
        });

        mb.Entity<FatturaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            // Numero univoco per tenant/azienda/anno.
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno, x.Numero }).IsUnique();
            e.Property(x => x.TotaleImponibile).HasPrecision(15, 2);
            e.Property(x => x.TotaleImposta).HasPrecision(15, 2);
            e.Property(x => x.TotaleDocumento).HasPrecision(15, 2);
            e.HasMany(x => x.Righe)
                .WithOne()
                .HasForeignKey(r => r.FatturaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RigaFatturaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantita).HasPrecision(15, 2);
            e.Property(x => x.PrezzoUnitario).HasPrecision(15, 2);
            e.Property(x => x.AliquotaIva).HasPrecision(8, 5);
        });

        // --- Parametri normativi ---
        mb.Entity<ParametriAnnoEntity>(e =>
        {
            e.HasKey(x => x.Anno);
            foreach (var p in new[] { nameof(ParametriAnnoEntity.DivisoreOrario),
                nameof(ParametriAnnoEntity.MaggiorazioneStraordinario),
                nameof(ParametriAnnoEntity.MaggiorazioneStraordinarioNotturno),
                nameof(ParametriAnnoEntity.MaggiorazioneStraordinarioFestivo),
                nameof(ParametriAnnoEntity.TfrDivisore),
                nameof(ParametriAnnoEntity.TfrContributoFondo),
                nameof(ParametriAnnoEntity.VariazioneIstatTfr) })
                e.Property(p).HasPrecision(12, 5);
        });
        mb.Entity<InpsAliquotaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Anno, x.Qualifica }).IsUnique();
            e.Property(x => x.Dipendente).HasPrecision(8, 5);
            e.Property(x => x.Datore).HasPrecision(8, 5);
        });
        mb.Entity<IrpefScaglioneEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Anno, x.DaImporto }).IsUnique();
            e.Property(x => x.DaImporto).HasPrecision(15, 2);
            e.Property(x => x.AImporto).HasPrecision(15, 2);
            e.Property(x => x.Aliquota).HasPrecision(8, 5);
        });
        mb.Entity<AddizionaleRegionaleEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Anno, x.Regione }).IsUnique();
            e.Property(x => x.Aliquota).HasPrecision(8, 5);
        });
        mb.Entity<AddizionaleComunaleEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Anno, x.CodiceCatastale }).IsUnique();
            e.Property(x => x.Aliquota).HasPrecision(8, 5);
        });
        mb.Entity<CcnlEntity>(e => e.HasKey(x => x.Codice));
        mb.Entity<CcnlLivelloEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CcnlCodice, x.Anno, x.Livello }).IsUnique();
            e.Property(x => x.MinimoTabellare).HasPrecision(15, 2);
            e.Property(x => x.Edr).HasPrecision(15, 2);
            e.Property(x => x.ImportoScatto).HasPrecision(15, 2);
        });
        mb.Entity<RegoleCcnlEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CcnlCodice, x.Anno }).IsUnique();
            e.Property(x => x.DivisoreOrario).HasPrecision(8, 2);
            e.Property(x => x.DivisoreGiornaliero).HasPrecision(8, 2);
            e.Property(x => x.GiorniFerieMese).HasPrecision(8, 4);
            e.Property(x => x.OrePermessiMese).HasPrecision(8, 2);
            foreach (var p in new[] { nameof(RegoleCcnlEntity.MaggiorazioneStraordinario),
                nameof(RegoleCcnlEntity.MaggiorazioneStraordinarioNotturno),
                nameof(RegoleCcnlEntity.MaggiorazioneStraordinarioFestivo) })
                e.Property(p).HasPrecision(6, 4);
        });

        // --- Autenticazione ---
        mb.Entity<TenantEntity>(e => e.HasKey(x => x.Id));
        mb.Entity<UtenteEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.SegretoTotp).HasConversion(protettore.ConverterStringa()); // GDPR: cifrato a riposo
        });
        mb.Entity<SessioneRefreshEntity>(e =>
        {
            e.ToTable("SessioniRefresh");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Id }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.FamigliaId });
            e.HasIndex(x => new { x.TenantId, x.UtenteId, x.RevocataUtc });
            e.Property(x => x.Versione).IsConcurrencyToken();
            e.HasOne<UtenteEntity>().WithMany().HasForeignKey(x => x.UtenteId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TenantEntity>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SessioneRefreshEntity>().WithMany().HasForeignKey(x => x.SostituitaDaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Segreto del provider SdI: cifrato a riposo (non deve stare in chiaro nel DB).
        mb.Entity<ConfigurazioneSdiEntity>(e =>
            e.Property(x => x.ApiKey).HasConversion(protettore.ConverterStringa()));

        // Posta: credenziale casella e corpo messaggio cifrati a riposo (stesso converter di ApiKey/SegretoTotp).
        mb.Entity<CasellaEmailEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Attiva });
            e.Property(x => x.Password).HasConversion(protettore.ConverterStringa());
        });
        mb.Entity<MessaggioEmailEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CasellaId, x.UidImap }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CasellaId, x.DataRicezione });
            e.Property(x => x.Corpo).HasConversion(protettore.ConverterStringa());
        });

        // --- Anagrafiche e cedolini del lavoro ---
        mb.Entity<DipendenteEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale }).IsUnique();
            e.Ignore(x => x.Iban);
            e.Property(x => x.IbanProtetto);
            e.HasMany(x => x.Rapporti).WithOne(r => r.Dipendente!).HasForeignKey(r => r.DipendenteId);
        });
        mb.Entity<RapportoLavoroEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId });
            e.Property(x => x.PercentualePartTime).HasPrecision(6, 3);
            e.Property(x => x.Superminimo).HasPrecision(15, 2);
            e.Property(x => x.FondoTfrIniziale).HasPrecision(15, 2);
        });
        mb.Entity<CartellinoEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.RapportoLavoroId, x.Anno, x.Mese }).IsUnique();
            foreach (var p in new[] { nameof(CartellinoEntity.OreStraordinario),
                nameof(CartellinoEntity.OreStraordinarioNotturno),
                nameof(CartellinoEntity.OreStraordinarioFestivo),
                nameof(CartellinoEntity.GiorniFerie), nameof(CartellinoEntity.OrePermessi),
                nameof(CartellinoEntity.OreAssenzaNonRetribuita) })
                e.Property(p).HasPrecision(8, 2);

            // L'assenza per malattia/infortunio rivela salute anche senza diagnosi: la terna resta
            // decimal/int nel dominio, ma nel DB esiste soltanto il payload AEAD tenant-bound.
            e.Ignore(x => x.GiorniMalattia);
            e.Ignore(x => x.NumeroEventoMalattiaAnno);
            e.Ignore(x => x.GiorniInfortunio);
            e.Property(x => x.DatiSaluteProtetti).IsRequired();

            // Il calendario dice quali giorni sono stati malattia: stessa protezione della terna.
            // Non è obbligatorio: i cartellini compilati a totali hanno la colonna vuota.
            e.Ignore(x => x.Giornate);
            e.Property(x => x.GiornateProtette).IsRequired();
        });
        mb.Entity<CedolinoEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.RapportoLavoroId, x.Anno, x.Mese }).IsUnique();
            foreach (var p in new[] { nameof(CedolinoEntity.TotaleCompetenze),
                nameof(CedolinoEntity.TotaleTrattenute), nameof(CedolinoEntity.ImponibileInps),
                nameof(CedolinoEntity.ContributiDipendente), nameof(CedolinoEntity.ContributiDatore),
                nameof(CedolinoEntity.ImponibileIrpef), nameof(CedolinoEntity.IrpefNetta),
                nameof(CedolinoEntity.AddizionaleRegionale), nameof(CedolinoEntity.AddizionaleComunale),
                nameof(CedolinoEntity.NettoInBusta), nameof(CedolinoEntity.QuotaTfr),
                nameof(CedolinoEntity.CostoAzienda), nameof(CedolinoEntity.ConguaglioIrpef) })
                e.Property(p).HasPrecision(15, 2);
            e.HasMany(x => x.Voci).WithOne().HasForeignKey(v => v.CedolinoId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<CedolinoVoceEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CedolinoId });
            e.Ignore(x => x.Codice);
            e.Ignore(x => x.Descrizione);
            e.Ignore(x => x.Tipo);
            e.Ignore(x => x.Quantita);
            e.Ignore(x => x.Base);
            e.Ignore(x => x.Importo);
            e.Property(x => x.DatiVoceProtetti).IsRequired();
        });

        // --- Pratiche apertura partita IVA / ComUnica ---
        mb.Entity<PraticaAperturaPartitaIvaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale });
            e.HasMany(x => x.Adempimenti)
                .WithOne()
                .HasForeignKey(a => a.PraticaAperturaPartitaIvaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<AdempimentoPraticaEntity>(e => e.HasKey(x => x.Id));

        // --- Contabilità IVA ---
        mb.Entity<SpesaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Data });
            e.Property(x => x.Imponibile).HasPrecision(15, 2);
            e.Property(x => x.AliquotaIva).HasPrecision(8, 5);
            e.Property(x => x.PercentualeDetraibilita).HasPrecision(8, 5);
            e.Property(x => x.Iva).HasPrecision(15, 2);
            e.Property(x => x.IvaDetraibile).HasPrecision(15, 2);
            e.Property(x => x.Totale).HasPrecision(15, 2);
        });
        mb.Entity<LiquidazioneIvaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Dal, x.Al });
            foreach (var p in new[] { nameof(LiquidazioneIvaEntity.IvaVendite),
                nameof(LiquidazioneIvaEntity.IvaAcquistiDetraibile),
                nameof(LiquidazioneIvaEntity.CreditoPeriodoPrecedente),
                nameof(LiquidazioneIvaEntity.Saldo),
                nameof(LiquidazioneIvaEntity.IvaDaVersare),
                nameof(LiquidazioneIvaEntity.CreditoDaRiportare) })
                e.Property(p).HasPrecision(15, 2);
            e.HasMany(x => x.Righe).WithOne().HasForeignKey(r => r.LiquidazioneIvaId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<RigaLiquidazioneIvaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Imponibile).HasPrecision(15, 2);
            e.Property(x => x.AliquotaIva).HasPrecision(8, 5);
            e.Property(x => x.Iva).HasPrecision(15, 2);
            e.Property(x => x.IvaDetraibile).HasPrecision(15, 2);
        });

        // --- Trasmissione fatture elettroniche (SdI) ---
        mb.Entity<TrasmissioneFatturaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId });
            e.HasIndex(x => x.FatturaId);
            e.HasOne(x => x.Fattura).WithMany().HasForeignKey(x => x.FatturaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Ricevute).WithOne().HasForeignKey(r => r.TrasmissioneFatturaId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<RicevutaSdiEntity>(e => e.HasKey(x => x.Id));

        // --- Deleghe intermediario ---
        mb.Entity<DelegaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.TipoAdempimento });
            e.HasOne(x => x.Azienda).WithMany().HasForeignKey(x => x.AziendaId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Avvisi di pagamento clienti dello studio ---
        mb.Entity<AvvisoPagamentoEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno, x.Mese }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Stato, x.DataScadenza });
            e.Property(x => x.Importo).HasPrecision(15, 2);
            e.Ignore(x => x.Iban);
            e.Property(x => x.IbanProtetto);
            e.HasOne(x => x.Azienda).WithMany().HasForeignKey(x => x.AziendaId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Scadenzario / promemoria generici ---
        mb.Entity<DurcEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.DataScadenza });
            e.HasOne<AziendaEntity>().WithMany().HasForeignKey(x => x.AziendaId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PosizioneInailEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno }).IsUnique();
            e.Property(x => x.TassoPerMille).HasPrecision(8, 3);
            e.HasOne<AziendaEntity>().WithMany().HasForeignKey(x => x.AziendaId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<PromemoriaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.DataScadenza });
            e.HasIndex(x => new { x.TenantId, x.Tipo, x.RiferimentoId });
            e.HasOne<AziendaEntity>().WithMany().HasForeignKey(x => x.AziendaId).OnDelete(DeleteBehavior.SetNull);
        });

        // --- Pratiche F24 (telematico) ---
        mb.Entity<PraticaF24Entity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno, x.Mese });
            e.Property(x => x.TotaleDebiti).HasPrecision(15, 2);
            e.Property(x => x.TotaleCrediti).HasPrecision(15, 2);
            e.Property(x => x.SaldoFinale).HasPrecision(15, 2);
        });

        // --- Pratiche INAIL (autoliquidazione) ---
        mb.Entity<PraticaInailEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno });
            e.Property(x => x.RetribuzioniAnno).HasPrecision(15, 2);
            e.Property(x => x.TassoPerMille).HasPrecision(8, 3);
            e.Property(x => x.Regolazione).HasPrecision(15, 2);
            e.Property(x => x.RataAnticipata).HasPrecision(15, 2);
            e.Property(x => x.RataGiaVersata).HasPrecision(15, 2);
            e.Property(x => x.TotaleDaVersare).HasPrecision(15, 2);
        });

        // --- Bilancio d'esercizio (CEE) ---
        mb.Entity<BilancioEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno });
            foreach (var p in new[] { nameof(BilancioEntity.ValoreDellaProduzione),
                nameof(BilancioEntity.CostiDellaProduzione), nameof(BilancioEntity.ProventiFinanziari),
                nameof(BilancioEntity.OneriFinanziari), nameof(BilancioEntity.Imposte),
                nameof(BilancioEntity.UtilePerditaEsercizio), nameof(BilancioEntity.TotaleAttivo),
                nameof(BilancioEntity.TotalePassivo), nameof(BilancioEntity.PatrimonioNetto),
                nameof(BilancioEntity.SbilancioAttivoPassivo) })
                e.Property(p).HasPrecision(15, 2);
            e.HasMany(x => x.Righe).WithOne().HasForeignKey(r => r.BilancioId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<RigaBilancioEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Importo).HasPrecision(15, 2);
        });

        // --- Audit log (GDPR / tracciabilità) ---
        mb.Entity<AuditLogEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.QuandoUtc });
            e.HasIndex(x => new { x.TenantId, x.Entita });
            e.Property(x => x.Categoria).HasMaxLength(40).HasDefaultValue("ModificaDati");
            e.Property(x => x.Azione).HasMaxLength(40);
            e.Property(x => x.Entita).HasMaxLength(100);
            e.Property(x => x.ChiaveEntita).HasMaxLength(500);
            e.Property(x => x.MetodoHttp).HasMaxLength(12);
            e.Property(x => x.PercorsoTemplate).HasMaxLength(300);
            e.Property(x => x.Esito).HasMaxLength(40);
            e.Property(x => x.CorrelationId).HasMaxLength(100);
        });

        // --- Retention GDPR ---
        mb.Entity<PolicyRetentionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Categoria, x.Versione }).IsUnique();
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.Property(x => x.BaseGiuridica).HasMaxLength(200);
            e.Property(x => x.RiferimentoDecisione).HasMaxLength(200);
            e.Property(x => x.IstruzioniBackup).HasMaxLength(300);
            e.Property(x => x.Finalita).HasMaxLength(250);
            e.Property(x => x.Decorrenza).HasMaxLength(200);
            e.Property(x => x.Azione).HasMaxLength(200);
        });
        mb.Entity<BloccoConservazioneEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Categoria, x.RevocatoUtc });
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.Property(x => x.MotivoCodice).HasMaxLength(60);
        });
        mb.Entity<AnteprimaRetentionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Categoria, x.ScadeUtc });
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.Property(x => x.ImprontaCandidati).HasMaxLength(64);
            e.Property(x => x.ImprontaBlocchi).HasMaxLength(64);
        });
        mb.Entity<EsecuzioneRetentionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ChiaveIdempotenza }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.AnteprimaId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Categoria, x.CompletataUtc });
            e.Property(x => x.Categoria).HasMaxLength(60);
            e.Property(x => x.Esito).HasMaxLength(40);
            e.Property(x => x.CorrelationId).HasMaxLength(100);
            e.Property(x => x.DigestPrecedente).HasMaxLength(64);
            e.Property(x => x.Digest).HasMaxLength(64);
        });

        // --- Archivio persone fisiche ---
        mb.Entity<PersonaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale }).IsUnique();
        });

        // --- Contatore mensile chiamate AI per tenant (AI inclusa nel canone) ---
        mb.Entity<ConsumoAiEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Anno, x.Mese }).IsUnique();
        });
    }
}
