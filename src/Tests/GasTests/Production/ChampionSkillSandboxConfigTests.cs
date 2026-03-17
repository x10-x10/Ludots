using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Config;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Gameplay.GAS;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Gameplay.GAS.Systems;
using Ludots.Core.Input.Config;
using Ludots.Core.Input.Orders;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Input.Selection;
using Ludots.Core.Presentation.Performers;
using Ludots.Core.Presentation.Projectiles;
using Ludots.Core.Presentation.Rendering;
using Ludots.Core.Scripting;
using Ludots.Core.UI.EntityCommandPanels;
using Ludots.Platform.Abstractions;
using Ludots.UI;
using Ludots.UI.Skia;
using NUnit.Framework;

namespace Ludots.Tests.GAS.Production
{
    [NonParallelizable]
    [TestFixture]
    public sealed class ChampionSkillSandboxConfigTests
    {
        private const float DeltaTime = 1f / 60f;
        private const string SandboxTacticalCameraId = "ChampionSkillSandbox.Camera.Tactical";
        private const string FreeCameraToolbarButtonId = "ChampionSkillSandbox.Camera.Free";
        private const string FollowSelectionToolbarButtonId = "ChampionSkillSandbox.Camera.Selection";
        private const string FollowSelectionGroupToolbarButtonId = "ChampionSkillSandbox.Camera.SelectionGroup";
        private const string ResetCameraToolbarButtonId = "ChampionSkillSandbox.Camera.Reset";
        private static readonly string[] SandboxMods =
        {
            "LudotsCoreMod",
            "CoreInputMod",
            "CameraProfilesMod",
            "EntityCommandPanelMod",
            "ChampionSkillSandboxMod"
        };

        [Test]
        public void ChampionSkillSandbox_TemplateConfig_LoadsJayceFormRouting()
        {
            using var engine = CreateEngine();

            var templates = new Dictionary<string, EntityTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (var template in engine.MapLoader.TemplateRegistry.GetAll())
            {
                templates[template.Id] = template;
            }

            var entity = new EntityBuilder(engine.World, templates, engine.MapLoader.PresentationAuthoringContext)
                .UseTemplate("champion_skill_sandbox_jayce")
                .Build();

            Assert.That(engine.World.Has<AbilityStateBuffer>(entity), Is.True);
            Assert.That(engine.World.Has<AbilityFormSetRef>(entity), Is.True);
            Assert.That(engine.World.Has<AbilityFormSlotBuffer>(entity), Is.True);
            Assert.That(engine.World.Has<PlayerOwner>(entity), Is.False, "Template should not hardcode scene ownership.");

            ref var abilities = ref engine.World.Get<AbilityStateBuffer>(entity);
            Assert.That(abilities.Count, Is.EqualTo(4));

            int hammerTagId = TagRegistry.GetId("State.Champion.Jayce.Hammer");
            Assert.That(hammerTagId, Is.GreaterThan(0), "Jayce hammer tag should be registered by sandbox GAS config.");

            ref var tags = ref engine.World.Get<GameplayTagContainer>(entity);
            tags.AddTag(hammerTagId);

            var routing = new AbilityFormRoutingSystem(
                engine.World,
                engine.GetService(CoreServiceKeys.AbilityFormSetRegistry),
                engine.GetService(CoreServiceKeys.TagOps));
            routing.Update(0f);

            ref var formSlots = ref engine.World.Get<AbilityFormSlotBuffer>(entity);
            var grantedSlots = default(GrantedSlotBuffer);
            int hammerQ = AbilityIdRegistry.GetId("Ability.Champion.Jayce.Hammer.ToTheSkies");
            int hammerW = AbilityIdRegistry.GetId("Ability.Champion.Jayce.Hammer.LightningField");
            int cannonR = AbilityIdRegistry.GetId("Ability.Champion.Jayce.Transform.Cannon");

            Assert.That(
                AbilitySlotResolver.Resolve(in abilities, in formSlots, hasForm: true, in grantedSlots, hasGranted: false, slotIndex: 0).AbilityId,
                Is.EqualTo(hammerQ));
            Assert.That(
                AbilitySlotResolver.Resolve(in abilities, in formSlots, hasForm: true, in grantedSlots, hasGranted: false, slotIndex: 1).AbilityId,
                Is.EqualTo(hammerW));
            Assert.That(
                AbilitySlotResolver.Resolve(in abilities, in formSlots, hasForm: true, in grantedSlots, hasGranted: false, slotIndex: 3).AbilityId,
                Is.EqualTo(cannonR));
        }

