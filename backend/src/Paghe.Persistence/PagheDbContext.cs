using Microsoft.EntityFrameworkCore;
using Paghe.Persistence.Entities;

namespace Paghe.Persistence;

/// <summary>
/// Contesto EF Core dell'applicazione. È indipendente dal provider: in sviluppo/test
/// si usa SQLite, in produzione PostgreSQL (Npgsql) — cambia solo la configurazione
/// di <c>UseSqlite/UseNpgsql</c> nell'host, non questo contesto.
/// </summary>
public class PagheDbContext : DbContext
{
    public PagheDbContext(DbContextOptions<PagheDbContext> options) : base(options) { }

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

    // Autenticazione
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UtenteEntity> Utenti => Set<UtenteEntity>();

    // Anagrafiche e cedolini del lavoro
    public DbSet<DipendenteEntity> Dipendenti => Set<DipendenteEntity>();
    public DbSet<RapportoLavoroEntity> Rapporti => Set<RapportoLavoroEntity>();
    public DbSet<CartellinoEntity> Cartellini => Set<CartellinoEntity>();
    public DbSet<CedolinoEntity> Cedolini => Set<CedolinoEntity>();
    public DbSet<PraticaAperturaPartitaIvaEntity> PraticheAperturaPartitaIva => Set<PraticaAperturaPartitaIvaEntity>();
    public DbSet<SpesaEntity> Spese => Set<SpesaEntity>();
    public DbSet<LiquidazioneIvaEntity> LiquidazioniIva => Set<LiquidazioneIvaEntity>();

    // Trasmissione fatture elettroniche (SdI) e deleghe intermediario
    public DbSet<TrasmissioneFatturaEntity> TrasmissioniFatture => Set<TrasmissioneFatturaEntity>();
    public DbSet<RicevutaSdiEntity> RicevuteSdi => Set<RicevutaSdiEntity>();
    public DbSet<DelegaEntity> Deleghe => Set<DelegaEntity>();

