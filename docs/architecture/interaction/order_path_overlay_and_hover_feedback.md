# Order Path Overlay and Hover Feedback

> SSOT scope: selected move path preview, shared order world-space resolution, and sandbox hover feedback reuse.
> This document describes the shipped runtime implemented by the current slice. It does not redefine generic camera follow, ability routing, or navigation execution beyond the parts directly used by move path and hover feedback.

## 1. Runtime Intent

The current interaction stack needs two kinds of immediate visual feedback without inventing parallel pipelines:

- selected unit right-click move orders should expose a visible path preview before and during movement
- hovered entities in `ChampionSkillSandboxMod` should expose a stable marker even outside active aiming mode

The shipped implementation keeps both behaviors on top of existing infrastructure:

- order/runtime reuse stays inside `src/Core/Gameplay/GAS/Orders/`
- presentation reuse stays inside `src/Core/Presentation/Rendering/GroundOverlayBuffer.cs`
- sandbox-specific indicator policy stays inside `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/Runtime/ChampionSkillSandboxRuntime.cs`

## 2. Reuse-First Design

The slice explicitly reuses these existing systems and boundaries:

- `src/Core/Gameplay/GAS/Systems/MoveToWorldCmOrderSystem.cs`
  - remains the authoritative `moveTo` order consumer
- `src/Core/Gameplay/GAS/Orders/CompositeOrderPlanner.cs`
  - continues to plan cast-followed-by-move sequences, now via shared spatial resolution
- `src/Core/Input/Orders/AbilityIndicatorOverlayBridge.cs`
  - remains the reference pattern for order-to-overlay bridging
- `mods/CoreInputMod/Triggers/InstallCoreInputOnGameStartTrigger.cs`
  - remains the single install point for generic input presentation systems
- `src/Core/Navigation/Pathing/IPathService.cs` runtime service contract when a map exposes pathing
  - selected move preview prefers the existing path solve result instead of hand-building a second route planner

No parallel renderer, no mod-local move runtime, and no duplicate spatial parsing path were introduced.

## 3. Shared Spatial Resolution

`src/Core/Gameplay/GAS/Orders/OrderWorldSpatialResolver.cs` centralizes the common world-space conversions that were previously duplicated across planner/runtime call sites:

- `TryResolveSpatialTarget`
- `TryResolveMoveDestination`
- `TryGetEntityWorldCm`
- `TryResolveProjectedQueuedOrigin`

Current consumers:

- `src/Core/Gameplay/GAS/Orders/CompositeOrderPlanner.cs`
- `src/Core/Gameplay/GAS/Systems/MoveToWorldCmOrderSystem.cs`

This keeps queued-order projection, cast anchor planning, and move destination extraction aligned on one interpretation of `OrderSpatial`.

## 4. Selected Move Path Preview

`src/Core/Input/Orders/SelectedMovePathOverlayBridge.cs` is the generic bridge responsible for previewing the current selected unit's move plan.

Its runtime behavior is:

1. Read the current selected entity from the existing selection/global context.
2. Resolve the currently active or queued move destination through `OrderWorldSpatialResolver`.
3. Query the shared path service when the current map exposes pathing.
4. Emit preview geometry into `GroundOverlayBuffer`.

The bridge is presented by `mods/CoreInputMod/Systems/SelectedMovePathPresentationSystem.cs` and registered in `mods/CoreInputMod/Triggers/InstallCoreInputOnGameStartTrigger.cs`.

### 4.1 Fallback Boundary

Some maps, including the current champion sandbox, boot a path service adapter without a board/path graph. In that case path solving is unavailable even though move execution still works.

For those maps the bridge falls back to a direct order-space segment preview instead of failing silently. This keeps the UX visible on maps that are intentionally lightweight while still preferring true path output when available.

Relevant code and evidence:

- `src/Core/Input/Orders/SelectedMovePathOverlayBridge.cs`
- `src/Tests/GasTests/SelectedMovePathOverlayBridgeTests.cs`
- `src/Tests/GasTests/OrderNavigationMoveRuntimeTests.cs`

## 5. Hover Marker Policy

`mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/Runtime/ChampionSkillSandboxRuntime.cs` now resolves hover indicator targets with two rules:

- any live hovered entity may receive the hover indicator, even when the input mapping is not currently aiming
- the currently selected entity is suppressed, so selection ring and hover ring do not stack on the same actor

This keeps the sandbox readable in normal selection/movement flow and avoids marker duplication noise.

The hover marker still reuses the existing performer/overlay stack:

- performer definitions live in `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/assets/Presentation/performers.json`
- runtime creation/destruction stays inside the existing performer command flow

## 6. Acceptance Evidence

Code evidence:

- `src/Core/Gameplay/GAS/Orders/OrderWorldSpatialResolver.cs`
- `src/Core/Input/Orders/SelectedMovePathOverlayBridge.cs`
- `mods/CoreInputMod/Systems/SelectedMovePathPresentationSystem.cs`
- `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/Runtime/ChampionSkillSandboxRuntime.cs`

Test evidence:

- `src/Tests/GasTests/SelectedMovePathOverlayBridgeTests.cs`
- `src/Tests/GasTests/Production/InputOrderConvergenceValidationTests.cs`
- `src/Tests/GasTests/Production/OrderCompositePlannerTests.cs`
- `src/Tests/GasTests/OrderNavigationMoveRuntimeTests.cs`
- `src/Tests/GasTests/Production/ChampionSkillSandboxConfigTests.cs`
- `src/Tests/GasTests/Production/ChampionSkillSandboxPlayableAcceptanceTests.cs`

Acceptance artifacts:

- `artifacts/acceptance/champion-skill-sandbox/battle-report.md`
- `artifacts/acceptance/champion-skill-sandbox/trace.jsonl`
- `artifacts/acceptance/champion-skill-sandbox/path.mmd`