        [Test]
        public void ChampionSkillSandbox_MapLoad_SeedsStateOwnershipAndPanelPresentation()
        {
            using var engine = CreateEngine();
            LoadMap(engine, "champion_skill_sandbox");

            AssertNamedEntityOwner(engine.World, "Ezreal Alpha", expectedPlayerId: 1);
            AssertNamedEntityOwner(engine.World, "Ezreal Cooldown", expectedPlayerId: 1);
            AssertNamedEntityOwner(engine.World, "Garen Alpha", expectedPlayerId: 1);
            AssertNamedEntityOwner(engine.World, "Garen Courage", expectedPlayerId: 1);
            AssertNamedEntityOwner(engine.World, "Jayce Cannon", expectedPlayerId: 1);
            AssertNamedEntityOwner(engine.World, "Jayce Hammer", expectedPlayerId: 1);

            AssertEntityHasTag(engine.World, "Ezreal Cooldown", "Cooldown.Champion.Ezreal.R");
            AssertEntityHasTag(engine.World, "Garen Courage", "State.Champion.Garen.Courage");
            AssertEntityHasTag(engine.World, "Jayce Hammer", "State.Champion.Jayce.Hammer");

            Entity selected = engine.GlobalContext.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var selectedObj) &&
                              selectedObj is Entity typedSelected
                ? typedSelected
                : Entity.Null;
            Assert.That(ReadEntityName(engine.World, selected), Is.EqualTo("Ezreal Alpha"), "Sandbox runtime should seed an initial controllable selection.");
            var performerRegistry = engine.GetService(CoreServiceKeys.PerformerDefinitionRegistry)
                ?? throw new InvalidOperationException("PerformerDefinitionRegistry missing.");
            Assert.That(
                performerRegistry.GetId("champion_skill_sandbox.selection_indicator"),
                Is.GreaterThan(0),
                "Sandbox performer config should register a dedicated selection indicator.");
            Assert.That(
                performerRegistry.GetId("champion_skill_sandbox.hover_indicator"),
                Is.GreaterThan(0),
                "Sandbox performer config should register a dedicated hover indicator.");
            var cameraRegistry = engine.GetService(CoreServiceKeys.VirtualCameraRegistry)
                ?? throw new InvalidOperationException("VirtualCameraRegistry missing.");
            Assert.That(
                cameraRegistry.TryGet(SandboxTacticalCameraId, out var sandboxCamera),
                Is.True,
                "Sandbox config should register a dedicated tactical camera profile.");
            Assert.That(sandboxCamera, Is.Not.Null);
            Assert.That(sandboxCamera!.ConfineTargetToWorldBounds, Is.True, "Sandbox tactical camera should clamp target to map bounds.");
            Assert.That(sandboxCamera.EdgePanRequiresPointerInsideViewport, Is.True, "Sandbox tactical camera should ignore edge pan when cursor is outside the viewport.");
            Assert.That(sandboxCamera.FollowMode, Is.EqualTo(CameraFollowMode.AlwaysFollow), "Sandbox tactical camera should allow dynamic follow targets from the toolbar.");
            Assert.That(
                engine.CurrentMapSession?.MapConfig?.DefaultCamera?.VirtualCameraId,
                Is.EqualTo(SandboxTacticalCameraId),
                "Sandbox map should use its dedicated tactical camera profile.");
            var overlays = engine.GetService(CoreServiceKeys.GroundOverlayBuffer)
                ?? throw new InvalidOperationException("GroundOverlayBuffer missing.");
            Assert.That(CountOverlays(overlays, GroundOverlayShape.Ring), Is.GreaterThan(0), "Initial sandbox selection should render a visible ring.");

            var source = ResolveGasPanelSource(engine);

            var ezrealSlots = new EntityCommandPanelSlotView[8];
            int ezrealCount = source.CopySlots(FindEntityByName(engine.World, "Ezreal Cooldown"), 0, ezrealSlots);
            Assert.That(ezrealCount, Is.EqualTo(4));
            Assert.That(ezrealSlots[3].DisplayLabel, Is.EqualTo("Trueshot Barrage"));
            Assert.That(ezrealSlots[3].ActionId, Is.EqualTo("SkillR"));
            Assert.That(ezrealSlots[3].DetailLabel, Is.EqualTo("Long line blast"));
            Assert.That(ezrealSlots[3].StateFlags.HasFlag(EntityCommandSlotStateFlags.Blocked), Is.True);

