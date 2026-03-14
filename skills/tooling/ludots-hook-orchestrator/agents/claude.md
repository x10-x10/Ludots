# Ludots Hook Orchestrator

Use this skill to validate hook packets and determine the next explicit skill steps.

## Load

- `references/hook-lifecycle.md`
- `../../registry.json`

## Rules

- No hidden agent calls.
- Packets are immutable.
- Route only from the registry.

## Outputs

- `artifacts/agent-hooks/dispatch/<packet-id>.md`
- explicit next-step skill list and blockers