    /// <summary>
    /// Compatibilità per il database SQLite di sviluppo creato con EnsureCreated:
    /// se esiste già, EF non aggiunge nuove tabelle. In produzione usare migrazioni EF.
    /// </summary>
    public void EnsureSchemaSviluppoCompatibile()
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
            return;

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PraticheAperturaPartitaIva" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PraticheAperturaPartitaIva" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NULL,
                "FormaGiuridica" TEXT NOT NULL,
                "RegimeContabile" TEXT NOT NULL,
                "DataInizioAttivita" TEXT NOT NULL,
                "Denominazione" TEXT NOT NULL,
                "CodiceFiscale" TEXT NOT NULL,
                "PartitaIva" TEXT NULL,
                "CodiceAteco" TEXT NOT NULL,
                "DescrizioneAttivita" TEXT NOT NULL,
                "IndirizzoSede" TEXT NOT NULL,
                "Cap" TEXT NOT NULL,
                "Comune" TEXT NOT NULL,
                "Provincia" TEXT NOT NULL,
                "Pec" TEXT NULL,
                "AttivitaCommercio" INTEGER NOT NULL,
                "AttivitaArtigiana" INTEGER NOT NULL,
                "HaDipendentiPrevisti" INTEGER NOT NULL,
                "SoggettaInail" INTEGER NOT NULL,
                "ModelloAgenziaEntrate" TEXT NOT NULL,
                "RichiedeComunicazioneUnica" INTEGER NOT NULL,
                "RichiedeRegistroImprese" INTEGER NOT NULL,
                "RichiedeCameraCommercio" INTEGER NOT NULL,
                "Stato" TEXT NOT NULL,
                "CreataIl" TEXT NOT NULL,
                CONSTRAINT "FK_PraticheAperturaPartitaIva_Aziende_AziendaId"
                    FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id")
            );
            CREATE TABLE IF NOT EXISTS "AdempimentoPraticaEntity" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AdempimentoPraticaEntity" PRIMARY KEY,
                "PraticaAperturaPartitaIvaId" TEXT NOT NULL,
                "Codice" TEXT NOT NULL,
                "Ente" TEXT NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "Obbligatorio" INTEGER NOT NULL,
                "Stato" TEXT NOT NULL,
                "Note" TEXT NULL,
                CONSTRAINT "FK_AdempimentoPraticaEntity_PraticheAperturaPartitaIva_PraticaAperturaPartitaIvaId"
                    FOREIGN KEY ("PraticaAperturaPartitaIvaId") REFERENCES "PraticheAperturaPartitaIva" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PraticheAperturaPartitaIva_AziendaId"
                ON "PraticheAperturaPartitaIva" ("AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_PraticheAperturaPartitaIva_TenantId_CodiceFiscale"
                ON "PraticheAperturaPartitaIva" ("TenantId", "CodiceFiscale");
            CREATE INDEX IF NOT EXISTS "IX_AdempimentoPraticaEntity_PraticaAperturaPartitaIvaId"
                ON "AdempimentoPraticaEntity" ("PraticaAperturaPartitaIvaId");

            CREATE TABLE IF NOT EXISTS "Spese" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Spese" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Data" TEXT NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "ContoCosto" TEXT NOT NULL,
                "Fornitore" TEXT NULL,
                "NumeroDocumento" TEXT NULL,
                "Imponibile" TEXT NOT NULL,
                "AliquotaIva" TEXT NOT NULL,
                "PercentualeDetraibilita" TEXT NOT NULL,
                "Iva" TEXT NOT NULL,
                "IvaDetraibile" TEXT NOT NULL,
                "Totale" TEXT NOT NULL,
                CONSTRAINT "FK_Spese_Aziende_AziendaId" FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "LiquidazioniIva" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_LiquidazioniIva" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Dal" TEXT NOT NULL,
                "Al" TEXT NOT NULL,
                "Periodicita" TEXT NOT NULL,
                "IvaVendite" TEXT NOT NULL,
                "IvaAcquistiDetraibile" TEXT NOT NULL,
                "CreditoPeriodoPrecedente" TEXT NOT NULL,
                "Saldo" TEXT NOT NULL,
                "IvaDaVersare" TEXT NOT NULL,
                "CreditoDaRiportare" TEXT NOT NULL,
                "CreataIl" TEXT NOT NULL,
                CONSTRAINT "FK_LiquidazioniIva_Aziende_AziendaId" FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "RigaLiquidazioneIvaEntity" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RigaLiquidazioneIvaEntity" PRIMARY KEY,
                "LiquidazioneIvaId" TEXT NOT NULL,
                "Data" TEXT NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "Tipo" TEXT NOT NULL,
                "Imponibile" TEXT NOT NULL,
                "AliquotaIva" TEXT NOT NULL,
                "Iva" TEXT NOT NULL,
                "IvaDetraibile" TEXT NOT NULL,
                CONSTRAINT "FK_RigaLiquidazioneIvaEntity_LiquidazioniIva_LiquidazioneIvaId"
                    FOREIGN KEY ("LiquidazioneIvaId") REFERENCES "LiquidazioniIva" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Spese_AziendaId" ON "Spese" ("AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_Spese_TenantId_AziendaId_Data" ON "Spese" ("TenantId", "AziendaId", "Data");
            CREATE INDEX IF NOT EXISTS "IX_LiquidazioniIva_AziendaId" ON "LiquidazioniIva" ("AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_LiquidazioniIva_TenantId_AziendaId_Dal_Al" ON "LiquidazioniIva" ("TenantId", "AziendaId", "Dal", "Al");
            CREATE INDEX IF NOT EXISTS "IX_RigaLiquidazioneIvaEntity_LiquidazioneIvaId"
                ON "RigaLiquidazioneIvaEntity" ("LiquidazioneIvaId");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "TrasmissioniFatture" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TrasmissioniFatture" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "FatturaId" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "NomeFileXml" TEXT NOT NULL,
                "Firmato" INTEGER NOT NULL,
                "Stato" TEXT NOT NULL,
                "IdentificativoSdi" TEXT NULL,
                "CodiceErrore" TEXT NULL,
                "MessaggioErrore" TEXT NULL,
                "CreataIl" TEXT NOT NULL,
                "InviataIl" TEXT NULL,
                "UltimoAggiornamento" TEXT NOT NULL,
                CONSTRAINT "FK_TrasmissioniFatture_Fatture_FatturaId"
                    FOREIGN KEY ("FatturaId") REFERENCES "Fatture" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "RicevuteSdi" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RicevuteSdi" PRIMARY KEY,
                "TrasmissioneFatturaId" TEXT NOT NULL,
                "Tipo" TEXT NOT NULL,
                "CodiceEsito" TEXT NULL,
                "Descrizione" TEXT NULL,
                "ContenutoGrezzo" TEXT NULL,
                "RicevutaIl" TEXT NOT NULL,
                CONSTRAINT "FK_RicevuteSdi_TrasmissioniFatture_TrasmissioneFatturaId"
                    FOREIGN KEY ("TrasmissioneFatturaId") REFERENCES "TrasmissioniFatture" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "Deleghe" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Deleghe" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "TipoAdempimento" TEXT NOT NULL,
                "Stato" TEXT NOT NULL,
                "DataInizio" TEXT NOT NULL,
                "DataFine" TEXT NULL,
                "Riferimento" TEXT NULL,
                "Note" TEXT NULL,
                "CreataIl" TEXT NOT NULL,
                CONSTRAINT "FK_Deleghe_Aziende_AziendaId"
                    FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_TrasmissioniFatture_TenantId_AziendaId"
                ON "TrasmissioniFatture" ("TenantId", "AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_TrasmissioniFatture_FatturaId"
                ON "TrasmissioniFatture" ("FatturaId");
            CREATE INDEX IF NOT EXISTS "IX_RicevuteSdi_TrasmissioneFatturaId"
                ON "RicevuteSdi" ("TrasmissioneFatturaId");
            CREATE INDEX IF NOT EXISTS "IX_Deleghe_AziendaId" ON "Deleghe" ("AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_Deleghe_TenantId_AziendaId_TipoAdempimento"
                ON "Deleghe" ("TenantId", "AziendaId", "TipoAdempimento");
            """);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<AziendaEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale });
            e.Property(x => x.RagioneSociale).IsRequired();
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
                nameof(ParametriAnnoEntity.TfrContributoFondo) })
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

        // --- Autenticazione ---
        mb.Entity<TenantEntity>(e => e.HasKey(x => x.Id));
        mb.Entity<UtenteEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
        });

        // --- Anagrafiche e cedolini del lavoro ---
        mb.Entity<DipendenteEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale }).IsUnique();
            e.HasMany(x => x.Rapporti).WithOne(r => r.Dipendente!).HasForeignKey(r => r.DipendenteId);
        });
        mb.Entity<RapportoLavoroEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId });
            e.Property(x => x.PercentualePartTime).HasPrecision(6, 3);
            e.Property(x => x.Superminimo).HasPrecision(15, 2);
        });
        mb.Entity<CartellinoEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.RapportoLavoroId, x.Anno, x.Mese }).IsUnique();
            foreach (var p in new[] { nameof(CartellinoEntity.OreStraordinario),
                nameof(CartellinoEntity.OreStraordinarioNotturno),
                nameof(CartellinoEntity.OreStraordinarioFestivo),
                nameof(CartellinoEntity.GiorniFerie), nameof(CartellinoEntity.OrePermessi),
                nameof(CartellinoEntity.GiorniMalattia), nameof(CartellinoEntity.OreAssenzaNonRetribuita) })
                e.Property(p).HasPrecision(8, 2);
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
                nameof(CedolinoEntity.CostoAzienda) })
                e.Property(p).HasPrecision(15, 2);
            e.HasMany(x => x.Voci).WithOne().HasForeignKey(v => v.CedolinoId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<CedolinoVoceEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantita).HasPrecision(12, 4);
            e.Property(x => x.Base).HasPrecision(15, 4);
            e.Property(x => x.Importo).HasPrecision(15, 2);
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
    }
}
