using System;
using Arch.Core;
using Arch.System;
using Ludots.Core.Config;
using Ludots.Core.Engine;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Navigation2D.Config;
using Ludots.Core.Navigation2D.Components;
using Ludots.Core.Navigation2D.Runtime;
using Ludots.Core.Scripting;
using Navigation2DPlaygroundMod.Input;

namespace Navigation2DPlaygroundMod.Systems
{
    public sealed class Navigation2DPlaygroundControlSystem : ISystem<float>
    {
        private readonly GameEngine _engine;
        private readonly World _world;
        private PlayerInputHandler? _input;

        private static readonly QueryDescription _scenarioQuery = new QueryDescription()
            .WithAll<NavPlaygroundTeam>();

        private static readonly QueryDescription _flowGoalQuery = new QueryDescription()
            .WithAll<NavFlowGoal2D>();

        public Navigation2DPlaygroundControlSystem(GameEngine engine)
        {
            _engine = engine;
            _world = engine.World;
        }

        public void Initialize()
        {
        }

        public void BeforeUpdate(in float t)
        {
        }

        public void Update(in float deltaTime)
        {
            if (!Navigation2DPlaygroundState.Enabled) return;
            ResolveInput();
            if (_input == null) return;

            if (_engine.GlobalContext.TryGetValue(CoreServiceKeys.Navigation2DRuntime.Name, out var runtimeObj) &&
                runtimeObj is Navigation2DRuntime navRuntime)
            {
                HandlePressed(Navigation2DPlaygroundInputActions.ToggleFlowEnabled, () => navRuntime.FlowEnabled = !navRuntime.FlowEnabled);
                HandlePressed(Navigation2DPlaygroundInputActions.ToggleFlowDebug, () => navRuntime.FlowDebugEnabled = !navRuntime.FlowDebugEnabled);
                HandlePressed(Navigation2DPlaygroundInputActions.CycleFlowDebugMode, () => navRuntime.FlowDebugMode = (navRuntime.FlowDebugMode + 1) % 3);
                HandlePressed(Navigation2DPlaygroundInputActions.IncreaseFlowIterations, () => navRuntime.FlowIterationsPerTick = Math.Clamp(navRuntime.FlowIterationsPerTick + 512, 0, 131072));
                HandlePressed(Navigation2DPlaygroundInputActions.DecreaseFlowIterations, () => navRuntime.FlowIterationsPerTick = Math.Clamp(navRuntime.FlowIterationsPerTick - 512, 0, 131072));
            }

            HandlePressed(Navigation2DPlaygroundInputActions.IncreaseAgentsPerTeam, () => AdjustAgentsPerTeam(GetPlaygroundConfig().AgentsPerTeamStep));
            HandlePressed(Navigation2DPlaygroundInputActions.DecreaseAgentsPerTeam, () => AdjustAgentsPerTeam(-GetPlaygroundConfig().AgentsPerTeamStep));
            HandlePressed(Navigation2DPlaygroundInputActions.PreviousScenario, PreviousScenario);
            HandlePressed(Navigation2DPlaygroundInputActions.NextScenario, NextScenario);
            HandlePressed(Navigation2DPlaygroundInputActions.ResetScenario, RespawnScenario);
        }

        public void AfterUpdate(in float t)
        {
        }

        public void Dispose()
        {
        }

        public static void PublishScenarioServices(
            GameEngine engine,
            Navigation2DPlaygroundConfig playgroundConfig,
            Navigation2DPlaygroundSpawnSummary summary,
            int agentsPerTeam,
            int scenarioIndex)
        {
            engine.SetService(Navigation2DPlaygroundKeys.AgentsPerTeam, agentsPerTeam);
            engine.SetService(Navigation2DPlaygroundKeys.LiveAgentsTotal, summary.DynamicAgents);
            engine.SetService(Navigation2DPlaygroundKeys.BlockerCount, summary.BlockerCount);
            engine.SetService(Navigation2DPlaygroundKeys.ScenarioIndex, scenarioIndex);
            engine.SetService(Navigation2DPlaygroundKeys.ScenarioCount, playgroundConfig.Scenarios.Count);
            engine.SetService(Navigation2DPlaygroundKeys.ScenarioTeamCount, summary.TeamCount);
            engine.SetService(Navigation2DPlaygroundKeys.ScenarioId, summary.ScenarioId);
            engine.SetService(Navigation2DPlaygroundKeys.ScenarioName, summary.ScenarioName);
            engine.SetService(Navigation2DPlaygroundKeys.FlowDebugLines, 0);
        }

