# Far validare i calcoli da un Consulente del Lavoro

> A cosa serve questo documento: PaghePro calcola buste paga, ma **nessun professionista ne ha
> ancora certificato i risultati**. Finché non accade, ogni cedolino esce con la dicitura
> «BOZZA DA VALIDARE» e il software non si può vendere. Questa è la cosa che vale di più e
> che costa meno: serve una persona, non altro codice.

## 1. Chi cercare

Va bene **un solo** professionista, purché abbia queste caratteristiche:

- è un **Consulente del Lavoro iscritto all'albo** (non un generico commercialista: le paghe
  sono la sua materia riservata);
- gestisce già buste paga vere, di aziende piccole, con i CCNL che ci interessano;
- ha voglia di guardare un software nuovo — di solito perché è scontento del suo, o perché
  intravede un vantaggio a farsi ascoltare mentre lo si costruisce.

Dove si trova: l'Ordine provinciale dei Consulenti del Lavoro pubblica l'elenco degli iscritti;
oppure passaparola fra imprenditori; oppure il consulente che già segue una ditta conosciuta.

**Cosa offrirgli.** Non chiedere un favore: offri uno scambio. Le formule che funzionano sono
l'uso gratuito a vita per il suo studio, una quota da primo cliente, o un compenso per le ore
di revisione. Vale la pena pagarlo: costa molto meno di un errore su una busta vera.

## 2. Che cosa chiedergli esattamente

Non «guarda se va bene». Una richiesta precisa, che si può soddisfare in poche ore:

> «Ti do tre buste paga calcolate dal mio software, con i dati di partenza. Tu le rifai come le
> faresti tu e mi dici, riga per riga, dove i numeri non coincidono e perché.»

Il confronto riga per riga è il metodo: il totale può tornare per caso, una riga no.

Materiale da consegnargli, già disponibile nel progetto:

- il PDF della busta calcolata (pagina **Cedolini** → scarica);
- i dati di partenza (azienda, CCNL, livello, ore, presenze del mese);
- l'elenco qui sotto dei punti dubbi, per non fargli cercare da solo dove guardare.

## 3. I punti da far verificare, in ordine di rischio

Sono le semplificazioni e le scelte che il software dichiara apertamente. Il consulente deve
dire per ciascuna: **giusto**, **sbagliato** (e come si fa), oppure **dipende** (da cosa).

### Retribuzione e contratto

1. **Minimi tabellari e scatti**: i valori dei CCNL caricati sono esempi non certificati, tranne
   quelli importati da fonte reale. Vanno confrontati con le tabelle vere del contratto.
2. **Divisore orario e giornaliero** usati per straordinari, permessi e assenze: ogni CCNL ha
   il suo, il software usa quello configurato per contratto e un default generale se manca.
3. **Maturazione di ferie e permessi**: ratei mensili e trattamento dei residui negativi (il
   software mostra i saldi sotto zero invece di azzerarli).
4. **Mensilità aggiuntive** (13ª e 14ª): competenza, ratei e riflesso sul TFR.

### Contributi

5. **Aliquote INPS per settore e qualifica**: sono il punto più delicato, perché cambiano per
   codice contributivo dell'azienda.
6. **Minimale contributivo** e sua applicazione ai part-time.
7. **Giorni retribuiti** esposti all'INPS: il software conta i giorni da **lunedì a sabato** del
   mese, meno le assenze non retribuite. Da confermare per i mensilizzati.
8. **Retribuzione teorica** della denuncia UniEmens: il software usa le sole voci fisse della
   busta (minimo, EDR, scatti, superminimo), senza straordinari e variabili.

### Fisco

9. **IRPEF**: scaglioni e detrazioni sono verificati al centesimo su casi reali, ma il
   **conguaglio di fine anno** e i casi particolari vanno controllati.
10. **Addizionali regionale e comunale**: sono trattenute con **metodo mensile semplificato**;
    il software lo dichiara anche nelle annotazioni della Certificazione Unica.
11. **Detrazioni** per lavoro dipendente e per familiari a carico.

### Eventi e casi particolari

12. **Malattia e infortunio**: il software storna la paga ordinaria prima di aggiungere le
    indennità. Da verificare su un caso reale con carenza ed eventi multipli nell'anno.
13. **Assegno unico / ANF**: oggi lo stato è dichiarato dall'operatore, non calcolato.
14. **TFR**: quota annua, rivalutazione e imposta sostitutiva.
15. **Cessazione del rapporto**: ultimo cedolino, ratei residui e TFR liquidato.

### Se ti interessa l'agricoltura

16. Il modulo agricolo riproduce al centesimo tre buste reali di un operaio a tempo determinato
    (Cuneo, 2025), ma resta un punto aperto dichiarato: la **quota di contributi deducibile ai
    fini fiscali** osservata è 7,80% ed è tenuta come parametro, non come formula. Le domande
    già formulate sono in `docs/buste-reali/busta-05-agricoltura-otd-cuneo-2025.md`.

### Fac-simile e 15 stili

17. Usare lo stesso caso validato per controllare almeno una stampa per ciascuna delle tre
    impaginazioni (Classico, Modulare, Editoriale): tutte devono riportare le medesime voci,
    progressivi, totali e diciture. Partire da **Modulare Blu**, che è il default del prodotto;
    le cinque palette di ogni impaginazione cambiano solo colore.
18. Confermare leggibilità di codici e importi su A4, stampa in scala di grigi, firma/sigla/timbro
    e presenza visibile di «non costituisce LUL» / «bozza / non certificato». La scelta estetica
    non sostituisce la validazione dei calcoli né l'eventuale processo autorizzato del LUL.

## 4. Come registrare le risposte

Ogni risposta del consulente va scritta in un file dentro `docs/buste-reali/`, come è già stato
fatto per le cinque buste analizzate finora: dato di partenza, valore del software, valore del
professionista, differenza, motivo. È così che il software impara: le regole confermate
diventano test automatici, e da quel momento non si possono più rompere senza accorgersene.

Quando un punto è confermato, va tolto da questo elenco. Quando l'elenco è vuoto per un CCNL,
quel contratto si può considerare validato — e le buste di quel contratto smettono di essere
bozze.

## 5. Bozza del primo messaggio

> Gentile [nome],
>
> sto sviluppando un software per buste paga e contabilità pensato per le micro-imprese e per
> gli studi che le seguono. Il motore di calcolo funziona, ma non voglio metterlo sul mercato
> senza che un Consulente del Lavoro ne abbia verificato i risultati: sarebbe un rischio per
> chi lo usa.
>
> Le chiedo poche ore del suo tempo, pagate: le consegno tre buste paga calcolate dal software
> con i relativi dati di partenza, lei le rifà come le farebbe normalmente e mi indica dove i
> numeri non coincidono. In cambio, oltre al compenso, le offro [uso gratuito per il suo studio
> / condizioni da primo cliente] e la possibilità di indirizzare le funzioni che le servono
> davvero.
>
> Se le interessa, le mando il materiale: sono tre PDF e una pagina di note.
>
> Cordiali saluti,
> [nome e recapito]

## 6. Cosa NON chiedergli

- Di firmare o «certificare» il software: nessun professionista lo farà, e non serve.
- Di dare i dati dei suoi clienti: per la verifica bastano casi costruiti o anonimi.
- Di rispondere gratis a un elenco infinito: il primo giro sono tre buste e i punti qui sopra.
