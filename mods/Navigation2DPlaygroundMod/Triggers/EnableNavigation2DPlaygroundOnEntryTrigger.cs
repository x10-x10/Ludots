using System;
using System.Threading.Tasks;
using Ludots.Core.Config;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Map;
using Ludots.Core.Modding;
using Ludots.Core.Navigation2D.Runtime;
using Ludots.Core.Physics2D.Systems;
using Ludots.Core.Presentation.Assets;
using Ludots.Core.Presentation.DebugDraw;
using Ludots.Core.Scripting;
using Navigation2DPlaygroundMod.Input;
using Navigation2DPlaygroundMod.Systems;

namespace Navigation2DPlaygroundMod.Triggers
{
    public sealed class EnableNavigation2DPlaygroundOnEntryTrigger : Trigger
    {
        private readonly IModContext _ctx;
        private bool _installed;
        private bool _inputContextActive;
        private const string InputContextId = Navigation2DPlaygroundInputContexts.Playground;

        public EnableNavigation2DPlaygroundOnEntryTrigger(IModContext ctx)
        {
            _ctx = ctx;
            EventKey = GameEvents.MapLoaded;
        }

        public override Task ExecuteAsync(ScriptContext context)
        {
            var engine = context.GetEngine();
            if (engine == null)
            {
                return Task.CompletedTask;
            }

            var mapId = context.Get(CoreServiceKeys.MapId);
            bool isEntry = mapId.Value == engine.MergedConfig.StartupMapId;

            if (isEntry)
            {
                if (!_installed)
                {
                    if (!engine.GlobalContext.TryGetValue(CoreServiceKeys.Navigation2DRuntime.Name, out var navObj) || navObj is not Navigation2DRuntime navRuntime)
                    {
                        throw new InvalidOperationException("Navigation2DPlaygroundMod requires Navigation2D.Enabled=true so Navigation2DRuntime exists in GlobalContext.");
                    }

                    navRuntime.FlowEnabled = true;

                    var debugDrawBuffer = new DebugDrawCommandBuffer();
                    engine.SetService(CoreServiceKeys.DebugDrawCommandBuffer, debugDrawBuffer);

                    engine.RegisterSystem(new Physics2DToWorldPositionSyncSystem(engine.World), SystemGroup.PostMovement);
                    engine.RegisterSystem(new IntegrationSystem2D(engine.World), SystemGroup.InputCollection);

                    engine.RegisterPresentationSystem(new Navigation2DPlaygroundControlSystem(engine));
                    var meshRegistry = context.Get(CoreServiceKeys.PresentationMeshAssetRegistry) as MeshAssetRegistry;
                    engine.RegisterPresentationSystem(new Navigation2DPlaygroundPresentationSystem(engine, debugDrawBuffer, meshRegistry));

                    GameConfig? gameConfig = engine.GetService(CoreServiceKeys.GameConfig);
                    var playgroundConfig = Navigation2DPlaygroundScenarioSpawner.GetPlaygroundConfig(gameConfig);
                    Navigation2DPlaygroundState.AgentsPerTeam = playgroundConfig.DefaultAgentsPerTeam;
                    Navigation2DPlaygroundState.CurrentScenarioIndex = playgroundConfig.DefaultScenarioIndex;

                    var scenario = Navigation2DPlaygroundScenarioSpawner.GetScenario(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex);
                    var summary = Navigation2DPlaygroundScenarioSpawner.SpawnScenario(engine.World, scenario, Navigation2DPlaygroundState.AgentsPerTeam);
                    Navigation2DPlaygroundControlSystem.PublishScenarioServices(
                        engine,
                        playgroundConfig,
                        summary,
                        Navigation2DPlaygroundState.AgentsPerTeam,
                        Navigation2DPlaygroundState.CurrentScenarioIndex);

                    _installed = true;
                    _ctx.Log($"[Navigation2DPlaygroundMod] Installed systems and spawned scenario '{summary.ScenarioName}'.");
                }

                Navigation2DPlaygroundState.Enabled = true;

                var input = context.Get(CoreServiceKeys.InputHandler);
                if (input != null && !_inputContextActive)
                {
                    EnsurePlaygroundInputSchema(input);
                    input.PushContext(InputContextId);
                    _inputContextActive = true;
                }
            }
            else
            {
                Navigation2DPlaygroundState.Enabled = false;
                var input = context.Get(CoreServiceKeys.InputHandler);
                if (input != null && _inputContextActive)
                {
                    input.PopContext(InputContextId);
                    _inputContextActive = false;
                }
            }

            return Task.CompletedTask;
        }

        private static void EnsurePlaygroundInputSchema(PlayerInputHandler input)
        {
            if (!input.HasContext(Navigation2DPlaygroundInputContexts.Playground))
            {
                throw new InvalidOperationException($"Missing input context: {Navigation2DPlaygroundInputContexts.Playground}");
            }

            if (!input.HasAction(Navigation2DPlaygroundInputActions.ToggleFlowEnabled)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.ToggleFlowEnabled}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.ToggleFlowDebug)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.ToggleFlowDebug}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.CycleFlowDebugMode)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.CycleFlowDebugMode}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.IncreaseFlowIterations)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.IncreaseFlowIterations}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.DecreaseFlowIterations)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.DecreaseFlowIterations}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.IncreaseAgentsPerTeam)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.IncreaseAgentsPerTeam}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.DecreaseAgentsPerTeam)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.DecreaseAgentsPerTeam}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.PreviousScenario)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.PreviousScenario}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.NextScenario)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.NextScenario}");
            if (!input.HasAction(Navigation2DPlaygroundInputActions.ResetScenario)) throw new InvalidOperationException($"Missing input action: {Navigation2DPlaygroundInputActions.ResetScenario}");
        }
    }
}
