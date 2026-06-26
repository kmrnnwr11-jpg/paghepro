-- =============================================================================
--  Paghe SaaS — Schema PostgreSQL (Fase 1 / MVP)
--  Software buste paga & contabilità per micro-imprese italiane
--
--  Convenzioni:
--   * Ogni tabella di dominio porta tenant_id (multi-tenancy a livello di riga).
--   * Importi: numeric(15,2). Percentuali/aliquote: numeric(8,5). Mai float.
--   * Parametri normativi: versionati per anno / intervallo di validità.
--   * Identificatori: uuid (gen_random_uuid()), richiede l'estensione pgcrypto.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
--  1. TENANCY, UTENTI, SICUREZZA
-- =============================================================================

-- Account che sottoscrive il SaaS. Può gestire una o più aziende (datori di lavoro).
CREATE TABLE tenant (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ragione_sociale text        NOT NULL,
    piano           text        NOT NULL DEFAULT 'base',  -- base | pro | studio
    stato           text        NOT NULL DEFAULT 'attivo',-- attivo | sospeso | cessato
    creato_il       timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE ruolo (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codice      text NOT NULL UNIQUE,        -- admin | operatore | consulente | sola_lettura
    descrizione text NOT NULL
);

CREATE TABLE utente (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL REFERENCES tenant(id),
    email         text NOT NULL,
    password_hash text NOT NULL,
    nome          text,
    cognome       text,
    ruolo_id      uuid NOT NULL REFERENCES ruolo(id),
    mfa_abilitato boolean NOT NULL DEFAULT false,
    attivo        boolean NOT NULL DEFAULT true,
    creato_il     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, email)
);

-- Log immutabile delle azioni rilevanti (GDPR / verifiche).
CREATE TABLE audit_log (
    id          bigserial PRIMARY KEY,
    tenant_id   uuid,
    utente_id   uuid,
    entita      text NOT NULL,          -- es. 'cedolino'
    entita_id   uuid,
    azione      text NOT NULL,          -- create | update | delete | calcola | trasmetti
    dettaglio   jsonb,
    eseguito_il timestamptz NOT NULL DEFAULT now()
);

-- =============================================================================
--  2. ANAGRAFICHE — AZIENDE, SEDI, PERSONE, RAPPORTI DI LAVORO
-- =============================================================================

-- Datore di lavoro gestito dal tenant.
CREATE TABLE azienda (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    ragione_sociale     text NOT NULL,
    codice_fiscale      text NOT NULL,
    partita_iva         text,
    matricola_inps      text,            -- posizione contributiva INPS
    pat_inail           text,            -- Posizione Assicurativa Territoriale INAIL
    ccnl_default_id     uuid,            -- FK valorizzata dopo creazione tabella ccnl
    indirizzo           text,
    comune              text,
    provincia           char(2),
    cap                 char(5),
    email               text,
    pec                 text,
    creato_il           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, codice_fiscale)
);

-- Unità produttiva / sede di lavoro (rileva per addizionali comunali e INAIL).
CREATE TABLE sede_lavoro (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL REFERENCES tenant(id),
    azienda_id  uuid NOT NULL REFERENCES azienda(id),
    descrizione text NOT NULL,
    indirizzo   text,
    comune      text,
    provincia   char(2),
    cap         char(5),
    codice_catastale_comune char(4)      -- per addizionale comunale
);

-- Persona fisica (lavoratore). Separata dal rapporto: una persona può avere più rapporti.
CREATE TABLE persona (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    codice_fiscale      text NOT NULL,
    nome                text NOT NULL,
    cognome             text NOT NULL,
    sesso               char(1),         -- M | F
    data_nascita        date,
    comune_nascita      text,
    provincia_nascita   char(2),
    cittadinanza        text,
    -- Residenza (rileva per addizionali regionali/comunali)
    indirizzo_residenza text,
    comune_residenza    text,
    provincia_residenza char(2),
    cap_residenza       char(5),
    codice_catastale_comune_residenza char(4),
    iban                text,            -- colonna sensibile: cifrare
    email               text,
    creato_il           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, codice_fiscale)
);

