# UniEmens: come si verifica il flusso col software ufficiale INPS

> Stato al 24 agosto 2026: un flusso generato da PaghePro è stato **elaborato correttamente dal
> software di controllo ufficiale INPS versione 4.2.0** (protocollo 4.32.0), con **0 errori
> bloccanti**, **0 forzabili** e produzione del file `.UniEM` e della ricevuta.
> È il criterio di chiusura previsto dalla Fase 6 del `PIANO.md`.

## Che cos'è e perché conta

L'UniEmens è la denuncia mensile che va all'INPS con retribuzioni e contributi dei dipendenti.
L'INPS non accetta un XML qualunque: prima di caricarlo sul portale bisogna farlo passare dal
proprio software di controllo, che produce un file con estensione `.UniEM`. **Solo quel file si
carica**, non l'XML di lavoro.

Il controllo esegue due passaggi distinti:

1. **validazione dello schema** (la forma del file);
2. **controlli estesi** — centinaia di regole di merito, che il programma scarica da internet
   e che verificano anche i dati sugli archivi INPS (per esempio se la matricola esiste davvero
   e a quale codice fiscale è intestata).

Per questo il progetto valida su due livelli: `UniEmensValidator` fa il primo controllo in
locale a ogni generazione, mentre il secondo resta un passaggio esterno e obbligatorio.

## Cosa fa PaghePro

- `UniEmensXmlWriter` genera il tracciato ufficiale (radice `DenunceMensili`, schema 4.32.0).
- `UniEmensValidator` valida contro lo schema XSD ufficiale **incorporato** nel programma:
  la verifica funziona anche senza internet, e i test la eseguono a ogni build.
- `UniEmensService` costruisce il flusso dai cedolini archiviati, dall'anagrafica e dal
  cartellino presenze, e restituisce l'elenco dei dati che il tracciato pretende ma che
  l'archivio non ha ancora (`daCompletare`).
- L'endpoint `POST /api/aziende/{id}/uniemens/mese` restituisce l'XML, l'esito della
  validazione di schema e quell'elenco.

## Come ripetere la verifica ufficiale

Serve il software INPS «UniEMens Individuale — Validatore Automatico» (distribuzione Linux) e
Docker. Il validatore è compilato per x86-64: su Mac con processore Apple gira in emulazione.

```bash
# 1. cartella di lavoro con l'XML da controllare
RUN=~/.cache/paghepro-uniemens-run
mkdir -p $RUN/{input,output,temp,results,logs}
cp flusso.xml $RUN/input/

# 2. controllo ufficiale (VALIDATORE = cartella "inps-controlli-uniemens-auto")
docker run --rm --platform linux/amd64 \
  -v "$RUN:/work" -v "$VALIDATORE:/validatore:ro" \
  mcr.microsoft.com/dotnet/runtime:8.0 \
  /validatore/INPS.ValidatoreAuto flat \
    --inputFolder /work/input --tempFolder /work/temp --outputFolder /work/output \
    --splitOutput --logFileFolder /work/logs --logFileName paghepro-log \
    --resultFilesFolder /work/results --proxy no
```

Esito atteso: `Elaborati correttamente: 1`, `exit 100`. Gli errori, se ci sono, stanno
nell'XML di riepilogo in `output/` e negli Excel/PDF in `results/`.

Il parametro `--proxy no` è obbligatorio: senza, il programma esce con `exit 21`. Il container
ha bisogno di internet, perché le regole dei controlli vengono scaricate dai servizi INPS.

## Regole imparate dal controllo ufficiale

Sono state scoperte facendo elaborare davvero un flusso, non leggendo la documentazione, e
sono ora coperte dai test in `UniEmensTests`:

| Controllo | Regola |
|---|---|
| DR01923 | `UnitaOperativa` e `UnitaProduttiva` sono obbligatorie: in assenza si scrive `0`. |
| DR00085 | Se c'è un contributo servono i `GiorniContribuiti` (le `OreContribuite` per i part-time: sono alternativi). |
| DR00070 | `RetribTeorica` è obbligatoria dal 2008: sono le voci fisse della busta, non il lordo con gli accessori. |
| DI00025 | Il codice contratto CNEL è obbligatorio su ogni denuncia individuale. |
| DI00148 | La cittadinanza usa la codifica ufficiale INPS: un codice inventato viene respinto. |
| EM00017 | Ogni settimana esposta vuole i corrispondenti elementi `Giorno`: il calendario non è facoltativo. |
| EM00002-5 | Codici fiscali e partite IVA sono verificati nel carattere di controllo: i codici «da esempio» vengono respinti. |
| EM00007 | La matricola INPS ha un formato controllato. |
| EM00032-33 | La coppia matricola/codice fiscale viene verificata sugli archivi INPS, con le caratteristiche contributive della posizione. |

Le ultime due righe spiegano perché **la verifica finale richiede dati veri**: una matricola
inventata non ha caratteristiche contributive e il flusso viene respinto anche se è perfetto.

## Anagrafica contributiva

I dati che il tracciato chiede sul lavoratore e che non stanno nella busta paga si registrano
nella scheda del dipendente, sezione **«Dati per la denuncia INPS (UniEmens)»**:

| Dato | Obbligatorio | Note |
|---|---|---|
| Cittadinanza | **sì** | tre cifre, codifica delle Comunicazioni Obbligatorie (la stessa dell'UNILAV) |
| Tipo di part-time | sì, se part-time | orizzontale `P`, verticale `V`, misto `M`; il tempo pieno (`F`) lo scrive il programma |
| Qualifica professionale ISTAT | no | classificazione CP2021 dalla competenza 05/2025 |
| Tipo di contribuzione | no | solo casi particolari: apprendisti, lavoro intermittente, agevolazioni |

Il codice contratto CNEL, anch'esso obbligatorio, non si digita: arriva dal CCNL dell'anno ed è
già registrato sul cedolino. Il codice comune viene dal codice catastale del rapporto.

I formati vengono controllati al salvataggio (un codice scritto male viene rifiutato subito,
con un messaggio in italiano) mentre la validità nel merito la stabilisce l'INPS al controllo.
Quel che manca finisce nell'elenco `daCompletare` della risposta, non viene inventato.

Nomi e ragioni sociali vengono adattati al tracciato solo nel file — maiuscole, niente accenti,
lunghezza massima — mentre l'archivio conserva la grafia vera: «Nicolò D'Amico» diventa
`NICOLO D'AMICO` nella denuncia e resta «Nicolò D'Amico» nell'anagrafica.

## Cosa manca ancora

- **Anagrafica**: la schermata per generare la denuncia dalla SPA (oggi c'è solo l'endpoint).
- **Eventi tutelati**: malattia, maternità, CIG e i relativi codici evento sulle settimane
  (coperture "1" e "2"), conguagli e recuperi. Il generatore copre il mese ordinario.
- **Gestioni speciali**: collaboratori, sport e spettacolo, pubblica amministrazione,
  agricoltura (`PosAgri`, che servirà al modulo agricolo).
- **Archiviazione della pratica** UniEmens con i suoi stati, come già avviene per l'F24.
- L'**invio** resta sui canali ufficiali: cassetto previdenziale INPS o intermediario
  abilitato. Il software prepara il file, non lo trasmette.
