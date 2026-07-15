# Adapter OpenAI/Codex

Questa skill segue lo standard `SKILL.md` e include `agents/openai.yaml` per metadati UI Codex.

## Installazione personale Codex

Dalla root del progetto:

```bash
mkdir -p ~/.codex/skills
ln -s "$PWD/agent-kit/paghepro-agent-squad" ~/.codex/skills/paghepro-agent-squad
```

In alternativa, copiare la cartella:

```bash
mkdir -p ~/.codex/skills
cp -R agent-kit/paghepro-agent-squad ~/.codex/skills/
```

## Uso

Invocare direttamente:

```text
Use $paghepro-agent-squad to coordinate a review of the PaghePro INAIL implementation and give a final verdict.
```

Oppure in italiano:

```text
Usa $paghepro-agent-squad per controllare fonti ufficiali, codice, test, verdetto finale e prossimo lavoro da mandare.
```

Codex puo usare subagent/tool dedicati quando disponibili. Se non disponibili, deve eseguire i ruoli come passaggi strutturati e dichiararlo chiaramente. Dopo un lavoro importante, la squadra include anche 3 passaggi di controllo consegna, prossimo incarico e ricerca mercato/GAP.