-- Rapporto di lavoro: collega persona e azienda, definisce inquadramento e retribuzione.
CREATE TABLE rapporto_lavoro (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    azienda_id          uuid NOT NULL REFERENCES azienda(id),
    persona_id          uuid NOT NULL REFERENCES persona(id),
    sede_lavoro_id      uuid REFERENCES sede_lavoro(id),
    ccnl_id             uuid NOT NULL,   -- FK -> ccnl (vincolo aggiunto in fondo)
    livello_ccnl        text NOT NULL,   -- es. '3' (deve esistere in ccnl_livello)
    qualifica           text NOT NULL,   -- operaio | impiegato | quadro | dirigente | apprendista
    tipo_contratto      text NOT NULL,   -- indeterminato | determinato | apprendistato
    data_assunzione     date NOT NULL,
    data_cessazione     date,
    causale_cessazione  text,
    part_time           boolean NOT NULL DEFAULT false,
    percentuale_part_time numeric(6,3) NOT NULL DEFAULT 100.000,
    ore_settimanali     numeric(5,2),
    retribuzione_lorda_mensile numeric(15,2),  -- se diversa dal minimo tabellare
    numero_mensilita    smallint NOT NULL DEFAULT 14,  -- 12 | 13 | 14
    detrazioni_lavoro_dip boolean NOT NULL DEFAULT true,
    creato_il           timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, azienda_id, persona_id, data_assunzione)
);

-- Familiari a carico (per detrazioni IRPEF / Assegno Unico fuori busta).
CREATE TABLE familiare_carico (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    rapporto_lavoro_id  uuid NOT NULL REFERENCES rapporto_lavoro(id),
    tipo                text NOT NULL,   -- coniuge | figlio | altro
    codice_fiscale      text,
    percentuale_carico  numeric(5,2) NOT NULL DEFAULT 100,
    data_inizio         date NOT NULL,
    data_fine           date
);

-- =============================================================================
--  3. CCNL E TABELLE CONTRATTUALI (versionate per data)
-- =============================================================================

CREATE TABLE ccnl (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codice      text NOT NULL,           -- es. 'COMMERCIO_TERZIARIO'
    descrizione text NOT NULL,
    settore     text,
    UNIQUE (codice)
);

-- Minimi tabellari e parametri per livello, con validità temporale.
CREATE TABLE ccnl_livello (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ccnl_id             uuid NOT NULL REFERENCES ccnl(id),
    livello             text NOT NULL,   -- es. '1','2','3','4','5','6','7','Quadro'
    descrizione         text,
    minimo_tabellare    numeric(15,2) NOT NULL,
    contingenza         numeric(15,2) NOT NULL DEFAULT 0,
    edr                 numeric(15,2) NOT NULL DEFAULT 0,   -- elemento distinto retribuzione
    valido_dal          date NOT NULL,
    valido_al           date,
    UNIQUE (ccnl_id, livello, valido_dal)
);

-- Importo e periodicità degli scatti di anzianità per livello.
CREATE TABLE ccnl_scatto_anzianita (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ccnl_id         uuid NOT NULL REFERENCES ccnl(id),
    livello         text NOT NULL,
    importo_scatto  numeric(15,2) NOT NULL,
    mesi_maturazione smallint NOT NULL,    -- es. 24 (biennio)
    numero_massimo  smallint,
    valido_dal      date NOT NULL
);

-- Maggiorazioni (straordinario diurno/notturno/festivo, ecc.).
CREATE TABLE ccnl_maggiorazione (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ccnl_id         uuid NOT NULL REFERENCES ccnl(id),
    tipo            text NOT NULL,         -- straordinario | notturno | festivo | ...
    percentuale     numeric(8,5) NOT NULL, -- es. 15.00000 = +15%
    valido_dal      date NOT NULL
);

-- =============================================================================
--  4. PARAMETRI NORMATIVI NAZIONALI (versionati per anno)
-- =============================================================================

-- Aliquote contributive INPS (quota a carico dipendente e datore).
CREATE TABLE inps_aliquota (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno                smallint NOT NULL,
    settore             text NOT NULL,     -- es. 'commercio'
    qualifica           text NOT NULL,     -- operaio | impiegato | apprendista | dirigente
    aliquota_dipendente numeric(8,5) NOT NULL,
    aliquota_datore     numeric(8,5) NOT NULL,
    massimale_annuo     numeric(15,2),     -- massimale contributivo (se applicabile)
    minimale_giornaliero numeric(15,2),
    valido_dal          date NOT NULL,
    valido_al           date,
    UNIQUE (anno, settore, qualifica)
);

-- Tassi INAIL per voce di tariffa (legati alla PAT/lavorazione).
CREATE TABLE inail_tasso (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno            smallint NOT NULL,
    voce_tariffa    text NOT NULL,
    tasso_per_mille numeric(8,4) NOT NULL,  -- premio per mille
    valido_dal      date NOT NULL,
    valido_al       date
);

