# Champion Skill Sandbox Delivery Plan

Date: 2026-03-16
Status: In Progress

## Goal

Deliver a minimal playable champion skill sandbox mod that:

- lets the player select multiple Ezreal, Garen, and Jayce instances
- shows different command panel content based on selected entity and current form/state
- supports three global cast modes through one shared toolbar
- uses the existing GAS / selection / command panel / indicator pipelines
- gives every skill a clear visible effect expression through the performer system
- supports camera reset, free camera, follow selected entity, and weighted selected-group follow

## Confirmed Reuse Baseline

The current implementation must continue to reuse these existing foundations instead of building a parallel stack:

- Selection SSOT: `docs/architecture/entity_selection_architecture.md`
- Entity command panel infrastructure: `docs/architecture/entity_command_panel_infrastructure.md`
- GAS layered execution: `docs/architecture/gas_combat_infrastructure.md`
- Performer pipeline: `docs/architecture/presentation_performer.md`
- Camera interaction acceptance references:
  - `mods/fixtures/camera/CameraAcceptanceMod/`
  - `docs/audits/camera_acceptance_projection_marker_recovery.md`

Concrete code entry points already confirmed:

- Command panel GAS source: `mods/EntityCommandPanelMod/Runtime/GasEntityCommandPanelSource.cs`
- Ability form slot routing: `src/Core/Gameplay/GAS/AbilityFormSetRegistry.cs`
- Ability form config loading: `src/Core/Gameplay/GAS/Config/AbilityFormSetConfigLoader.cs`
- Selection runtime: `src/Core/Input/Selection/`
- Indicator bridge: `mods/CoreInputMod/Systems/AbilityAimOverlayPresentationSystem.cs`
- Performer runtime:
  - `src/Core/Presentation/Systems/PerformerRuleSystem.cs`
  - `src/Core/Presentation/Systems/PerformerRuntimeSystem.cs`
  - `src/Core/Presentation/Systems/PerformerEmitSystem.cs`
- Projectile runtime entity creation:
  - `src/Core/Gameplay/GAS/BuiltinHandlers.cs`
  - `src/Core/Gameplay/GAS/Systems/ProjectileRuntimeSystem.cs`

## Current Delivery State

Already implemented in the sandbox branch:

- multi-instance Ezreal / Garen / Jayce sandbox mod with command panel focus sync
- ability form slot routing for Jayce via GAS ability-form infra
- three cast modes:
  - `SmartCast`
  - `SmartCastWithIndicator`
  - `PressReleaseAimCast`
- command panel toolbar mode switching
- selection marker and hover marker
- camera confine and edge-pan safety for the sandbox tactical camera
- camera reset button
- camera toolbar follow modes:
  - free
  - selected entity
  - weighted selected group

Recent camera-follow slice commit:

- `c7cb67c feat(camera): add selected-group follow mode`

## Confirmed Gaps

### 1. Projectile entities are logical only

`LaunchProjectile` currently creates a real ECS entity through `BuiltinHandlers.HandleCreateProjectile`, but that entity does not automatically receive performer/bootstrap presentation state.

Relevant code:

- `src/Core/Gameplay/GAS/BuiltinHandlers.cs`
- `src/Core/Gameplay/GAS/Systems/ProjectileRuntimeSystem.cs`
- `src/Core/Presentation/Systems/PresentationStartupPerformerSystem.cs`

Impact:

- projectile skills can be logically correct but visually ambiguous
- the player cannot reliably tell whether the projectile spawned, traveled, or hit

### 2. Sandbox skill FX are still mostly generic

Current sandbox feedback uses a small runtime helper and generic pulses:

- `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/Runtime/ChampionSkillSandboxVisualFeedback.cs`

This is not yet enough to make each skill visually distinct.

### 3. Projectile skills still use instant search in config

The sandbox currently author-calls several projectile-like skills as `Search` instead of real `LaunchProjectile`.

