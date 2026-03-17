# Tech Debt Report: TD-2026-03-12-input-order-contract-and-rts-local-input

Date: 2026-03-12
Reporter: Codex
Owner: Interaction/Core Input
Severity: P1
Scope: Cross-layer

## Trigger
- Scenario: validating interaction showcase delivery against the external interaction SSOT.
- Entry point: `src/Core/Input/Orders/InputOrderMappingSystem.cs` and `mods/RtsDemoMod`.
- Repro steps:
  1. Configure a held `StartEnd` mapping with only a base order type key.
  2. Observe `.End` silently falling back to the base order type.
  3. Launch `RtsDemoMod` and inspect local input initialization.
  4. Observe missing `assets/Input/input_order_mappings.json`, missing `assets/Input/default_input.json`, and missing RTS startup context.

## Evidence
- `src/Core/Input/Orders/InputOrderMappingSystem.cs`
- `mods/CoreInputMod/Systems/LocalOrderSourceHelper.cs`
- `mods/RtsDemoMod/assets/game.json`
- `mods/RtsDemoMod/assets/Input/default_input.json`
- `mods/RtsDemoMod/assets/Input/input_order_mappings.json`
- `src/Tests/GasTests/InputOrderContractTests.cs`

## Impact
- User-visible impact: held abilities can dispatch the wrong order type; RTS demo local hotkeys fail to initialize.
- Correctness/stability risk: input/order contract drift stays hidden until runtime and breaks interaction parity.
- Blast radius: all mods using `InputOrderMappingSystem` or `LocalOrderSourceHelper`.

## Fuse Decision
- Mode: hard-stop
- Reason: missing suffixed order registrations and missing RTS gameplay input are configuration defects and must fail explicitly instead of degrading via fallback.
- Observability fields: targeted regression tests, explicit asset presence, explicit startup context.

## Containment and Follow-up
- Immediate containment: remove the hidden suffixed-key fallback, add explicit RTS input assets, wire RTS startup context, and resolve `moveTo` in shared local input wiring.
- Permanent fix direction: keep input/order contracts strict and require every mod using local input to ship a complete input asset set.
- Target milestone: fixed in `feat/mod-interaction-showcase`; keep future mods gated by regression tests.