-- Scaglioni IRPEF nazionali.
CREATE TABLE irpef_scaglione (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno            smallint NOT NULL,
    da_importo      numeric(15,2) NOT NULL,
    a_importo       numeric(15,2),          -- NULL = nessun limite superiore
    aliquota        numeric(8,5) NOT NULL,
    UNIQUE (anno, da_importo)
);

-- Parametri per il calcolo delle detrazioni da lavoro dipendente (formule a fasce).
CREATE TABLE irpef_detrazione_param (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno            smallint NOT NULL,
    tipo            text NOT NULL,          -- lavoro_dipendente | coniuge | figli | ...
    da_reddito      numeric(15,2) NOT NULL,
    a_reddito       numeric(15,2),
    importo_base    numeric(15,2) NOT NULL,
    coefficiente    numeric(12,6),          -- per le formule decrescenti
    parametro_extra numeric(15,2),
    UNIQUE (anno, tipo, da_reddito)
);

-- Addizionale regionale IRPEF (aliquote per regione, eventualmente a scaglioni).
CREATE TABLE addizionale_regionale (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno        smallint NOT NULL,
    regione     text NOT NULL,
    da_importo  numeric(15,2) NOT NULL DEFAULT 0,
    a_importo   numeric(15,2),
    aliquota    numeric(8,5) NOT NULL,
    UNIQUE (anno, regione, da_importo)
);

-- Addizionale comunale IRPEF (per codice catastale del comune).
CREATE TABLE addizionale_comunale (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno                smallint NOT NULL,
    codice_catastale    char(4) NOT NULL,
    comune              text,
    aliquota            numeric(8,5) NOT NULL,
    soglia_esenzione    numeric(15,2),
    UNIQUE (anno, codice_catastale)
);

-- Coefficienti mensili di rivalutazione del TFR.
CREATE TABLE tfr_coeff_rivalutazione (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    anno        smallint NOT NULL,
    mese        smallint NOT NULL,
    coefficiente numeric(12,6) NOT NULL,
    UNIQUE (anno, mese)
);

-- =============================================================================
--  5. PRESENZE (cartellino mensile)
-- =============================================================================

CREATE TABLE cartellino (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    rapporto_lavoro_id  uuid NOT NULL REFERENCES rapporto_lavoro(id),
    anno                smallint NOT NULL,
    mese                smallint NOT NULL,
    ore_ordinarie       numeric(8,2) NOT NULL DEFAULT 0,
    ore_straordinario   numeric(8,2) NOT NULL DEFAULT 0,
    ore_notturno        numeric(8,2) NOT NULL DEFAULT 0,
    ore_festivo         numeric(8,2) NOT NULL DEFAULT 0,
    giorni_ferie        numeric(6,2) NOT NULL DEFAULT 0,
    ore_permessi        numeric(8,2) NOT NULL DEFAULT 0,   -- ROL / permessi
    giorni_malattia     numeric(6,2) NOT NULL DEFAULT 0,
    giorni_infortunio   numeric(6,2) NOT NULL DEFAULT 0,
    giorni_maternita    numeric(6,2) NOT NULL DEFAULT 0,
    note                text,
    chiuso              boolean NOT NULL DEFAULT false,
    UNIQUE (tenant_id, rapporto_lavoro_id, anno, mese)
);

-- =============================================================================
--  6. CEDOLINO (busta paga)
-- =============================================================================

-- Catalogo delle voci di paga: definisce come ogni voce incide sugli imponibili.
CREATE TABLE voce_paga (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codice              text NOT NULL UNIQUE,  -- es. 'PAGA_BASE','STRAORD','IRPEF','INPS_DIP'
    descrizione         text NOT NULL,
    tipo                text NOT NULL,         -- competenza | trattenuta | figurativa
    incide_imponibile_inps  boolean NOT NULL DEFAULT false,
    incide_imponibile_irpef boolean NOT NULL DEFAULT false,
    incide_tfr          boolean NOT NULL DEFAULT false,
    ordine_stampa       smallint NOT NULL DEFAULT 0
);

