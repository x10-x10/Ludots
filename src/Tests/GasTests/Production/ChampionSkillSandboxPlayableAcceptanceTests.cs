using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Arch.Core;
using CoreInputMod.ViewMode;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Orders;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Input.Config;
using Ludots.Core.Input.Orders;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Input.Selection;
using Ludots.Core.Mathematics;
using Ludots.Core.Presentation.Camera;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Presentation.Components;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Presentation.Systems;
using Ludots.Core.Presentation.Utils;
using Ludots.Core.Scripting;
using Ludots.Core.Systems;
using Ludots.Core.UI.EntityCommandPanels;
using Ludots.Platform.Abstractions;
using Ludots.UI;
using Ludots.UI.Skia;
using NUnit.Framework;

namespace Ludots.Tests.GAS.Production
{
    [NonParallelizable]
    [TestFixture]
    public sealed class ChampionSkillSandboxPlayableAcceptanceTests
    {
        private const float DeltaTime = 1f / 60f;
        private const string MapId = "champion_skill_sandbox";
        private const string SmartCastModeId = "ChampionSkillSandbox.Mode.SmartCast";
        private const string IndicatorModeId = "ChampionSkillSandbox.Mode.Indicator";
        private const string PressReleaseModeId = "ChampionSkillSandbox.Mode.PressReleaseAim";
        private const string SandboxTacticalCameraId = "ChampionSkillSandbox.Camera.Tactical";
        private const string TestInputBackendKey = "Tests.ChampionSkillSandbox.InputBackend";
        private const string HeadlessCameraKey = "Tests.ChampionSkillSandbox.HeadlessCamera";

        private static readonly string[] AcceptanceMods =
        {
            "LudotsCoreMod",
            "CoreInputMod",
            "CameraProfilesMod",
            "EntityCommandPanelMod",
            "ChampionSkillSandboxMod"
        };
        private static readonly Vector2[] HoverProbeOffsets =
        {
            Vector2.Zero,
            new Vector2(0f, -24f),
            new Vector2(0f, 24f),
            new Vector2(-24f, 0f),
            new Vector2(24f, 0f),
            new Vector2(-36f, -36f),
            new Vector2(36f, -36f),
            new Vector2(-36f, 36f),
            new Vector2(36f, 36f),
            new Vector2(0f, -48f),
            new Vector2(0f, 48f),
            new Vector2(-48f, 0f),
            new Vector2(48f, 0f),
            new Vector2(-64f, -24f),
            new Vector2(64f, -24f),
            new Vector2(-64f, 24f),
            new Vector2(64f, 24f)
        };