Current config to convert:

- `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/assets/GAS/effects.json`

Priority projectile candidates:

- Ezreal Q
- Ezreal R
- Jayce Cannon Q

## Delivery Decisions

### A. Keep slot/form logic in GAS, not in the sandbox

The user reminder was correct: ability-form route-to-slot already exists and should remain the single source of truth.

The sandbox will keep using:

- `AbilityFormSetRegistry`
- `AbilityFormRoutingSystem`
- command panel effective-slot resolution

### B. Add a reusable projectile-to-performer bridge instead of fake sandbox-only missiles

Projectile remains a real ECS entity.

The next reusable extension will attach performer startup state to runtime projectile entities so that projectile visuals can be authored declaratively and reused by mods.

Target outcome:

- projectile entity is still moved by `ProjectileRuntimeSystem`
- projectile entity gains performer bootstrap state for visible primitive VFX
- sandbox authors only need config bindings, not custom projectile presenter code

### C. Skill cast / hit / state cues should go through performer commands

For non-projectile skill feedback, the sandbox will emit `PresentationCommand.CreatePerformer` based on `GasPresentationEvent` so cues can anchor to:

- caster for cast windup / self-buff / aura cues
- target for impact / execute / hit cues

This keeps feedback inside the performer system while allowing per-skill anchor choice.

### D. Primitive-first visuals are sufficient

For this sandbox, visible primitive effects are acceptable and preferred over introducing incomplete art pipelines.

Expected visual vocabulary:

- projectile body: sphere / pulse marker
- zone skill: ring / circle / rectangle overlay
- melee / strike skill: short burst marker on actor or target
- toggle / aura skill: persistent ring or marker performer

## Implementation Slices

### Slice 1. Camera follow toolbar

Completed.

Evidence:

- `src/Core/Gameplay/Camera/FollowTargets/SelectedGroupFollowTarget.cs`
- `mods/showcases/champion_skill_sandbox/ChampionSkillSandboxMod/Runtime/ChampionSkillCastModeToolbarProvider.cs`
- `src/Tests/GasTests/Production/ChampionSkillSandboxConfigTests.cs`
- `src/Tests/ThreeCTests/CameraRuntimeConvergenceTests.cs`

### Slice 2. Reusable projectile performer bootstrap

Planned Core work:

- add a generic projectile presentation binding/bootstrap path
- ensure runtime-created projectile entities can receive startup performers and stable presentation identity
- cover the bootstrap path with focused GAS / presentation tests

### Slice 3. Convert sandbox projectile skills to real `LaunchProjectile`

Planned sandbox work:

- convert Ezreal Q / R and Jayce Cannon Q from `Search` to `LaunchProjectile`
- author projectile performer bindings/config
- keep impact damage in existing GAS hit effects

### Slice 4. Replace generic sandbox feedback with per-skill performer FX

Planned sandbox work:

- add performer definitions for cast, projectile, hit, aura, and execute cues
- route sandbox visual feedback through `PresentationCommandBuffer`
- make every showcased skill visibly different

### Slice 5. Acceptance verification

Required evidence:

- targeted unit/config tests
- runnable raylib sandbox launch
- visual verification of:
  - selection marker
  - hover marker
  - cast mode switching
  - projectile spawn/travel/hit visibility
  - camera follow modes

## Initial Acceptance Checklist

- selecting different champion instances changes the command panel immediately
- Jayce form switching changes current slots through GAS form routing
- three cast modes behave differently and can be switched from the toolbar
- move order remains usable for spacing
- selection and hover are visually legible
- projectile skills spawn visible performer-driven missiles
- every skill has a distinct cast and/or impact effect cue
- camera can reset, stay confined, and follow current selection or weighted group

## Open Risk

The current projectile runtime only stores logic travel data. If later features need richer projectile-specific rendering metadata beyond startup performers, a dedicated shared projectile presentation contract may still be needed in Core.
