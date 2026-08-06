using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paghe.Migrazioni.Sqlite.Migrazioni
{
    /// <inheritdoc />
    public partial class Iniziale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddizionaliComunali",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    CodiceCatastale = table.Column<string>(type: "TEXT", nullable: false),
                    Aliquota = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddizionaliComunali", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AddizionaliRegionali",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Regione = table.Column<string>(type: "TEXT", nullable: false),
                    Aliquota = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddizionaliRegionali", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequenza = table.Column<long>(type: "INTEGER", nullable: false),
                    GrantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UtenteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Evento = table.Column<string>(type: "TEXT", nullable: false),
                    Dettaglio = table.Column<string>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: true),
                    QuandoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HashPrecedente = table.Column<string>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiScopeGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PurposeCode = table.Column<string>(type: "TEXT", nullable: false),
                    Modalita = table.Column<string>(type: "TEXT", nullable: false),
                    ScopeLevel = table.Column<string>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Periodo = table.Column<string>(type: "TEXT", nullable: true),
                    RisorseAmmesse = table.Column<string>(type: "TEXT", nullable: false),
                    OperazioniAmmesse = table.Column<string>(type: "TEXT", nullable: false),
                    AutonomiaMax = table.Column<string>(type: "TEXT", nullable: false),
                    PolicyVersione = table.Column<string>(type: "TEXT", nullable: false),
                    Nonce = table.Column<string>(type: "TEXT", nullable: false),
                    CreatoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScadeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TimeoutInattivitaMinuti = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoUsoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MaxLetture = table.Column<int>(type: "INTEGER", nullable: false),
                    LettureUsate = table.Column<int>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    RevocatoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Versione = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiScopeGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnteprimeRetention",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    VersionePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    SogliaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Candidati = table.Column<int>(type: "INTEGER", nullable: false),
                    Bloccati = table.Column<int>(type: "INTEGER", nullable: false),
                    Elaborabili = table.Column<int>(type: "INTEGER", nullable: false),
                    ImprontaCandidati = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ImprontaBlocchi = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScadeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreataDaUtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConsumataUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnteprimeRetention", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtenteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, defaultValue: "ModificaDati"),
                    Azione = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Entita = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChiaveEntita = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CampiModificati = table.Column<string>(type: "TEXT", nullable: true),
                    MetodoHttp = table.Column<string>(type: "TEXT", maxLength: 12, nullable: true),
                    PercorsoTemplate = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Esito = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    QuandoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Aziende",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RagioneSociale = table.Column<string>(type: "TEXT", nullable: false),
                    PartitaIva = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceFiscale = table.Column<string>(type: "TEXT", nullable: true),
                    Indirizzo = table.Column<string>(type: "TEXT", nullable: false),
                    Cap = table.Column<string>(type: "TEXT", nullable: false),
                    Comune = table.Column<string>(type: "TEXT", nullable: false),
                    Provincia = table.Column<string>(type: "TEXT", nullable: false),
                    Nazione = table.Column<string>(type: "TEXT", nullable: false),
                    RegimeFiscale = table.Column<string>(type: "TEXT", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Pec = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceDestinatarioSdi = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceCatastale = table.Column<string>(type: "TEXT", nullable: true),
                    Settore = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceAteco = table.Column<string>(type: "TEXT", nullable: true),
                    MatricolaInps = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceSedeInps = table.Column<string>(type: "TEXT", nullable: true),
                    CscInps = table.Column<string>(type: "TEXT", nullable: true),
                    PatInail = table.Column<string>(type: "TEXT", nullable: true),
                    CcnlDefault = table.Column<string>(type: "TEXT", nullable: true),
                    LivelloDefault = table.Column<string>(type: "TEXT", nullable: true),
                    IbanProtetto = table.Column<string>(type: "TEXT", nullable: true),
                    LegaleRappresentanteNome = table.Column<string>(type: "TEXT", nullable: true),
                    LegaleRappresentanteCognome = table.Column<string>(type: "TEXT", nullable: true),
                    LegaleRappresentanteCodiceFiscale = table.Column<string>(type: "TEXT", nullable: true),
                    CanalePredefinito = table.Column<string>(type: "TEXT", nullable: true),
                    Logo = table.Column<byte[]>(type: "BLOB", nullable: true),
                    HaDipendenti = table.Column<bool>(type: "INTEGER", nullable: false),
                    TipoContribuente = table.Column<string>(type: "TEXT", nullable: true),
                    AssoggettataIva = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aziende", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bilanci",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    ValoreDellaProduzione = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    CostiDellaProduzione = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    ProventiFinanziari = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    OneriFinanziari = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Imposte = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    UtilePerditaEsercizio = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotaleAttivo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotalePassivo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    PatrimonioNetto = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    SbilancioAttivoPassivo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Quadra = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    BilancioJson = table.Column<string>(type: "TEXT", nullable: false),
                    Bozza = table.Column<string>(type: "TEXT", nullable: false),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAggiornamento = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bilanci", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlocchiConservazione",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    MotivoCodice = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CreatoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatoDaUtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RevocatoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevocatoDaUtenteId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlocchiConservazione", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cartellini",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RapportoLavoroId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Mese = table.Column<int>(type: "INTEGER", nullable: false),
                    OreStraordinario = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    OreStraordinarioNotturno = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    OreStraordinarioFestivo = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    GiorniFerie = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    OrePermessi = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    OreExFestivita = table.Column<decimal>(type: "TEXT", nullable: false),
                    DatiSaluteProtetti = table.Column<string>(type: "TEXT", nullable: false),
                    OreAssenzaNonRetribuita = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartellini", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaselleEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Etichetta = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Porta = table.Column<int>(type: "INTEGER", nullable: false),
                    Utente = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: true),
                    Ssl = table.Column<bool>(type: "INTEGER", nullable: false),
                    Attiva = table.Column<bool>(type: "INTEGER", nullable: false),
                    UltimoUidSincronizzato = table.Column<long>(type: "INTEGER", nullable: true),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaSincronizzazioneIl = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaselleEmail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ccnl",
                columns: table => new
                {
                    Codice = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ccnl", x => x.Codice);
                });

            migrationBuilder.CreateTable(
                name: "CcnlLivelli",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CcnlCodice = table.Column<string>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Livello = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    MinimoTabellare = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Edr = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    ImportoScatto = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CcnlLivelli", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cedolini",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RapportoLavoroId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Mese = table.Column<int>(type: "INTEGER", nullable: false),
                    TotaleCompetenze = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotaleTrattenute = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    ImponibileInps = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    ContributiDipendente = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    ContributiDatore = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    ImponibileIrpef = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    IrpefNetta = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    AddizionaleRegionale = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    AddizionaleComunale = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    NettoInBusta = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    QuotaTfr = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    CostoAzienda = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    FerieMaturate = table.Column<decimal>(type: "TEXT", nullable: false),
                    FerieGodute = table.Column<decimal>(type: "TEXT", nullable: false),
                    FerieResidue = table.Column<decimal>(type: "TEXT", nullable: false),
                    PermessiMaturati = table.Column<decimal>(type: "TEXT", nullable: false),
                    PermessiGoduti = table.Column<decimal>(type: "TEXT", nullable: false),
                    PermessiResidui = table.Column<decimal>(type: "TEXT", nullable: false),
                    ConguaglioIrpef = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    CalcolatoIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cedolini", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurazioniSdi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Modalita = table.Column<string>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    EndpointInvio = table.Column<string>(type: "TEXT", nullable: false),
                    EndpointEsito = table.Column<string>(type: "TEXT", nullable: false),
                    CampoId = table.Column<string>(type: "TEXT", nullable: false),
                    CampoStato = table.Column<string>(type: "TEXT", nullable: false),
                    FirmaLocale = table.Column<bool>(type: "INTEGER", nullable: false),
                    Attivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    AggiornataIl = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurazioniSdi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsumiAi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Mese = table.Column<int>(type: "INTEGER", nullable: false),
                    Chiamate = table.Column<int>(type: "INTEGER", nullable: false),
                    AggiornatoIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumiAi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DichiarazioniRedditiPf",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RapportoLavoroId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    AltriRedditi = table.Column<decimal>(type: "TEXT", nullable: false),
                    OneriDeducibili = table.Column<decimal>(type: "TEXT", nullable: false),
                    OneriDetraibili19 = table.Column<decimal>(type: "TEXT", nullable: false),
                    Acconti = table.Column<decimal>(type: "TEXT", nullable: false),
                    AggiornataIl = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DichiarazioniRedditiPf", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dipendenti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodiceFiscale = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Cognome = table.Column<string>(type: "TEXT", nullable: false),
                    IbanProtetto = table.Column<string>(type: "TEXT", nullable: true),
                    Anonimizzato = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dipendenti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EsecuzioniRetention",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnteprimaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChiaveIdempotenza = table.Column<Guid>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    VersionePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    SogliaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CandidatiAnteprima = table.Column<int>(type: "INTEGER", nullable: false),
                    Bloccati = table.Column<int>(type: "INTEGER", nullable: false),
                    Elaborati = table.Column<int>(type: "INTEGER", nullable: false),
                    RestantiStimati = table.Column<int>(type: "INTEGER", nullable: false),
                    Esito = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IniziataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EseguitaDaUtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DigestPrecedente = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Digest = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EsecuzioniRetention", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Immobili",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    CategoriaCatastale = table.Column<string>(type: "TEXT", nullable: false),
                    RenditaCatastale = table.Column<decimal>(type: "TEXT", nullable: false),
                    PercentualePossesso = table.Column<decimal>(type: "TEXT", nullable: false),
                    MesiPossesso = table.Column<int>(type: "INTEGER", nullable: false),
                    AliquotaPerMille = table.Column<decimal>(type: "TEXT", nullable: false),
                    AbitazionePrincipale = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatoIl = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Immobili", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InpsAliquote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Qualifica = table.Column<string>(type: "TEXT", nullable: false),
                    Dipendente = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    Datore = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InpsAliquote", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IrpefScaglioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    DaImporto = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    AImporto = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: true),
                    Aliquota = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrpefScaglioni", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessaggiEmail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CasellaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UidImap = table.Column<long>(type: "INTEGER", nullable: false),
                    Mittente = table.Column<string>(type: "TEXT", nullable: false),
                    Oggetto = table.Column<string>(type: "TEXT", nullable: false),
                    DataRicezione = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Anteprima = table.Column<string>(type: "TEXT", nullable: false),
                    Letto = table.Column<bool>(type: "INTEGER", nullable: false),
                    Urgente = table.Column<bool>(type: "INTEGER", nullable: true),
                    Pubblicita = table.Column<bool>(type: "INTEGER", nullable: true),
                    UrgenteProposto = table.Column<bool>(type: "INTEGER", nullable: true),
                    PubblicitaProposto = table.Column<bool>(type: "INTEGER", nullable: true),
                    ConfidenzaUrgente = table.Column<decimal>(type: "TEXT", nullable: true),
                    ConfidenzaPubblicita = table.Column<decimal>(type: "TEXT", nullable: true),
                    FonteProposta = table.Column<string>(type: "TEXT", nullable: true),
                    Corpo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessaggiEmail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParametriAnno",
                columns: table => new
                {
                    Anno = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DivisoreOrario = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    MaggiorazioneStraordinario = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    MaggiorazioneStraordinarioNotturno = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    MaggiorazioneStraordinarioFestivo = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    TfrDivisore = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    TfrContributoFondo = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: false),
                    VariazioneIstatTfr = table.Column<decimal>(type: "TEXT", precision: 12, scale: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametriAnno", x => x.Anno);
                });

            migrationBuilder.CreateTable(
                name: "Persone",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Cognome = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    CodiceFiscale = table.Column<string>(type: "TEXT", nullable: false),
                    Sesso = table.Column<string>(type: "TEXT", nullable: true),
                    DataNascita = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ComuneNascita = table.Column<string>(type: "TEXT", nullable: true),
                    ProvinciaNascita = table.Column<string>(type: "TEXT", nullable: true),
                    IndirizzoResidenza = table.Column<string>(type: "TEXT", nullable: true),
                    CapResidenza = table.Column<string>(type: "TEXT", nullable: true),
                    ComuneResidenza = table.Column<string>(type: "TEXT", nullable: true),
                    ProvinciaResidenza = table.Column<string>(type: "TEXT", nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    CreataIl = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persone", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyRetention",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Versione = table.Column<int>(type: "INTEGER", nullable: false),
                    GiorniConservazione = table.Column<int>(type: "INTEGER", nullable: true),
                    Attiva = table.Column<bool>(type: "INTEGER", nullable: false),
                    Approvata = table.Column<bool>(type: "INTEGER", nullable: false),
                    BaseGiuridica = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RiferimentoDecisione = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IstruzioniBackup = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Finalita = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Decorrenza = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Azione = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreataDaUtenteId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyRetention", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PraticheF24",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Mese = table.Column<int>(type: "INTEGER", nullable: false),
                    DataVersamento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotaleDebiti = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotaleCrediti = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    SaldoFinale = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Valido = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModelloJson = table.Column<string>(type: "TEXT", nullable: false),
                    Bozza = table.Column<string>(type: "TEXT", nullable: false),
                    TracciatoUfficiale = table.Column<string>(type: "TEXT", nullable: false),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAggiornamento = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PraticheF24", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PraticheInail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Pat = table.Column<string>(type: "TEXT", nullable: false),
                    VoceTariffa = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroCedolini = table.Column<int>(type: "INTEGER", nullable: false),
                    RetribuzioniAnno = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TassoPerMille = table.Column<decimal>(type: "TEXT", precision: 8, scale: 3, nullable: false),
                    Regolazione = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    RataAnticipata = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    RataGiaVersata = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotaleDaVersare = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Bozza = table.Column<string>(type: "TEXT", nullable: false),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAggiornamento = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PraticheInail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RagioneSociale = table.Column<string>(type: "TEXT", nullable: false),
                    CreatoIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Sospeso = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Utenti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Ruolo = table.Column<string>(type: "TEXT", nullable: false),
                    Attivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatoIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueFattoriAttivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    SegretoTotp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utenti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AvvisiPagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Mese = table.Column<int>(type: "INTEGER", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    Importo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    DataEmissione = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataScadenza = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    MetodoPagamento = table.Column<string>(type: "TEXT", nullable: false),
                    IbanProtetto = table.Column<string>(type: "TEXT", nullable: true),
                    Causale = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    PagatoIl = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RiferimentoPagamento = table.Column<string>(type: "TEXT", nullable: true),
                    NumeroPromemoria = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoPromemoriaIl = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAggiornamento = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvvisiPagamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvvisiPagamento_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deleghe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TipoAdempimento = table.Column<string>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    DataInizio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataFine = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Riferimento = table.Column<string>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deleghe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deleghe_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Durc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", nullable: false),
                    DataEmissione = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataScadenza = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Regolare = table.Column<bool>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreatoIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Durc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Durc_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fatture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TipoDocumento = table.Column<string>(type: "TEXT", nullable: false),
                    Divisa = table.Column<string>(type: "TEXT", nullable: false),
                    CessionarioDenominazione = table.Column<string>(type: "TEXT", nullable: false),
                    CessionarioPartitaIva = table.Column<string>(type: "TEXT", nullable: true),
                    CessionarioCodiceFiscale = table.Column<string>(type: "TEXT", nullable: true),
                    CessionarioIndirizzo = table.Column<string>(type: "TEXT", nullable: false),
                    CessionarioCap = table.Column<string>(type: "TEXT", nullable: false),
                    CessionarioComune = table.Column<string>(type: "TEXT", nullable: false),
                    CessionarioProvincia = table.Column<string>(type: "TEXT", nullable: false),
                    CessionarioNazione = table.Column<string>(type: "TEXT", nullable: false),
                    CodiceDestinatario = table.Column<string>(type: "TEXT", nullable: false),
                    TotaleImponibile = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotaleImposta = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    TotaleDocumento = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fatture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fatture_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiquidazioniIva",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Dal = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Al = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Periodicita = table.Column<string>(type: "TEXT", nullable: false),
                    IvaVendite = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    IvaAcquistiDetraibile = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    CreditoPeriodoPrecedente = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Saldo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    IvaDaVersare = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    CreditoDaRiportare = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiquidazioniIva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiquidazioniIva_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PosizioniInail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    Pat = table.Column<string>(type: "TEXT", nullable: false),
                    VoceTariffa = table.Column<string>(type: "TEXT", nullable: false),
                    TassoPerMille = table.Column<decimal>(type: "TEXT", precision: 8, scale: 3, nullable: false),
                    AggiornataIl = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosizioniInail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosizioniInail_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PraticheAperturaPartitaIva",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TipoPratica = table.Column<string>(type: "TEXT", nullable: false),
                    FormaGiuridica = table.Column<string>(type: "TEXT", nullable: false),
                    RegimeContabile = table.Column<string>(type: "TEXT", nullable: false),
                    DataInizioAttivita = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Denominazione = table.Column<string>(type: "TEXT", nullable: false),
                    CodiceFiscale = table.Column<string>(type: "TEXT", nullable: false),
                    PartitaIva = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceAteco = table.Column<string>(type: "TEXT", nullable: false),
                    DescrizioneAttivita = table.Column<string>(type: "TEXT", nullable: false),
                    IndirizzoSede = table.Column<string>(type: "TEXT", nullable: false),
                    Cap = table.Column<string>(type: "TEXT", nullable: false),
                    Comune = table.Column<string>(type: "TEXT", nullable: false),
                    Provincia = table.Column<string>(type: "TEXT", nullable: false),
                    Pec = table.Column<string>(type: "TEXT", nullable: true),
                    AttivitaCommercio = table.Column<bool>(type: "INTEGER", nullable: false),
                    AttivitaArtigiana = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImpresaAgricola = table.Column<bool>(type: "INTEGER", nullable: false),
                    HaDipendentiPrevisti = table.Column<bool>(type: "INTEGER", nullable: false),
                    SoggettaInail = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModelloAgenziaEntrate = table.Column<string>(type: "TEXT", nullable: false),
                    RichiedeComunicazioneUnica = table.Column<bool>(type: "INTEGER", nullable: false),
                    RichiedeRegistroImprese = table.Column<bool>(type: "INTEGER", nullable: false),
                    RichiedeCameraCommercio = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PraticheAperturaPartitaIva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PraticheAperturaPartitaIva_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Promemoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    RiferimentoId = table.Column<string>(type: "TEXT", nullable: true),
                    Titolo = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    DataScadenza = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    GiorniPreavviso = table.Column<int>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Messaggio = table.Column<string>(type: "TEXT", nullable: false),
                    CreatoIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAggiornamento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InviatoIl = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promemoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Promemoria_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Spese",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    ContoCosto = table.Column<string>(type: "TEXT", nullable: false),
                    Fornitore = table.Column<string>(type: "TEXT", nullable: true),
                    NumeroDocumento = table.Column<string>(type: "TEXT", nullable: true),
                    Imponibile = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    AliquotaIva = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    PercentualeDetraibilita = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    Iva = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    IvaDetraibile = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Totale = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spese", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spese_Aziende_AziendaId",
                        column: x => x.AziendaId,
                        principalTable: "Aziende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigheBilancio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BilancioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Codice = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    Voce = table.Column<string>(type: "TEXT", nullable: false),
                    Importo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigheBilancio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigheBilancio_Bilanci_BilancioId",
                        column: x => x.BilancioId,
                        principalTable: "Bilanci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CedolinoVoceEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CedolinoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DatiVoceProtetti = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CedolinoVoceEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CedolinoVoceEntity_Cedolini_CedolinoId",
                        column: x => x.CedolinoId,
                        principalTable: "Cedolini",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rapporti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DipendenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Matricola = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceCcnl = table.Column<string>(type: "TEXT", nullable: false),
                    Livello = table.Column<string>(type: "TEXT", nullable: false),
                    Qualifica = table.Column<string>(type: "TEXT", nullable: false),
                    TipoContratto = table.Column<string>(type: "TEXT", nullable: false),
                    OreSettimanali = table.Column<decimal>(type: "TEXT", nullable: false),
                    NumeroMensilita = table.Column<int>(type: "INTEGER", nullable: false),
                    PartTime = table.Column<bool>(type: "INTEGER", nullable: false),
                    PercentualePartTime = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: false),
                    ScattiAnzianita = table.Column<int>(type: "INTEGER", nullable: false),
                    Superminimo = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    Regione = table.Column<string>(type: "TEXT", nullable: false),
                    CodiceCatastaleComune = table.Column<string>(type: "TEXT", nullable: false),
                    DetrazioniLavoroDipendente = table.Column<bool>(type: "INTEGER", nullable: false),
                    GiorniDetrazioneAnno = table.Column<int>(type: "INTEGER", nullable: false),
                    DataAssunzione = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataCessazione = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FondoTfrIniziale = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rapporti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rapporti_Dipendenti_DipendenteId",
                        column: x => x.DipendenteId,
                        principalTable: "Dipendenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessioniRefresh",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UtenteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamigliaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HashSegreto = table.Column<string>(type: "TEXT", nullable: false),
                    Client = table.Column<string>(type: "TEXT", nullable: false),
                    CreataUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScadeAssolutaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScadeInattivitaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsataUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevocataUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MotivoRevoca = table.Column<string>(type: "TEXT", nullable: true),
                    SostituitaDaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Versione = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessioniRefresh", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessioniRefresh_SessioniRefresh_SostituitaDaId",
                        column: x => x.SostituitaDaId,
                        principalTable: "SessioniRefresh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessioniRefresh_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessioniRefresh_Utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "Utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigaFatturaEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FatturaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    Quantita = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    PrezzoUnitario = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    AliquotaIva = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    NaturaIva = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigaFatturaEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigaFatturaEntity_Fatture_FatturaId",
                        column: x => x.FatturaId,
                        principalTable: "Fatture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrasmissioniFatture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AziendaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FatturaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    NomeFileXml = table.Column<string>(type: "TEXT", nullable: false),
                    Firmato = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    IdentificativoSdi = table.Column<string>(type: "TEXT", nullable: true),
                    CodiceErrore = table.Column<string>(type: "TEXT", nullable: true),
                    MessaggioErrore = table.Column<string>(type: "TEXT", nullable: true),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InviataIl = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UltimoAggiornamento = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrasmissioniFatture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrasmissioniFatture_Fatture_FatturaId",
                        column: x => x.FatturaId,
                        principalTable: "Fatture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RigaLiquidazioneIvaEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LiquidazioneIvaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Imponibile = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    AliquotaIva = table.Column<decimal>(type: "TEXT", precision: 8, scale: 5, nullable: false),
                    Iva = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false),
                    IvaDetraibile = table.Column<decimal>(type: "TEXT", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RigaLiquidazioneIvaEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RigaLiquidazioneIvaEntity_LiquidazioniIva_LiquidazioneIvaId",
                        column: x => x.LiquidazioneIvaId,
                        principalTable: "LiquidazioniIva",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdempimentoPraticaEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PraticaAperturaPartitaIvaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Codice = table.Column<string>(type: "TEXT", nullable: false),
                    Ente = table.Column<string>(type: "TEXT", nullable: false),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: false),
                    Obbligatorio = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdempimentoPraticaEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdempimentoPraticaEntity_PraticheAperturaPartitaIva_PraticaAperturaPartitaIvaId",
                        column: x => x.PraticaAperturaPartitaIvaId,
                        principalTable: "PraticheAperturaPartitaIva",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RicevuteSdi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrasmissioneFatturaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    CodiceEsito = table.Column<string>(type: "TEXT", nullable: true),
                    Descrizione = table.Column<string>(type: "TEXT", nullable: true),
                    ContenutoGrezzo = table.Column<string>(type: "TEXT", nullable: true),
                    RicevutaIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RicevuteSdi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RicevuteSdi_TrasmissioniFatture_TrasmissioneFatturaId",
                        column: x => x.TrasmissioneFatturaId,
                        principalTable: "TrasmissioniFatture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddizionaliComunali_Anno_CodiceCatastale",
                table: "AddizionaliComunali",
                columns: new[] { "Anno", "CodiceCatastale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddizionaliRegionali_Anno_Regione",
                table: "AddizionaliRegionali",
                columns: new[] { "Anno", "Regione" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdempimentoPraticaEntity_PraticaAperturaPartitaIvaId",
                table: "AdempimentoPraticaEntity",
                column: "PraticaAperturaPartitaIvaId");

            migrationBuilder.CreateIndex(
                name: "IX_AiAudit_TenantId_Sequenza",
                table: "AiAudit",
                columns: new[] { "TenantId", "Sequenza" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiScopeGrants_TenantId_Stato",
                table: "AiScopeGrants",
                columns: new[] { "TenantId", "Stato" });

            migrationBuilder.CreateIndex(
                name: "IX_AnteprimeRetention_TenantId_Categoria_ScadeUtc",
                table: "AnteprimeRetention",
                columns: new[] { "TenantId", "Categoria", "ScadeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_Entita",
                table: "AuditLog",
                columns: new[] { "TenantId", "Entita" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TenantId_QuandoUtc",
                table: "AuditLog",
                columns: new[] { "TenantId", "QuandoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AvvisiPagamento_AziendaId",
                table: "AvvisiPagamento",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_AvvisiPagamento_TenantId_AziendaId_Anno_Mese",
                table: "AvvisiPagamento",
                columns: new[] { "TenantId", "AziendaId", "Anno", "Mese" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvvisiPagamento_TenantId_Stato_DataScadenza",
                table: "AvvisiPagamento",
                columns: new[] { "TenantId", "Stato", "DataScadenza" });

            migrationBuilder.CreateIndex(
                name: "IX_Aziende_TenantId_CodiceFiscale",
                table: "Aziende",
                columns: new[] { "TenantId", "CodiceFiscale" });

            migrationBuilder.CreateIndex(
                name: "IX_Bilanci_TenantId_AziendaId_Anno",
                table: "Bilanci",
                columns: new[] { "TenantId", "AziendaId", "Anno" });

            migrationBuilder.CreateIndex(
                name: "IX_BlocchiConservazione_TenantId_Categoria_RevocatoUtc",
                table: "BlocchiConservazione",
                columns: new[] { "TenantId", "Categoria", "RevocatoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Cartellini_TenantId_RapportoLavoroId_Anno_Mese",
                table: "Cartellini",
                columns: new[] { "TenantId", "RapportoLavoroId", "Anno", "Mese" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaselleEmail_TenantId_Attiva",
                table: "CaselleEmail",
                columns: new[] { "TenantId", "Attiva" });

            migrationBuilder.CreateIndex(
                name: "IX_CcnlLivelli_CcnlCodice_Anno_Livello",
                table: "CcnlLivelli",
                columns: new[] { "CcnlCodice", "Anno", "Livello" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cedolini_TenantId_RapportoLavoroId_Anno_Mese",
                table: "Cedolini",
                columns: new[] { "TenantId", "RapportoLavoroId", "Anno", "Mese" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CedolinoVoceEntity_CedolinoId",
                table: "CedolinoVoceEntity",
                column: "CedolinoId");

            migrationBuilder.CreateIndex(
                name: "IX_CedolinoVoceEntity_TenantId_CedolinoId",
                table: "CedolinoVoceEntity",
                columns: new[] { "TenantId", "CedolinoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumiAi_TenantId_Anno_Mese",
                table: "ConsumiAi",
                columns: new[] { "TenantId", "Anno", "Mese" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deleghe_AziendaId",
                table: "Deleghe",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Deleghe_TenantId_AziendaId_TipoAdempimento",
                table: "Deleghe",
                columns: new[] { "TenantId", "AziendaId", "TipoAdempimento" });

            migrationBuilder.CreateIndex(
                name: "IX_Dipendenti_TenantId_CodiceFiscale",
                table: "Dipendenti",
                columns: new[] { "TenantId", "CodiceFiscale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Durc_AziendaId",
                table: "Durc",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Durc_TenantId_AziendaId_DataScadenza",
                table: "Durc",
                columns: new[] { "TenantId", "AziendaId", "DataScadenza" });

            migrationBuilder.CreateIndex(
                name: "IX_EsecuzioniRetention_TenantId_AnteprimaId",
                table: "EsecuzioniRetention",
                columns: new[] { "TenantId", "AnteprimaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EsecuzioniRetention_TenantId_Categoria_CompletataUtc",
                table: "EsecuzioniRetention",
                columns: new[] { "TenantId", "Categoria", "CompletataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EsecuzioniRetention_TenantId_ChiaveIdempotenza",
                table: "EsecuzioniRetention",
                columns: new[] { "TenantId", "ChiaveIdempotenza" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fatture_AziendaId",
                table: "Fatture",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Fatture_TenantId_AziendaId_Anno_Numero",
                table: "Fatture",
                columns: new[] { "TenantId", "AziendaId", "Anno", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InpsAliquote_Anno_Qualifica",
                table: "InpsAliquote",
                columns: new[] { "Anno", "Qualifica" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IrpefScaglioni_Anno_DaImporto",
                table: "IrpefScaglioni",
                columns: new[] { "Anno", "DaImporto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiquidazioniIva_AziendaId",
                table: "LiquidazioniIva",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_LiquidazioniIva_TenantId_AziendaId_Dal_Al",
                table: "LiquidazioniIva",
                columns: new[] { "TenantId", "AziendaId", "Dal", "Al" });

            migrationBuilder.CreateIndex(
                name: "IX_MessaggiEmail_CasellaId_UidImap",
                table: "MessaggiEmail",
                columns: new[] { "CasellaId", "UidImap" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessaggiEmail_TenantId_CasellaId_DataRicezione",
                table: "MessaggiEmail",
                columns: new[] { "TenantId", "CasellaId", "DataRicezione" });

            migrationBuilder.CreateIndex(
                name: "IX_Persone_TenantId_CodiceFiscale",
                table: "Persone",
                columns: new[] { "TenantId", "CodiceFiscale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyRetention_TenantId_Categoria_Versione",
                table: "PolicyRetention",
                columns: new[] { "TenantId", "Categoria", "Versione" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosizioniInail_AziendaId",
                table: "PosizioniInail",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_PosizioniInail_TenantId_AziendaId_Anno",
                table: "PosizioniInail",
                columns: new[] { "TenantId", "AziendaId", "Anno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PraticheAperturaPartitaIva_AziendaId",
                table: "PraticheAperturaPartitaIva",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_PraticheAperturaPartitaIva_TenantId_CodiceFiscale",
                table: "PraticheAperturaPartitaIva",
                columns: new[] { "TenantId", "CodiceFiscale" });

            migrationBuilder.CreateIndex(
                name: "IX_PraticheF24_TenantId_AziendaId_Anno_Mese",
                table: "PraticheF24",
                columns: new[] { "TenantId", "AziendaId", "Anno", "Mese" });

            migrationBuilder.CreateIndex(
                name: "IX_PraticheInail_TenantId_AziendaId_Anno",
                table: "PraticheInail",
                columns: new[] { "TenantId", "AziendaId", "Anno" });

            migrationBuilder.CreateIndex(
                name: "IX_Promemoria_AziendaId",
                table: "Promemoria",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Promemoria_TenantId_DataScadenza",
                table: "Promemoria",
                columns: new[] { "TenantId", "DataScadenza" });

            migrationBuilder.CreateIndex(
                name: "IX_Promemoria_TenantId_Tipo_RiferimentoId",
                table: "Promemoria",
                columns: new[] { "TenantId", "Tipo", "RiferimentoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rapporti_DipendenteId",
                table: "Rapporti",
                column: "DipendenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Rapporti_TenantId_AziendaId",
                table: "Rapporti",
                columns: new[] { "TenantId", "AziendaId" });

            migrationBuilder.CreateIndex(
                name: "IX_RicevuteSdi_TrasmissioneFatturaId",
                table: "RicevuteSdi",
                column: "TrasmissioneFatturaId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaFatturaEntity_FatturaId",
                table: "RigaFatturaEntity",
                column: "FatturaId");

            migrationBuilder.CreateIndex(
                name: "IX_RigaLiquidazioneIvaEntity_LiquidazioneIvaId",
                table: "RigaLiquidazioneIvaEntity",
                column: "LiquidazioneIvaId");

            migrationBuilder.CreateIndex(
                name: "IX_RigheBilancio_BilancioId",
                table: "RigheBilancio",
                column: "BilancioId");

            migrationBuilder.CreateIndex(
                name: "IX_SessioniRefresh_SostituitaDaId",
                table: "SessioniRefresh",
                column: "SostituitaDaId");

            migrationBuilder.CreateIndex(
                name: "IX_SessioniRefresh_TenantId_FamigliaId",
                table: "SessioniRefresh",
                columns: new[] { "TenantId", "FamigliaId" });

            migrationBuilder.CreateIndex(
                name: "IX_SessioniRefresh_TenantId_Id",
                table: "SessioniRefresh",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessioniRefresh_TenantId_UtenteId_RevocataUtc",
                table: "SessioniRefresh",
                columns: new[] { "TenantId", "UtenteId", "RevocataUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SessioniRefresh_UtenteId",
                table: "SessioniRefresh",
                column: "UtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Spese_AziendaId",
                table: "Spese",
                column: "AziendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Spese_TenantId_AziendaId_Data",
                table: "Spese",
                columns: new[] { "TenantId", "AziendaId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_TrasmissioniFatture_FatturaId",
                table: "TrasmissioniFatture",
                column: "FatturaId");

            migrationBuilder.CreateIndex(
                name: "IX_TrasmissioniFatture_TenantId_AziendaId",
                table: "TrasmissioniFatture",
                columns: new[] { "TenantId", "AziendaId" });

            migrationBuilder.CreateIndex(
                name: "IX_Utenti_Email",
                table: "Utenti",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddizionaliComunali");

            migrationBuilder.DropTable(
                name: "AddizionaliRegionali");

            migrationBuilder.DropTable(
                name: "AdempimentoPraticaEntity");

            migrationBuilder.DropTable(
                name: "AiAudit");

            migrationBuilder.DropTable(
                name: "AiScopeGrants");

            migrationBuilder.DropTable(
                name: "AnteprimeRetention");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "AvvisiPagamento");

            migrationBuilder.DropTable(
                name: "BlocchiConservazione");

            migrationBuilder.DropTable(
                name: "Cartellini");

            migrationBuilder.DropTable(
                name: "CaselleEmail");

            migrationBuilder.DropTable(
                name: "Ccnl");

            migrationBuilder.DropTable(
                name: "CcnlLivelli");

            migrationBuilder.DropTable(
                name: "CedolinoVoceEntity");

            migrationBuilder.DropTable(
                name: "ConfigurazioniSdi");

            migrationBuilder.DropTable(
                name: "ConsumiAi");

            migrationBuilder.DropTable(
                name: "Deleghe");

            migrationBuilder.DropTable(
                name: "DichiarazioniRedditiPf");

            migrationBuilder.DropTable(
                name: "Durc");

            migrationBuilder.DropTable(
                name: "EsecuzioniRetention");

            migrationBuilder.DropTable(
                name: "Immobili");

            migrationBuilder.DropTable(
                name: "InpsAliquote");

            migrationBuilder.DropTable(
                name: "IrpefScaglioni");

            migrationBuilder.DropTable(
                name: "MessaggiEmail");

            migrationBuilder.DropTable(
                name: "ParametriAnno");

            migrationBuilder.DropTable(
                name: "Persone");

            migrationBuilder.DropTable(
                name: "PolicyRetention");

            migrationBuilder.DropTable(
                name: "PosizioniInail");

            migrationBuilder.DropTable(
                name: "PraticheF24");

            migrationBuilder.DropTable(
                name: "PraticheInail");

            migrationBuilder.DropTable(
                name: "Promemoria");

            migrationBuilder.DropTable(
                name: "Rapporti");

            migrationBuilder.DropTable(
                name: "RicevuteSdi");

            migrationBuilder.DropTable(
                name: "RigaFatturaEntity");

            migrationBuilder.DropTable(
                name: "RigaLiquidazioneIvaEntity");

            migrationBuilder.DropTable(
                name: "RigheBilancio");

            migrationBuilder.DropTable(
                name: "SessioniRefresh");

            migrationBuilder.DropTable(
                name: "Spese");

            migrationBuilder.DropTable(
                name: "PraticheAperturaPartitaIva");

            migrationBuilder.DropTable(
                name: "Cedolini");

            migrationBuilder.DropTable(
                name: "Dipendenti");

            migrationBuilder.DropTable(
                name: "TrasmissioniFatture");

            migrationBuilder.DropTable(
                name: "LiquidazioniIva");

            migrationBuilder.DropTable(
                name: "Bilanci");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropTable(
                name: "Utenti");

            migrationBuilder.DropTable(
                name: "Fatture");

            migrationBuilder.DropTable(
                name: "Aziende");
        }
    }
}
