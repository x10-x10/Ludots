---
name: ludots-visual-capture
description: Capture deterministic screenshots or recordings for Ludots UI and visual flows, then write an evidence manifest. Use when a hook requests visual evidence or when a reviewer needs first-hand capture artifacts.
---

# Ludots Visual Capture

Use this skill to produce deterministic screenshots and recordings.

## Load References

1. Read `references/capture-checklist.md`.
2. Read `../../README.md` if hook packet paths are unclear.

## Mandatory Rules

1. Capture deterministic scenarios.
- Record build, branch, scenario, and capture tool.
- Avoid ad-hoc manual steps without notes.

2. Preserve raw evidence.
- Keep original screenshots or recordings.
- If blocked, emit `visual.capture.blocked` with an explicit reason.

3. Always write an evidence manifest.
- Every capture run must produce a machine-readable manifest.

## Workflow

1. Consume `visual.capture.requested`.
2. Run the requested scenario and capture screenshots or video.
3. Store artifacts under `artifacts/evidence/<subject>/capture/`.
4. Write `artifacts/evidence/<subject>/manifest.json`.
5. Emit `visual.capture.completed` or `visual.capture.blocked`.

## Output Requirements

Provide:
- evidence manifest
- raw screenshot and/or video paths
- capture notes with tool and scenario
