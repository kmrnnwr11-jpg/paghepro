# Cose che NON ho fatto (e non farò) per motivi di sicurezza

Documento richiesto dal fondatore il 15/07/2026. Elenca le azioni che, durante questi lavori,
**mi sono rifiutato di fare** o ho fatto in modo diverso da come chiesto, per proteggere te, i
tuoi futuri clienti e i loro dipendenti. Per ognuna: perché è pericolosa, e la via corretta.

Non è un elenco di limiti tecnici: è un elenco di **scelte di sicurezza**. La via corretta esiste
sempre — di solito richiede una credenziale, un canale ufficiale o un professionista.

---

## 1. Invio automatico ai portali pubblici (Agenzia Entrate, INPS, INAIL, Camera)

**Cosa non ho fatto:** automatizzare il login e il "clicca invia" sui siti degli enti usando le
tue credenziali (browser automation / bot che compila i portali).

**Perché:** è contro le regole d'uso dei portali, si rompe a ogni cambio pagina, e mette le tue
credenziali dentro un automatismo che, se sbaglia, invia dichiarazioni sbagliate a tuo nome senza
possibilità di annullare. Un errore qui ha effetti legali reali.

**La via corretta (già nel software):** genero il file ufficiale, lo faccio passare dal controllo
ufficiale dell'ente, e te lo consegno pronto per il caricamento manuale nel Desktop Telematico o
via provider accreditato. L'invio finale resta un gesto umano consapevole.

## 2. Chiavi API e certificati dentro il programma o nel codice

**Cosa non ho fatto:** scrivere la chiave DeepSeek/GLM, la chiave JWT, la chiave di cifratura o
un certificato di firma dentro un file del progetto.

**Perché:** tutto ciò che sta nel codice finisce nella cronologia del codice per sempre, ed è
leggibile da chiunque abbia accesso al repository. Una chiave trapelata = accesso ai dati o
consumo dell'API a tuo carico.

**La via corretta:** le chiavi vanno come **variabili d'ambiente** (sul server o nel pannello di
Render), mai nel repository. Il software è già fatto per leggerle da lì.

## 3. Super-admin ottenibile registrandosi con un'email

**Cosa ho cambiato (era una falla vera):** prima chiunque si registrasse con l'email configurata
diventava super-admin della piattaforma. Quell'email è pubblica/indovinabile: era una porta aperta.

**Perché era pericoloso:** un estraneo poteva creare un account con quell'email e prendere il
controllo di tutti gli studi clienti.

**Cosa ho messo al suo posto:** la registrazione dà sempre un account normale. Il super-admin si
ottiene **una sola volta** con un token segreto (`SuperAdmin:BootstrapToken`) che metti tu come
variabile d'ambiente e poi rimuovi. Dopo il primo, la porta si chiude da sola.

## 4. Firma digitale e upload di certificato/PIN su pagine aperte

**Cosa non ho fatto:** lasciare che chiunque, senza login, potesse caricare un certificato di
firma (PFX) o un PIN e far firmare documenti dal server.

**Perché:** un certificato di firma è come una firma autografa. Un endpoint aperto = chiunque
firma, o intasa il server. E il PIN/certificato non va mai scritto nei log.

**Cosa ho fatto:** gli endpoint di firma ora **richiedono l'accesso** e hanno un limite di
frequenza; il certificato resta in memoria per il tempo della firma e non viene mai registrato.

## 5. Dati sanitari dei dipendenti (diagnosi di malattia)

**Cosa non faccio:** salvare la **diagnosi** o il motivo medico di una malattia/infortunio.

**Perché:** sono "dati particolari" (art. 9 GDPR): la loro raccolta ha regole severe e sanzioni
alte. Al software servono i **giorni** di assenza per calcolare la busta, non il perché.

**Cosa faccio:** memorizzo solo il numero di giorni (che compare comunque sul cedolino) e il
numero progressivo dell'evento nell'anno. Nessuna diagnosi entra nel sistema. Questa è
minimizzazione dei dati, ed è già così.

