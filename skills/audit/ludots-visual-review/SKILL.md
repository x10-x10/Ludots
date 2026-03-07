---
name: ludots-visual-review
description: Review Ludots screenshots and extracted frames, then emit a structured visual verdict with evidence links. Use when visual acceptance or audit requires a dedicated review pass.
---

# Ludots Visual Review

Use this skill to turn visual evidence into a structured review result.

## Load References

1. Read `references/review-checklist.md`.
2. Read `../../README.md` only if hook dependencies are unclear.

## Mandatory Rules

1. Review against explicit criteria.
- Use acceptance requirements, issue expectations, or UI checklists.

2. Link exact evidence.
- Each finding must cite concrete frame or screenshot paths.

3. Do not invent runtime causes.
- Visual review reports what is visible and what criteria fail.

## Workflow

1. Consume `visual.frames.ready`.
2. Compare frames or screenshots against acceptance criteria.
3. Write review result:
- `artifacts/reviews/<subject>/review.json`
- `artifacts/reviews/<subject>/review.md`
4. Emit `visual.review.completed`.

## Output Requirements

Provide:
- structured review result conforming to contract
- human-readable summary
- severity-ranked findings with evidence paths
