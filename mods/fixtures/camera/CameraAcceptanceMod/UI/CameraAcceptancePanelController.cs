using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Arch.Core;
using CameraAcceptanceMod.Runtime;
using CoreInputMod.ViewMode;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Input.Selection;
using Ludots.Core.Presentation.Camera;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Scripting;
using Ludots.UI;
using Ludots.UI.Compose;
using Ludots.UI.Reactive;
using Ludots.UI.Runtime;

namespace CameraAcceptanceMod.UI
{
    internal sealed class CameraAcceptancePanelController
    {
        private const float PanelWidth = 520f;
        private static readonly Vector2 CaptainOriginCm = new(3400f, 2200f);
        private static readonly Vector2 CaptainMovedCm = new(4200f, 2800f);

        private readonly ReactivePage<CameraAcceptancePanelState> _page;
        private CameraAcceptancePanelState _lastState = CameraAcceptancePanelState.Empty;
        private GameEngine? _engine;

        public CameraAcceptancePanelController()
        {
            _page = new ReactivePage<CameraAcceptancePanelState>(CameraAcceptancePanelState.Empty, BuildRoot);
        }

        public UiScene Scene => _page.Scene;

        public bool MountOrSync(UIRoot root, GameEngine engine)
        {
            ArgumentNullException.ThrowIfNull(root);
            ArgumentNullException.ThrowIfNull(engine);

            _engine = engine;

            bool changed = false;
            if (!ReferenceEquals(root.Scene, _page.Scene))
            {
                root.MountScene(_page.Scene);
                root.IsDirty = true;
                changed = true;
            }

            if (ApplyStateSnapshot(engine))
            {
                root.IsDirty = true;
                changed = true;
            }

            return changed;
        }

        public void ClearIfOwned(UIRoot root)
        {
            ArgumentNullException.ThrowIfNull(root);

            if (ReferenceEquals(root.Scene, _page.Scene))
            {
                root.ClearScene();
            }

            _lastState = CameraAcceptancePanelState.Empty;
            _page.SetState(_ => CameraAcceptancePanelState.Empty);
            _engine = null;
        }