            var garenSlots = new EntityCommandPanelSlotView[8];
            int garenCount = source.CopySlots(FindEntityByName(engine.World, "Garen Courage"), 0, garenSlots);
            Assert.That(garenCount, Is.EqualTo(4));
            Assert.That(garenSlots[1].DisplayLabel, Is.EqualTo("Courage"));
            Assert.That(garenSlots[1].ActionId, Is.EqualTo("SkillW"));
            Assert.That(garenSlots[1].StateFlags.HasFlag(EntityCommandSlotStateFlags.Active), Is.True);

            var jayceSlots = new EntityCommandPanelSlotView[8];
            int jayceCount = source.CopySlots(FindEntityByName(engine.World, "Jayce Hammer"), 0, jayceSlots);
            Assert.That(jayceCount, Is.EqualTo(4));
            Assert.That(jayceSlots[0].DisplayLabel, Is.EqualTo("To The Skies!"));
            Assert.That(jayceSlots[0].ActionId, Is.EqualTo("SkillQ"));
            Assert.That(jayceSlots[0].StateFlags.HasFlag(EntityCommandSlotStateFlags.FormOverride), Is.True);
            Assert.That(jayceSlots[3].DisplayLabel, Is.EqualTo("Mercury Cannon"));
        }

        [Test]
        public void ChampionSkillSandbox_CastModeToolbar_SwitchesInteractionModeAndPanelHints()
        {
            using var engine = CreateEngine();
            LoadMap(engine, "champion_skill_sandbox");

            var toolbar = engine.GetService(CoreServiceKeys.EntityCommandPanelToolbarProvider)
                ?? throw new InvalidOperationException("Toolbar provider missing.");
            var mapping = WaitForActiveInputOrderMapping(engine);
            Assert.That(toolbar.IsVisible, Is.True);
            Assert.That(mapping.InteractionMode, Is.EqualTo(InteractionModeType.SmartCast));

            var buttons = new EntityCommandPanelToolbarButtonView[5];
            int buttonCount = toolbar.CopyButtons(buttons);
            Assert.That(buttonCount, Is.EqualTo(5), "Small buffers should safely truncate the toolbar.");

            buttons = new EntityCommandPanelToolbarButtonView[8];
            buttonCount = toolbar.CopyButtons(buttons);
            Assert.That(buttonCount, Is.EqualTo(7));
            Assert.That(buttons[0].ButtonId, Is.EqualTo("ChampionSkillSandbox.Mode.SmartCast"));
            Assert.That(buttons[0].Active, Is.True);
            Assert.That(buttons[3].ButtonId, Is.EqualTo(FreeCameraToolbarButtonId));
            Assert.That(buttons[3].Active, Is.True);
            Assert.That(buttons[6].ButtonId, Is.EqualTo(ResetCameraToolbarButtonId));
            Assert.That(toolbar.Subtitle, Does.Contain("RMB Move"));

            var source = ResolveGasPanelSource(engine);
            Entity ezreal = FindEntityByName(engine.World, "Ezreal Alpha");
            var slots = new EntityCommandPanelSlotView[8];

            source.CopySlots(ezreal, 0, slots);
            Assert.That(slots[0].DetailLabel, Is.EqualTo("Line shot to cursor"));

            toolbar.Activate("ChampionSkillSandbox.Mode.Indicator");
            Tick(engine, 1);
            Assert.That(mapping.InteractionMode, Is.EqualTo(InteractionModeType.SmartCastWithIndicator));
            toolbar.CopyButtons(buttons);
            Assert.That(buttons[1].Active, Is.True);

            source.CopySlots(ezreal, 0, slots);
            Assert.That(slots[0].DetailLabel, Is.EqualTo("Hold to preview line shot"));

            toolbar.Activate("ChampionSkillSandbox.Mode.PressReleaseAim");
            Tick(engine, 1);
            Assert.That(mapping.InteractionMode, Is.EqualTo(InteractionModeType.PressReleaseAimCast));
            toolbar.CopyButtons(buttons);
            Assert.That(buttons[2].Active, Is.True);

            source.CopySlots(ezreal, 0, slots);
            Assert.That(slots[0].DetailLabel, Is.EqualTo("Release key, then confirm line shot"));

            InputOrderMapping? command = mapping.GetMapping("Command");
            Assert.That(command, Is.Not.Null);
            Assert.That(command!.OrderTypeKey, Is.EqualTo("moveTo"));
            Assert.That(command.SelectionType, Is.EqualTo(OrderSelectionType.Position));

            Entity localPlayer = engine.GetService(CoreServiceKeys.LocalPlayerEntity);
            Entity ezrealCooldown = FindEntityByName(engine.World, "Ezreal Cooldown");
            Entity garenAlpha = FindEntityByName(engine.World, "Garen Alpha");
            ref var selection = ref engine.World.Get<SelectionBuffer>(localPlayer);
            selection.Clear();
            selection.Add(ezreal);
            selection.Add(garenAlpha);
            engine.World.Set(localPlayer, selection);

            engine.World.Add(ezreal, new CameraFollowWeight { Value = 1f });
            engine.World.Add(garenAlpha, new CameraFollowWeight { Value = 3f });

            toolbar.Activate(FollowSelectionToolbarButtonId);
            Tick(engine, 2);
            toolbar.CopyButtons(buttons);
            Assert.That(buttons[3].Active, Is.False);
            Assert.That(buttons[4].Active, Is.True);

            engine.GameSession.Camera.Update(DeltaTime);
            Assert.That(engine.GameSession.Camera.FollowTargetPositionCm.HasValue, Is.True);

            toolbar.Activate(FollowSelectionGroupToolbarButtonId);
            Tick(engine, 2);
            toolbar.CopyButtons(buttons);
            Assert.That(buttons[5].Active, Is.True);
            engine.GameSession.Camera.Update(DeltaTime);
            Assert.That(engine.GameSession.Camera.FollowTargetPositionCm.HasValue, Is.True);

            Vector2 ezrealPos = engine.World.Get<WorldPositionCm>(ezreal).Value.ToVector2();
            Vector2 garenPos = engine.World.Get<WorldPositionCm>(garenAlpha).Value.ToVector2();
            Vector2 expectedGroup = (ezrealPos + (garenPos * 3f)) / 4f;
            Assert.That(engine.GameSession.Camera.FollowTargetPositionCm!.Value.X, Is.EqualTo(expectedGroup.X).Within(0.01f));
            Assert.That(engine.GameSession.Camera.FollowTargetPositionCm!.Value.Y, Is.EqualTo(expectedGroup.Y).Within(0.01f));

            toolbar.Activate(FreeCameraToolbarButtonId);
            Tick(engine, 2);
            toolbar.CopyButtons(buttons);
            Assert.That(buttons[3].Active, Is.True);
            Assert.That(engine.GameSession.Camera.FollowTargetPositionCm, Is.Null);

            engine.GameSession.Camera.ApplyPose(new CameraPoseRequest
            {
                VirtualCameraId = SandboxTacticalCameraId,
                TargetCm = new Vector2(2600f, 1480f),
                DistanceCm = 6100f,
                Pitch = 62f,
                FovYDeg = 50f,
            });
            Tick(engine, 1);

            toolbar.Activate(ResetCameraToolbarButtonId);
            Tick(engine, 4);

            Assert.That(engine.GameSession.Camera.VirtualCameraBrain?.ActiveCameraId, Is.EqualTo(SandboxTacticalCameraId));
            Assert.That(engine.GameSession.Camera.State.TargetCm.X, Is.EqualTo(1850f).Within(0.01f));
            Assert.That(engine.GameSession.Camera.State.TargetCm.Y, Is.EqualTo(980f).Within(0.01f));
            Assert.That(engine.GameSession.Camera.State.DistanceCm, Is.EqualTo(3900f).Within(0.01f));
            Assert.That(engine.GameSession.Camera.State.Pitch, Is.EqualTo(54f).Within(0.01f));
            Assert.That(engine.GameSession.Camera.State.FovYDeg, Is.EqualTo(42f).Within(0.01f));
        }

