# Guida semplice: la decisione DPA sul provider AI

> Per il fondatore. È l'unica decisione che oggi blocca il collaudo di Aurora con un
> modello vero. Nessun codice va scritto: è una scelta da fare a mente fredda, una volta.

## Che cos'è il DPA, in una frase

Quando Aurora manda un testo a un provider AI (DeepSeek, o altri), quel testo esce dal tuo
server e viene elaborato dai computer del provider. Il **DPA** (Data Processing Agreement,
"accordo sul trattamento dei dati") è il contratto in cui il provider promette per iscritto
cosa fa con quei dati: dove li elabora, quanto li conserva, se li usa per addestrare i suoi
modelli, chi altro li vede. Senza questo accordo, mandare dati di buste paga a un provider
è un rischio GDPR che ricade su di te.

## La distinzione che decide tutto: dati finti o dati veri

| Scenario | Cosa serve | Verdetto |
|---|---|---|
| **Collaudo con dati finti** (aziende demo, dipendenti inventati) | Solo prudenza: nessun dato reale nei messaggi | ✅ Puoi partire con qualunque provider, anche subito |
| **Uso con dati veri dei clienti** (nomi, presenze, stipendi) | DPA firmato + garanzie sul trasferimento extra-UE | ⚠️ Da valutare provider per provider, vedi sotto |
| **Modello locale sul tuo Mac/server** (Ollama, gate G1) | Nessun DPA: i dati non escono mai | ✅ La strada più pulita per i dati veri |

## Cosa verificare su DeepSeek (o su qualunque provider), passo-passo

Cerca sul sito ufficiale del provider le pagine **"Terms of Use"**, **"Privacy Policy"** e,
se esiste, **"Data Processing Agreement"** o **"DPA"**. Poi rispondi a queste cinque domande:

1. **Dove vengono elaborati i dati?** DeepSeek è una società cinese: i dati passano da server
   fuori dall'Unione Europea, in un Paese senza decisione di adeguatezza GDPR. Questo è il
   punto più pesante: per dati personali veri servirebbero garanzie contrattuali forti
   (clausole contrattuali standard) che il provider deve offrire per iscritto.
2. **I tuoi messaggi vengono usati per addestrare i modelli?** Se sì, o se non è scritto
   chiaramente di no, i dati dei clienti potrebbero finire dentro il modello. Non accettabile
   per dati veri.
3. **Quanto vengono conservati?** Cerca la durata di conservazione dei prompt. "Finché serve"
   senza limite chiaro è un no.
4. **C'è un DPA firmabile per aziende?** I provider seri (OpenAI, Anthropic, Mistral, Azure)
   offrono un DPA standard per i clienti business. Se il provider non ne ha uno, per i dati
   veri è squalificato in partenza.
5. **Chi altro vede i dati?** Sub-fornitori, moderazione umana, autorità del Paese del
   provider. Deve essere elencato.

## La raccomandazione onesta

- **Per il collaudo A3 (shadow/DryRun)**: usa **dati finti** e la coppia tenant:utente di
  collaudo. Così puoi verificare Aurora con DeepSeek o altri senza esposizione: nessun dato
  reale nei messaggi = nessun problema DPA. È il percorso previsto dal piano.
- **Per l'uso vero con i clienti**: la strada più solida è il **modello locale** (gate G1,
  Ollama sul tuo hardware): zero trasferimenti, zero DPA, zero canoni API. In alternativa un
  provider **europeo o con DPA e clausole standard** (es. Mistral in Francia, o i grandi
  provider USA con DPA firmato). **DeepSeek cloud con dati veri dei clienti è sconsigliato**
  finché non offre garanzie scritte sul trasferimento extra-UE — oggi il rischio resterebbe
  tuo.

## Cosa fare concretamente (mezz'ora)

1. Decidi il collaudo: se ti basta partire con dati finti, la decisione è già presa — dimmi
   «collaudo con dati finti» e configuro coppia shadow e DryRun senza toccare dati reali.
2. Per il futuro con dati veri: leggi Privacy Policy e Terms del provider che preferisci con
   le cinque domande sopra; se una risposta manca o non ti convince, il provider non è pronto
   per i dati dei tuoi clienti.
3. Se vuoi la strada locale: il gate G1 (prova tecnica di isolamento di Ollama) è già
   descritto nel piano e non dipende da nessun contratto.

> Nota: questa guida non sostituisce il parere del professionista privacy previsto dal piano
> GDPR. Serve a farti arrivare da lui con la domanda giusta, non a saltarlo.
