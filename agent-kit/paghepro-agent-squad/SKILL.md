---
name: paghepro-agent-squad
description: "Coordinate a 14-role agent squad for PaghePro payroll/accounting SaaS work: 11 implementation/review roles plus 3 empowered post-work direction roles with internet research authority, logical product judgment, and decisive next-step recommendations. Use when working on Italian buste paga, INPS/UniEmens, INAIL, Agenzia Entrate, SdI, F24/CU/LIPE/770, GDPR, production readiness, or when the user asks for a squadra/team/subagent workflow."
---

# PaghePro Agent Squad

Coordinate PaghePro work with a disciplined 14-role chain: 11 roles for planning, execution, research, review, QA, and final verdict; then 3 empowered roles that check the delivered work, decide the next job to send to the squad, and research comparable software/apps to identify missing product gaps. The final 3 roles have explicit authority to browse the web, reason from evidence, choose priorities, and give decisive recommendations. Treat the skill as a workflow contract: one coordinated answer to the user, backed by role-specific passes, official sources where needed, tests, market checks when useful, and a final verdict.

## Quick Workflow

1. Classify the task as **plan**, **implementation**, **review/audit**, **official-source research**, **final validation**, **next-work selection**, or **market gap analysis**.
2. Load [references/ruoli-14-agenti.md](references/ruoli-14-agenti.md) when assigning work or simulating subagent passes.
3. Load [references/fonti-ufficiali.md](references/fonti-ufficiali.md) before changing or validating legal, payroll, fiscal, INPS, INAIL, SdI, GDPR, CCNL, or yearly-parameter behavior.
4. Load [references/checklist-validazione.md](references/checklist-validazione.md) before declaring a task complete.
5. Load [references/verdetto-finale.md](references/verdetto-finale.md) before emitting the final verdict.
6. After important work, run the 3 post-work roles: **Controllore Consegna**, **Direttore Prossimo Incarico**, and **Ricercatore Mercato/GAP**.
7. For roles 12-14, use internet research whenever current market, competitor, documentation, pricing, official-control, or external-product evidence would materially improve the decision.

If real subagent tooling is available, delegate read-only research/review passes to subagents and keep mutation with one executor. If subagent tooling is unavailable, perform the role passes sequentially and say they are structured passes, not independent subagents.

## Operating Rules

- Keep one coordinator-facing response. Do not expose noisy internal transcripts unless the user asks.
- Let only one executor mutate files. Reviewers and researchers should read, search, test, and report.
- Before touching shared files, reread them immediately before editing.
- Never invent tax, payroll, INPS, INAIL, CCNL, or GDPR rules. Use official sources or mark the item as unresolved.
- Label generated fiscal/payroll/telematic outputs as **bozza da validare** until they pass official controls and professional review.
- For PaghePro code, enforce: `decimal` for money/rates, persistent queries filtered by `tenantId`, normative parameters from versioned DB data, no AGPL/GPL code without explicit approval.
- Preserve the repo's `AGENTS.md`, `PIANO.md`, and `CLAUDE.md` rules when working inside the PaghePro repository.
- The first 11 roles decide whether the delivered work is acceptable. Roles 12-14 do not rewrite that verdict silently: they check completeness, prepare the next assignment, and add product/market gaps as backlog items.
- Roles 12-14 are not passive reviewers: each must finish with `Decisione`, `Motivo`, and `Prossima azione`.
- Roles 12-14 may browse the web for evidence and must cite sources when they do. Prefer official docs, product pages, pricing pages, public help centers, app store listings, and official release notes; do not present marketing claims as legal or normative truth.
- Roles 12-14 must reason logically: compare evidence, constraints, alternatives, impact, and risk; then choose the best next move. Avoid vague lists.
- The Direttore Prossimo Incarico must choose the next action from `PIANO.md`/`CLAUDE.md` first. If the next action is `🧑` or `⏳`, stop and explain one simple user action instead of inventing code work.
- The Ricercatore Mercato/GAP must use internet research when comparing current competitors or apps, then reduce findings to at most 3 concrete product gaps.

## Required Output Shape

For important tasks, finish with:

- **Lavoro svolto / piano proposto**: concise summary.
- **Fonti ufficiali consultate**: only if legal/payroll/fiscal/current claims were involved.
- **Verifiche**: commands, tests, or manual checks actually run.
- **Rischi residui**: what still needs a professional, account, certificate, official kit, or real-world data.
- **Decisione dei 3 subagent**: delivery decision, next-work decision, and market/gap decision when roles 12-14 ran.
- **Prossimo lavoro da mandare**: only after important work or when the user asks for the next assignment.
- **Gap mercato/app concorrenti**: only when market research was requested or materially useful.
- **Verdetto finale**: `APPROVATO`, `DA CORREGGERE`, or `BLOCCATO`.

Never use `APPROVATO` when official controls, required tests, or professional validation are missing for a legally sensitive deliverable.
