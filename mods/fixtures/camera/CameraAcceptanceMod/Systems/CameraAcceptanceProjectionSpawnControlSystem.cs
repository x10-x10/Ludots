using Arch.System;
using CameraAcceptanceMod.Runtime;
using Ludots.Core.Engine;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Scripting;

namespace CameraAcceptanceMod.Systems
{
    internal sealed class CameraAcceptanceProjectionSpawnControlSystem : ISystem<float>
    {
        private readonly GameEngine _engine;

        public CameraAcceptanceProjectionSpawnControlSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float t) { }
        public void AfterUpdate(in float t) { }
        public void Dispose() { }

        public void Update(in float t)
        {
            if (!string.Equals(_engine.CurrentMapSession?.MapId.Value, CameraAcceptanceIds.ProjectionMapId, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_engine.GlobalContext.TryGetValue(CoreServiceKeys.AuthoritativeInput.Name, out var inputObj) ||
                inputObj is not IInputActionReader input)
            {
                return;
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.ProjectionSpawnCountDecreaseActionId))
            {
                CameraAcceptanceRuntime.AdjustProjectionSpawnCount(_engine, -CameraAcceptanceIds.ProjectionSpawnCountStep);
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.ProjectionSpawnCountIncreaseActionId))
            {
                CameraAcceptanceRuntime.AdjustProjectionSpawnCount(_engine, CameraAcceptanceIds.ProjectionSpawnCountStep);
            }
        }
    }
}