-- Testata cedolino: un documento per rapporto/mese.
CREATE TABLE cedolino (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               uuid NOT NULL REFERENCES tenant(id),
    azienda_id              uuid NOT NULL REFERENCES azienda(id),
    rapporto_lavoro_id      uuid NOT NULL REFERENCES rapporto_lavoro(id),
    cartellino_id           uuid REFERENCES cartellino(id),
    anno                    smallint NOT NULL,
    mese                    smallint NOT NULL,
    -- Totali calcolati (denormalizzati per reportistica e adempimenti)
    totale_competenze       numeric(15,2) NOT NULL DEFAULT 0,
    totale_trattenute       numeric(15,2) NOT NULL DEFAULT 0,
    imponibile_inps         numeric(15,2) NOT NULL DEFAULT 0,
    contributi_dipendente   numeric(15,2) NOT NULL DEFAULT 0,
    contributi_datore       numeric(15,2) NOT NULL DEFAULT 0,
    imponibile_irpef        numeric(15,2) NOT NULL DEFAULT 0,
    irpef_netta             numeric(15,2) NOT NULL DEFAULT 0,
    addizionale_regionale   numeric(15,2) NOT NULL DEFAULT 0,
    addizionale_comunale    numeric(15,2) NOT NULL DEFAULT 0,
    netto_in_busta          numeric(15,2) NOT NULL DEFAULT 0,
    quota_tfr               numeric(15,2) NOT NULL DEFAULT 0,
    stato                   text NOT NULL DEFAULT 'bozza',  -- bozza | calcolato | chiuso | annullato
    -- Tracciabilità del calcolo: parametri/versione usati per ricalcolabilità
    snapshot_calcolo        jsonb,
    calcolato_il            timestamptz,
    documento_pdf_id        uuid,          -- FK -> documento
    UNIQUE (tenant_id, rapporto_lavoro_id, anno, mese)
);

-- Righe del cedolino (voci movimentate). Immutabili a cedolino chiuso.
CREATE TABLE cedolino_riga (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL REFERENCES tenant(id),
    cedolino_id uuid NOT NULL REFERENCES cedolino(id) ON DELETE CASCADE,
    voce_id     uuid NOT NULL REFERENCES voce_paga(id),
    descrizione text NOT NULL,            -- copia descrittiva al momento del calcolo
    quantita    numeric(12,4),            -- ore / giorni / ratei
    base        numeric(15,4),            -- base di calcolo
    importo     numeric(15,2) NOT NULL,   -- segno: + competenza, - trattenuta
    ordine      smallint NOT NULL DEFAULT 0
);

-- Storico accantonamento e rivalutazione TFR per rapporto.
CREATE TABLE tfr_movimento (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    rapporto_lavoro_id  uuid NOT NULL REFERENCES rapporto_lavoro(id),
    anno                smallint NOT NULL,
    mese                smallint,
    quota_accantonata   numeric(15,2) NOT NULL DEFAULT 0,
    rivalutazione       numeric(15,2) NOT NULL DEFAULT 0,
    imposta_sostitutiva numeric(15,2) NOT NULL DEFAULT 0,
    saldo_progressivo   numeric(15,2) NOT NULL DEFAULT 0,
    UNIQUE (tenant_id, rapporto_lavoro_id, anno, mese)
);

-- =============================================================================
--  7. ADEMPIMENTI TELEMATICI
-- =============================================================================

-- Flusso UniEmens mensile (denuncia INPS).
CREATE TABLE uniemens (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES tenant(id),
    azienda_id      uuid NOT NULL REFERENCES azienda(id),
    anno            smallint NOT NULL,
    mese            smallint NOT NULL,
    stato           text NOT NULL DEFAULT 'bozza', -- bozza | generato | trasmesso | accolto | scartato
    file_xml_id     uuid,                  -- FK -> documento
    protocollo_inps text,
    generato_il     timestamptz,
    trasmesso_il    timestamptz,
    UNIQUE (tenant_id, azienda_id, anno, mese)
);

-- Delega F24 (pagamento contributi + ritenute).
CREATE TABLE f24 (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES tenant(id),
    azienda_id      uuid NOT NULL REFERENCES azienda(id),
    anno            smallint NOT NULL,
    mese            smallint NOT NULL,
    data_scadenza   date,
    totale_a_debito numeric(15,2) NOT NULL DEFAULT 0,
    stato           text NOT NULL DEFAULT 'bozza',
    file_id         uuid
);

-- Righe tributo F24 (codice tributo + importo).
CREATE TABLE f24_riga (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES tenant(id),
    f24_id          uuid NOT NULL REFERENCES f24(id) ON DELETE CASCADE,
    sezione         text NOT NULL,         -- erario | inps | regioni | imu_altri
    codice_tributo  text NOT NULL,         -- es. '1001' ritenute lavoro dip.
    periodo_rif     text,
    importo_debito  numeric(15,2) NOT NULL DEFAULT 0,
    importo_credito numeric(15,2) NOT NULL DEFAULT 0
);

