# MUD Battle Log Spec

## Purpose

Provide readable acceptance logs aligned with real gameplay behavior.

## Required Sections

1. Header
- scenario name
- build/version
- seed/map/clock
- execution timestamp

2. Timeline
- ordered ticks or phases
- actor, action, target, result
- key state deltas (hp, tags, cooldown, position, status)

3. Outcome
- success/failure decision
- failed assertions (if any)
- reason codes

4. Summary Stats
- total actions
- key damage/heal/control counters
- dropped/budget/fuse counters

## Format Guidance

- Keep one line per meaningful event.
- Prefer gameplay language over raw debug jargon.
- Include stable IDs for cross-reference with trace file.

## Example Event Line

`[T+012] Unit#A.Cast(Fireball) -> Unit#B | Hit | HP 420 -> 355 | Tag+Burn(3s)`