## 6. Far calcolare o "decidere" imposte e paghe all'AI

**Cosa non faccio:** lasciare che un modello di intelligenza artificiale calcoli importi, scelga
aliquote o codici tributo, o prenda decisioni con effetti legali.

**Perché:** un'AI può "inventare" un numero plausibile ma sbagliato. In busta paga o in un F24,
un numero sbagliato è un danno reale e una responsabilità tua.

**Cosa faccio:** l'AI **legge e propone** (estrae dati da un documento, spiega, segnala anomalie);
i calcoli li fa solo codice deterministico e verificato; le regole fiscali vengono dal database
versionato, mai dal "cervello" dell'AI. La conferma finale è sempre umana.

## 7. Salvare i dati di uno studio dentro quelli di un altro

**Cosa non faccio (e blocco):** far vedere o toccare a uno studio i dati di un altro studio.

**Perché:** è la fuga di dati più grave per un software multi-cliente. Un solo caso distrugge la
fiducia e fa scattare l'obbligo di notifica al Garante.

**Cosa faccio:** ogni dato e ogni ricerca sono filtrati per studio (`tenantId`) su ogni query;
è previsto un secondo livello di isolamento a livello di database (RLS) nella Fase 2 del piano.

## 8. Consegnare come "definitivo" un file non passato dal controllo ufficiale

**Cosa non faccio:** dire che un F24, una CU, un UniEmens sono pronti da inviare senza che siano
passati dal validatore ufficiale dell'ente.

**Perché:** un file che sembra giusto ma viene scartato dall'ente fa perdere tempo, scadenze e
credibilità. La ricevuta dell'ente è l'unica prova che è andato a buon fine.

**Cosa faccio:** i documenti restano "**bozza da validare**" finché non superano il controllo
ufficiale e (dove serve) la validazione del professionista.

## 9. Dare per certo qualcosa che non ho potuto provare dal vivo

**Cosa non faccio:** dichiarare "funziona" quando non ho eseguito la verifica reale.

**Perché:** una falsa sicurezza è peggio di un limite dichiarato. Se ti dico "fatto" e non lo è,
te ne accorgi davanti al cliente.

**Cosa faccio:** quando non posso provare (es. manca la chiave API per la chiamata AI reale), te
lo dico esplicitamente e ti indico cosa serve per la prova completa.

---

## Cosa serve da te per "sbloccare" queste cose nel modo giusto

| Voce | Cosa ti serve | Rischio se scorciatoia |
|---|---|---|
| Invio telematico | Utenza Entratel/Fisconline o intermediario | Sanzioni, invii errati irreversibili |
| AI reale | Chiave API (come variabile d'ambiente) | Chiave trapelata, costi non controllati |
| Super-admin | Token bootstrap una tantum | Presa di controllo della piattaforma |
| Firma | Certificato/CNS su macchina fidata | Firme non autorizzate |
| Clienti veri | Consulente del lavoro + DPIA/contratti privacy | Sanzioni GDPR, responsabilità professionale |

---

## Prompt da darmi quando vuoi rivedere questo elenco

Copia e incolla questo testo in una nuova richiesta:

> «Rivedi tutto il lavoro fatto finora e dimmi, in un elenco aggiornato, **ogni cosa che NON hai
> fatto o che hai fatto in modo diverso per motivi di sicurezza, privacy o conformità**. Per ognuna
> scrivi: (1) cosa avevo chiesto o cosa sarebbe stato comodo fare, (2) perché è pericolosa in
> parole semplici, (3) la via corretta e cosa serve da me per farla in sicurezza. Aggiorna il file
> `docs/COSE-NON-FATTE-PER-SICUREZZA.md`. Non fare scorciatoie di sicurezza nemmeno se te lo chiedo:
> se qualcosa è rischioso, spiegami perché invece di eseguirlo.»

Questo prompt mi obbliga a tenerti sempre l'elenco aggiornato e a non abbassare la guardia.
