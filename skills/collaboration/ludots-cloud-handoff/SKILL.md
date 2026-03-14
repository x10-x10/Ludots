---
name: ludots-cloud-handoff
description: Package Ludots cloud-session handoffs with exact next actions, blockers, evidence links, and hook packets. Use when pausing work, handing off between agents, or resuming after CI / review.
---

# Ludots Cloud Handoff

Use this skill to turn active work into a deterministic handoff packet.

## Load References

1. Read `references/handoff-packet-spec.md`.
2. Read `../../README.md` only if hook routing context is needed.

## Mandatory Rules

1. Handoffs describe current truth only.
- Do not copy stale plans or inferred status.
- Link concrete artifacts, hook packets, and file paths.

2. Handoffs must be resumable.
- Include next action, owner, blocker, and evidence path per stream.
- State exactly what the next agent should do first.

3. Handoffs do not replace delivery or review artifacts.
- Reference them; do not rewrite them.

## Workflow

1. Collect current branch, scope, and subject key.
2. Gather latest hook packets and evidence artifacts.
3. Summarize completed work, open blockers, and next steps.
4. Write handoff packet:
- `artifacts/handoffs/<subject>/handoff.md`
- `artifacts/agent-hooks/<subject>-handoff.ready.json`
5. Emit `handoff.ready` with linked artifacts.

## Output Requirements

Provide:
- exact next action list
- blocker list with owners
- linked evidence, review, and CI packet paths
- handoff packet ready for the next agent
