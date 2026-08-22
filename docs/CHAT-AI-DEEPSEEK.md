# Aurora: runtime governato, provider ancora spento

> Stato dal 10 agosto 2026: **DeepSeek e gli altri provider cloud sono sospesi**. Il nome di
> questo file resta per compatibilità con i collegamenti storici.

Nell'installazione personale PaghePro può usare il database SQLite locale:

```text
backend/src/Paghe.Api/paghe.db
```

Nel deploy SaaS il database può invece essere PostgreSQL: «deterministico» non significa
necessariamente «eseguito sul computer dell'utente». Con i provider spenti nessuna riga viene
inviata a un modello. Dopo una futura accensione approvata, soltanto le proiezioni minimizzate
lette tramite un mandato valido potranno entrare nel runtime.

La configurazione è fail-closed: `Provider=Spento` non può essere riattivato da URL/modello/chiave;
`Ai:SoloLocale=true` vieta il cloud; `ProviderCloudApprovato=false` blocca il profilo SaaS e
`GateLocaleG1Verificato=false` blocca Ollama. Gli endpoint compatibili richiedono inoltre una
allowlist host esatta. Gli handler AI non usano proxy né redirect. `render.yaml` mantiene Aurora
spenta e disabilita i deploy automatici.

## Cosa vede l'utente

L'icona **Aurora** è sempre disponibile nelle schermate autenticate, anche come pulsante mobile
flottante. Per chiedere una spiegazione:

1. seleziona una frase, un errore o una voce nella parte centrale di PaghePro;
2. premi l'icona Aurora;
3. controlla l'anteprima e invia la domanda oppure usa uno dei pulsanti dedicati al testo.

Il testo è limitato a 4.000 caratteri, resta solo nella memoria della schermata, è rimovibile e
viene inviato una volta sola. Input, menu, pannello Aurora e aree private non vengono acquisiti;
anche una selezione che attraversa un'area privata viene rifiutata.

### Controllo cedolini in lotti da 10

Nel pannello è disponibile il primo percorso governato di Aurora. Dopo aver scelto azienda e
mese, l'utente vede prima finalità, durata e budget effettivi decisi dal server e conferma
l'avvio. Il Governor emette un mandato S2 di 30 minuti; la policy corrente assegna un budget
complessivo di 250 record, mentre ogni singola lettura cedolini resta limitata a un lotto di 10.
Il broker restituisce solo nominativo e stato operativo del cedolino. Non escono importi, codice
fiscale, IBAN o dati di malattia/infortunio.

Il pannello mostra quota, scadenza, «Dati letti dal controllo» e «Revoca mandato». Se i rapporti sono più di 10,
questo incremento mostra soltanto i primi dieci: il checkpoint successivo richiederà un cursore
server-side verificabile. Dieci è un checkpoint, non un limite globale del prodotto. Il controllo
non calcola e non salva cedolini; porta soltanto a
uno stato leggibile (`cartellino presente`, `da compilare`, `già elaborato`, `bloccato`,
`protetto`). «Cartellino presente» è volutamente preliminare: non promette che il motore paghe
supererà tutti i controlli normativi e di periodo.

## Protezioni attive

- Il testo selezionato viaggia in un campo separato ed è marcato come contenuto non fidato.
- Le selezioni discontinue con più intervalli sono rifiutate; basta un'area privata attraversata
  perché l'intera selezione venga scartata.
- Un filtro server deterministico blocca password, chiavi private e formati noti di token prima
  che possano raggiungere un modello.
- Senza lease la chat non chiama il provider e non riceve strumenti. Con una lease valida il
  registry offre soltanto i tool di lettura compatibili con ruolo, risorsa, operazione e scope;
  ogni esecuzione torna al broker e la quota viene riletta dal server dopo il turno.
- Gli endpoint di scrittura/conferma AI rispondono `409` al gate A2; i dati si salvano soltanto
  dai normali moduli PaghePro.
- La cronologia proveniente dal browser conserva solo le domande `user`, con limiti; risposte
  `assistant`, messaggi `system`, `tool` e chiamate forgiate sono scartati. Dopo una domanda con
  testo selezionato non viene restituita storia, così neppure un'eventuale citazione del modello
  può essere ritrasmessa al turno seguente.
- Se la richiesta è ambigua o incompleta, Aurora deve fare una domanda concreta prima di
  proseguire.

## Modello locale preparato, non ancora attivato

Per il Mac M3 con 24 GB il candidato qualificabile è `qwen3.5:9b`: 6,6 GB, licenza Apache-2.0,
supporto strumenti e molte lingue. La configurazione prevista, solo dopo la chiusura del gate G1
e la rimozione deliberata del blocco nel codice, è:

```text
Ai__Provider=Compatibile
Ai__SoloLocale=true
Ai__GateLocaleG1Verificato=true
Ai__BaseUrl=http://127.0.0.1:11434/v1
Ai__Modello=qwen3.5:9b
Ai__ModelloChat=qwen3.5:9b
Ai__LimiteMensileChiamate=0
```

Il modello resta **spento per ora**: il piano privacy-first vieta di accenderlo finché non sono
provati isolamento del processo locale, assenza di egress, vault/chiave reale e backup/restore.
Inoltre l'installazione Homebrew di Ollama 0.15.6 presente sul Mac va in crash durante l'avvio e
deve essere sostituita con l'app macOS ufficiale prima del collaudo.

## Passi cloud ancora manuali

Il repository non può sospendere un'istanza Render già creata né revocare una chiave sul portale
DeepSeek. Dopo aver verificato l'eventuale esportazione dei dati online:

1. revocare la vecchia chiave DeepSeek dal portale del provider;
2. nel dashboard Render sospendere il servizio `paghepro`;
3. solo dopo backup e confronto dei conteggi, sospendere Supabase.

Non cancellare database o servizi cloud prima di aver verificato una copia locale ripristinabile.

## File principali

| Funzione | File |
| --- | --- |
| Client locale OpenAI-compatible | `backend/src/Paghe.Api/AiChatClient.cs` |
| Confini chat, contesto e cronologia | `backend/src/Paghe.Api/AiChatEndpoints.cs` |
| Filtro segreti pre-modello | `backend/src/Paghe.Api/LocalSecretBoundaryFilter.cs` |
| Configurazione solo-loopback | `backend/src/Paghe.Api/EstrattoreAiAnthropic.cs` |
| Pannello Aurora | `frontend/src/Aurora.tsx` |
| Richiesta lotto cedolini | `frontend/src/auroraCedolini.ts` |
| Acquisizione testo selezionato | `frontend/src/auroraContext.ts` |
| Governor e broker | `backend/src/Paghe.Persistence/Ai/`, `backend/src/Paghe.Api/AiGovernorEndpoints.cs` |
| Test | `backend/tests/Paghe.Tests/AiChatTests.cs`, `backend/tests/Paghe.Tests/AiGovernorTests.cs`, `backend/tests/Paghe.Tests/AiQueryBrokerTests.cs`, `frontend/tests/auroraContext.test.ts`, `frontend/tests/auroraCedolini.test.ts` |
