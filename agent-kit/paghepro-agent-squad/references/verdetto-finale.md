# Verdetto Finale

Il Validatore Finale deve chiudere ogni lavoro importante con uno di questi verdetti.

## APPROVATO

Usare solo se:

- build/test richiesti sono verdi o il lavoro non richiede codice;
- fonti ufficiali necessarie sono state consultate;
- review e meta-review non hanno findings bloccanti;
- output normativi restano etichettati correttamente se non ancora certificati;
- eventuali limiti residui sono non bloccanti e dichiarati.

Formato:

```text
Verdetto finale: APPROVATO
Motivo: ...
Verifiche: ...
Rischi residui: ...
```

## DA CORREGGERE

Usare se il lavoro e recuperabile ma presenta errori, test mancanti, fonti insufficienti, UX confusa, regressioni o controlli incompleti.

Formato:

```text
Verdetto finale: DA CORREGGERE
Correzioni obbligatorie:
- ...
Verifiche da rifare:
- ...
```

## BLOCCATO

Usare se manca un elemento esterno indispensabile o non si puo procedere senza decisione umana: account provider, kit ufficiale, certificato, dati reali, consulente, legale, credenziali, scelta di prodotto.

Formato:

```text
Verdetto finale: BLOCCATO
Blocco: ...
Azione utente richiesta:
1. ...
```

## Regole

- Non usare `APPROVATO` per calcoli paghe o file ufficiali se manca validazione del professionista o controllo ufficiale richiesto.
- Non nascondere warning: distinguere warning tecnico, rischio normativo e azione utente.
- Se la richiesta e solo un piano, il verdetto valuta il piano, non il prodotto.