        [Test]
        public void ChampionSkillSandbox_ProjectileBindingsAndSkillCueConfigs_AreRegistered()
        {
            using var engine = CreateEngine();

            var effects = engine.GetService(CoreServiceKeys.EffectTemplateRegistry)
                ?? throw new InvalidOperationException("EffectTemplateRegistry missing.");
            var projectileBindings = engine.GetService(CoreServiceKeys.ProjectilePresentationBindingRegistry)
                ?? throw new InvalidOperationException("ProjectilePresentationBindingRegistry missing.");
            var performers = engine.GetService(CoreServiceKeys.PerformerDefinitionRegistry)
                ?? throw new InvalidOperationException("PerformerDefinitionRegistry missing.");

            AssertProjectileEffect(
                effects,
                projectileBindings,
                performers,
                projectileEffectKey: "Effect.Champion.Ezreal.MysticShot",
                resolveEffectKey: "Effect.Champion.Ezreal.MysticShotResolve",
                projectilePerformerKey: "champion_skill_sandbox.projectile.ezreal_q");
            AssertProjectileEffect(
                effects,
                projectileBindings,
                performers,
                projectileEffectKey: "Effect.Champion.Ezreal.TrueshotBarrage",
                resolveEffectKey: "Effect.Champion.Ezreal.TrueshotBarrageResolve",
                projectilePerformerKey: "champion_skill_sandbox.projectile.ezreal_r");
            AssertProjectileEffect(
                effects,
                projectileBindings,
                performers,
                projectileEffectKey: "Effect.Champion.Jayce.Cannon.ShockBlast",
                resolveEffectKey: "Effect.Champion.Jayce.Cannon.ShockBlastResolve",
                projectilePerformerKey: "champion_skill_sandbox.projectile.jayce_q");

            Assert.That(performers.GetId("champion_skill_sandbox.cue.ezreal_arcane_shift"), Is.GreaterThan(0));
            Assert.That(performers.GetId("champion_skill_sandbox.cue.ezreal_essence_flux_cast"), Is.GreaterThan(0));
            Assert.That(performers.GetId("champion_skill_sandbox.cue.ezreal_essence_flux_hit"), Is.GreaterThan(0));
            Assert.That(performers.GetId("champion_skill_sandbox.cue.garen_courage"), Is.GreaterThan(0));
            Assert.That(performers.GetId("champion_skill_sandbox.cue.garen_demacian_justice_hit"), Is.GreaterThan(0));
            Assert.That(performers.GetId("champion_skill_sandbox.cue.jayce_hammer_lightning_field"), Is.GreaterThan(0));
            Assert.That(performers.GetId("champion_skill_sandbox.cue.jayce_transform_hammer"), Is.GreaterThan(0));
        }

