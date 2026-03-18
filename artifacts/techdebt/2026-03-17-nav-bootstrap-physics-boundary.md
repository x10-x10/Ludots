# Tech Debt Report: nav-bootstrap-physics-boundary

Date: 2026-03-17
Reporter: Codex
Owner: Core Navigation / Physics maintainers
Severity: P2
Scope: Cross-layer

## Trigger
- Scenario: `champion_skill_stress` crowded combat scene looked like collision / avoidance was ineffective.
- Entry point: `Ludots.Core.Navigation2D.Systems.NavOrderAgentBootstrapSystem`
- Repro steps:
  1. Spawn stress combat teams in `champion_skill_stress`.
  2. Attempt to harden the generic order-driven bootstrap path so units author the same minimum runtime state as playground agents.
  3. Observe that `PhysicsMaterial2D` cannot be referenced from `Ludots.Core`, because Physics2D depends on Core rather than the reverse.

## Evidence
- `src/Core/Navigation2D/Systems/NavOrderAgentBootstrapSystem.cs`
- `src/Core/Ludots.Core.csproj`
- `src/Core/Ludots.Physics2D/Ludots.Physics2D.csproj`
- `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/Systems/ChampionSkillStressSpawnSystem.cs`
- `src/Tests/GasTests/OrderNavigationMoveRuntimeTests.cs`
- `src/Tests/GasTests/Production/ChampionSkillSandboxConfigTests.cs`

## Impact
- User-visible impact: feature authors may assume Core can fully author Physics2D runtime components for order-driven units, then hit compile-time or layering failures.
- Correctness/stability risk: repeated mod-side workarounds can drift from playground / core behavior and reintroduce unstable crowd scenes.
- Blast radius: any mod that wants generic order-driven nav/physics bootstrap behavior.

## Fuse Decision
- Mode: isolation
- Reason: keep Core responsible only for cross-layer-safe transform/history bootstrap (`Position2D`, `PreviousPosition2D`, `PreviousWorldPositionCm`, nav agent state) and rely on Physics2D-side defaults for material until a shared abstraction exists.
- Observability fields:
  - debt id: `nav-bootstrap-physics-boundary`
  - impacted scenario: `champion_skill_stress`
  - branch reason: `core-cannot-reference-physics2d-material`

## Containment and Follow-up
- Immediate containment: fixed the stress formation overlap bug; hardened Core bootstrap only with components that do not violate the current dependency graph.
- Permanent fix direction: extract shared physics bootstrap contracts/components to a layer both Core and Physics2D can legally reference, or move material bootstrap responsibility into Physics2D runtime systems.
- Target milestone: next nav/physics infrastructure pass touching order-driven movement bootstrap.