        private UiElementBuilder BuildRoot(ReactiveContext<CameraAcceptancePanelState> context)
        {
            CameraAcceptancePanelState state = context.State;
            if (string.IsNullOrWhiteSpace(state.MapId))
            {
                return Ui.Card(
                        Ui.Text("Camera Acceptance").FontSize(22f).Bold().Color("#F7FAFF"),
                        Ui.Text("No active acceptance map.").FontSize(13f).Color("#8EA2BD"))
                    .Width(PanelWidth)
                    .Padding(16f)
                    .Gap(10f)
                    .Radius(18f)
                    .Background("#101A29")
                    .Absolute(16f, 16f)
                    .ZIndex(20);
            }

            var children = new List<UiElementBuilder>
            {
                Ui.Text("Camera Acceptance").FontSize(22f).Bold().Color("#F7FAFF"),
                Ui.Text(state.MapDescription).FontSize(14f).Color("#D0D8E6").WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text($"Map: {state.MapId}").FontSize(13f).Color("#8EA2BD"),
                Ui.Text($"Camera: {state.ActiveCameraId}").FontSize(13f).Color("#8EA2BD"),
                Ui.Text($"Mode: {state.ActiveModeId}").FontSize(13f).Color("#8EA2BD"),
                Ui.Text($"Selection: {state.SelectedName}").FontSize(13f).Color("#8EA2BD"),
                Ui.Text($"Selected IDs: {state.SelectedIdsSummary}").FontSize(13f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text($"Follow Target: {state.FollowTarget}").FontSize(13f).Color("#8EA2BD"),
                Ui.Text("Viewport telemetry: native retained diagnostics card").FontSize(13f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text("Scenarios").FontSize(12f).Bold().Color("#F4C77D"),
                Ui.Row(
                        BuildMapButton("Proj", state.MapId == CameraAcceptanceIds.ProjectionMapId, CameraAcceptanceIds.ProjectionMapId),
                        BuildMapButton("Hotpath", state.MapId == CameraAcceptanceIds.HotpathMapId, CameraAcceptanceIds.HotpathMapId),
                        BuildMapButton("RTS", state.MapId == CameraAcceptanceIds.RtsMapId, CameraAcceptanceIds.RtsMapId),
                        BuildMapButton("TPS", state.MapId == CameraAcceptanceIds.TpsMapId, CameraAcceptanceIds.TpsMapId),
                        BuildMapButton("Blend", state.MapId == CameraAcceptanceIds.BlendMapId, CameraAcceptanceIds.BlendMapId),
                        BuildMapButton("Follow", state.MapId == CameraAcceptanceIds.FollowMapId, CameraAcceptanceIds.FollowMapId),
                        BuildMapButton("Stack", state.MapId == CameraAcceptanceIds.StackMapId, CameraAcceptanceIds.StackMapId))
                    .Wrap()
                    .Gap(8f),
                Ui.Text("Actions").FontSize(12f).Bold().Color("#F4C77D"),
                BuildScenarioActions(state)
            };

            if (string.Equals(state.MapId, CameraAcceptanceIds.ProjectionMapId, StringComparison.OrdinalIgnoreCase))
            {
                children.Add(Ui.Text($"Projection Spawn Batch: {state.ProjectionSpawnCount}").FontSize(13f).Color("#8EA2BD"));
            }

            if (string.Equals(state.MapId, CameraAcceptanceIds.HotpathMapId, StringComparison.OrdinalIgnoreCase))
            {
                children.Add(BuildHotpathControls(state));
                children.Add(BuildHotpathVisibleWindow(state));
            }

            children.Add(BuildSelectedIdsSection(state.SelectedIds));
            children.Add(Ui.Text("How To Verify").FontSize(12f).Bold().Color("#F4C77D"));
            children.Add(Ui.Text(state.ControlsDescription).FontSize(12f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal));

            UiElementBuilder mainPanel = Ui.Card(children.ToArray()).Width(PanelWidth)
                .Padding(16f)
                .Gap(10f)
                .Radius(18f)
                .Background("#101A29")
                .Absolute(16f, 16f)
                .ZIndex(20);

            if (state.DiagnosticsLines.Length != 0)
            {
                mainPanel.Child(
                    BuildDiagnosticsSection(state)
                        .Absolute(PanelWidth + 16f, 0f)
                        .ZIndex(50));
            }

            return mainPanel;
        }

        private static UiElementBuilder BuildDiagnosticsSection(CameraAcceptancePanelState state)
        {
            var children = new List<UiElementBuilder>
            {
                Ui.Text("Native Diagnostics").FontSize(14f).Bold().Color("#F7FAFF")
            };

            for (int i = 0; i < state.DiagnosticsLines.Length; i++)
            {
                children.Add(
                    Ui.Text(state.DiagnosticsLines[i])
                        .FontSize(i == 0 ? 14f : 12f)
                        .Color(i == 0 ? "#F7FAFF" : "#C7D3E1")
                        .WhiteSpace(UiWhiteSpace.Normal));
            }

            return Ui.Card(children.ToArray())
                .Padding(14f)
                .Gap(6f)
                .Radius(16f)
                .Background("#08111BE8")
                .ZIndex(30);
        }

        private UiElementBuilder BuildHotpathControls(CameraAcceptancePanelState state)
        {
            return Ui.Column(
                    Ui.Text("Presentation Hotpath").FontSize(12f).Bold().Color("#F4C77D"),
                    Ui.Text($"HUD {OnOff(state.DiagnosticsHudEnabled)} | Labels {OnOff(state.SelectionTextEnabled)} | Bars {OnOff(state.HotpathBarsEnabled)} | Text {OnOff(state.HotpathHudTextEnabled)}")
                        .FontSize(12f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal),
                    Ui.Text($"Terrain {OnOff(state.TerrainEnabled)} | Prims {OnOff(state.PrimitivesEnabled)} | Crowd {OnOff(state.HotpathCullCrowdEnabled)}")
                        .FontSize(12f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal),
                    Ui.Text($"Crowd {state.HotpathCrowdCount} | Visible {state.HotpathVisibleCrowdCount} | Labels {state.HotpathSelectionLabelCount} | Bars {state.HotpathBarItemCount} | Text {state.HotpathHudTextItemCount}")
                        .FontSize(12f).Color("#C7D3E1").WhiteSpace(UiWhiteSpace.Normal),
                    Ui.Text($"Sweep {state.HotpathSweepPhase} | Cycle {state.HotpathSweepCycle} | Target {state.HotpathSweepTarget} | Sample stride {state.HotpathVisibleSampleStride}")
                        .FontSize(12f).Color("#C7D3E1").WhiteSpace(UiWhiteSpace.Normal),
                    Ui.Row(
                            BuildActionButton("Panel", state.PanelEnabled, TogglePanel),
                            BuildActionButton("HUD", state.DiagnosticsHudEnabled, ToggleDiagnosticsHud),
                            BuildActionButton("Labels", state.SelectionTextEnabled, ToggleSelectionText),
                            BuildActionButton("Bars", state.HotpathBarsEnabled, ToggleHotpathBars))
                        .Wrap().Gap(8f),
                    Ui.Row(
                            BuildActionButton("Text", state.HotpathHudTextEnabled, ToggleHotpathHudText),
                            BuildActionButton("Terrain", state.TerrainEnabled, ToggleTerrain),
                            BuildActionButton("Prims", state.PrimitivesEnabled, TogglePrimitives),
                            BuildActionButton("Crowd", state.HotpathCullCrowdEnabled, ToggleHotpathCullCrowd))
                        .Wrap().Gap(8f))
                .Gap(8f);
        }

        private static UiElementBuilder BuildHotpathVisibleWindow(CameraAcceptancePanelState state)
        {
            var children = new List<UiElementBuilder>
            {
                Ui.Text("Visible Sample Window").FontSize(12f).Bold().Color("#F4C77D"),
                Ui.Text($"Showing {state.HotpathVisibleSampleWindow.Length} sampled rows out of {state.HotpathVisibleCrowdCount} visible entities (stride {state.HotpathVisibleSampleStride}).")
                    .FontSize(12f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal)
            };

            if (state.HotpathVisibleSampleWindow.Length == 0)
            {
                children.Add(Ui.Text("No visible crowd entities in the current sweep window.").FontSize(12f).Color("#8EA2BD"));
            }
            else
            {
                for (int i = 0; i < state.HotpathVisibleSampleWindow.Length; i++)
                {
                    children.Add(Ui.Text(state.HotpathVisibleSampleWindow[i]).FontSize(12f).Color("#D0D8E6"));
                }
            }

            return Ui.Column(children.ToArray()).Gap(4f).Padding(10f).Radius(12f).Background("#121B29");
        }

        private static UiElementBuilder BuildSelectedIdsSection(IReadOnlyList<string> selectedIds)
        {
            var children = new List<UiElementBuilder> { Ui.Text("Selection Buffer").FontSize(12f).Bold().Color("#F4C77D") };
            if (selectedIds.Count == 0)
            {
                children.Add(Ui.Text("none").FontSize(12f).Color("#8EA2BD"));
            }
            else
            {
                for (int i = 0; i < selectedIds.Count; i++)
                {
                    children.Add(Ui.Text(selectedIds[i]).FontSize(12f).Color("#D0D8E6"));
                }
            }

            return Ui.Column(children.ToArray()).Gap(4f);
        }

        private UiElementBuilder BuildScenarioActions(CameraAcceptancePanelState state)
        {
            return state.MapId switch
            {
                CameraAcceptanceIds.ProjectionMapId => Ui.Text(
                        $"Left click ground to spawn a random scatter batch. Q/E adjusts the batch by {CameraAcceptanceIds.ProjectionSpawnCountStep}; current batch = {state.ProjectionSpawnCount}.")
                    .FontSize(12f)
                    .Color("#8EA2BD")
                    .WhiteSpace(UiWhiteSpace.Normal),
                CameraAcceptanceIds.HotpathMapId => Ui.Text(
                        $"This scene deterministically fills {CameraAcceptanceIds.HotpathCrowdTargetCount} dummies over multiple frames, then sweeps the camera back and forth automatically while the panel samples visible entities.")
                    .FontSize(12f)
                    .Color("#8EA2BD")
                    .WhiteSpace(UiWhiteSpace.Normal),
                CameraAcceptanceIds.BlendMapId => Ui.Row(
                        BuildActionButton("Cut", state.ActiveBlendCameraId == CameraAcceptanceIds.BlendCutCameraId, () => SetBlendCamera(CameraAcceptanceIds.BlendCutCameraId)),
                        BuildActionButton("Linear", state.ActiveBlendCameraId == CameraAcceptanceIds.BlendLinearCameraId, () => SetBlendCamera(CameraAcceptanceIds.BlendLinearCameraId)),
                        BuildActionButton("Smooth", state.ActiveBlendCameraId == CameraAcceptanceIds.BlendSmoothCameraId, () => SetBlendCamera(CameraAcceptanceIds.BlendSmoothCameraId)))
                    .Wrap()
                    .Gap(8f),
                CameraAcceptanceIds.FollowMapId => Ui.Column(
                        Ui.Row(
                                BuildActionButton("Close", state.ActiveModeId == CameraAcceptanceIds.FollowCloseModeId, () => SwitchViewMode(CameraAcceptanceIds.FollowCloseModeId)),
                                BuildActionButton("Wide", state.ActiveModeId == CameraAcceptanceIds.FollowWideModeId, () => SwitchViewMode(CameraAcceptanceIds.FollowWideModeId)))
                            .Wrap()
                            .Gap(8f),
                        Ui.Row(BuildActionButton("Move Captain", false, ToggleCaptainPosition))
                            .Wrap()
                            .Gap(8f))
                    .Gap(8f),
                CameraAcceptanceIds.StackMapId => Ui.Row(
                        BuildActionButton("Reveal", false, () => RequestVirtualCamera(CameraAcceptanceIds.StackRevealShotId, clear: false)),
                        BuildActionButton("Alert", false, () => RequestVirtualCamera(CameraAcceptanceIds.StackAlertShotId, clear: false)),
                        BuildActionButton("Clear", false, () => RequestVirtualCamera(id: null, clear: true)))
                    .Wrap()
                    .Gap(8f),
                CameraAcceptanceIds.RtsMapId => Ui.Row(BuildActionButton("RTS Mode", state.ActiveModeId == CameraAcceptanceIds.RtsModeId, () => SwitchViewMode(CameraAcceptanceIds.RtsModeId)))
                    .Wrap()
                    .Gap(8f),
                CameraAcceptanceIds.TpsMapId => Ui.Row(BuildActionButton("TPS Mode", state.ActiveModeId == CameraAcceptanceIds.TpsModeId, () => SwitchViewMode(CameraAcceptanceIds.TpsModeId)))
                    .Wrap()
                    .Gap(8f),
                _ => Ui.Text("Interact directly in world view for this scenario.").FontSize(12f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal)
            };
        }

        private static UiElementBuilder BuildActionButton(string label, bool active, Action onClick)
        {
            return Ui.Button(label, _ => onClick())
                .Padding(10f, 8f)
                .Radius(10f)
                .Background(active ? "#5B441A" : "#121B29")
                .Color("#F7FAFF");
        }

        private UiElementBuilder BuildMapButton(string label, bool active, string mapId)
        {
            return Ui.Button(label, _ => LoadAcceptanceMap(mapId))
                .Padding(10f, 8f)
                .Radius(999f)
                .Background(active ? "#244E66" : "#182436")
                .Color(active ? "#F7FAFF" : "#C7D3E1");
        }

        private bool ApplyStateSnapshot(GameEngine engine)
        {
            CameraAcceptancePanelState next = CaptureState(engine);
            if (StateEquals(_lastState, next))
            {
                return false;
            }

            _lastState = next;
            _page.SetState(_ => next);
            return true;
        }

        private CameraAcceptancePanelState CaptureState(GameEngine engine)
        {
            string mapId = engine.CurrentMapSession?.MapId.Value ?? string.Empty;
            if (!CameraAcceptanceIds.IsAcceptanceMap(mapId))
            {
                return CameraAcceptancePanelState.Empty;
            }

            string[] selectedIds = ResolveSelectedEntityIds(engine);
            CameraAcceptanceDiagnosticsState? diagnostics = engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState);
            RenderDebugState? renderDebug = engine.GetService(CoreServiceKeys.RenderDebugState);
            string[] diagnosticsLines = BuildDiagnosticsLines(engine, mapId, diagnostics, renderDebug);

            return new CameraAcceptancePanelState(
                mapId,
                CameraAcceptanceIds.DescribeMap(mapId),
                CameraAcceptanceIds.DescribeControls(mapId),
                engine.GameSession.Camera.VirtualCameraBrain?.ActiveCameraId ?? "none",
                ResolveActiveModeId(engine),
                ResolveSelectedEntityName(engine) ?? "none",
                selectedIds.Length == 0 ? "none" : string.Join(", ", selectedIds),
                selectedIds,
                FormatVector(engine.GameSession.Camera.FollowTargetPositionCm),
                ResolveActiveBlendCameraId(engine),
                CameraAcceptanceRuntime.ResolveProjectionSpawnCount(engine),
                renderDebug?.DrawSkiaUi ?? true,
                diagnostics?.HudEnabled ?? true,
                diagnostics?.TextEnabled ?? true,
                diagnostics?.HotpathBarsEnabled ?? true,
                diagnostics?.HotpathHudTextEnabled ?? true,
                renderDebug?.DrawTerrain ?? true,
                renderDebug?.DrawPrimitives ?? true,
                diagnostics?.HotpathCullCrowdEnabled ?? true,
                diagnostics?.HotpathCrowdCount ?? 0,
                diagnostics?.HotpathVisibleCrowdCount ?? 0,
                diagnostics?.HotpathBarItemCount ?? 0,
                diagnostics?.HotpathHudTextItemCount ?? 0,
                diagnostics?.HotpathSelectionLabelCount ?? 0,
                diagnostics?.HotpathVisibleSampleStride ?? 1,
                diagnostics?.HotpathSweepPhase ?? "inactive",
                diagnostics?.HotpathSweepCycle ?? 0,
                diagnostics?.HotpathSweepTarget ?? "none",
                diagnostics?.HotpathVisibleSampleWindow ?? Array.Empty<string>(),
                diagnosticsLines);
        }

        private static string[] BuildDiagnosticsLines(
            GameEngine engine,
            string mapId,
            CameraAcceptanceDiagnosticsState? diagnostics,
            RenderDebugState? renderDebug)
        {
            if (diagnostics == null || renderDebug == null || !diagnostics.HudEnabled)
            {
                return Array.Empty<string>();
            }

            long start = Stopwatch.GetTimestamp();
            var lines = new List<string>(10)
            {
                $"Camera Acceptance | FPS={diagnostics.SmoothedFps:F1} | Frame={diagnostics.SmoothedFrameMs:F2}ms",
                $"F6 Panel[{OnOff(renderDebug.DrawSkiaUi)}]  F7 HUD[{OnOff(diagnostics.HudEnabled)}]  F8 Labels[{OnOff(diagnostics.TextEnabled)}]"
            };

            if (string.Equals(mapId, CameraAcceptanceIds.HotpathMapId, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"Build panel={diagnostics.PanelSyncMs:F2}ms  diagHud={diagnostics.HudBuildMs:F2}ms  labels={diagnostics.TextBuildMs:F2}ms  sample={diagnostics.HotpathVisibleSampleMs:F2}ms");
                lines.Add($"Build bars={diagnostics.HotpathBarBuildMs:F2}ms  hudText={diagnostics.HotpathHudTextBuildMs:F2}ms  prims={diagnostics.HotpathPrimitiveBuildMs:F2}ms");
                lines.Add($"F9 Bars[{OnOff(diagnostics.HotpathBarsEnabled)}]  F10 Text[{OnOff(diagnostics.HotpathHudTextEnabled)}]  F11 Terrain[{OnOff(renderDebug.DrawTerrain)}]  F12 Prim[{OnOff(renderDebug.DrawPrimitives)}]  C Crowd[{OnOff(diagnostics.HotpathCullCrowdEnabled)}]");
                lines.Add($"Hotpath crowd={diagnostics.HotpathCrowdCount}  visible={diagnostics.HotpathVisibleCrowdCount}  bars={diagnostics.HotpathBarItemCount}  hudText={diagnostics.HotpathHudTextItemCount}  labels={diagnostics.HotpathSelectionLabelCount}  stride={diagnostics.HotpathVisibleSampleStride}");
                lines.Add($"Sweep phase={diagnostics.HotpathSweepPhase}  cycle={diagnostics.HotpathSweepCycle}  target={diagnostics.HotpathSweepTarget}");
            }
            else
            {
                lines.Add($"Build panel={diagnostics.PanelSyncMs:F2}ms  hud={diagnostics.HudBuildMs:F2}ms  text={diagnostics.TextBuildMs:F2}ms");
            }

            if (engine.GetService(CoreServiceKeys.PresentationTimingDiagnostics) is PresentationTimingDiagnostics timings)
            {
                lines.Add($"Adapter uiIn={timings.UiInputMs:F2}ms  uiRender={timings.UiRenderMs:F2}ms  uiUpload={timings.UiUploadMs:F2}ms");
                lines.Add($"Adapter overlayDraw={timings.ScreenOverlayDrawMs:F2}ms");
                lines.Add($"Core cull={timings.CameraCullingMs:F2}ms  vis={timings.VisibleEntitiesLastFrame}  cam={timings.CameraPresenterMs:F2}ms  hudProj={timings.WorldHudProjectionMs:F2}ms");
                lines.Add($"Terrain render={timings.TerrainRenderMs:F2}ms  build={timings.TerrainChunkBuildMs:F2}ms  chunks={timings.TerrainChunksDrawnLastFrame}  built={timings.TerrainChunksBuiltLastFrame}");
                lines.Add($"Primitive draw={timings.PrimitiveRenderMs:F2}ms  instances={timings.PrimitiveInstancesLastFrame}  batches={timings.PrimitiveBatchesLastFrame}");
            }

            if (string.Equals(mapId, CameraAcceptanceIds.ProjectionMapId, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"Spawn Batch={CameraAcceptanceRuntime.ResolveProjectionSpawnCount(engine)} | Q/E +/-{CameraAcceptanceIds.ProjectionSpawnCountStep}");
            }

            diagnostics.ObserveHudBuild((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            return lines.ToArray();
        }

        private void LoadAcceptanceMap(string mapId)
        {
            GameEngine engine = RequireEngine();
            string? currentMapId = engine.CurrentMapSession?.MapId.Value;
            if (string.Equals(currentMapId, mapId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (CameraAcceptanceIds.IsAcceptanceMap(currentMapId))
            {
                engine.UnloadMap(currentMapId!);
            }

            engine.LoadMap(mapId);
            SyncMountedRoot();
        }

        private void SwitchViewMode(string modeId)
        {
            GameEngine engine = RequireEngine();
            if (ResolveViewModeManager(engine) is ViewModeManager viewModeManager)
            {
                viewModeManager.SwitchTo(modeId);
                SyncMountedRoot();
            }
        }

        private void SetBlendCamera(string cameraId)
        {
            GameEngine engine = RequireEngine();
            engine.GlobalContext[CameraAcceptanceIds.ActiveBlendCameraIdKey] = cameraId;
            SyncMountedRoot();
        }

        private void RequestVirtualCamera(string? id, bool clear)
        {
            GameEngine engine = RequireEngine();
            engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
            {
                Id = id ?? string.Empty,
                Clear = clear
            });
        }

        private void TogglePanel()
        {
            GameEngine engine = RequireEngine();
            if (engine.GetService(CoreServiceKeys.RenderDebugState) is not RenderDebugState renderDebug)
            {
                return;
            }

            renderDebug.DrawSkiaUi = !renderDebug.DrawSkiaUi;
            if (!renderDebug.DrawSkiaUi && engine.GetService(CoreServiceKeys.UIRoot) is UIRoot root)
            {
                ClearIfOwned(root);
                return;
            }

            SyncMountedRoot();
        }

        private void ToggleDiagnosticsHud()
        {
            if (TryGetDiagnosticsState(RequireEngine(), out var diagnostics))
            {
                diagnostics.HudEnabled = !diagnostics.HudEnabled;
                SyncMountedRoot();
            }
        }

        private void ToggleSelectionText()
        {
            if (TryGetDiagnosticsState(RequireEngine(), out var diagnostics))
            {
                diagnostics.TextEnabled = !diagnostics.TextEnabled;
                SyncMountedRoot();
            }
        }

        private void ToggleHotpathBars()
        {
            if (TryGetDiagnosticsState(RequireEngine(), out var diagnostics))
            {
                diagnostics.HotpathBarsEnabled = !diagnostics.HotpathBarsEnabled;
                SyncMountedRoot();
            }
        }

        private void ToggleHotpathHudText()
        {
            if (TryGetDiagnosticsState(RequireEngine(), out var diagnostics))
            {
                diagnostics.HotpathHudTextEnabled = !diagnostics.HotpathHudTextEnabled;
                SyncMountedRoot();
            }
        }

        private void ToggleTerrain()
        {
            GameEngine engine = RequireEngine();
            if (engine.GetService(CoreServiceKeys.RenderDebugState) is RenderDebugState renderDebug)
            {
                renderDebug.DrawTerrain = !renderDebug.DrawTerrain;
                SyncMountedRoot();
            }
        }

        private void TogglePrimitives()
        {
            GameEngine engine = RequireEngine();
            if (engine.GetService(CoreServiceKeys.RenderDebugState) is RenderDebugState renderDebug)
            {
                renderDebug.DrawPrimitives = !renderDebug.DrawPrimitives;
                SyncMountedRoot();
            }
        }

        private void ToggleHotpathCullCrowd()
        {
            if (TryGetDiagnosticsState(RequireEngine(), out var diagnostics))
            {
                diagnostics.HotpathCullCrowdEnabled = !diagnostics.HotpathCullCrowdEnabled;
                SyncMountedRoot();
            }
        }

        private void ToggleCaptainPosition()
        {
            GameEngine engine = RequireEngine();
            Entity entity = FindEntityByName(engine.World, CameraAcceptanceIds.CaptainName);
            if (entity == Entity.Null || !engine.World.Has<WorldPositionCm>(entity))
            {
                return;
            }

            ref var position = ref engine.World.Get<WorldPositionCm>(entity);
            Vector2 current = ToVector2(position);
            Vector2 next = Vector2.Distance(current, CaptainOriginCm) < 1f ? CaptainMovedCm : CaptainOriginCm;
            position = WorldPositionCm.FromCm((int)next.X, (int)next.Y);
            SyncMountedRoot();
        }

        private void SyncMountedRoot()
        {
            GameEngine engine = RequireEngine();
            if (engine.GetService(CoreServiceKeys.UIRoot) is not UIRoot root)
            {
                return;
            }

            if (!ReferenceEquals(root.Scene, _page.Scene))
            {
                return;
            }

            if (ApplyStateSnapshot(engine))
            {
                root.IsDirty = true;
            }
        }

        private GameEngine RequireEngine()
        {
            return _engine ?? throw new InvalidOperationException("CameraAcceptancePanelController is not bound to an engine.");
        }

        private static bool StateEquals(CameraAcceptancePanelState left, CameraAcceptancePanelState right)
        {
            if (!string.Equals(left.MapId, right.MapId, StringComparison.Ordinal) ||
                !string.Equals(left.MapDescription, right.MapDescription, StringComparison.Ordinal) ||
                !string.Equals(left.ControlsDescription, right.ControlsDescription, StringComparison.Ordinal) ||
                !string.Equals(left.ActiveCameraId, right.ActiveCameraId, StringComparison.Ordinal) ||
                !string.Equals(left.ActiveModeId, right.ActiveModeId, StringComparison.Ordinal) ||
                !string.Equals(left.SelectedName, right.SelectedName, StringComparison.Ordinal) ||
                !string.Equals(left.SelectedIdsSummary, right.SelectedIdsSummary, StringComparison.Ordinal) ||
                !string.Equals(left.FollowTarget, right.FollowTarget, StringComparison.Ordinal) ||
                !string.Equals(left.ActiveBlendCameraId, right.ActiveBlendCameraId, StringComparison.Ordinal) ||
                left.ProjectionSpawnCount != right.ProjectionSpawnCount ||
                left.PanelEnabled != right.PanelEnabled ||
                left.DiagnosticsHudEnabled != right.DiagnosticsHudEnabled ||
                left.SelectionTextEnabled != right.SelectionTextEnabled ||
                left.HotpathBarsEnabled != right.HotpathBarsEnabled ||
                left.HotpathHudTextEnabled != right.HotpathHudTextEnabled ||
                left.TerrainEnabled != right.TerrainEnabled ||
                left.PrimitivesEnabled != right.PrimitivesEnabled ||
                left.HotpathCullCrowdEnabled != right.HotpathCullCrowdEnabled ||
                left.HotpathCrowdCount != right.HotpathCrowdCount ||
                left.HotpathVisibleCrowdCount != right.HotpathVisibleCrowdCount ||
                left.HotpathBarItemCount != right.HotpathBarItemCount ||
                left.HotpathHudTextItemCount != right.HotpathHudTextItemCount ||
                left.HotpathSelectionLabelCount != right.HotpathSelectionLabelCount ||
                left.HotpathVisibleSampleStride != right.HotpathVisibleSampleStride ||
                left.HotpathSweepCycle != right.HotpathSweepCycle ||
                !string.Equals(left.HotpathSweepPhase, right.HotpathSweepPhase, StringComparison.Ordinal) ||
                !string.Equals(left.HotpathSweepTarget, right.HotpathSweepTarget, StringComparison.Ordinal) ||
                left.DiagnosticsLines.Length != right.DiagnosticsLines.Length)
            {
                return false;
            }

            if (left.SelectedIds.Length != right.SelectedIds.Length ||
                left.HotpathVisibleSampleWindow.Length != right.HotpathVisibleSampleWindow.Length)
            {
                return false;
            }

            for (int i = 0; i < left.SelectedIds.Length; i++)
            {
                if (!string.Equals(left.SelectedIds[i], right.SelectedIds[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int i = 0; i < left.HotpathVisibleSampleWindow.Length; i++)
            {
                if (!string.Equals(left.HotpathVisibleSampleWindow[i], right.HotpathVisibleSampleWindow[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int i = 0; i < left.DiagnosticsLines.Length; i++)
            {
                if (!string.Equals(left.DiagnosticsLines[i], right.DiagnosticsLines[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static ViewModeManager? ResolveViewModeManager(GameEngine engine)
        {
            if (engine.GlobalContext.TryGetValue(ViewModeManager.GlobalKey, out var managerObj) &&
                managerObj is ViewModeManager manager)
            {
                return manager;
            }

            return null;
        }

        private static string ResolveActiveModeId(GameEngine engine)
        {
            return engine.GlobalContext.TryGetValue(ViewModeManager.ActiveModeIdKey, out var modeObj) && modeObj is string modeId
                ? modeId
                : "map-default";
        }

        private static string ResolveActiveBlendCameraId(GameEngine engine)
        {
            return engine.GlobalContext.TryGetValue(CameraAcceptanceIds.ActiveBlendCameraIdKey, out var value) &&
                   value is string cameraId &&
                   !string.IsNullOrWhiteSpace(cameraId)
                ? cameraId
                : CameraAcceptanceIds.BlendSmoothCameraId;
        }

        private static string? ResolveSelectedEntityName(GameEngine engine)
        {
            if (!engine.GlobalContext.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var value) ||
                value is not Entity entity ||
                entity == Entity.Null ||
                !engine.World.IsAlive(entity) ||
                !engine.World.Has<Name>(entity))
            {
                return null;
            }

            return engine.World.Get<Name>(entity).Value;
        }

        private static string[] ResolveSelectedEntityIds(GameEngine engine)
        {
            Span<Entity> selected = stackalloc Entity[SelectionBuffer.CAPACITY];
            int count = CameraAcceptanceSelectionView.CopySelectedEntities(engine.World, engine.GlobalContext, selected);
            if (count <= 0)
            {
                return Array.Empty<string>();
            }

            string[] lines = new string[count];
            for (int i = 0; i < count; i++)
            {
                lines[i] = CameraAcceptanceSelectionView.FormatEntityId(selected[i]);
            }

            return lines;
        }

        private static bool TryGetDiagnosticsState(GameEngine engine, out CameraAcceptanceDiagnosticsState diagnostics)
        {
            if (engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState state)
            {
                diagnostics = state;
                return true;
            }

            diagnostics = null!;
            return false;
        }

        private static string FormatVector(Vector2? value)
        {
            if (!value.HasValue)
            {
                return "none";
            }

            return $"{value.Value.X:0},{value.Value.Y:0}";
        }

        private static Vector2 ToVector2(WorldPositionCm position)
        {
            var value = position.ToWorldCmInt2();
            return new Vector2(value.X, value.Y);
        }

        private static Entity FindEntityByName(World world, string name)
        {
            Entity result = Entity.Null;
            var query = new QueryDescription().WithAll<Name>();
            world.Query(in query, (Entity entity, ref Name entityName) =>
            {
                if (string.Equals(entityName.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    result = entity;
                }
            });

            return result;
        }

        private static string OnOff(bool value) => value ? "ON" : "OFF";

        private sealed record CameraAcceptancePanelState(
            string MapId,
            string MapDescription,
            string ControlsDescription,
            string ActiveCameraId,
            string ActiveModeId,
            string SelectedName,
            string SelectedIdsSummary,
            string[] SelectedIds,
            string FollowTarget,
            string ActiveBlendCameraId,
            int ProjectionSpawnCount,
            bool PanelEnabled,
            bool DiagnosticsHudEnabled,
            bool SelectionTextEnabled,
            bool HotpathBarsEnabled,
            bool HotpathHudTextEnabled,
            bool TerrainEnabled,
            bool PrimitivesEnabled,
            bool HotpathCullCrowdEnabled,
            int HotpathCrowdCount,
            int HotpathVisibleCrowdCount,
            int HotpathBarItemCount,
            int HotpathHudTextItemCount,
            int HotpathSelectionLabelCount,
            int HotpathVisibleSampleStride,
            string HotpathSweepPhase,
            int HotpathSweepCycle,
            string HotpathSweepTarget,
            string[] HotpathVisibleSampleWindow,
            string[] DiagnosticsLines)
        {
            public static CameraAcceptancePanelState Empty { get; } = new(
                string.Empty,
                string.Empty,
                string.Empty,
                "none",
                "map-default",
                "none",
                "none",
                Array.Empty<string>(),
                "none",
                CameraAcceptanceIds.BlendSmoothCameraId,
                CameraAcceptanceIds.ProjectionSpawnCountDefault,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                0,
                0,
                0,
                0,
                0,
                1,
                "inactive",
                0,
                "none",
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }
}
