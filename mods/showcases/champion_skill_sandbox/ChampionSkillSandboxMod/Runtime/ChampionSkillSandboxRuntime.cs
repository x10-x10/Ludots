using System;
using System.Threading.Tasks;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Input.Selection;
using Ludots.Core.Scripting;
using Ludots.Core.UI.EntityCommandPanels;

namespace ChampionSkillSandboxMod.Runtime
{
    internal sealed class ChampionSkillSandboxRuntime
    {
        private EntityCommandPanelHandle _focusPanelHandle = EntityCommandPanelHandle.Invalid;
        private Entity _lastPanelTarget = Entity.Null;
        private string _lastMapId = string.Empty;
        private bool _statesApplied;

        public Task HandleMapFocusedAsync(ScriptContext context)
        {
            if (context.GetEngine() is not GameEngine engine)
            {
                return Task.CompletedTask;
            }

            if (!ChampionSkillSandboxIds.IsSandboxMap(engine.CurrentMapSession?.MapId.Value))
            {
                Disable(engine);
                return Task.CompletedTask;
            }

            EnsureMode(engine);
            EnsureScenarioState(engine);
            SyncFocusPanel(engine);
            return Task.CompletedTask;
        }

        public Task HandleMapUnloadedAsync(ScriptContext context)
        {
            if (context.GetEngine() is not GameEngine engine)
            {
                return Task.CompletedTask;
            }

            if (ChampionSkillSandboxIds.IsSandboxMap(context.Get(CoreServiceKeys.MapId).Value))
            {
                Disable(engine);
            }

            return Task.CompletedTask;
        }

        public void Update(GameEngine engine)
        {
            if (!ChampionSkillSandboxIds.IsSandboxMap(engine.CurrentMapSession?.MapId.Value))
            {
                Disable(engine);
                return;
            }

            EnsureMode(engine);
            EnsureScenarioState(engine);
            SyncFocusPanel(engine);
        }

        private void EnsureScenarioState(GameEngine engine)
        {
            string mapId = engine.CurrentMapSession?.MapId.Value ?? string.Empty;
            if (!string.Equals(_lastMapId, mapId, StringComparison.OrdinalIgnoreCase))
            {
                _lastMapId = mapId;
                _statesApplied = false;
            }

            if (_statesApplied)
            {
                return;
            }

            ApplyInitialTag(engine, ChampionSkillSandboxIds.EzrealCooldownName, ChampionSkillSandboxIds.EzrealBlockedTag);
            ApplyInitialTag(engine, ChampionSkillSandboxIds.GarenCourageName, ChampionSkillSandboxIds.GarenCourageTag);
            ApplyInitialTag(engine, ChampionSkillSandboxIds.JayceHammerName, ChampionSkillSandboxIds.JayceHammerTag);
            SeedInitialSelection(engine);
            _statesApplied = true;
        }

        private static void SeedInitialSelection(GameEngine engine)
        {
            Entity selected = engine.GetService(CoreServiceKeys.SelectedEntity);
            if (engine.World.IsAlive(selected))
            {
                return;
            }

            Entity fallback = ResolveChampionEntity(engine, ChampionSkillSandboxIds.EzrealAlphaName);
            if (fallback == Entity.Null)
            {
                return;
            }

            engine.GlobalContext[CoreServiceKeys.SelectedEntity.Name] = fallback;

            SelectionRuntime? selection = engine.GetService(CoreServiceKeys.SelectionRuntime);
            Entity owner = engine.GetService(CoreServiceKeys.LocalPlayerEntity);
            if (selection == null || owner == Entity.Null || !engine.World.IsAlive(owner))
            {
                return;
            }

            Span<Entity> selectionBuffer = stackalloc Entity[1];
            selectionBuffer[0] = fallback;
            selection.ReplaceSelection(owner, SelectionSetKeys.Ambient, selectionBuffer);
        }

        private static void ApplyInitialTag(GameEngine engine, string entityName, string tagName)
        {
            Entity entity = ResolveChampionEntity(engine, entityName);
            if (entity == Entity.Null)
            {
                return;
            }

            if (!engine.World.Has<GameplayTagContainer>(entity))
            {
                engine.World.Add(entity, new GameplayTagContainer());
            }

            int tagId = TagRegistry.Register(tagName);
            ref var tags = ref engine.World.Get<GameplayTagContainer>(entity);
            if (!tags.HasTag(tagId))
            {
                tags.AddTag(tagId);
                engine.World.Set(entity, tags);
            }
        }