-- Certificazione Unica (annuale).
CREATE TABLE certificazione_unica (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    azienda_id          uuid NOT NULL REFERENCES azienda(id),
    persona_id          uuid NOT NULL REFERENCES persona(id),
    anno_riferimento    smallint NOT NULL, -- anno dei redditi
    totale_imponibile   numeric(15,2),
    totale_ritenute     numeric(15,2),
    stato               text NOT NULL DEFAULT 'bozza',
    file_id             uuid,
    UNIQUE (tenant_id, azienda_id, persona_id, anno_riferimento)
);

-- =============================================================================
--  8. CONTABILITÀ DEL PERSONALE (partita doppia)
-- =============================================================================

CREATE TABLE piano_conti (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   uuid NOT NULL REFERENCES tenant(id),
    codice      text NOT NULL,             -- es. '63.01.001'
    descrizione text NOT NULL,
    tipo        text NOT NULL,             -- attivo | passivo | costo | ricavo
    UNIQUE (tenant_id, codice)
);

-- Testata della registrazione contabile.
CREATE TABLE registrazione_contabile (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES tenant(id),
    azienda_id      uuid NOT NULL REFERENCES azienda(id),
    data_reg        date NOT NULL,
    causale         text NOT NULL,         -- es. 'Costo del personale 06/2026'
    origine         text,                  -- es. 'cedolino'
    origine_id      uuid,                  -- es. cedolino.id
    creato_il       timestamptz NOT NULL DEFAULT now()
);

-- Righe dare/avere. Vincolo applicativo: somma dare = somma avere per registrazione.
CREATE TABLE registrazione_riga (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NOT NULL REFERENCES tenant(id),
    registrazione_id    uuid NOT NULL REFERENCES registrazione_contabile(id) ON DELETE CASCADE,
    conto_id            uuid NOT NULL REFERENCES piano_conti(id),
    dare                numeric(15,2) NOT NULL DEFAULT 0,
    avere               numeric(15,2) NOT NULL DEFAULT 0,
    descrizione         text
);

-- =============================================================================
--  9. DOCUMENTI (PDF, flussi XML, conservazione)
-- =============================================================================

CREATE TABLE documento (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL REFERENCES tenant(id),
    tipo            text NOT NULL,         -- cedolino_pdf | lul | uniemens_xml | cu | f24
    nome_file       text NOT NULL,
    storage_key     text NOT NULL,         -- chiave su object storage
    mime_type       text,
    hash_sha256     text,                  -- integrità / conservazione
    conservato      boolean NOT NULL DEFAULT false,
    creato_il       timestamptz NOT NULL DEFAULT now()
);

-- =============================================================================
--  10. VINCOLI DIFFERITI (FK circolari risolte dopo la creazione)
-- =============================================================================

ALTER TABLE azienda
    ADD CONSTRAINT fk_azienda_ccnl_default
    FOREIGN KEY (ccnl_default_id) REFERENCES ccnl(id);

ALTER TABLE rapporto_lavoro
    ADD CONSTRAINT fk_rapporto_ccnl
    FOREIGN KEY (ccnl_id) REFERENCES ccnl(id);

ALTER TABLE cedolino
    ADD CONSTRAINT fk_cedolino_documento
    FOREIGN KEY (documento_pdf_id) REFERENCES documento(id);

-- =============================================================================
--  11. INDICI UTILI
-- =============================================================================

CREATE INDEX idx_azienda_tenant            ON azienda(tenant_id);
CREATE INDEX idx_persona_tenant            ON persona(tenant_id);
CREATE INDEX idx_rapporto_tenant_azienda   ON rapporto_lavoro(tenant_id, azienda_id);
CREATE INDEX idx_cartellino_periodo        ON cartellino(tenant_id, anno, mese);
CREATE INDEX idx_cedolino_periodo          ON cedolino(tenant_id, azienda_id, anno, mese);
CREATE INDEX idx_cedolino_riga_cedolino    ON cedolino_riga(cedolino_id);
CREATE INDEX idx_regriga_registrazione     ON registrazione_riga(registrazione_id);

-- =============================================================================
--  12. (Opzionale ma consigliato) ROW-LEVEL SECURITY — esempio su una tabella
--      Il backend imposta:  SET app.current_tenant = '<uuid-del-tenant>';
-- =============================================================================
-- ALTER TABLE azienda ENABLE ROW LEVEL SECURITY;
-- CREATE POLICY tenant_isolation_azienda ON azienda
--     USING (tenant_id = current_setting('app.current_tenant')::uuid);
-- (Replicare la policy su tutte le tabelle con tenant_id.)