        [Test]
        public void ChampionSkillSandbox_PlayableFlow_WritesAcceptanceArtifacts()
        {
            string repoRoot = FindRepoRoot();
            string artifactDir = Path.Combine(repoRoot, "artifacts", "acceptance", "champion-skill-sandbox");
            Directory.CreateDirectory(artifactDir);

            var timeline = new List<string>();
            var snapshots = new List<AcceptanceSnapshot>();
            var frameTimesMs = new List<double>();

            using var engine = CreateEngine();
            var overlays = engine.GetService(CoreServiceKeys.GroundOverlayBuffer)
                ?? throw new InvalidOperationException("GroundOverlayBuffer missing.");
            var primitives = engine.GetService(CoreServiceKeys.PresentationPrimitiveDrawBuffer)
                ?? throw new InvalidOperationException("PrimitiveDrawBuffer missing.");
            var worldHud = engine.GetService(CoreServiceKeys.PresentationWorldHudBuffer)
                ?? throw new InvalidOperationException("WorldHudBatchBuffer missing.");
            var toolbar = engine.GetService(CoreServiceKeys.EntityCommandPanelToolbarProvider)
                ?? throw new InvalidOperationException("Toolbar provider missing.");
            var backend = GetInputBackend(engine);

            LoadMap(engine, MapId, frameTimesMs);
            Assert.That(engine.TriggerManager.Errors.Count, Is.EqualTo(0));
            Assert.That(GetActiveModeId(engine), Is.EqualTo(SmartCastModeId));
            Assert.That(GetSelectedEntityName(engine), Is.EqualTo("Ezreal Alpha"));
            Assert.That(CountOverlays(overlays, GroundOverlayShape.Ring), Is.GreaterThan(0), "Sandbox should show a visible selection ring for the current focus unit.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "map_loaded");
            timeline.Add("[T+001] champion_skill_sandbox loaded | default mode Quick Cast | default focus Ezreal Alpha");

            SelectNamedEntity(engine, backend, "Ezreal Cooldown", frameTimesMs);
            var ezrealCooldownSlots = CopySelectedSlots(engine);
            Assert.That(ezrealCooldownSlots[3].Label, Is.EqualTo("Trueshot Barrage"));
            Assert.That(ezrealCooldownSlots[3].Flags, Does.Contain("Blocked"));
            Assert.That(CountOverlays(overlays, GroundOverlayShape.Ring), Is.GreaterThan(0), "Selection ring should survive champion switching.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "select_ezreal_cooldown");
            timeline.Add("[T+002] Select(Ezreal Cooldown) -> panel shows R blocked by cooldown state");

            SelectNamedEntity(engine, backend, "Garen Courage", frameTimesMs);
            var garenSlots = CopySelectedSlots(engine);
            Assert.That(garenSlots[1].Label, Is.EqualTo("Courage"));
            Assert.That(garenSlots[1].Flags, Does.Contain("Active"));
            Assert.That(CountOverlays(overlays, GroundOverlayShape.Ring), Is.GreaterThan(0), "Selection ring should stay visible on Garen.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "select_garen_courage");
            timeline.Add("[T+003] Select(Garen Courage) -> panel shows W active from toggle state");

            SelectNamedEntity(engine, backend, "Jayce Hammer", frameTimesMs);
            var jayceHammerSlots = CopySelectedSlots(engine);
            Assert.That(jayceHammerSlots[0].Label, Is.EqualTo("To The Skies!"));
            Assert.That(jayceHammerSlots[0].Flags, Does.Contain("FormOverride"));
            Assert.That(CountOverlays(overlays, GroundOverlayShape.Ring), Is.GreaterThan(0), "Selection ring should stay visible on Jayce.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "select_jayce_hammer");
            timeline.Add("[T+004] Select(Jayce Hammer) -> panel routes to hammer-form Q/W/E/R");

            SelectNamedEntity(engine, backend, "Ezreal Alpha", frameTimesMs);
            Vector2 ezrealStart = ReadPosition(engine.World, "Ezreal Alpha");
            Vector2 moveTargetScreen = GetGroundScreenFromWorld(engine, ezrealStart + new Vector2(220f, 0f));
            RightClickWorld(engine, backend, moveTargetScreen, frameTimesMs);
            TickUntil(
                engine,
                frameTimesMs,
                () => ReadPosition(engine.World, "Ezreal Alpha").X > ezrealStart.X + 80f,
                maxFrames: 32);
            Vector2 ezrealAfterMove = ReadPosition(engine.World, "Ezreal Alpha");
            Assert.That(ezrealAfterMove.X, Is.GreaterThan(ezrealStart.X + 80f), "Right-click move should let the selected champion create distance.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "move_reposition");
            timeline.Add($"[T+005] Ezreal Alpha.Move(RMB) -> X {ezrealStart.X:0} to {ezrealAfterMove.X:0} to create spacing");

            engine.GameSession.Camera.ApplyPose(new CameraPoseRequest
            {
                VirtualCameraId = SandboxTacticalCameraId,
                TargetCm = new Vector2(2860f, 1620f),
                DistanceCm = 6200f,
                Pitch = 62f,
                FovYDeg = 50f,
            });
            Tick(engine, 1, frameTimesMs);
            PressButton(engine, backend, "<Keyboard>/f4", frameTimesMs);
            TickUntil(
                engine,
                frameTimesMs,
                () => IsCameraNear(engine.GameSession.Camera.State, new Vector2(1850f, 980f), 3900f, 54f, 42f),
                maxFrames: 6);
            Tick(engine, 2, frameTimesMs);
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "camera_reset");
            timeline.Add("[T+006] Camera.Reset(F4) -> tactical view restored to sandbox default pose");

            float ezrealDistanceToDummy = ReadDistance(engine.World, "Ezreal Alpha", "Target Dummy A");
            Assert.That(ezrealDistanceToDummy, Is.LessThanOrEqualTo(840f), "Sandbox layout should keep Target Dummy A inside Ezreal Q range for the opening smart-cast proof.");
            float dummyHealthBeforeQ = ReadHealth(engine.World, "Target Dummy A");
            SetMouseWorld(engine, backend, GetEntityScreen(engine, "Target Dummy A"), frameTimesMs);
            PressButton(engine, backend, "<Keyboard>/q", frameTimesMs);
            Tick(engine, 4, frameTimesMs);
            float dummyHealthAfterQ = ReadHealth(engine.World, "Target Dummy A");
            Assert.That(
                dummyHealthAfterQ,
                Is.LessThan(dummyHealthBeforeQ),
                $"{BuildInputActionDiagnostics(engine, "SkillQ")} || {BuildAbilityDiagnostics(engine, "Ezreal Alpha")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)} || {BuildFeedbackDiagnostics(primitives, worldHud)} || distanceToDummy={ezrealDistanceToDummy:0.##}");
            Assert.That(CountPrimitiveMarkers(primitives), Is.GreaterThan(0), "Smart cast hit should emit visible pulse markers.");
            Assert.That(CountWorldHudItems(worldHud, WorldHudItemKind.Text), Is.GreaterThan(0), "Smart cast hit should emit visible world text feedback.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "smartcast_hit");
            timeline.Add($"[T+007] Ezreal Alpha.Cast(Mystic Shot) -> Target Dummy A | Hit | HP {dummyHealthBeforeQ:0} -> {dummyHealthAfterQ:0}");

            toolbar.Activate(IndicatorModeId);
            Tick(engine, 1, frameTimesMs);
            Assert.That(GetActiveModeId(engine), Is.EqualTo(IndicatorModeId));
            Vector2 dummyHoverPoint = FindHoverScreenPoint(engine, backend, "Target Dummy A", GetEntityScreen(engine, "Target Dummy A"), frameTimesMs);
            Assert.That(ReadHoveredEntityName(engine), Is.EqualTo("Target Dummy A"));
            SetMouseWorld(engine, backend, dummyHoverPoint, frameTimesMs);
            int baselineIndicatorLines = CountOverlays(overlays, GroundOverlayShape.Line);
            int baselineIndicatorRings = CountOverlays(overlays, GroundOverlayShape.Ring);
            HoldButton(engine, backend, "<Keyboard>/r", holdFrames: 2, frameTimesMs);
            Assert.That(
                CountOverlays(overlays, GroundOverlayShape.Line),
                Is.GreaterThan(baselineIndicatorLines),
                $"{BuildInputActionDiagnostics(engine, "SkillR")} || {BuildAbilityDiagnostics(engine, "Ezreal Alpha")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)}");
            TickUntil(
                engine,
                frameTimesMs,
                () => CountOverlays(overlays, GroundOverlayShape.Ring) > baselineIndicatorRings,
                maxFrames: 8);
            Assert.That(
                CountOverlays(overlays, GroundOverlayShape.Ring),
                Is.GreaterThan(baselineIndicatorRings),
                "Indicator hover should add a dedicated marker on the hovered target.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "indicator_hover_target");
            timeline.Add("[T+008] Indicator hover over Target Dummy A shows an extra target marker before release");
            float dummyHealthBeforeR = ReadHealth(engine.World, "Target Dummy A");
            ReleaseButton(engine, backend, "<Keyboard>/r", frameTimesMs);
            Tick(engine, 4, frameTimesMs);
            float dummyHealthAfterR = ReadHealth(engine.World, "Target Dummy A");
            Assert.That(
                dummyHealthAfterR,
                Is.LessThan(dummyHealthBeforeR),
                $"{BuildInputActionDiagnostics(engine, "SkillR")} || {BuildAbilityDiagnostics(engine, "Ezreal Alpha")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)} || {BuildFeedbackDiagnostics(primitives, worldHud)}");
            Assert.That(CountPrimitiveMarkers(primitives), Is.GreaterThan(0), "Indicator release hit should emit visible pulse markers.");
            Assert.That(CountWorldHudItems(worldHud, WorldHudItemKind.Text), Is.GreaterThan(0), "Indicator release hit should emit visible world text feedback.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "indicator_release_hit");
            timeline.Add($"[T+009] Indicator mode hold-release previews Trueshot Barrage, then fires on release | HP {dummyHealthBeforeR:0} -> {dummyHealthAfterR:0}");

            SelectNamedEntity(engine, backend, "Jayce Cannon", frameTimesMs);
            toolbar.Activate(PressReleaseModeId);
            Tick(engine, 1, frameTimesMs);
            Assert.That(GetActiveModeId(engine), Is.EqualTo(PressReleaseModeId));

            float jayceDistanceToDummy = ReadDistance(engine.World, "Jayce Cannon", "Target Dummy A");
            Assert.That(jayceDistanceToDummy, Is.LessThanOrEqualTo(880f), "Sandbox layout should keep Target Dummy A inside Jayce Cannon Q range for press-release confirm proof.");
            SetMouseWorld(engine, backend, GetEntityScreen(engine, "Target Dummy A"), frameTimesMs);
            int baselineAimLines = CountOverlays(overlays, GroundOverlayShape.Line);
            float dummyHealthBeforeCancel = ReadHealth(engine.World, "Target Dummy A");
            PressButton(engine, backend, "<Keyboard>/q", frameTimesMs);
            Tick(engine, 1, frameTimesMs);
            Assert.That(
                CountOverlays(overlays, GroundOverlayShape.Line),
                Is.GreaterThan(baselineAimLines),
                $"{BuildInputActionDiagnostics(engine, "SkillQ")} || {BuildAbilityDiagnostics(engine, "Jayce Cannon")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)} || distanceToDummy={jayceDistanceToDummy:0.##}");
            RightClickWorld(engine, backend, GetEntityScreen(engine, "Target Dummy A"), frameTimesMs);
            Tick(engine, 2, frameTimesMs);
            Assert.That(
                CountOverlays(overlays, GroundOverlayShape.Line),
                Is.EqualTo(baselineAimLines),
                $"{BuildInputActionDiagnostics(engine, "SkillQ")} || {BuildAbilityDiagnostics(engine, "Jayce Cannon")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)}");
            float dummyHealthAfterCancel = ReadHealth(engine.World, "Target Dummy A");
            Assert.That(dummyHealthAfterCancel, Is.EqualTo(dummyHealthBeforeCancel).Within(0.001f));

            PressButton(engine, backend, "<Keyboard>/q", frameTimesMs);
            Tick(engine, 1, frameTimesMs);
            Assert.That(
                CountOverlays(overlays, GroundOverlayShape.Line),
                Is.GreaterThan(baselineAimLines),
                $"{BuildInputActionDiagnostics(engine, "SkillQ")} || {BuildAbilityDiagnostics(engine, "Jayce Cannon")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)}");
            LeftClickWorld(engine, backend, GetEntityScreen(engine, "Target Dummy A"), frameTimesMs);
            Tick(engine, 4, frameTimesMs);
            float dummyHealthAfterConfirm = ReadHealth(engine.World, "Target Dummy A");
            Assert.That(
                dummyHealthAfterConfirm,
                Is.LessThan(dummyHealthAfterCancel),
                $"{BuildInputActionDiagnostics(engine, "SkillQ")} || {BuildAbilityDiagnostics(engine, "Jayce Cannon")} || {BuildSelectionStateDiagnostics(engine)} || {BuildOverlayDiagnostics(overlays)} || {BuildFeedbackDiagnostics(primitives, worldHud)}");
            Assert.That(CountPrimitiveMarkers(primitives), Is.GreaterThan(0), "Press-release confirm hit should emit visible pulse markers.");
            Assert.That(CountWorldHudItems(worldHud, WorldHudItemKind.Text), Is.GreaterThan(0), "Press-release confirm hit should emit visible world text feedback.");
            CaptureSnapshot(engine, overlays, primitives, worldHud, snapshots, "press_release_confirm_hit");
            timeline.Add($"[T+010] Press-release aim cast shows confirm cursor for Jayce Cannon Q | cancel keeps HP {dummyHealthBeforeCancel:0} | confirm hits to {dummyHealthAfterConfirm:0}");

            File.WriteAllText(Path.Combine(artifactDir, "trace.jsonl"), BuildTraceJsonl(snapshots));
            File.WriteAllText(Path.Combine(artifactDir, "battle-report.md"), BuildBattleReport(timeline, snapshots, frameTimesMs));
            File.WriteAllText(Path.Combine(artifactDir, "path.mmd"), BuildPathMermaid());
        }

        private static GameEngine CreateEngine()
        {
            string repoRoot = FindRepoRoot();
            string assetsRoot = Path.Combine(repoRoot, "assets");
            var modPaths = RepoModPaths.ResolveExplicit(repoRoot, AcceptanceMods);

            var engine = new GameEngine();
            engine.InitializeWithConfigPipeline(modPaths, assetsRoot);
            InstallInput(engine);

            var uiRoot = new UIRoot(new SkiaUiRenderer());
            uiRoot.Resize(1920f, 1080f);
            engine.SetService(CoreServiceKeys.UIRoot, uiRoot);
            engine.SetService(CoreServiceKeys.UiTextMeasurer, (object)new SkiaTextMeasurer());
            engine.SetService(CoreServiceKeys.UiImageSizeProvider, (object)new SkiaImageSizeProvider());

            var view = new StubViewController(1920f, 1080f);
            engine.SetService(CoreServiceKeys.ViewController, view);
            var cameraAdapter = new StubCameraAdapter();
            var timingDiagnostics = engine.GetService(CoreServiceKeys.PresentationTimingDiagnostics);
            var cameraPresenter = new CameraPresenter(engine.SpatialCoords, cameraAdapter, timingDiagnostics);
            var screenProjector = new CoreScreenProjector(engine.GameSession.Camera, view);
            var screenRayProvider = new CoreScreenRayProvider(engine.GameSession.Camera, view);
            screenProjector.BindPresenter(cameraPresenter);
            screenRayProvider.BindPresenter(cameraPresenter);
            engine.SetService(CoreServiceKeys.ScreenProjector, screenProjector);
            engine.SetService(CoreServiceKeys.ScreenRayProvider, screenRayProvider);

            var culling = new CameraCullingSystem(engine.World, engine.GameSession.Camera, engine.SpatialQueries, view, timingDiagnostics);
            engine.RegisterPresentationSystem(culling);
            engine.SetService(CoreServiceKeys.CameraCullingDebugState, culling.DebugState);
            engine.GlobalContext[HeadlessCameraKey] = new HeadlessCameraRuntime(
                cameraPresenter,
                engine.GetService(CoreServiceKeys.PresentationFrameSetup));

            engine.Start();
            return engine;
        }

        private static void InstallInput(GameEngine engine)
        {
            var inputConfig = new InputConfigPipelineLoader(engine.ConfigPipeline).Load();
            var backend = new TestInputBackend();
            var inputHandler = new PlayerInputHandler(backend, inputConfig);
            for (int i = 0; i < engine.MergedConfig.StartupInputContexts.Count; i++)
            {
                inputHandler.PushContext(engine.MergedConfig.StartupInputContexts[i]);
            }

            engine.SetService(CoreServiceKeys.InputHandler, inputHandler);
            engine.SetService(CoreServiceKeys.InputBackend, (IInputBackend)backend);
            engine.SetService(CoreServiceKeys.UiCaptured, false);
            backend.SetMousePosition(new Vector2(960f, 540f));
            engine.GlobalContext[TestInputBackendKey] = backend;
        }

        private static void LoadMap(GameEngine engine, string mapId, List<double> frameTimesMs, int frames = 12)
        {
            engine.LoadMap(mapId);
            Assert.That(engine.CurrentMapSession, Is.Not.Null, $"{mapId} should create a live map session.");
            Tick(engine, frames, frameTimesMs);
        }

        private static TestInputBackend GetInputBackend(GameEngine engine)
        {
            return engine.GlobalContext[TestInputBackendKey] as TestInputBackend
                ?? throw new InvalidOperationException("Champion skill sandbox test input backend is missing.");
        }

        private static void SelectNamedEntity(GameEngine engine, TestInputBackend backend, string name, List<double> frameTimesMs)
        {
            Entity target = FindEntityByName(engine.World, name);
            SelectionRuntime selection = engine.GetService(CoreServiceKeys.SelectionRuntime)
                ?? throw new InvalidOperationException("SelectionRuntime missing.");

            Entity owner = engine.GetService(CoreServiceKeys.LocalPlayerEntity);
            if (!engine.World.IsAlive(owner))
            {
                owner = target;
                engine.GlobalContext[CoreServiceKeys.LocalPlayerEntity.Name] = owner;
            }

            Span<Entity> next = stackalloc Entity[1];
            next[0] = target;
            selection.ReplaceSelection(owner, SelectionSetKeys.Ambient, next);
            Tick(engine, 1, frameTimesMs);
            TickUntil(
                engine,
                frameTimesMs,
                () => string.Equals(GetSelectedEntityName(engine), name, StringComparison.Ordinal),
                maxFrames: 12);
        }

        private static void PressButton(GameEngine engine, TestInputBackend backend, string path, List<double> frameTimesMs)
        {
            backend.SetButton(path, true);
            Tick(engine, 2, frameTimesMs);
            backend.SetButton(path, false);
            Tick(engine, 2, frameTimesMs);
        }

        private static void HoldButton(GameEngine engine, TestInputBackend backend, string path, int holdFrames, List<double> frameTimesMs)
        {
            backend.SetButton(path, true);
            Tick(engine, Math.Max(1, holdFrames), frameTimesMs);
        }

        private static void ReleaseButton(GameEngine engine, TestInputBackend backend, string path, List<double> frameTimesMs)
        {
            backend.SetButton(path, false);
            Tick(engine, 2, frameTimesMs);
        }

        private static void LeftClickWorld(GameEngine engine, TestInputBackend backend, Vector2 screenPosition, List<double> frameTimesMs)
        {
            SetMouseWorld(engine, backend, screenPosition, frameTimesMs);
            backend.SetButton("<Mouse>/LeftButton", true);
            Tick(engine, 2, frameTimesMs);
            backend.SetButton("<Mouse>/LeftButton", false);
            Tick(engine, 2, frameTimesMs);
        }

        private static void RightClickWorld(GameEngine engine, TestInputBackend backend, Vector2 screenPosition, List<double> frameTimesMs)
        {
            SetMouseWorld(engine, backend, screenPosition, frameTimesMs);
            backend.SetButton("<Mouse>/RightButton", true);
            Tick(engine, 2, frameTimesMs);
            backend.SetButton("<Mouse>/RightButton", false);
            Tick(engine, 2, frameTimesMs);
        }

        private static void SetMouseWorld(GameEngine engine, TestInputBackend backend, Vector2 screenPosition, List<double> frameTimesMs)
        {
            backend.SetMousePosition(screenPosition);
            Tick(engine, 1, frameTimesMs);
        }

        private static void Tick(GameEngine engine, int frames, List<double> frameTimesMs)
        {
            for (int i = 0; i < frames; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                engine.SetService(CoreServiceKeys.UiCaptured, false);
                engine.Tick(DeltaTime);
                UpdateHeadlessCamera(engine);
                frameTimesMs.Add((Stopwatch.GetTimestamp() - t0) * 1000d / Stopwatch.Frequency);
            }
        }

        private static void TickUntil(GameEngine engine, List<double> frameTimesMs, Func<bool> predicate, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (predicate())
                {
                    return;
                }

                Tick(engine, 1, frameTimesMs);
            }

            Assert.That(predicate(), Is.True, $"Predicate was not satisfied within {maxFrames} frames.");
        }

        private static IReadOnlyList<PanelSlotSnapshot> CopySelectedSlots(GameEngine engine)
        {
            string selectedName = GetSelectedEntityName(engine);
            Entity selected = FindEntityByName(engine.World, selectedName);
            var slots = new EntityCommandPanelSlotView[8];
            int count = ResolveGasPanelSource(engine).CopySlots(selected, 0, slots);
            var result = new List<PanelSlotSnapshot>(count);
            for (int i = 0; i < count; i++)
            {
                EntityCommandPanelSlotView slot = slots[i];
                result.Add(new PanelSlotSnapshot(
                    slot.SlotIndex,
                    slot.ActionId,
                    slot.DisplayLabel,
                    slot.DetailLabel,
                    FormatFlags(slot.StateFlags)));
            }

            return result;
        }

        private static string FormatFlags(EntityCommandSlotStateFlags flags)
        {
            if (flags == EntityCommandSlotStateFlags.None)
            {
                return "None";
            }

            var values = new List<string>(4);
            if (flags.HasFlag(EntityCommandSlotStateFlags.Base)) values.Add(nameof(EntityCommandSlotStateFlags.Base));
            if (flags.HasFlag(EntityCommandSlotStateFlags.FormOverride)) values.Add(nameof(EntityCommandSlotStateFlags.FormOverride));
            if (flags.HasFlag(EntityCommandSlotStateFlags.Blocked)) values.Add(nameof(EntityCommandSlotStateFlags.Blocked));
            if (flags.HasFlag(EntityCommandSlotStateFlags.Active)) values.Add(nameof(EntityCommandSlotStateFlags.Active));
            if (flags.HasFlag(EntityCommandSlotStateFlags.Empty)) values.Add(nameof(EntityCommandSlotStateFlags.Empty));
            return string.Join("|", values);
        }

        private static void CaptureSnapshot(
            GameEngine engine,
            GroundOverlayBuffer overlays,
            PrimitiveDrawBuffer primitives,
            WorldHudBatchBuffer worldHud,
            List<AcceptanceSnapshot> snapshots,
            string step)
        {
            string selectedName = GetSelectedEntityName(engine);
            var trackedEntities = new[]
            {
                "Ezreal Alpha",
                "Ezreal Cooldown",
                "Garen Courage",
                "Jayce Hammer",
                "Target Dummy A"
            };

            var states = new List<EntityState>(trackedEntities.Length);
            foreach (string name in trackedEntities)
            {
                states.Add(new EntityState(name, ReadHealth(engine.World, name)));
            }

            snapshots.Add(new AcceptanceSnapshot(
                Step: step,
                ActiveModeId: GetActiveModeId(engine),
                SelectedEntity: selectedName,
                Camera: new CameraSnapshot(
                    engine.GameSession.Camera.State.TargetCm.X,
                    engine.GameSession.Camera.State.TargetCm.Y,
                    engine.GameSession.Camera.State.DistanceCm,
                    engine.GameSession.Camera.State.Pitch,
                    engine.GameSession.Camera.State.FovYDeg),
                PanelSlots: CopySelectedSlots(engine),
                OverlayCounts: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["circle"] = CountOverlays(overlays, GroundOverlayShape.Circle),
                    ["cone"] = CountOverlays(overlays, GroundOverlayShape.Cone),
                    ["line"] = CountOverlays(overlays, GroundOverlayShape.Line),
                    ["ring"] = CountOverlays(overlays, GroundOverlayShape.Ring)
                },
                PrimitiveCount: CountPrimitiveMarkers(primitives),
                WorldTextCount: CountWorldHudItems(worldHud, WorldHudItemKind.Text),
                Entities: states));
        }

        private static string BuildTraceJsonl(IReadOnlyList<AcceptanceSnapshot> snapshots)
        {
            var lines = new List<string>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                AcceptanceSnapshot snapshot = snapshots[i];
                lines.Add(JsonSerializer.Serialize(new
                {
                    event_id = $"champion-sandbox-{i + 1:000}",
                    step = snapshot.Step,
                    active_mode_id = snapshot.ActiveModeId,
                    selected_entity = snapshot.SelectedEntity,
                    camera = new
                    {
                        target_x_cm = snapshot.Camera.TargetXCm,
                        target_y_cm = snapshot.Camera.TargetYCm,
                        distance_cm = snapshot.Camera.DistanceCm,
                        pitch = snapshot.Camera.Pitch,
                        fov_y_deg = snapshot.Camera.FovYDeg
                    },
                    panel_slots = snapshot.PanelSlots.Select(slot => new
                    {
                        slot_index = slot.SlotIndex,
                        action_id = slot.ActionId,
                        label = slot.Label,
                        detail = slot.Detail,
                        flags = slot.Flags
                     }),
                     overlay_counts = snapshot.OverlayCounts,
                     primitive_count = snapshot.PrimitiveCount,
                     world_text_count = snapshot.WorldTextCount,
                     entities = snapshot.Entities.Select(entity => new
                     {
                         name = entity.Name,
                        health = entity.Health
                    })
                }));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildBattleReport(IReadOnlyList<string> timeline, IReadOnlyList<AcceptanceSnapshot> snapshots, IReadOnlyList<double> frameTimesMs)
        {
            double medianTickMs = Median(frameTimesMs);
            double maxTickMs = frameTimesMs.Count == 0 ? 0d : frameTimesMs.Max();
            AcceptanceSnapshot finalSnapshot = snapshots[^1];

            var sb = new StringBuilder();
            sb.AppendLine("# Scenario: champion-skill-sandbox");
            sb.AppendLine();
            sb.AppendLine("## Header");
            sb.AppendLine("- build: GasTests / ChampionSkillSandbox_PlayableFlow_WritesAcceptanceArtifacts");
            sb.AppendLine("- map: champion_skill_sandbox");
            sb.AppendLine("- clock: FixedFrame @ 60 Hz");
            sb.AppendLine($"- execution_timestamp_utc: {DateTime.UtcNow:O}");
            sb.AppendLine();
            sb.AppendLine("## Timeline");
            foreach (string entry in timeline)
            {
                sb.AppendLine(entry);
            }

            sb.AppendLine();
            sb.AppendLine("## Outcome");
            sb.AppendLine("- result: success");
            sb.AppendLine("- failure_branch: press-release aim cancel preserved target HP before confirm");
            sb.AppendLine($"- final_selected: {finalSnapshot.SelectedEntity}");
            sb.AppendLine($"- final_mode: {finalSnapshot.ActiveModeId}");
            sb.AppendLine($"- final_camera_target_cm: ({finalSnapshot.Camera.TargetXCm:0.##}, {finalSnapshot.Camera.TargetYCm:0.##})");
            sb.AppendLine($"- final_camera_distance_cm: {finalSnapshot.Camera.DistanceCm:0.##}");
            sb.AppendLine($"- final_selection_ring_count: {finalSnapshot.OverlayCounts["ring"]}");
            sb.AppendLine($"- final_feedback_primitives: {finalSnapshot.PrimitiveCount}");
            sb.AppendLine($"- final_feedback_world_text: {finalSnapshot.WorldTextCount}");
            sb.AppendLine();
            sb.AppendLine("## Summary Stats");
            sb.AppendLine("- total_actions: 10");
            sb.AppendLine("- selection_switches: 4");
            sb.AppendLine("- move_commands: 1");
            sb.AppendLine("- camera_resets: 1");
            sb.AppendLine("- successful_hits: 3");
            sb.AppendLine("- cancelled_casts: 1");
            sb.AppendLine($"- median_tick_ms: {medianTickMs:0.###}");
            sb.AppendLine($"- max_tick_ms: {maxTickMs:0.###}");
            return sb.ToString();
        }

        private static string BuildPathMermaid()
        {
            return string.Join(Environment.NewLine, new[]
            {
                "flowchart TD",
                "    A[\"MapLoaded: sandbox boot -> Ezreal Alpha selected\"] --> B[\"Selection: Ezreal Cooldown -> R blocked\"]",
                "    B --> C[\"Selection: Garen Courage -> W active\"]",
                "    C --> D[\"Selection: Jayce Hammer -> hammer form routed\"]",
                "    D --> E[\"Move: RMB command repositions Ezreal Alpha\"]",
                "    E --> F[\"Camera: F4 reset restores sandbox tactical pose\"]",
                "    F --> G[\"SmartCast: Ezreal Q -> Target Dummy A hit\"]",
                "    G --> H[\"Indicator: hold R on dummy -> hover marker appears\"]",
                "    H --> I[\"Indicator: release -> Trueshot Barrage hit\"]",
                "    I --> J[\"PressReleaseAim: toolbar switch -> Jayce Cannon Q preview\"]",
                "    J --> K[\"RightClick confirm branch: cancel -> HP unchanged\"]",
                "    J --> L[\"LeftClick confirm branch: hit -> HP reduced\"]"
            });
        }

        private static bool IsCameraNear(CameraState state, Vector2 targetCm, float distanceCm, float pitch, float fovYDeg)
        {
            return MathF.Abs(state.TargetCm.X - targetCm.X) <= 0.01f &&
                   MathF.Abs(state.TargetCm.Y - targetCm.Y) <= 0.01f &&
                   MathF.Abs(state.DistanceCm - distanceCm) <= 0.01f &&
                   MathF.Abs(state.Pitch - pitch) <= 0.01f &&
                   MathF.Abs(state.FovYDeg - fovYDeg) <= 0.01f;
        }

        private static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
            {
                return 0d;
            }

            var ordered = values.OrderBy(value => value).ToArray();
            int middle = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[middle - 1] + ordered[middle]) * 0.5d
                : ordered[middle];
        }

        private static int CountOverlays(GroundOverlayBuffer overlays, GroundOverlayShape shape)
        {
            int count = 0;
            foreach (ref readonly var item in overlays.GetSpan())
            {
                if (item.Shape == shape)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPrimitiveMarkers(PrimitiveDrawBuffer primitives)
        {
            return primitives.GetSpan().Length;
        }

        private static int CountWorldHudItems(WorldHudBatchBuffer worldHud, WorldHudItemKind kind)
        {
            int count = 0;
            foreach (ref readonly var item in worldHud.GetSpan())
            {
                if (item.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetActiveModeId(GameEngine engine)
        {
            return engine.GlobalContext.TryGetValue(ViewModeManager.ActiveModeIdKey, out var modeIdObj) && modeIdObj is string modeId
                ? modeId
                : string.Empty;
        }

        private static string GetSelectedEntityName(GameEngine engine)
        {
            if (engine.GlobalContext.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var selectedObj) &&
                selectedObj is Entity selected &&
                engine.World.IsAlive(selected) &&
                engine.World.TryGet(selected, out Name name))
            {
                return name.Value;
            }

            return string.Empty;
        }

        private static IEntityCommandPanelSource ResolveGasPanelSource(GameEngine engine)
        {
            var registry = engine.GetService(CoreServiceKeys.EntityCommandPanelSourceRegistry)
                ?? throw new InvalidOperationException("EntityCommandPanelSourceRegistry missing.");
            Assert.That(registry.TryGet("gas.ability-slots", out IEntityCommandPanelSource source), Is.True);
            return source;
        }

        private static Vector2 FindHoverScreenPoint(GameEngine engine, TestInputBackend backend, string entityName, Vector2 projectedScreenPoint, List<double> frameTimesMs)
        {
            var hoveredSamples = new List<string>(HoverProbeOffsets.Length);
            for (int i = 0; i < HoverProbeOffsets.Length; i++)
            {
                Vector2 candidate = projectedScreenPoint + HoverProbeOffsets[i];
                backend.SetMousePosition(candidate);
                Tick(engine, 1, frameTimesMs);

                string hovered = ReadHoveredEntityName(engine);
                hoveredSamples.Add($"{candidate.X:0.0},{candidate.Y:0.0}->{hovered}");
                if (string.Equals(hovered, entityName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            Assert.Fail(
                $"Failed to hover '{entityName}' near projected point ({projectedScreenPoint.X:0.0},{projectedScreenPoint.Y:0.0}). Samples: {string.Join(" | ", hoveredSamples)}");
            return projectedScreenPoint;
        }

        private static void UpdateHeadlessCamera(GameEngine engine)
        {
            if (!engine.GlobalContext.TryGetValue(HeadlessCameraKey, out object? runtimeObj) ||
                runtimeObj is not HeadlessCameraRuntime runtime)
            {
                return;
            }

            float alpha = runtime.PresentationFrameSetup?.GetInterpolationAlpha() ?? 1f;
            runtime.CameraPresenter.Update(engine.GameSession.Camera, alpha);
        }

        private static string ReadHoveredEntityName(GameEngine engine)
        {
            return engine.GlobalContext.TryGetValue(CoreServiceKeys.HoveredEntity.Name, out object? hoveredObj) &&
                   hoveredObj is Entity hovered &&
                   hovered != Entity.Null &&
                   engine.World.TryGet(hovered, out Name name)
                ? name.Value
                : string.Empty;
        }

        private static Vector2 GetEntityScreen(GameEngine engine, string name)
        {
            Entity entity = FindEntityByName(engine.World, name);
            var projector = engine.GetService(CoreServiceKeys.ScreenProjector)
                ?? throw new InvalidOperationException("ScreenProjector was not installed.");
            if (engine.World.TryGet(entity, out VisualTransform transform))
            {
                return projector.WorldToScreen(transform.Position);
            }

            ref var position = ref engine.World.Get<WorldPositionCm>(entity);
            return projector.WorldToScreen(WorldUnits.WorldCmToVisualMeters(position.Value, yMeters: 0f));
        }

        private static Vector2 GetGroundScreenFromWorld(GameEngine engine, Vector2 worldCm)
        {
            var projector = engine.GetService(CoreServiceKeys.ScreenProjector)
                ?? throw new InvalidOperationException("ScreenProjector was not installed.");
            return projector.WorldToScreen(new Vector3(WorldUnits.CmToM(worldCm.X), 0f, WorldUnits.CmToM(worldCm.Y)));
        }

        private static Entity FindEntityByName(World world, string entityName)
        {
            Entity found = Entity.Null;
            var query = new QueryDescription().WithAll<Name>();
            world.Query(in query, (Entity entity, ref Name name) =>
            {
                if (found != Entity.Null)
                {
                    return;
                }

                if (string.Equals(name.Value, entityName, StringComparison.Ordinal))
                {
                    found = entity;
                }
            });

            Assert.That(found, Is.Not.EqualTo(Entity.Null), $"Entity '{entityName}' should exist on champion_skill_sandbox.");
            return found;
        }

        private static Vector2 ReadPosition(World world, string name)
        {
            Entity entity = FindEntityByName(world, name);
            Assert.That(world.TryGet(entity, out WorldPositionCm position), Is.True);
            var worldCm = position.ToWorldCmInt2();
            return new Vector2(worldCm.X, worldCm.Y);
        }

        private static float ReadDistance(World world, string fromName, string toName)
        {
            return Vector2.Distance(ReadPosition(world, fromName), ReadPosition(world, toName));
        }

        private static float ReadHealth(World world, string name)
        {
            Entity entity = FindEntityByName(world, name);
            int healthId = AttributeRegistry.GetId("Health");
            if (healthId < 0 || !world.TryGet(entity, out AttributeBuffer attributes))
            {
                return 0f;
            }

            return attributes.GetCurrent(healthId);
        }

        private static string BuildAbilityDiagnostics(GameEngine engine, string actorName)
        {
            Entity actor = FindEntityByName(engine.World, actorName);
            var details = new List<string>();

            if (engine.World.TryGet(actor, out OrderBuffer orders))
            {
                details.Add(orders.HasActive ? $"activeOrder={orders.ActiveOrder.Order.OrderTypeId}" : "activeOrder=<none>");
                details.Add(orders.HasPending ? $"pendingOrder={orders.PendingOrder.Order.OrderTypeId}" : "pendingOrder=<none>");
                if (orders.HasQueued)
                {
                    details.Add($"queuedOrders={orders.QueuedCount}");
                }
            }
            else
            {
                details.Add("orderBuffer=<missing>");
            }

            if (engine.World.TryGet(actor, out BlackboardIntBuffer ints) &&
                ints.TryGet(OrderBlackboardKeys.Cast_SlotIndex, out int slotIndex))
            {
                details.Add($"castSlot={slotIndex}");
            }

            if (engine.World.TryGet(actor, out BlackboardSpatialBuffer spatial))
            {
                int pointCount = spatial.GetPointCount(OrderBlackboardKeys.Cast_TargetPosition);
                details.Add($"castPoints={pointCount}");
                if (pointCount > 0 &&
                    spatial.TryGetPointAt(OrderBlackboardKeys.Cast_TargetPosition, pointCount - 1, out var point))
                {
                    details.Add($"castTarget=({point.X:0.##},{point.Z:0.##})");
                }
            }

            if (engine.World.TryGet(actor, out AbilityExecInstance exec))
            {
                details.Add($"execSlot={exec.AbilitySlot}");
                details.Add($"execState={exec.State}");
                details.Add($"execHasTargetPos={exec.HasTargetPos != 0}");
            }
            else
            {
                details.Add("exec=<none>");
            }

            return string.Join(" | ", details);
        }

        private static string BuildInputActionDiagnostics(GameEngine engine, string actionId)
        {
            var details = new List<string>();

            if (engine.GetService(CoreServiceKeys.InputHandler) is PlayerInputHandler liveInput)
            {
                details.Add($"livePressed={liveInput.PressedThisFrame(actionId)}");
                details.Add($"liveDown={liveInput.IsDown(actionId)}");
                Vector2 pointer = liveInput.ReadAction<Vector2>("PointerPos");
                details.Add($"livePointer=({pointer.X:0.##},{pointer.Y:0.##})");
            }
            else
            {
                details.Add("liveInput=<missing>");
            }

            if (engine.GetService(CoreServiceKeys.AuthoritativeInput) is IInputActionReader authoritativeInput)
            {
                details.Add($"authPressed={authoritativeInput.PressedThisFrame(actionId)}");
                details.Add($"authDown={authoritativeInput.IsDown(actionId)}");
                Vector2 pointer = authoritativeInput.ReadAction<Vector2>("PointerPos");
                details.Add($"authPointer=({pointer.X:0.##},{pointer.Y:0.##})");
            }
            else
            {
                details.Add("authoritativeInput=<missing>");
            }

            if (engine.GetService(CoreServiceKeys.ActiveInputOrderMapping) is InputOrderMappingSystem mapping)
            {
                details.Add($"mappingMode={mapping.InteractionMode}");
                details.Add($"mappingAiming={mapping.IsAiming}");
                if (mapping.GetMapping(actionId) is InputOrderMapping actionMapping)
                {
                    details.Add($"selectionType={actionMapping.SelectionType}");
                    details.Add($"orderTypeKey={actionMapping.OrderTypeKey}");
                }
            }
            else
            {
                details.Add("activeMapping=<missing>");
            }

            details.Add($"activeMode={GetActiveModeId(engine)}");
            return string.Join(" | ", details);
        }

        private static string BuildSelectionStateDiagnostics(GameEngine engine)
        {
            var details = new List<string>
            {
                $"selected={GetSelectedEntityName(engine)}"
            };

            if (engine.GlobalContext.TryGetValue(CoreServiceKeys.HoveredEntity.Name, out var hoveredObj) &&
                hoveredObj is Entity hovered &&
                engine.World.IsAlive(hovered) &&
                engine.World.TryGet(hovered, out Name hoveredName))
            {
                details.Add($"hovered={hoveredName.Value}");
            }
            else
            {
                details.Add("hovered=<none>");
            }

            return string.Join(" | ", details);
        }

        private static string BuildOverlayDiagnostics(GroundOverlayBuffer overlays)
        {
            return $"overlays=count:{overlays.Count},circle:{CountOverlays(overlays, GroundOverlayShape.Circle)},cone:{CountOverlays(overlays, GroundOverlayShape.Cone)},line:{CountOverlays(overlays, GroundOverlayShape.Line)},ring:{CountOverlays(overlays, GroundOverlayShape.Ring)}";
        }

        private static string BuildFeedbackDiagnostics(PrimitiveDrawBuffer primitives, WorldHudBatchBuffer worldHud)
        {
            return $"feedback=primitives:{CountPrimitiveMarkers(primitives)},worldText:{CountWorldHudItems(worldHud, WorldHudItemKind.Text)}";
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string srcDir = Path.Combine(dir.FullName, "src");
                string assetsDir = Path.Combine(dir.FullName, "assets");
                if (Directory.Exists(srcDir) && Directory.Exists(assetsDir))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Failed to locate repository root from test output directory.");
        }

        private sealed record AcceptanceSnapshot(
            string Step,
            string ActiveModeId,
            string SelectedEntity,
            CameraSnapshot Camera,
            IReadOnlyList<PanelSlotSnapshot> PanelSlots,
            IReadOnlyDictionary<string, int> OverlayCounts,
            int PrimitiveCount,
            int WorldTextCount,
            IReadOnlyList<EntityState> Entities);

        private sealed record CameraSnapshot(
            float TargetXCm,
            float TargetYCm,
            float DistanceCm,
            float Pitch,
            float FovYDeg);

        private sealed record PanelSlotSnapshot(
            int SlotIndex,
            string ActionId,
            string Label,
            string Detail,
            string Flags);

        private sealed record EntityState(
            string Name,
            float Health);

        private sealed class TestInputBackend : IInputBackend
        {
            private readonly Dictionary<string, bool> _buttons = new(StringComparer.Ordinal);
            private Vector2 _mousePosition;

            public void SetButton(string path, bool isDown)
            {
                _buttons[path] = isDown;
            }

            public void SetMousePosition(Vector2 position)
            {
                _mousePosition = position;
            }

            public float GetAxis(string devicePath) => 0f;
            public bool GetButton(string devicePath) => _buttons.TryGetValue(devicePath, out bool isDown) && isDown;
            public Vector2 GetMousePosition() => _mousePosition;
            public float GetMouseWheel() => 0f;
            public void EnableIME(bool enable) { }
            public void SetIMECandidatePosition(int x, int y) { }
            public string GetCharBuffer() => string.Empty;
        }

        private sealed class StubViewController : IViewController
        {
            public StubViewController(float width, float height)
            {
                Resolution = new Vector2(width, height);
            }

            public Vector2 Resolution { get; }
            public float Fov => 60f;
            public float AspectRatio => Resolution.Y <= 0f ? 1f : Resolution.X / Resolution.Y;
        }

        private sealed class HeadlessCameraRuntime
        {
            public HeadlessCameraRuntime(CameraPresenter cameraPresenter, PresentationFrameSetupSystem? presentationFrameSetup)
            {
                CameraPresenter = cameraPresenter;
                PresentationFrameSetup = presentationFrameSetup;
            }

            public CameraPresenter CameraPresenter { get; }
            public PresentationFrameSetupSystem? PresentationFrameSetup { get; }
        }

        private sealed class StubCameraAdapter : ICameraAdapter
        {
            public CameraRenderState3D LastState { get; private set; }

            public void UpdateCamera(in CameraRenderState3D state)
            {
                LastState = state;
            }
        }
    }
}