        private void ResolveInput()
        {
            if (_input != null) return;
            if (_engine.GlobalContext.TryGetValue(CoreServiceKeys.InputHandler.Name, out var inputObj) && inputObj is PlayerInputHandler handler)
            {
                _input = handler;
            }
        }

        private void HandlePressed(string actionId, Action onPressed)
        {
            if (_input!.PressedThisFrame(actionId))
            {
                onPressed();
            }
        }

        private void AdjustAgentsPerTeam(int delta)
        {
            var playgroundConfig = GetPlaygroundConfig();
            var scenario = Navigation2DPlaygroundScenarioSpawner.GetScenario(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex);
            int maxAgentsPerTeam = GetMaxAgentsPerTeam(scenario.TeamCount);
            int next = Navigation2DPlaygroundState.AgentsPerTeam + delta;
            if (next < 0) next = 0;
            if (next > maxAgentsPerTeam) next = maxAgentsPerTeam;
            if (next == Navigation2DPlaygroundState.AgentsPerTeam) return;

            Navigation2DPlaygroundState.AgentsPerTeam = next;
            RespawnScenario();
        }

        private void PreviousScenario()
        {
            var playgroundConfig = GetPlaygroundConfig();
            Navigation2DPlaygroundState.CurrentScenarioIndex = Navigation2DPlaygroundScenarioSpawner.ClampScenarioIndex(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex - 1);
            ClampAgentsPerTeamForCurrentScenario(playgroundConfig);
            RespawnScenario();
        }

        private void NextScenario()
        {
            var playgroundConfig = GetPlaygroundConfig();
            Navigation2DPlaygroundState.CurrentScenarioIndex = Navigation2DPlaygroundScenarioSpawner.ClampScenarioIndex(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex + 1);
            ClampAgentsPerTeamForCurrentScenario(playgroundConfig);
            RespawnScenario();
        }

        private void RespawnScenario()
        {
            _world.Destroy(in _scenarioQuery);
            _world.Destroy(in _flowGoalQuery);

            var playgroundConfig = GetPlaygroundConfig();
            Navigation2DPlaygroundState.CurrentScenarioIndex = Navigation2DPlaygroundScenarioSpawner.ClampScenarioIndex(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex);
            ClampAgentsPerTeamForCurrentScenario(playgroundConfig);

            var scenario = Navigation2DPlaygroundScenarioSpawner.GetScenario(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex);
            var summary = Navigation2DPlaygroundScenarioSpawner.SpawnScenario(_world, scenario, Navigation2DPlaygroundState.AgentsPerTeam);
            PublishScenarioServices(_engine, playgroundConfig, summary, Navigation2DPlaygroundState.AgentsPerTeam, Navigation2DPlaygroundState.CurrentScenarioIndex);
        }

        private Navigation2DPlaygroundConfig GetPlaygroundConfig()
        {
            GameConfig? gameConfig = _engine.GetService(CoreServiceKeys.GameConfig);
            return Navigation2DPlaygroundScenarioSpawner.GetPlaygroundConfig(gameConfig);
        }

        private void ClampAgentsPerTeamForCurrentScenario(Navigation2DPlaygroundConfig playgroundConfig)
        {
            var scenario = Navigation2DPlaygroundScenarioSpawner.GetScenario(playgroundConfig, Navigation2DPlaygroundState.CurrentScenarioIndex);
            int maxAgentsPerTeam = GetMaxAgentsPerTeam(scenario.TeamCount);
            if (Navigation2DPlaygroundState.AgentsPerTeam > maxAgentsPerTeam)
            {
                Navigation2DPlaygroundState.AgentsPerTeam = maxAgentsPerTeam;
            }
        }

        private int GetMaxAgentsPerTeam(int teamCount)
        {
            GameConfig? gameConfig = _engine.GetService(CoreServiceKeys.GameConfig);
            int maxAgents = (gameConfig?.Navigation2D ?? new Navigation2DConfig()).CloneValidated().MaxAgents;
            return Math.Max(0, maxAgents / Math.Max(1, teamCount));
        }
    }
}
