# Interaction Showcase Stage Handoff

- date: `2026-03-12`
- branch: `feat/mod-interaction-showcase`
- stage commit: `94f5277`

## Scope closed in this stage

- Added core `moveTo` runtime execution path for world-centimeter movement orders and wired multi-select queue/stop flows into the production order pipeline.
- Fixed GAS caller-parameter preservation for line/vector/displacement paths so blink-dash-rune and displacement presets resolve from preserved origin/target params even when exec state is missing.
- Moved live input sampling ahead of simulation in `GameEngine.Tick()` so authoritative input snapshots are current-frame instead of one-frame late.
- Added `AbilityAimOverlayPresentationSystem` in `CoreInputMod` so aim/indicator previews are emitted during presentation instead of being lost from simulation-time buffer writes.
- Hardened production acceptance for interaction showcase, including indicator-mode baseline accounting against the persistent selection ring overlay.

## Green commands

```powershell
dotnet test src\Tests\GasTests\GasTests.csproj -c Release /m:1 --filter "FullyQualifiedName~InteractionShowcasePlayableAcceptanceTests.InteractionShowcase_PlayableFlow_WritesAcceptanceArtifacts"
```

```powershell
dotnet test src\Tests\GasTests\GasTests.csproj -c Release /m:1 --filter "FullyQualifiedName~AuthoritativeInputConvergenceTests|FullyQualifiedName~InputOrderConvergenceValidationTests|FullyQualifiedName~InteractionShowcasePlayableAcceptanceTests|FullyQualifiedName~ProductionAllModsValidationTests|FullyQualifiedName~TargetResolver_LineSearch_UsesPreservedCallerParams_WhenExecIsMissing|FullyQualifiedName~BuiltinHandler_ApplyDisplacement_UsesPreservedCallerTargetPoint_WhenExecIsMissing|FullyQualifiedName~InteractionShowcase_ArcweaverRuneBurst_AfterBlinkDash_DirectVectorCast_DamagesTarget|FullyQualifiedName~InteractionShowcase_ArcweaverRuneBurst_AfterBlinkDash_ChordInput_DamagesTarget|FullyQualifiedName~InteractionShowcase_ArcweaverRuneBurst_AfterQBlinkGuardDash_ChordInput_DamagesTarget"
```

## Key files for follow-up agents

- `src/Tests/GasTests/Production/InteractionShowcasePlayableAcceptanceTests.cs`
- `mods/CoreInputMod/Systems/AbilityAimOverlayPresentationSystem.cs`
- `src/Core/Engine/GameEngine.cs`
- `src/Core/Input/Orders/InputOrderMappingSystem.cs`
- `artifacts/acceptance/interaction-showcase/feature_coverage_matrix.md`

## Next acceptance frontier

- Continue feature-by-feature implementation against the 170+ interaction cases from `docs/architecture/interaction/`.
- Reuse the now-stabilized base for:
  - unit selection variants
  - indicator/aim-cast/toggle/channel flows
  - vector and multi-stage skills
  - high-volume stress scenarios on the showcase stress map
