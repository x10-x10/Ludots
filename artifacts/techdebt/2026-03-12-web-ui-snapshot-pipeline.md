# Tech Debt Fuse: Web UI Snapshot Pipeline Saturation

## Debt Id
- `TD-2026-03-12-web-ui-snapshot-pipeline`

## Trigger
- Real `camera_acceptance` web launch now has the correct backend path and reactive `UiScene` updates, but sustained interaction still feels heavy.
- The concrete user-visible symptom after correctness fixes is: performer/HUD flicker is reduced, state updates now arrive, but web runtime remains laggy and can become temporarily unresponsive under continuous presentation churn.

## Impact
- Scope: cross-layer
- Severity: high
- User-facing symptom:
  - `camera_acceptance` on web can hitch under ongoing world + HUD + UI updates
  - interaction correctness is preserved, but responsiveness is still below product quality
- Architectural risk:
  - current web path serializes large self-contained presentation snapshots over a lossy latest-frame transport
  - UI is redrawn from canvas snapshots on the browser main thread, so transport cost and render cost compound each other

## Evidence
- Correctness containment now lives in:
  - `src/Adapters/Web/Ludots.Adapter.Web/Streaming/PresentationExtractor.cs`
  - `src/Adapters/Web/Ludots.Adapter.Web/Services/WebInputBackend.cs`
  - `src/Core/Input/Systems/InputRuntimeSystem.cs`
  - `src/Client/Web/src/core/FrameDecoder.ts`
  - `src/Client/Web/src/rendering/HudRenderer.ts`
- Reactive panel stabilization now lives in:
  - `mods/fixtures/camera/CameraAcceptanceMod/UI/CameraAcceptancePanelController.cs`
  - `mods/fixtures/camera/CameraAcceptanceMod/Runtime/CameraAcceptanceRuntime.cs`
  - `src/Tests/ThreeCTests/CameraAcceptanceModTests.cs`
- Runtime evidence:
  - `artifacts/validation/web-raylib/web_camera_acceptance_fixed_before.png`
  - `artifacts/validation/web-raylib/web_camera_acceptance_fixed_after_click.png`
  - `artifacts/validation/web-raylib/web_camera_acceptance_soak_after_12s.png`
  - live health on 2026-03-12 showed `framesDropped` still present on the session while using full snapshots

## Gap Summary
- Current architecture keeps correctness by forcing full self-contained frames.
- Current transport still keeps only the latest pending frame per client.
- Current browser path still decodes and repaints world/HUD/UI on the main thread.
- Current UI overlay is immediate-mode canvas redraw from `UiScene` payloads, not a retained diff-applied scene graph.

## Fuse Decision
- Fuse mode: isolate
- Short-term containment:
  - keep web presentation on full self-contained snapshots
  - do not re-enable inter-frame delta on the current drop-latest transport
  - keep camera acceptance panel on one mounted reactive `UiScene` instead of per-frame remount
  - treat current web adapter as correctness-first, not performance-closed
- Explicitly not treated as complete fix:
  - this does not make the web launcher production-performant
  - this does not solve transport backpressure, snapshot size, or main-thread render saturation

## Permanent Fix Direction
1. Replace drop-latest frame delivery with an acked/paced presentation channel that supports keyframes plus reliable incremental sections.
2. Split presentation transport into independently paced streams:
   - world primitives / HUD
   - screen overlay
   - `UiScene` diff stream
3. Move web UI from full canvas snapshot redraw toward retained scene application using stable node ids and structural diffs.
4. Add backpressure and frame-budget observability:
   - encoded bytes per frame
   - decode/apply time
   - render time
   - dropped-frame counters per section
5. Add a browser soak benchmark for `camera_acceptance` that fails if tick progression stays alive but visible frame application stalls.

## Owner
- `Web Adapter + UI Runtime`

## Due Window
- before declaring the web launcher performance-acceptable for default player-facing use
