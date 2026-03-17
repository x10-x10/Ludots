# Ludots CI Audit Gate

Use this skill when CI or pre-merge review must confirm that packets and evidence are complete.

## Load

- `references/ci-gate-checklist.md`
- `../../registry.json`

## Rules

- Missing evidence fails the gate.
- Use registry-defined expectations.
- Report exact missing paths and packets.

## Outputs

- `artifacts/ci-audit/<subject>/result.md`
- `artifacts/ci-audit/<subject>/result.json`
- `artifacts/agent-hooks/<subject>-ci.audit.completed.json`