        private static GameEngine CreateEngine()
        {
            string repoRoot = FindRepoRoot();
            string assetsRoot = Path.Combine(repoRoot, "assets");
            var modPaths = RepoModPaths.ResolveExplicit(repoRoot, SandboxMods);

            var engine = new GameEngine();
            engine.InitializeWithConfigPipeline(modPaths, assetsRoot);
            InstallInput(engine);
            InstallUi(engine);
            engine.Start();
            return engine;
        }

        private static void InstallInput(GameEngine engine)
        {
            var inputConfig = new InputConfigPipelineLoader(engine.ConfigPipeline).Load();
            var backend = new NullInputBackend();
            var inputHandler = new PlayerInputHandler(backend, inputConfig);
            for (int i = 0; i < engine.MergedConfig.StartupInputContexts.Count; i++)
            {
                inputHandler.PushContext(engine.MergedConfig.StartupInputContexts[i]);
            }

            engine.SetService(CoreServiceKeys.InputHandler, inputHandler);
            engine.SetService(CoreServiceKeys.InputBackend, (IInputBackend)backend);
            engine.SetService(CoreServiceKeys.UiCaptured, false);
        }

        private static void InstallUi(GameEngine engine)
        {
            var uiRoot = new UIRoot(new SkiaUiRenderer());
            uiRoot.Resize(1920f, 1080f);
            engine.SetService(CoreServiceKeys.UIRoot, uiRoot);
            engine.SetService(CoreServiceKeys.UiTextMeasurer, (object)new SkiaTextMeasurer());
            engine.SetService(CoreServiceKeys.UiImageSizeProvider, (object)new SkiaImageSizeProvider());
        }

        private static void LoadMap(GameEngine engine, string mapId, int frames = 12)
        {
            engine.LoadMap(mapId);
            Assert.That(engine.CurrentMapSession, Is.Not.Null, $"{mapId} should create a live map session.");
            Tick(engine, frames);
            Assert.That(engine.TriggerManager.Errors.Count, Is.EqualTo(0), "Sandbox map should load without trigger errors.");
        }

