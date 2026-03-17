# Ludots PR / Issue Orchestration

Use this skill to assemble reviewer-facing PR or issue packets from accepted evidence.

## Load

- `references/pr-issue-packet-spec.md`

## Rules

- Keep one packet per subject.
- Link review, handoff, and CI artifacts.
- Request visual evidence when the change is visual.

## Outputs

- `artifacts/pr/<subject>/packet.md`
- `artifacts/agent-hooks/<subject>-pr.packet.ready.json`
