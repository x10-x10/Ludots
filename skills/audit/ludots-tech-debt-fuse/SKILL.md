---
name: ludots-tech-debt-fuse
description: Escalate cross-layer technical debt and circuit-breaker decisions when upper-layer Ludots work reveals lower-layer defects. Use when feature implementation discovers platform, core, engine, config, or infrastructure defects that should be reported, bounded, or fused instead of silently bypassed.
---

# Ludots Tech Debt and Fuse

Use this skill when development finds lower-layer issues with non-local impact.

## Load References

1. Read `references/impact-matrix.md`.
2. Read `references/fuse-playbook.md`.
3. Read `references/debt-report-template.md`.
4. Read `references/escalation-rules.md`.

## Mandatory Rules

1. Do not silently bypass lower-layer defects.
2. Classify impact scope before choosing workaround.
3. Choose a fuse strategy for unsafe paths.
4. Emit debt report with concrete evidence and owner.

## Workflow

1. Capture defect signal.
- Record trigger scenario, layer, and first failing entrypoint.

2. Classify impact.
- Use blast radius classification: local, subsystem, cross-layer, global.
- Assign severity based on user impact and data correctness risk.

3. Decide fuse action.
- Choose stop, degrade-with-explicit-flag, or isolate.
- Ensure fuse behavior is observable in logs and acceptance output.

4. Emit debt report.
- Write `artifacts/techdebt/<date>-<id>.md` using template.
- Include affected modules, risk, and short-term containment.

5. Update delivery artifacts.
- Link debt id in feature acceptance report.
- Add follow-up item to KANBAN/alignment docs with evidence paths.

## Output Requirements

Required outputs:
- debt report file
- fuse decision note
- linked evidence paths (`src/...`, `docs/...`, `tests/...`)
- owner + due window

