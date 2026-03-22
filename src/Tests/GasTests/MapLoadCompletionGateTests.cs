using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arch.Core;
using Ludots.Adapter.UE5;
using Ludots.Core.Components;
using Ludots.Core.Config;
using Ludots.Core.Diagnostics;
using Ludots.Core.Engine;
using Ludots.Core.Map;
using Ludots.Core.Modding;
using Ludots.Core.Scripting;
using Ludots.Tests;
using Ludots.UI;
using Ludots.UI.Runtime;
using Ludots.UI.Skia;
using NUnit.Framework;
using X28MainMenuMod.Runtime;

namespace GasTests
{
    [TestFixture]
    public sealed class MapLoadCompletionGateTests
    {
        [Test]
        public void LoadMap_WithoutGate_FiresMapLoadedImmediately()
        {
            using var engine = CreateCoreEngine();
            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("audit_outer");

            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].MapId, Is.EqualTo("audit_outer"));
            Assert.That(loaded[0].Status.Succeeded, Is.True);
            Assert.That(loaded[0].Status.IsDeferred, Is.False);
        }

        [Test]
        public void LoadMap_WithPendingGate_DelaysMapLoadedUntilReady()
        {
            using var engine = CreateCoreEngine();
            var gate = new ControlledMapLoadGate();
            gate.Delay("audit_outer");
            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("audit_outer");

            Assert.That(loaded.Count, Is.EqualTo(0));
            Assert.That(engine.GetService(CoreServiceKeys.MapLoadStatus).State, Is.EqualTo(MapLoadCompletionState.Pending));
            Assert.That(CountMapEntities(engine.World, "audit_outer"), Is.GreaterThan(0));
            Assert.That(CountSuspendedMapEntities(engine.World, "audit_outer"), Is.EqualTo(CountMapEntities(engine.World, "audit_outer")));

            gate.Get("audit_outer").Result = MapLoadCompletionResult.Ready();
            engine.Tick(0.016f);

            Assert.That(loaded.Select(x => x.MapId), Is.EqualTo(new[] { "audit_outer" }));
            Assert.That(loaded[0].Status.Succeeded, Is.True);
            Assert.That(loaded[0].Status.IsDeferred, Is.True);
            Assert.That(CountSuspendedMapEntities(engine.World, "audit_outer"), Is.EqualTo(0));
        }

        [Test]
        public void LoadMap_WhenPendingFails_FiresMapLoadedWithFailureStatus()
        {
            using var engine = CreateCoreEngine();
            var gate = new ControlledMapLoadGate();
            gate.Delay("audit_outer");
            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("audit_outer");
            gate.Get("audit_outer").Result = MapLoadCompletionResult.Failed("host timed out");
            engine.Tick(0.016f);

            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].Status.Failed, Is.True);
            Assert.That(loaded[0].Status.ErrorMessage, Is.EqualTo("host timed out"));
            Assert.That(CountSuspendedMapEntities(engine.World, "audit_outer"), Is.EqualTo(CountMapEntities(engine.World, "audit_outer")));
        }

        [Test]
        public void LoadMap_PendingThenUnload_DoesNotFireLateMapLoaded()
        {
            using var engine = CreateCoreEngine();
            var gate = new ControlledMapLoadGate();
            gate.Delay("audit_outer");
            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("audit_outer");
            ControlledPendingLoad pending = gate.Get("audit_outer");

            engine.UnloadMap("audit_outer");
            pending.Result = MapLoadCompletionResult.Ready();
            engine.Tick(0.016f);

            Assert.That(loaded.Count, Is.EqualTo(0));
            Assert.That(pending.CancelCount, Is.EqualTo(1));
        }

        [Test]
        public void LoadMap_ReloadCancelsOldPendingLoad()
        {
            using var engine = CreateCoreEngine();
            var gate = new ControlledMapLoadGate();
            gate.Delay("audit_outer");
            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("audit_outer");
            ControlledPendingLoad firstPending = gate.Get("audit_outer");

            gate.AllowImmediate("audit_outer");
            engine.LoadMap("audit_outer");

            Assert.That(loaded.Count(x => x.MapId == "audit_outer"), Is.EqualTo(1));

            firstPending.Result = MapLoadCompletionResult.Ready();
            engine.Tick(0.016f);

            Assert.That(firstPending.CancelCount, Is.EqualTo(1));
            Assert.That(loaded.Count(x => x.MapId == "audit_outer"), Is.EqualTo(1));
        }

        [Test]
        public void PushMap_UsesSameDeferredCompletionPath()
        {
            using var engine = CreateCoreEngine();
            var gate = new ControlledMapLoadGate();
            gate.Delay("audit_inner");
            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("audit_outer");
            engine.PushMap("audit_inner");

            Assert.That(loaded.Count(x => x.MapId == "audit_inner"), Is.EqualTo(0));
            Assert.That(engine.CurrentMapSession?.MapId.Value, Is.EqualTo("audit_inner"));
            Assert.That(CountSuspendedMapEntities(engine.World, "audit_inner"), Is.EqualTo(CountMapEntities(engine.World, "audit_inner")));

            gate.Get("audit_inner").Result = MapLoadCompletionResult.Ready();
            engine.Tick(0.016f);

            Assert.That(loaded.Count(x => x.MapId == "audit_inner"), Is.EqualTo(1));
            Assert.That(CountSuspendedMapEntities(engine.World, "audit_inner"), Is.EqualTo(0));
        }

        [Test]
        public void X28Gate_PureLudotsMap_DoesNotEnterPending()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var engine = new GameEngine();

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_main_menu"));

            Assert.That(pending, Is.Null);
            Assert.That(gate.TryGetState("x28_main_menu", out _), Is.False);
        }

        [Test]
        public void X28Gate_EntryMap_WaitsForConfiguredMainMenuWorldBind()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostConfiguredMainMenuWorldName, "MainMenu");
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 1));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_main_menu"));

            Assert.That(pending, Is.Not.Null);
            Assert.That(gate.TryGetState("x28_main_menu", out X28MapLoadState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Pending));

            Assert.That(pending!.Poll().State, Is.EqualTo(MapLoadCompletionState.Pending));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 2));

            Assert.That(pending.Poll().State, Is.EqualTo(MapLoadCompletionState.Ready));
            Assert.That(gate.TryGetState("x28_main_menu", out state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Ready));
        }

        [Test]
        public void X28Gate_EntryMap_WhenHostAlreadyOnConfiguredMainMenu_DoesNotEnterPending()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostConfiguredMainMenuWorldName, "MainMenu");
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 3));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_main_menu"));

            Assert.That(pending, Is.Null);
            Assert.That(gate.TryGetState("x28_main_menu", out _), Is.False);
        }

        [Test]
        public void X28Gate_EntryMap_WhenConfiguredWorldMissingButHostStillOnBootstrapEntry_WaitsForMainMenuBind()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 1));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_main_menu"));

            Assert.That(pending, Is.Not.Null);
            Assert.That(pending!.Poll().State, Is.EqualTo(MapLoadCompletionState.Pending));
            Assert.That(gate.TryGetState("x28_main_menu", out X28MapLoadState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Pending));

            engine.SetService(UE5AdapterServiceKeys.HostConfiguredMainMenuWorldName, "MainMenu");
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Splash", 2));

            Assert.That(pending.Poll().State, Is.EqualTo(MapLoadCompletionState.Pending));
            Assert.That(gate.TryGetState("x28_main_menu", out state), Is.True);
            Assert.That(state.ExpectedWorldName, Is.EqualTo("MainMenu"));
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Pending));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 3));

            Assert.That(pending.Poll().State, Is.EqualTo(MapLoadCompletionState.Ready));
            Assert.That(gate.TryGetState("x28_main_menu", out state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Ready));
        }

        [Test]
        public void X28Gate_EntryMap_WhenConfiguredWorldMissingOutsideBootstrap_DoesNotEnterPending()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 1));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_main_menu"));

            Assert.That(pending, Is.Null);
            Assert.That(gate.TryGetState("x28_main_menu", out _), Is.False);
        }

        [Test]
        public void X28Gate_UePreview_RequestsNavigatorAndCompletesAfterBindGenerationAdvances()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var navigator = new FakeHostLevelNavigator();
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostLevelNavigator, navigator);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenuWorld", 10));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_mode_ue_template"));

            Assert.That(pending, Is.Not.Null);
            Assert.That(navigator.LoadRequests.Count, Is.EqualTo(1));
            Assert.That(navigator.LoadRequests[0].LevelPath, Is.EqualTo("/Game/Mods/Template/Maps/TemplateMap"));
            Assert.That(gate.TryGetState("x28_mode_ue_template", out X28MapLoadState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Pending));

            navigator.Snapshot = new HostLevelNavigationSnapshot(
                HostLevelTransitionMode.PreviewMod,
                HostLevelNavigationState.Active,
                navigator.LoadRequests[0].LevelPath,
                navigator.LoadRequests[0].LevelPath,
                "TemplateMap",
                string.Empty);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("TemplateMap", 11));

            Assert.That(pending!.Poll().State, Is.EqualTo(MapLoadCompletionState.Ready));
            Assert.That(gate.TryGetState("x28_mode_ue_template", out state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Ready));
        }

        [Test]
        public void X28Gate_UePreview_WorldMatchWithoutBindAdvance_RemainsPending()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var navigator = new FakeHostLevelNavigator();
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostLevelNavigator, navigator);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenuWorld", 5));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_mode_ue_template"));

            Assert.That(pending, Is.Not.Null);
            Assert.That(navigator.LoadRequests.Count, Is.EqualTo(1));

            navigator.Snapshot = new HostLevelNavigationSnapshot(
                HostLevelTransitionMode.PreviewMod,
                HostLevelNavigationState.Active,
                navigator.LoadRequests[0].LevelPath,
                navigator.LoadRequests[0].LevelPath,
                "TemplateMap",
                string.Empty);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("TemplateMap", 5));

            Assert.That(pending!.Poll().State, Is.EqualTo(MapLoadCompletionState.Pending));
            Assert.That(gate.TryGetState("x28_mode_ue_template", out X28MapLoadState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Pending));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("TemplateMap", 6));

            Assert.That(pending.Poll().State, Is.EqualTo(MapLoadCompletionState.Ready));
            Assert.That(gate.TryGetState("x28_mode_ue_template", out state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Ready));
        }

        [Test]
        public void X28Gate_X28Campaign_UsesLaunchDelegateAndWaitsForResolvedWorld()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var navigator = new FakeHostLevelNavigator();
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostLevelNavigator, navigator);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenuWorld", 20));

            string? capturedLaunchRequest = null;
            engine.GlobalContext["X28.HostScenario.Launch"] = (Func<string, string>)(json =>
            {
                capturedLaunchRequest = json;
                return """
{
  "success": true,
  "sourceMapId": "x28_mode_x28_campaign_template",
  "scenarioId": 101,
  "playerForcePrefabId": 11,
  "difficultyValue": 1,
  "resolvedLevelPath": "/Game/Mods/Template/Maps/TemplateMap",
  "errorMessage": ""
}
""";
            });

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_mode_x28_campaign_template"));

            Assert.That(pending, Is.Not.Null);
            Assert.That(capturedLaunchRequest, Is.Not.Null.And.Not.Empty);
            Assert.That(navigator.LoadRequests.Count, Is.EqualTo(0));
            Assert.That(gate.TryGetState("x28_mode_x28_campaign_template", out X28MapLoadState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Pending));

            navigator.Snapshot = new HostLevelNavigationSnapshot(
                HostLevelTransitionMode.None,
                HostLevelNavigationState.Idle,
                string.Empty,
                string.Empty,
                "TemplateMap",
                string.Empty);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("TemplateMap", 21));

            Assert.That(pending!.Poll().State, Is.EqualTo(MapLoadCompletionState.Ready));
            Assert.That(gate.TryGetState("x28_mode_x28_campaign_template", out state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Ready));
            Assert.That(state.ResolvedLevelPath, Is.EqualTo("/Game/Mods/Template/Maps/TemplateMap"));
        }

        [Test]
        public void X28Gate_FailureState_IsSurfacedInsteadOfInfinitePending()
        {
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);
            var navigator = new FakeHostLevelNavigator
            {
                NextLoadResult = HostLevelNavigationResult.Fail(HostLevelNavigationSnapshot.Empty, "navigator failure")
            };
            var engine = new GameEngine();
            engine.SetService(UE5AdapterServiceKeys.HostLevelNavigator, navigator);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenuWorld", 30));

            IPendingMapLoad pending = gate.BeginPendingLoad(CreateRequest(engine, "x28_mode_ue_template"));

            Assert.That(pending, Is.Not.Null);
            Assert.That(pending!.Poll().State, Is.EqualTo(MapLoadCompletionState.Failed));
            Assert.That(gate.TryGetState("x28_mode_ue_template", out X28MapLoadState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(X28MapLoadPhase.Failed));
            Assert.That(state.ErrorMessage, Does.Contain("navigator failure"));
        }

        [Test]
        public void X28MainMenuInitialLoad_IsDeferredUntilHostMainMenuWorldReady()
        {
            using var engine = CreateWorkspaceEngine();
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);

            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);
            engine.SetService(UE5AdapterServiceKeys.HostConfiguredMainMenuWorldName, "MainMenu");
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 1));

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("x28_main_menu");

            Assert.That(loaded, Is.Empty);
            Assert.That(engine.GetService(CoreServiceKeys.MapLoadStatus).State, Is.EqualTo(MapLoadCompletionState.Pending));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 2));
            engine.Tick(0.016f);

            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].MapId, Is.EqualTo("x28_main_menu"));
            Assert.That(loaded[0].Status.Succeeded, Is.True);
            Assert.That(loaded[0].Status.IsDeferred, Is.True);
        }

        [Test]
        public void X28MainMenuInitialLoad_WithoutConfiguredWorldName_WaitsForLateMainMenuResolution()
        {
            using var engine = CreateWorkspaceEngine();
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);

            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 1));

            var loaded = new List<(string MapId, MapLoadStatus Status)>();
            RegisterMapLoadedRecorder(engine, loaded);

            engine.LoadMap("x28_main_menu");

            Assert.That(loaded, Is.Empty);
            Assert.That(engine.GetService(CoreServiceKeys.MapLoadStatus).State, Is.EqualTo(MapLoadCompletionState.Pending));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 2));
            engine.Tick(0.016f);

            Assert.That(loaded, Is.Empty);

            engine.SetService(UE5AdapterServiceKeys.HostConfiguredMainMenuWorldName, "MainMenu");
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Splash", 3));
            engine.Tick(0.016f);

            Assert.That(loaded, Is.Empty);

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 4));
            engine.Tick(0.016f);

            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].MapId, Is.EqualTo("x28_main_menu"));
            Assert.That(loaded[0].Status.Succeeded, Is.True);
            Assert.That(loaded[0].Status.IsDeferred, Is.True);
        }

        [Test]
        public void X28MainMenuReturn_ReloadsFreshMainMenuInsteadOfResumingSuspendedSession()
        {
            const string entryMapId = "x28_main_menu";
            const string modeMapId = "x28_mode_x28_campaign_default";
            const string mainMenuWorldName = "MainMenuWorld";
            const string gameplayWorldName = "TemplateMap";

            using var engine = CreateWorkspaceEngine();
            var navigator = new FakeHostLevelNavigator();
            var context = new TestModContext();

            engine.SetService(UE5AdapterServiceKeys.HostLevelNavigator, navigator);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot(mainMenuWorldName, 1));

            var loaded = new List<string>();
            var resumed = new List<string>();
            var unloaded = new List<string>();
            RegisterMapEventRecorder(engine, GameEvents.MapLoaded, loaded);
            RegisterMapEventRecorder(engine, GameEvents.MapResumed, resumed);
            RegisterMapEventRecorder(engine, GameEvents.MapUnloaded, unloaded);

            engine.LoadMap(entryMapId);

            MapSession? originalMainMenuSession = engine.MapSessions?.GetSession(new MapId(entryMapId));
            Assert.That(originalMainMenuSession, Is.Not.Null);

            engine.GlobalContext["X28.HostScenario.Launch"] = (Func<string, string>)(_ =>
                """
{
  "success": true,
  "sourceMapId": "x28_mode_x28_campaign_default",
  "scenarioId": 101,
  "playerForcePrefabId": 11,
  "difficultyValue": 1,
  "resolvedLevelPath": "/Game/Mods/Template/Maps/TemplateMap",
  "errorMessage": ""
}
""");
            engine.GlobalContext["X28.HostScenario.ReturnToMainMenu"] = (Func<string, string>)(_ =>
                """
{
  "success": true,
  "errorMessage": ""
}
""");

            var gate = new X28MapLoadCompletionGate(context);
            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);
            object runtime = CreateX28MainMenuRuntime(context, gate);
            SetNonPublicField(runtime, "_mainMenuAssetName", mainMenuWorldName);

            engine.LoadMap(modeMapId);
            Assert.That(engine.CurrentMapSession?.MapId.Value, Is.EqualTo(modeMapId));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot(gameplayWorldName, 2));
            engine.Tick(0.016f);

            Assert.That(loaded, Does.Contain(modeMapId));
            InvokeInstanceMethod(runtime, "Sync", engine);

            object mode = ResolveHostMapMode(context, modeMapId);
            InvokeInstanceMethod(runtime, "BackToMainMenu", engine, mode);

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot(mainMenuWorldName, 3));
            InvokeInstanceMethod(runtime, "Sync", engine);

            MapSession? reloadedMainMenuSession = engine.MapSessions?.GetSession(new MapId(entryMapId));
            Assert.That(reloadedMainMenuSession, Is.Not.Null);
            Assert.That(engine.CurrentMapSession?.MapId.Value, Is.EqualTo(entryMapId));
            Assert.That(ReferenceEquals(originalMainMenuSession, reloadedMainMenuSession), Is.False);
            Assert.That(engine.MapSessions?.GetSession(new MapId(modeMapId)), Is.Null);
            Assert.That(loaded.Count(x => string.Equals(x, entryMapId, StringComparison.OrdinalIgnoreCase)), Is.EqualTo(2));
            Assert.That(unloaded.Count(x => string.Equals(x, entryMapId, StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
            Assert.That(unloaded, Does.Contain(modeMapId));
            Assert.That(resumed, Does.Not.Contain(entryMapId));
        }

        [Test]
        public void X28MainMenuRuntime_EntryMapSyncRefreshesMainMenuWorldNameAfterHostWorldSettles()
        {
            using var engine = CreateWorkspaceEngine();
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 0));

            object runtime = CreateX28MainMenuRuntime(context, gate);
            engine.LoadMap("x28_main_menu");

            InvokeInstanceMethod(runtime, "Sync", engine);
            Assert.That(GetNonPublicField<string>(runtime, "_mainMenuAssetName"), Is.EqualTo("Entry"));

            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 1));
            InvokeInstanceMethod(runtime, "Sync", engine);

            Assert.That(GetNonPublicField<string>(runtime, "_mainMenuAssetName"), Is.EqualTo("MainMenu"));
        }

        [Test]
        public void X28PreviewReturn_ReloadsMainMenuEvenWhenCachedMainMenuWorldWasBootstrapEntry()
        {
            const string entryMapId = "x28_main_menu";
            const string previewMapId = "x28_mode_ue_template";

            using var engine = CreateWorkspaceEngine();
            var navigator = new FakeHostLevelNavigator();
            var context = new TestModContext();
            var gate = new X28MapLoadCompletionGate(context);

            engine.SetService(CoreServiceKeys.MapLoadCompletionGate, gate);
            engine.SetService(UE5AdapterServiceKeys.HostLevelNavigator, navigator);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("Entry", 0));

            var loaded = new List<string>();
            var unloaded = new List<string>();
            RegisterMapEventRecorder(engine, GameEvents.MapLoaded, loaded);
            RegisterMapEventRecorder(engine, GameEvents.MapUnloaded, unloaded);

            object runtime = CreateX28MainMenuRuntime(context, gate);

            engine.LoadMap(entryMapId);
            InvokeInstanceMethod(runtime, "Sync", engine);
            Assert.That(GetNonPublicField<string>(runtime, "_mainMenuAssetName"), Is.EqualTo("Entry"));

            engine.LoadMap(previewMapId);
            navigator.Snapshot = new HostLevelNavigationSnapshot(
                HostLevelTransitionMode.PreviewMod,
                HostLevelNavigationState.Active,
                "/Game/Mods/Template/Maps/TemplateMap",
                "/Game/Mods/Template/Maps/TemplateMap",
                "TemplateMap",
                string.Empty);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("TemplateMap", 1));
            engine.Tick(0.016f);
            InvokeInstanceMethod(runtime, "Sync", engine);

            object mode = ResolveHostMapMode(context, previewMapId);
            InvokeInstanceMethod(runtime, "BackToMainMenu", engine, mode);

            navigator.Snapshot = new HostLevelNavigationSnapshot(
                HostLevelTransitionMode.None,
                HostLevelNavigationState.Idle,
                string.Empty,
                string.Empty,
                "MainMenu",
                string.Empty);
            engine.SetService(UE5AdapterServiceKeys.HostWorldBindingState, new HostWorldBindingSnapshot("MainMenu", 2));
            InvokeInstanceMethod(runtime, "Sync", engine);

            Assert.That(engine.CurrentMapSession?.MapId.Value, Is.EqualTo(entryMapId));
            Assert.That(engine.MapSessions?.GetSession(new MapId(previewMapId)), Is.Null);
            Assert.That(GetNonPublicField<string>(runtime, "_mainMenuAssetName"), Is.EqualTo("MainMenu"));
            Assert.That(loaded.Count(x => string.Equals(x, entryMapId, StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
            Assert.That(unloaded, Does.Contain(previewMapId));
        }

        private static void RegisterMapLoadedRecorder(GameEngine engine, List<(string MapId, MapLoadStatus Status)> loaded)
        {
            engine.TriggerManager.RegisterEventHandler(GameEvents.MapLoaded, ctx =>
            {
                loaded.Add((ctx.Get(CoreServiceKeys.MapId).Value, ctx.Get(CoreServiceKeys.MapLoadStatus)));
                return Task.CompletedTask;
            });
        }

        private static void RegisterMapEventRecorder(GameEngine engine, EventKey eventKey, List<string> events)
        {
            engine.TriggerManager.RegisterEventHandler(eventKey, ctx =>
            {
                events.Add(ctx.Get(CoreServiceKeys.MapId).Value);
                return Task.CompletedTask;
            });
        }

        private static GameEngine CreateCoreEngine()
        {
            string repoRoot = FindLudotsRepoRoot();
            string assetsRoot = Path.Combine(repoRoot, "assets");
            var modPaths = RepoModPaths.ResolveExplicit(repoRoot, new[] { "LudotsCoreMod", "AuditPlaygroundMod" });

            var engine = new GameEngine();
            engine.InitializeWithConfigPipeline(modPaths, assetsRoot);
            engine.Start();
            return engine;
        }

        private static GameEngine CreateWorkspaceEngine()
        {
            string repoRoot = FindLudotsRepoRoot();
            string workspaceRoot = FindWorkspaceRoot();
            string assetsRoot = Path.Combine(repoRoot, "assets");
            var modPaths = RepoModPaths.ResolveExplicit(repoRoot, new[] { "LudotsCoreMod", "CoreInputMod" });
            modPaths.Add(Path.Combine(workspaceRoot, "Mods", "X28MainMenuMod"));

            var engine = new GameEngine();
            engine.InitializeWithConfigPipeline(modPaths, assetsRoot);
            engine.Start();
            AttachUiServices(engine);
            return engine;
        }

        private static int CountMapEntities(World world, string mapId)
        {
            int count = 0;
            MapId target = new MapId(mapId);
            var query = new QueryDescription().WithAll<MapEntity>();
            world.Query(in query, (Entity _, ref MapEntity mapEntity) =>
            {
                if (mapEntity.MapId == target)
                {
                    count++;
                }
            });
            return count;
        }

        private static int CountSuspendedMapEntities(World world, string mapId)
        {
            int count = 0;
            MapId target = new MapId(mapId);
            var query = new QueryDescription().WithAll<MapEntity, SuspendedTag>();
            world.Query(in query, (Entity _, ref MapEntity mapEntity, ref SuspendedTag _) =>
            {
                if (mapEntity.MapId == target)
                {
                    count++;
                }
            });
            return count;
        }

        private static MapLoadCompletionRequest CreateRequest(GameEngine engine, string mapId)
        {
            var config = new MapConfig { Id = mapId };
            var session = new MapSession(new MapId(mapId), config);
            return new MapLoadCompletionRequest(engine, new MapId(mapId), config, session, IsPush: false);
        }

        private static void AttachUiServices(GameEngine engine)
        {
            var uiRoot = new UIRoot(new NoopUiRenderer());
            uiRoot.Resize(1920f, 1080f);
            engine.SetService(CoreServiceKeys.UIRoot, (object)uiRoot);
            engine.SetService(CoreServiceKeys.UiTextMeasurer, (object)new SkiaTextMeasurer());
            engine.SetService(CoreServiceKeys.UiImageSizeProvider, (object)new SkiaImageSizeProvider());
        }

        private static object CreateX28MainMenuRuntime(IModContext context, X28MapLoadCompletionGate gate)
        {
            Type runtimeType = typeof(X28MapLoadCompletionGate).Assembly.GetType("X28MainMenuMod.Runtime.X28MainMenuRuntime", throwOnError: true)!;
            return Activator.CreateInstance(runtimeType, context, gate)
                ?? throw new InvalidOperationException("Failed to create X28MainMenuRuntime.");
        }

        private static object ResolveHostMapMode(IModContext context, string mapId)
        {
            Type resolverType = typeof(X28MapLoadCompletionGate).Assembly.GetType("X28MainMenuMod.Runtime.X28HostMapModeResolver", throwOnError: true)!;
            MethodInfo tryResolve = resolverType.GetMethod("TryResolve", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(resolverType.FullName, "TryResolve");
            object?[] args = { context, mapId, null };
            bool resolved = tryResolve.Invoke(null, args) is true;
            Assert.That(resolved, Is.True, $"Failed to resolve host mode map '{mapId}'.");
            Assert.That(args[2], Is.Not.Null);
            return args[2]!;
        }

        private static object? InvokeInstanceMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(target.GetType().FullName, methodName);
            return method.Invoke(target, args);
        }

        private static void SetNonPublicField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }

        private static T GetNonPublicField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
            return (T)field.GetValue(target)!;
        }

        private static string FindLudotsRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "mods")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate Ludots repo root.");
        }

        private static string FindWorkspaceRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 12 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Mods", "X28MainMenuMod")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Scripts")) &&
                    File.Exists(Path.Combine(dir.FullName, "Scripts", "config.txt")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate X28Ludots workspace root.");
        }

        private sealed class ControlledMapLoadGate : IMapLoadCompletionGate
        {
            private readonly HashSet<string> _delayedMapIds = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, ControlledPendingLoad> _pendingLoads = new(StringComparer.OrdinalIgnoreCase);

            public void Delay(string mapId)
            {
                _delayedMapIds.Add(mapId);
            }

            public void AllowImmediate(string mapId)
            {
                _delayedMapIds.Remove(mapId);
            }

            public ControlledPendingLoad Get(string mapId)
            {
                return _pendingLoads[mapId];
            }

            public IPendingMapLoad BeginPendingLoad(in MapLoadCompletionRequest request)
            {
                if (!_delayedMapIds.Contains(request.MapId.Value))
                {
                    return null!;
                }

                var pending = new ControlledPendingLoad();
                _pendingLoads[request.MapId.Value] = pending;
                return pending;
            }
        }

        private sealed class ControlledPendingLoad : IPendingMapLoad
        {
            public MapLoadCompletionResult Result { get; set; } = MapLoadCompletionResult.Pending();
            public int CancelCount { get; private set; }

            public MapLoadCompletionResult Poll() => Result;

            public void Cancel()
            {
                CancelCount++;
            }
        }

        private sealed class FakeHostLevelNavigator : IHostLevelNavigator
        {
            public readonly List<HostLevelLoadRequest> LoadRequests = new();

            public HostLevelNavigationSnapshot Snapshot { get; set; } = HostLevelNavigationSnapshot.Empty;
            public HostLevelNavigationResult NextLoadResult { get; set; } = HostLevelNavigationResult.Ok(HostLevelNavigationSnapshot.Empty);
            public HostLevelNavigationResult NextExitResult { get; set; } = HostLevelNavigationResult.Ok(HostLevelNavigationSnapshot.Empty);

            public HostLevelNavigationResult Load(in HostLevelLoadRequest request)
            {
                LoadRequests.Add(request);
                return NextLoadResult;
            }

            public HostLevelNavigationResult ExitPreview()
            {
                return NextExitResult;
            }
        }

        private sealed class TestModContext : IModContext
        {
            private readonly VirtualFileSystem _vfs = new();

            public TestModContext()
            {
                string modRoot = Path.Combine(FindWorkspaceRoot(), "Mods", "X28MainMenuMod");
                Assert.That(Directory.Exists(modRoot), Is.True, $"Missing X28MainMenuMod test root: {modRoot}");
                Assert.That(File.Exists(Path.Combine(modRoot, "assets", "host-map-bindings.json")), Is.True);
                _vfs.Mount(ModId, modRoot);
            }

            public string ModId { get; } = "X28MainMenuMod";
            public IVirtualFileSystem VFS => _vfs;
            public FunctionRegistry FunctionRegistry => null!;
            public SystemFactoryRegistry SystemFactoryRegistry => null!;
            public TriggerDecoratorRegistry TriggerDecorators => null!;
            public LogChannel LogChannel => LogChannels.Engine;

            public void OnEvent(EventKey eventKey, Func<ScriptContext, Task> handler)
            {
            }

            public void Log(string message)
            {
            }

            public void Log(LogLevel level, string message)
            {
            }

            public Stream GetResource(string uri)
            {
                return _vfs.GetStream(uri);
            }
        }

        private sealed class NoopUiRenderer : IUiRenderer
        {
            public void Render(UiScene scene, float width, float height)
            {
            }
        }

    }
}
