# Test Path Visualization Spec

## Purpose

Make acceptance flows auditable at a glance.

## Required Artifacts

1. `path.mmd` (Mermaid)
- state graph or sequence diagram
- includes happy path and at least one failure/guard branch

2. `trace.jsonl`
- machine-readable event stream
- stable event ids and timestamps

## Minimum Path Content

- scenario start
- core player action chain
- key system gates (validation, budget, rule checks)
- outcome state

## Node Label Guidance

- Use `<phase>: <action> -> <result>`
- Include guard conditions on branch edges

## Example Branch Label

`if cooldown_ready == false -> reject_order`

