---
name: ludots-hook-orchestrator
description: Validate Ludots hook packets and derive the next explicit skill actions from the shared registry. Use when packet-driven collaboration needs deterministic routing without hidden agent calls.
---

# Ludots Hook Orchestrator

Use this skill to coordinate explicit hook-based workflows.

## Load References

1. Read `references/hook-lifecycle.md`.
2. Read `../../registry.json`.
3. Read `../../contracts/hook.schema.json` when validating packets.

## Mandatory Rules

1. No hidden delegation.
- This skill routes work by writing next-step instructions and validating packets.
- It does not simulate or conceal another agent run.

2. Packets are immutable.
- Never edit an emitted packet in place.
- Emit a new packet or dispatch note for the next state.

3. Registry is the routing source of truth.
- Do not invent producers or consumers outside `skills/registry.json`.

## Workflow

1. Load packet and validate schema.
2. Resolve matching consumers from the registry.
3. Write dispatch note to `artifacts/agent-hooks/dispatch/<packet-id>.md`.
4. Stop with explicit blockers if contracts or artifacts are incomplete.

## Output Requirements

Provide:
- packet validation result
- next skill list in execution order
- missing artifact or schema blockers
