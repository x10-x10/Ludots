---
name: ludots-ci-audit-gate
description: Gate Ludots PR and CI flows on complete hook packets, visual review artifacts, and handoff evidence. Use when validating merge readiness or automation completeness.
---

# Ludots CI Audit Gate

Use this skill to fail fast on missing evidence and incomplete collaboration packets.

## Load References

1. Read `references/ci-gate-checklist.md`.
2. Read `../../registry.json`.

## Mandatory Rules

1. No silent pass on missing evidence.
- Missing packet, manifest, or review artifact is a failing condition unless explicitly scoped out.

2. Gate against current registry rules.
- Required hooks and artifacts come from the shared skill system, not ad-hoc CI logic.

3. Report exact missing paths.
- Every failure must identify the missing or invalid packet or artifact.

## Workflow

1. Gather subject packet set.
2. Verify required hook packets and linked artifact paths.
3. Write result:
- `artifacts/ci-audit/<subject>/result.md`
- `artifacts/ci-audit/<subject>/result.json`
4. Emit `ci.audit.completed`.

## Output Requirements

Provide:
- pass/fail gate summary
- exact missing packets or artifacts
- linked reviewer-ready evidence map