        private static InputOrderMappingSystem WaitForActiveInputOrderMapping(GameEngine engine, int maxFrames = 24)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                var mapping = engine.GetService(CoreServiceKeys.ActiveInputOrderMapping);
                if (mapping != null)
                {
                    return mapping;
                }

                Tick(engine, 1);
            }

            throw new InvalidOperationException("Active input order mapping missing.");
        }

        private static void Tick(GameEngine engine, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                engine.Tick(DeltaTime);
            }
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

        private static void AssertProjectileEffect(
            EffectTemplateRegistry effects,
            ProjectilePresentationBindingRegistry projectileBindings,
            PerformerDefinitionRegistry performers,
            string projectileEffectKey,
            string resolveEffectKey,
            string projectilePerformerKey)
        {
            int projectileEffectId = EffectTemplateIdRegistry.GetId(projectileEffectKey);
            int resolveEffectId = EffectTemplateIdRegistry.GetId(resolveEffectKey);
            int projectilePerformerId = performers.GetId(projectilePerformerKey);

            Assert.That(projectileEffectId, Is.GreaterThan(0), $"{projectileEffectKey} should be registered.");
            Assert.That(resolveEffectId, Is.GreaterThan(0), $"{resolveEffectKey} should be registered.");
            Assert.That(projectilePerformerId, Is.GreaterThan(0), $"{projectilePerformerKey} should be registered.");

            Assert.That(effects.TryGet(projectileEffectId, out var projectileEffect), Is.True);
            Assert.That(projectileEffect.PresetType, Is.EqualTo(EffectPresetType.LaunchProjectile));
            Assert.That(projectileEffect.Projectile.ImpactEffectTemplateId, Is.EqualTo(resolveEffectId));

            Assert.That(effects.TryGet(resolveEffectId, out var resolveEffect), Is.True);
            Assert.That(resolveEffect.PresetType, Is.EqualTo(EffectPresetType.Search));

            Assert.That(projectileBindings.TryGet(resolveEffectId, out var binding), Is.True);
            Assert.That(binding.ImpactEffectTemplateId, Is.EqualTo(resolveEffectId));
            Assert.That(binding.StartupPerformers.Count, Is.EqualTo(1));
            Assert.That(binding.StartupPerformers.Get(0), Is.EqualTo(projectilePerformerId));
        }

        private static IEntityCommandPanelSource ResolveGasPanelSource(GameEngine engine)
        {
            var registry = engine.GetService(CoreServiceKeys.EntityCommandPanelSourceRegistry)
                ?? throw new InvalidOperationException("EntityCommandPanelSourceRegistry missing.");
            Assert.That(registry.TryGet("gas.ability-slots", out IEntityCommandPanelSource source), Is.True);
            return source;
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

        private static string ReadEntityName(World world, Entity entity)
        {
            return entity != Entity.Null && world.IsAlive(entity) && world.TryGet(entity, out Name name)
                ? name.Value
                : string.Empty;
        }

        private static void AssertNamedEntityOwner(World world, string entityName, int expectedPlayerId)
        {
            Entity found = FindEntityByName(world, entityName);
            Assert.That(world.TryGet(found, out PlayerOwner owner), Is.True, $"{entityName} should have PlayerOwner.");
            Assert.That(owner.PlayerId, Is.EqualTo(expectedPlayerId), $"{entityName} should receive PlayerOwner from map instance overrides.");
        }

        private static void AssertEntityHasTag(World world, string entityName, string tagName)
        {
            Entity found = FindEntityByName(world, entityName);
            int tagId = TagRegistry.GetId(tagName);
            Assert.That(tagId, Is.GreaterThan(0), $"{tagName} should be registered.");
            Assert.That(world.TryGet(found, out GameplayTagContainer tags), Is.True, $"{entityName} should carry gameplay tags.");
            Assert.That(tags.HasTag(tagId), Is.True, $"{entityName} should contain tag {tagName}.");
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

        private sealed class NullInputBackend : IInputBackend
        {
            public float GetAxis(string devicePath) => 0f;
            public bool GetButton(string devicePath) => false;
            public System.Numerics.Vector2 GetMousePosition() => System.Numerics.Vector2.Zero;
            public float GetMouseWheel() => 0f;
            public void EnableIME(bool enable) { }
            public void SetIMECandidatePosition(int x, int y) { }
            public string GetCharBuffer() => string.Empty;
        }
    }
}
