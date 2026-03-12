using System;
using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using CameraAcceptanceMod.Runtime;
using CoreInputMod.ViewMode;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Input.Selection;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Scripting;
using Ludots.Core.Systems;
using Ludots.UI;
using Ludots.UI.Compose;
using Ludots.UI.Reactive;
using Ludots.UI.Runtime;

namespace CameraAcceptanceMod.UI
{
    internal sealed class CameraAcceptancePanelController
    {
        private const float PanelWidth = 500f;
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
                Ui.Text($"Viewport: {state.VisibleSummary}").FontSize(13f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal),
                Ui.Text("Scenarios").FontSize(12f).Bold().Color("#F4C77D"),
                Ui.Row(
                    BuildMapButton("Proj", state.MapId == CameraAcceptanceIds.ProjectionMapId, CameraAcceptanceIds.ProjectionMapId),
                    BuildMapButton("RTS", state.MapId == CameraAcceptanceIds.RtsMapId, CameraAcceptanceIds.RtsMapId),
                    BuildMapButton("TPS", state.MapId == CameraAcceptanceIds.TpsMapId, CameraAcceptanceIds.TpsMapId),
                    BuildMapButton("Blend", state.MapId == CameraAcceptanceIds.BlendMapId, CameraAcceptanceIds.BlendMapId),
                    BuildMapButton("Follow", state.MapId == CameraAcceptanceIds.FollowMapId, CameraAcceptanceIds.FollowMapId),
                    BuildMapButton("Stack", state.MapId == CameraAcceptanceIds.StackMapId, CameraAcceptanceIds.StackMapId))
                    .Wrap()
                    .Gap(8f),
                Ui.Text("Actions").FontSize(12f).Bold().Color("#F4C77D"),
                BuildScenarioActions(state),
                BuildSelectedIdsSection(state.SelectedIds),
                Ui.Text("How To Verify").FontSize(12f).Bold().Color("#F4C77D"),
                Ui.Text(state.ControlsDescription).FontSize(12f).Color("#8EA2BD").WhiteSpace(UiWhiteSpace.Normal)
            };

            return Ui.Card(children.ToArray()).Width(PanelWidth)
                .Padding(16f)
                .Gap(10f)
                .Radius(18f)
                .Background("#101A29")
                .Absolute(16f, 16f)
                .ZIndex(20);
        }

        private static UiElementBuilder BuildSelectedIdsSection(IReadOnlyList<string> selectedIds)
        {
            var children = new List<UiElementBuilder>
            {
                Ui.Text("Selection Buffer").FontSize(12f).Bold().Color("#F4C77D")
            };

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
                    Ui.Row(
                        BuildActionButton("Move Captain", false, ToggleCaptainPosition))
                        .Wrap()
                        .Gap(8f))
                    .Gap(8f),
                CameraAcceptanceIds.StackMapId => Ui.Row(
                    BuildActionButton("Reveal", false, () => RequestVirtualCamera(CameraAcceptanceIds.StackRevealShotId, clear: false)),
                    BuildActionButton("Alert", false, () => RequestVirtualCamera(CameraAcceptanceIds.StackAlertShotId, clear: false)),
                    BuildActionButton("Clear", false, () => RequestVirtualCamera(id: null, clear: true)))
                    .Wrap()
                    .Gap(8f),
                CameraAcceptanceIds.RtsMapId => Ui.Row(
                    BuildActionButton("RTS Mode", state.ActiveModeId == CameraAcceptanceIds.RtsModeId, () => SwitchViewMode(CameraAcceptanceIds.RtsModeId)))
                    .Wrap()
                    .Gap(8f),
                CameraAcceptanceIds.TpsMapId => Ui.Row(
                    BuildActionButton("TPS Mode", state.ActiveModeId == CameraAcceptanceIds.TpsModeId, () => SwitchViewMode(CameraAcceptanceIds.TpsModeId)))
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
                ResolveVisibleEntitySummary(engine),
                ResolveActiveBlendCameraId(engine));
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
                !string.Equals(left.VisibleSummary, right.VisibleSummary, StringComparison.Ordinal) ||
                !string.Equals(left.ActiveBlendCameraId, right.ActiveBlendCameraId, StringComparison.Ordinal))
            {
                return false;
            }

            if (left.SelectedIds.Length != right.SelectedIds.Length)
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

        private static string FormatVector(Vector2? value)
        {
            if (!value.HasValue)
            {
                return "none";
            }

            return $"{value.Value.X:0},{value.Value.Y:0}";
        }

        private static string ResolveVisibleEntitySummary(GameEngine engine)
        {
            int visibleCount = 0;
            if (engine.GlobalContext.TryGetValue(CoreServiceKeys.CameraCullingDebugState.Name, out var cullingObj) &&
                cullingObj is CameraCullingDebugState cullingState)
            {
                visibleCount = cullingState.VisibleEntityCount;
            }

            string[] names = new string[6];
            int nameCount = 0;
            var query = new QueryDescription().WithAll<Name, CullState>();
            engine.World.Query(in query, (Entity entity, ref Name name, ref CullState cull) =>
            {
                if (!cull.IsVisible || nameCount >= names.Length)
                {
                    return;
                }

                names[nameCount++] = name.Value;
            });

            if (nameCount == 0)
            {
                return visibleCount > 0 ? $"{visibleCount} visible" : "no visible entities";
            }

            string joined = string.Join(", ", names, 0, nameCount);
            return visibleCount > 0 ? $"{visibleCount} visible: {joined}" : joined;
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
            string VisibleSummary,
            string ActiveBlendCameraId)
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
                "no visible entities",
                CameraAcceptanceIds.BlendSmoothCameraId);
        }
    }
}
