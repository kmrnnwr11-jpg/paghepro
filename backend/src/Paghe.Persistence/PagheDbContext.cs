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

    // Bilancio d'esercizio riclassificato (CEE) per azienda/anno
    public DbSet<BilancioEntity> Bilanci => Set<BilancioEntity>();
    public DbSet<RigaBilancioEntity> RigheBilancio => Set<RigaBilancioEntity>();

    // Trasmissione fatture elettroniche (SdI) e deleghe intermediario
    public DbSet<TrasmissioneFatturaEntity> TrasmissioniFatture => Set<TrasmissioneFatturaEntity>();
    public DbSet<RicevutaSdiEntity> RicevuteSdi => Set<RicevutaSdiEntity>();
    public DbSet<DelegaEntity> Deleghe => Set<DelegaEntity>();
    public DbSet<PraticaF24Entity> PraticheF24 => Set<PraticaF24Entity>();
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

    // Archivio persone fisiche (rappresentanti, soci): riusate per compilare i moduli ufficiali
    public DbSet<PersonaEntity> Persone => Set<PersonaEntity>();
    public DbSet<ConsumoAiEntity> ConsumiAi => Set<ConsumoAiEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Audit automatico di ogni inserimento/modifica/cancellazione tenant-scoped.
        optionsBuilder.AddInterceptors(new Gdpr.AuditSaveChangesInterceptor());
        base.OnConfiguring(optionsBuilder);
    }

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

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "ConfigurazioniSdi" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ConfigurazioniSdi" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "Modalita" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "BaseUrl" TEXT NOT NULL,
                "ApiKey" TEXT NULL,
                "EndpointInvio" TEXT NOT NULL,
                "EndpointEsito" TEXT NOT NULL,
                "CampoId" TEXT NOT NULL,
                "CampoStato" TEXT NOT NULL,
                "FirmaLocale" INTEGER NOT NULL,
                "Attivo" INTEGER NOT NULL,
                "AggiornataIl" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConfigurazioniSdi_TenantId"
                ON "ConfigurazioniSdi" ("TenantId");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "DichiarazioniRedditiPf" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DichiarazioniRedditiPf" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "RapportoLavoroId" TEXT NOT NULL,
                "Anno" INTEGER NOT NULL,
                "AltriRedditi" TEXT NOT NULL,
                "OneriDeducibili" TEXT NOT NULL,
                "OneriDetraibili19" TEXT NOT NULL,
                "Acconti" TEXT NOT NULL,
                "AggiornataIl" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DichiarazioniRedditiPf_Tenant_Rapporto_Anno"
                ON "DichiarazioniRedditiPf" ("TenantId", "RapportoLavoroId", "Anno");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Immobili" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Immobili" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "CategoriaCatastale" TEXT NOT NULL,
                "RenditaCatastale" TEXT NOT NULL,
                "PercentualePossesso" TEXT NOT NULL,
                "MesiPossesso" INTEGER NOT NULL,
                "AliquotaPerMille" TEXT NOT NULL,
                "AbitazionePrincipale" INTEGER NOT NULL,
                "CreatoIl" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_Immobili_TenantId_AziendaId"
                ON "Immobili" ("TenantId", "AziendaId");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "AvvisiPagamento" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AvvisiPagamento" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Anno" INTEGER NOT NULL,
                "Mese" INTEGER NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "Importo" TEXT NOT NULL,
                "DataEmissione" TEXT NOT NULL,
                "DataScadenza" TEXT NOT NULL,
                "Stato" TEXT NOT NULL,
                "MetodoPagamento" TEXT NOT NULL,
                "Iban" TEXT NULL,
                "Causale" TEXT NULL,
                "Note" TEXT NULL,
                "PagatoIl" TEXT NULL,
                "RiferimentoPagamento" TEXT NULL,
                "NumeroPromemoria" INTEGER NOT NULL,
                "UltimoPromemoriaIl" TEXT NULL,
                "CreataIl" TEXT NOT NULL,
                "UltimoAggiornamento" TEXT NOT NULL,
                CONSTRAINT "FK_AvvisiPagamento_Aziende_AziendaId"
                    FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AvvisiPagamento_TenantId_AziendaId_Anno_Mese"
                ON "AvvisiPagamento" ("TenantId", "AziendaId", "Anno", "Mese");
            CREATE INDEX IF NOT EXISTS "IX_AvvisiPagamento_TenantId_Stato_DataScadenza"
                ON "AvvisiPagamento" ("TenantId", "Stato", "DataScadenza");
            CREATE INDEX IF NOT EXISTS "IX_AvvisiPagamento_AziendaId"
                ON "AvvisiPagamento" ("AziendaId");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Durc" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Durc" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Numero" TEXT NOT NULL,
                "DataEmissione" TEXT NOT NULL,
                "DataScadenza" TEXT NOT NULL,
                "Regolare" INTEGER NOT NULL,
                "Note" TEXT NULL,
                "CreatoIl" TEXT NOT NULL,
                CONSTRAINT "FK_Durc_Aziende_AziendaId"
                    FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Durc_AziendaId" ON "Durc" ("AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_Durc_TenantId_AziendaId_DataScadenza"
                ON "Durc" ("TenantId", "AziendaId", "DataScadenza");

            CREATE TABLE IF NOT EXISTS "Promemoria" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Promemoria" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NULL,
                "Tipo" TEXT NOT NULL,
                "RiferimentoId" TEXT NULL,
                "Titolo" TEXT NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "DataScadenza" TEXT NOT NULL,
                "GiorniPreavviso" INTEGER NOT NULL,
                "Stato" TEXT NOT NULL,
                "Messaggio" TEXT NOT NULL,
                "CreatoIl" TEXT NOT NULL,
                "UltimoAggiornamento" TEXT NOT NULL,
                "InviatoIl" TEXT NULL,
                CONSTRAINT "FK_Promemoria_Aziende_AziendaId"
                    FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_Promemoria_AziendaId" ON "Promemoria" ("AziendaId");
            CREATE INDEX IF NOT EXISTS "IX_Promemoria_TenantId_DataScadenza"
                ON "Promemoria" ("TenantId", "DataScadenza");
            CREATE INDEX IF NOT EXISTS "IX_Promemoria_TenantId_Tipo_RiferimentoId"
                ON "Promemoria" ("TenantId", "Tipo", "RiferimentoId");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PraticheF24" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PraticheF24" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Anno" INTEGER NOT NULL,
                "Mese" INTEGER NOT NULL,
                "DataVersamento" TEXT NOT NULL,
                "TotaleDebiti" TEXT NOT NULL,
                "TotaleCrediti" TEXT NOT NULL,
                "SaldoFinale" TEXT NOT NULL,
                "Stato" TEXT NOT NULL,
                "Valido" INTEGER NOT NULL,
                "ModelloJson" TEXT NOT NULL,
                "Bozza" TEXT NOT NULL,
                "TracciatoUfficiale" TEXT NOT NULL DEFAULT '',
                "CreataIl" TEXT NOT NULL,
                "UltimoAggiornamento" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PraticheF24_TenantId_AziendaId_Anno_Mese"
                ON "PraticheF24" ("TenantId", "AziendaId", "Anno", "Mese");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Bilanci" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Bilanci" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Anno" INTEGER NOT NULL,
                "ValoreDellaProduzione" TEXT NOT NULL,
                "CostiDellaProduzione" TEXT NOT NULL,
                "ProventiFinanziari" TEXT NOT NULL,
                "OneriFinanziari" TEXT NOT NULL,
                "Imposte" TEXT NOT NULL,
                "UtilePerditaEsercizio" TEXT NOT NULL,
                "TotaleAttivo" TEXT NOT NULL,
                "TotalePassivo" TEXT NOT NULL,
                "PatrimonioNetto" TEXT NOT NULL,
                "SbilancioAttivoPassivo" TEXT NOT NULL,
                "Quadra" INTEGER NOT NULL,
                "Stato" TEXT NOT NULL,
                "BilancioJson" TEXT NOT NULL,
                "Bozza" TEXT NOT NULL,
                "CreataIl" TEXT NOT NULL,
                "UltimoAggiornamento" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "RigheBilancio" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_RigheBilancio" PRIMARY KEY,
                "BilancioId" TEXT NOT NULL,
                "Codice" TEXT NOT NULL,
                "Descrizione" TEXT NOT NULL,
                "Voce" TEXT NOT NULL,
                "Importo" TEXT NOT NULL,
                CONSTRAINT "FK_RigheBilancio_Bilanci_BilancioId"
                    FOREIGN KEY ("BilancioId") REFERENCES "Bilanci" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Bilanci_TenantId_AziendaId_Anno"
                ON "Bilanci" ("TenantId", "AziendaId", "Anno");
            CREATE INDEX IF NOT EXISTS "IX_RigheBilancio_BilancioId"
                ON "RigheBilancio" ("BilancioId");
            """);

        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "AuditLog" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_AuditLog" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "UtenteId" TEXT NULL,
                "Azione" TEXT NOT NULL,
                "Entita" TEXT NOT NULL,
                "ChiaveEntita" TEXT NOT NULL,
                "CampiModificati" TEXT NULL,
                "QuandoUtc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_AuditLog_TenantId_QuandoUtc"
                ON "AuditLog" ("TenantId", "QuandoUtc");
            CREATE INDEX IF NOT EXISTS "IX_AuditLog_TenantId_Entita"
                ON "AuditLog" ("TenantId", "Entita");
            """);

        // Colonne 2FA aggiunte a una tabella Utenti preesistente (EnsureCreated non altera le tabelle).
        if (!ColonnaSqliteEsiste("Utenti", "DueFattoriAttivo"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Utenti\" ADD COLUMN \"DueFattoriAttivo\" INTEGER NOT NULL DEFAULT 0;");
        if (!ColonnaSqliteEsiste("Utenti", "SegretoTotp"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Utenti\" ADD COLUMN \"SegretoTotp\" TEXT NULL;");

        // Flag GDPR (diritto all'oblio) su una tabella Dipendenti preesistente.
        if (!ColonnaSqliteEsiste("Dipendenti", "Anonimizzato"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Dipendenti\" ADD COLUMN \"Anonimizzato\" INTEGER NOT NULL DEFAULT 0;");

        // Flag sospensione studio (super-admin) su una tabella Tenants preesistente.
        if (!ColonnaSqliteEsiste("Tenants", "Sospeso"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Tenants\" ADD COLUMN \"Sospeso\" INTEGER NOT NULL DEFAULT 0;");

        // Conguaglio IRPEF di dicembre archiviato sul cedolino.
        if (!ColonnaSqliteEsiste("Cedolini", "ConguaglioIrpef"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Cedolini\" ADD COLUMN \"ConguaglioIrpef\" TEXT NOT NULL DEFAULT '0.0';");

        // Variazione ISTAT per la rivalutazione TFR, per anno.
        if (!ColonnaSqliteEsiste("ParametriAnno", "VariazioneIstatTfr"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"ParametriAnno\" ADD COLUMN \"VariazioneIstatTfr\" TEXT NULL;");

        // Fondo TFR maturato prima dell'uso del software, sul rapporto.
        if (!ColonnaSqliteEsiste("Rapporti", "FondoTfrIniziale"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Rapporti\" ADD COLUMN \"FondoTfrIniziale\" TEXT NULL;");

        // Settore attività dell'azienda: serve a guidare ATECO, CCNL e cedolini.
        if (!ColonnaSqliteEsiste("Aziende", "Settore"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Aziende\" ADD COLUMN \"Settore\" TEXT NULL;");

        // Assoggettamento IVA: le aziende esistenti restano assoggettate (default vero).
        if (!ColonnaSqliteEsiste("Aziende", "AssoggettataIva"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE \"Aziende\" ADD COLUMN \"AssoggettataIva\" INTEGER NOT NULL DEFAULT 1;");

        // Posizioni INAIL (PAT + tasso) per l'autoliquidazione.
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PosizioniInail" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PosizioniInail" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "AziendaId" TEXT NOT NULL,
                "Anno" INTEGER NOT NULL,
                "Pat" TEXT NOT NULL,
                "VoceTariffa" TEXT NOT NULL,
                "TassoPerMille" TEXT NOT NULL,
                "AggiornataIl" TEXT NOT NULL,
                CONSTRAINT "FK_PosizioniInail_Aziende_AziendaId"
                    FOREIGN KEY ("AziendaId") REFERENCES "Aziende" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PosizioniInail_TenantId_AziendaId_Anno"
                ON "PosizioniInail" ("TenantId", "AziendaId", "Anno");
            CREATE INDEX IF NOT EXISTS "IX_PosizioniInail_AziendaId"
                ON "PosizioniInail" ("AziendaId");
            """);

        // Archivio persone fisiche per la compilazione automatica dei moduli ufficiali.
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Persone" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Persone" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "Cognome" TEXT NOT NULL,
                "Nome" TEXT NOT NULL,
                "CodiceFiscale" TEXT NOT NULL,
                "Sesso" TEXT NULL,
                "DataNascita" TEXT NULL,
                "ComuneNascita" TEXT NULL,
                "ProvinciaNascita" TEXT NULL,
                "IndirizzoResidenza" TEXT NULL,
                "CapResidenza" TEXT NULL,
                "ComuneResidenza" TEXT NULL,
                "ProvinciaResidenza" TEXT NULL,
                "Telefono" TEXT NULL,
                "Email" TEXT NULL,
                "CreataIl" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Persone_TenantId_CodiceFiscale"
                ON "Persone" ("TenantId", "CodiceFiscale");
            """);

        // Contatore mensile delle chiamate AI per tenant ("AI inclusa nel canone").
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "ConsumiAi" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ConsumiAi" PRIMARY KEY,
                "TenantId" TEXT NOT NULL,
                "Anno" INTEGER NOT NULL,
                "Mese" INTEGER NOT NULL,
                "Chiamate" INTEGER NOT NULL,
                "AggiornatoIl" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConsumiAi_TenantId_Anno_Mese"
                ON "ConsumiAi" ("TenantId", "Anno", "Mese");
            """);
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
                Database.ExecuteSqlRaw(sql + ";");
            }
        }
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

    /// <summary>True se la colonna esiste già nella tabella SQLite (via PRAGMA table_info).</summary>
    private bool ColonnaSqliteEsiste(string tabella, string colonna)
    {
        var conn = Database.GetDbConnection();
        var daAprire = conn.State != ConnectionState.Open;
        if (daAprire) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tabella}\");";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                if (string.Equals(reader.GetString(1), colonna, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        finally
        {
            if (daAprire) conn.Close();
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // GDPR: cifratura applicativa a riposo dei dati personali sensibili (IBAN).
        var protettore = ProtettoreDati.Corrente;

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

        // --- Autenticazione ---
        mb.Entity<TenantEntity>(e => e.HasKey(x => x.Id));
        mb.Entity<UtenteEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.SegretoTotp).HasConversion(protettore.ConverterStringa()); // GDPR: cifrato a riposo
        });

        // Segreto del provider SdI: cifrato a riposo (non deve stare in chiaro nel DB).
        mb.Entity<ConfigurazioneSdiEntity>(e =>
            e.Property(x => x.ApiKey).HasConversion(protettore.ConverterStringa()));

        // --- Anagrafiche e cedolini del lavoro ---
        mb.Entity<DipendenteEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CodiceFiscale }).IsUnique();
            e.Property(x => x.Iban).HasConversion(protettore.ConverterStringa()); // GDPR: cifrato a riposo
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
                nameof(CedolinoEntity.CostoAzienda), nameof(CedolinoEntity.ConguaglioIrpef) })
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

        // --- Avvisi di pagamento clienti dello studio ---
        mb.Entity<AvvisoPagamentoEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.AziendaId, x.Anno, x.Mese }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Stato, x.DataScadenza });
            e.Property(x => x.Importo).HasPrecision(15, 2);
            e.Property(x => x.Iban).HasConversion(protettore.ConverterStringa()); // GDPR: cifrato a riposo
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
