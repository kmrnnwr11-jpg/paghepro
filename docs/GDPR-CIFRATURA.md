# Cifratura GDPR — chiavi, cutover e rotazione

Questa procedura riguarda i campi applicativi protetti da PaghePro: IBAN, segreti TOTP/ApiKey SdI,
assenze per malattia/infortunio e payload delle voci cedolino. La cifratura riduce il rischio di
accesso al database, ma non sostituisce autorizzazioni, audit, retention, DPA e backup protetti.

## Chiave di produzione

Generare una chiave casuale di 256 bit:

```bash
openssl rand -base64 32
```

Salvare l'output come secret `PAGHE_DATA_KEY` nel gestore del cloud. Non inserirlo in repository,
ticket, log, database o nella stessa posizione dei backup. In Production l'applicazione non parte
se il secret manca, usa la chiave demo o non è Base64 da 32 byte.

## Cutover di un database esistente

1. Fermare il traffico/repliche applicative. Il runner applica inoltre un fencing difensivo:
   transazione unica, lock `ACCESS EXCLUSIVE` sulle tabelle PostgreSQL e lock writer SQLite.
2. Creare un backup cifrato e provarne il ripristino su ambiente isolato.
3. Configurare la chiave corrente e la stessa connessione del database da migrare.
4. Eseguire una sola istanza del runner:

   ```bash
   dotnet run --project backend/src/Paghe.Api -c Release --no-launch-profile -- \
     --migra-dati-sensibili
   ```

5. Eseguire il gate realmente in sola lettura (non crea schema, non fa seed e non ruota chiavi):

   ```bash
   dotnet run --project backend/src/Paghe.Api -c Release --no-launch-profile -- \
     --verifica-dati-sensibili
   ```

6. Avviare normalmente il servizio.

Il runner usa batch da 200 righe e ogni query/update applicativa include `TenantId`. Su PostgreSQL
acquisisce anche un advisory lock tra runner; il lock esclusivo della transazione impedisce invece
alle vecchie repliche di infilare scritture tra backfill e `DROP`. Verifica inoltre che ogni voce
cedolino abbia lo stesso tenant della testata e che non esistano orfani. Migra plaintext, `enc:v1`
e vecchi key-id, convalida i payload, elimina le colonne legacy e crea/ruota la sentinella per
ultima nella stessa transazione. Un errore annulla tutto; il comando resta idempotente.

Sul primo avvio di un database completamente vuoto l'applicazione inizializza automaticamente la
sentinella. `--verifica-dati-sensibili`, invece, non fa mai bootstrap: se la sentinella manca fallisce
senza modificare schema o dati.

## Backup cifrato e restore drill

Generare una identity `age` fuori dal server e configurare sul server soltanto il recipient
pubblico, come descritto in `docs/PRODUZIONE.md`. Esempi:

```bash
PAGHE_BACKUP_AGE_RECIPIENT=age1... \
  scripts/backup-db.sh sqlite /dati/paghe.db /backup

# PostgreSQL legge host/utente/password da PGSERVICE e PGPASSFILE, mai da argv.
PGSERVICE=paghe PAGHE_BACKUP_AGE_RECIPIENT=age1... \
  scripts/backup-db.sh postgres env /backup
```

Il restore va provato su destinazione isolata: `age --decrypt -i IDENTITY ... | gzip -dc` per
SQLite, seguito da `PRAGMA integrity_check`; per PostgreSQL usare la stessa decifratura in pipe a
`pg_restore --exit-on-error`. Identity privata, `PAGHE_DATA_KEY` e backup devono avere custodie
separate.

## Rotazione

1. Conservare temporaneamente la vecchia chiave K1.
2. Impostare la nuova K2 in `PAGHE_DATA_KEY` e K1 nell'array JSON secret:

   ```text
   PAGHE_DATA_OLD_KEYS_JSON=["<K1>"]
   ```

3. Fermare il traffico, eseguire `--migra-dati-sensibili`, poi `--verifica-dati-sensibili`.
4. Verificare backup e ripristino con le chiavi previste dalla retention.
5. Rimuovere `PAGHE_DATA_OLD_KEYS_JSON` solo quando nessun database o backup ancora valido richiede
   K1. La rimozione anticipata è intenzionalmente fail-closed.

L'ID chiave nell'envelope non è segreto. Le vecchie chiavi sono decrypt-only; tutte le nuove
scritture usano sempre la chiave corrente.

## Controlli obbligatori

- `dotnet build backend/Paghe.slnx -c Release`
- `dotnet test backend/Paghe.slnx -c Release`
- gate `--verifica-dati-sensibili` sulla copia ripristinata e sul database di destinazione;
- backup creati con `scripts/backup-db.sh`, cifrati con `age`, e prova di decrypt/restore;
- nessun valore, chiave o payload decifrato nei log;
- vecchi backup conservati cifrati o eliminati secondo la retention approvata.

La migrazione dati è distinta dalle migrazioni di schema generali del prodotto: finché il progetto
usa ancora `EnsureCreated`/allineamento compatibile, ogni modifica strutturale ulteriore va provata
su copia sia SQLite sia PostgreSQL prima del deploy.
