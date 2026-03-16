using System;
using System.Threading.Tasks;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Input.Selection;
using Ludots.Core.Presentation.Commands;
using Ludots.Core.Presentation.Performers;
using Ludots.Core.Scripting;
using Ludots.Core.UI.EntityCommandPanels;

namespace ChampionSkillSandboxMod.Runtime
{
    internal sealed class ChampionSkillSandboxRuntime
    {
        private EntityCommandPanelHandle _focusPanelHandle = EntityCommandPanelHandle.Invalid;
        private Entity _lastPanelTarget = Entity.Null;
        private Entity _selectionIndicatorTarget = Entity.Null;
        private string _lastMapId = string.Empty;
        private bool _scenarioTagsApplied;
        private bool _initialSelectionApplied;

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
                _scenarioTagsApplied = false;
                _initialSelectionApplied = false;
            }

            if (!_scenarioTagsApplied)
            {
                ApplyInitialTag(engine, ChampionSkillSandboxIds.EzrealCooldownName, ChampionSkillSandboxIds.EzrealBlockedTag);
                ApplyInitialTag(engine, ChampionSkillSandboxIds.GarenCourageName, ChampionSkillSandboxIds.GarenCourageTag);
                ApplyInitialTag(engine, ChampionSkillSandboxIds.JayceHammerName, ChampionSkillSandboxIds.JayceHammerTag);
                _scenarioTagsApplied = true;
            }

            if (!_initialSelectionApplied)
            {
                _initialSelectionApplied = SeedInitialSelection(engine);
            }
        }

        private static bool SeedInitialSelection(GameEngine engine)
        {
            if (engine.GlobalContext.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var selectedObj) &&
                selectedObj is Entity selected &&
                engine.World.IsAlive(selected))
            {
                return true;
            }

            Entity fallback = ResolveChampionEntity(engine, ChampionSkillSandboxIds.EzrealAlphaName);
            if (fallback == Entity.Null)
            {
                return false;
            }

            engine.GlobalContext[CoreServiceKeys.SelectedEntity.Name] = fallback;

            SelectionRuntime? selection = engine.GetService(CoreServiceKeys.SelectionRuntime);
            Entity owner = ResolveOrAssignLocalPlayer(engine, fallback);
            if (selection == null || owner == Entity.Null || !engine.World.IsAlive(owner))
            {
                return false;
            }

            Span<Entity> selectionBuffer = stackalloc Entity[1];
            selectionBuffer[0] = fallback;
            selection.ReplaceSelection(owner, SelectionSetKeys.Ambient, selectionBuffer);
            return true;
        }

        private static Entity ResolveOrAssignLocalPlayer(GameEngine engine, Entity fallback)
        {
            Entity local = engine.GetService(CoreServiceKeys.LocalPlayerEntity);
            if (engine.World.IsAlive(local))
            {
                return local;
            }

            Entity resolved = IsControllableChampion(engine, fallback)
                ? fallback
                : ResolveFirstControllableChampion(engine);
            if (resolved != Entity.Null)
            {
                engine.GlobalContext[CoreServiceKeys.LocalPlayerEntity.Name] = resolved;
            }

            return resolved;
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
            SyncSelectionIndicator(engine, visible ? target : Entity.Null);
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

        private static Entity ResolveFirstControllableChampion(GameEngine engine)
        {
            Entity result = Entity.Null;
            var query = new QueryDescription().WithAll<AbilityStateBuffer, PlayerOwner>();
            engine.World.Query(in query, (Entity entity, ref AbilityStateBuffer _, ref PlayerOwner owner) =>
            {
                if (result != Entity.Null || owner.PlayerId != 1)
                {
                    return;
                }

                result = entity;
            });
            return result;
        }

        private void Disable(GameEngine engine)
        {
            DestroySelectionIndicator(engine);

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
            _selectionIndicatorTarget = Entity.Null;
            _scenarioTagsApplied = false;
            _initialSelectionApplied = false;
            _lastMapId = string.Empty;
        }

        private void SyncSelectionIndicator(GameEngine engine, Entity target)
        {
            if (_selectionIndicatorTarget == target)
            {
                return;
            }

            DestroySelectionIndicator(engine);
            _selectionIndicatorTarget = target;
            if (target == Entity.Null)
            {
                return;
            }

            PresentationCommandBuffer? commands = engine.GetService(CoreServiceKeys.PresentationCommandBuffer);
            PerformerDefinitionRegistry? performers = engine.GetService(CoreServiceKeys.PerformerDefinitionRegistry);
            if (commands == null || performers == null)
            {
                return;
            }

            int definitionId = performers.GetId(ChampionSkillSandboxIds.SelectionIndicatorPerformerKey);
            if (definitionId <= 0)
            {
                throw new InvalidOperationException(
                    $"Performer '{ChampionSkillSandboxIds.SelectionIndicatorPerformerKey}' is required by ChampionSkillSandboxMod.");
            }

            commands.TryAdd(new PresentationCommand
            {
                Kind = PresentationCommandKind.CreatePerformer,
                IdA = definitionId,
                IdB = ChampionSkillSandboxIds.SelectionIndicatorScopeId,
                Source = target,
            });
        }

        private void DestroySelectionIndicator(GameEngine engine)
        {
            if (engine.GetService(CoreServiceKeys.PresentationCommandBuffer) is not PresentationCommandBuffer commands)
            {
                return;
            }

            commands.TryAdd(new PresentationCommand
            {
                Kind = PresentationCommandKind.DestroyPerformerScope,
                IdA = ChampionSkillSandboxIds.SelectionIndicatorScopeId,
            });
        }
    }
}