        private void EnsureMode(GameEngine engine)
        {
            if (!engine.GlobalContext.TryGetValue(CoreInputMod.ViewMode.ViewModeManager.GlobalKey, out var managerObj) ||
                managerObj is not CoreInputMod.ViewMode.ViewModeManager viewModeManager)
            {
                return;
            }

            if (!ChampionSkillSandboxIds.IsSandboxMode(viewModeManager.ActiveMode?.Id))
            {
                viewModeManager.SwitchTo(ChampionSkillSandboxIds.SmartCastModeId);
            }
        }

        private void SyncFocusPanel(GameEngine engine)
        {
            IEntityCommandPanelService? service = engine.GetService(CoreServiceKeys.EntityCommandPanelService);
            if (service == null)
            {
                return;
            }

            Entity target = ResolvePanelTarget(engine);
            bool visible = target != Entity.Null;

            if (!_focusPanelHandle.IsValid)
            {
                Entity initialTarget = visible ? target : ResolveChampionEntity(engine, ChampionSkillSandboxIds.EzrealAlphaName);
                _focusPanelHandle = service.Open(new EntityCommandPanelOpenRequest
                {
                    TargetEntity = initialTarget,
                    SourceId = "gas.ability-slots",
                    InstanceKey = "champion-skill-sandbox.focus",
                    Anchor = new EntityCommandPanelAnchor(EntityCommandPanelAnchorPreset.BottomCenter, 0f, 18f),
                    Size = new EntityCommandPanelSize(460f, 276f),
                    InitialGroupIndex = 0,
                    StartVisible = visible
                });
                _lastPanelTarget = initialTarget;
            }

            if (!_focusPanelHandle.IsValid)
            {
                return;
            }

            if (visible && _lastPanelTarget != target)
            {
                service.RebindTarget(_focusPanelHandle, target);
                _lastPanelTarget = target;
            }

            service.SetVisible(_focusPanelHandle, visible);
        }

        private static Entity ResolvePanelTarget(GameEngine engine)
        {
            Entity selected = engine.GetService(CoreServiceKeys.SelectedEntity);
            if (IsControllableChampion(engine, selected))
            {
                return selected;
            }

            Entity local = engine.GetService(CoreServiceKeys.LocalPlayerEntity);
            if (IsControllableChampion(engine, local))
            {
                return local;
            }

            return Entity.Null;
        }

        private static bool IsControllableChampion(GameEngine engine, Entity entity)
        {
            return entity != Entity.Null &&
                   engine.World.IsAlive(entity) &&
                   engine.World.Has<AbilityStateBuffer>(entity) &&
                   engine.World.TryGet(entity, out PlayerOwner owner) &&
                   owner.PlayerId == 1;
        }

        private static Entity ResolveChampionEntity(GameEngine engine, string entityName)
        {
            Entity result = Entity.Null;
            var query = new QueryDescription().WithAll<Name>();
            engine.World.Query(in query, (Entity entity, ref Name name) =>
            {
                if (string.Equals(name.Value, entityName, StringComparison.OrdinalIgnoreCase))
                {
                    result = entity;
                }
            });
            return result;
        }

        private void Disable(GameEngine engine)
        {
            if (_focusPanelHandle.IsValid &&
                engine.GetService(CoreServiceKeys.EntityCommandPanelService) is IEntityCommandPanelService service)
            {
                service.Close(_focusPanelHandle);
            }

            if (engine.GlobalContext.TryGetValue(CoreInputMod.ViewMode.ViewModeManager.GlobalKey, out var managerObj) &&
                managerObj is CoreInputMod.ViewMode.ViewModeManager viewModeManager &&
                ChampionSkillSandboxIds.IsSandboxMode(viewModeManager.ActiveMode?.Id))
            {
                viewModeManager.ClearActiveMode();
            }

            _focusPanelHandle = EntityCommandPanelHandle.Invalid;
            _lastPanelTarget = Entity.Null;
            _statesApplied = false;
            _lastMapId = string.Empty;
        }
    }
}
