# Adapter Claude Code

Questa skill segue lo standard `SKILL.md`, quindi puo essere usata in Claude Code come skill personale o di progetto.

## Installazione progetto

Dalla root del progetto:

```bash
mkdir -p .claude/skills
ln -s ../../agent-kit/paghepro-agent-squad .claude/skills/paghepro-agent-squad
```

In alternativa, copiare la cartella:

```bash
mkdir -p .claude/skills
cp -R agent-kit/paghepro-agent-squad .claude/skills/
```

## Installazione personale

```bash
mkdir -p ~/.claude/skills
ln -s "$PWD/agent-kit/paghepro-agent-squad" ~/.claude/skills/paghepro-agent-squad
```

## Uso

Invocare direttamente:

```text
/paghepro-agent-squad controlla questo piano UniEmens con squadra agenti e verdetto finale
```

Oppure chiedere in linguaggio naturale:

```text
Usa la squadra agenti PaghePro per verificare questa modifica GDPR e indicare il prossimo lavoro da mandare.
```

Claude Code puo usare subagent reali se configurati. Se non sono configurati, la skill resta valida come workflow a passaggi strutturati. Dopo un lavoro importante, la squadra include anche 3 passaggi di controllo consegna, prossimo incarico e ricerca mercato/GAP.
