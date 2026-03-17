# Impact Matrix

## Scope Levels

1. Local
- A single feature path or one module.

2. Subsystem
- Multiple modules in one subsystem.

3. Cross-layer
- Affects boundaries between Core/Adapter/Mod/App.

4. Global
- Can corrupt shared config/state or break startup/runtime broadly.

## Severity Guide

- P0: correctness/data safety/runtime stability risk at subsystem+ scale
- P1: major feature correctness risk with clear user-facing impact
- P2: bounded degradation with workaround
- P3: low-impact debt with no immediate correctness risk

## Required Fields

- impacted layers
- affected paths
- reproducible trigger
- current containment
- unresolved risk

