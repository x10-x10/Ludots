using Arch.System;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Scripting;

namespace CameraAcceptanceMod.Systems
{
    public sealed class CameraStackAcceptanceSystem : ISystem<float>
    {
        private readonly GameEngine _engine;

        public CameraStackAcceptanceSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float t) { }
        public void AfterUpdate(in float t) { }
        public void Dispose() { }

        public void Update(in float t)
        {
            if (!string.Equals(_engine.CurrentMapSession?.MapId.Value, CameraAcceptanceIds.StackMapId, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_engine.GlobalContext.TryGetValue(CoreServiceKeys.AuthoritativeInput.Name, out var inputObj) ||
                inputObj is not IInputActionReader input)
            {
                return;
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.StackRevealActionId))
            {
                _engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
                {
                    Id = CameraAcceptanceIds.StackRevealShotId
                });
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.StackAlertActionId))
            {
                _engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
                {
                    Id = CameraAcceptanceIds.StackAlertShotId
                });
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.StackClearActionId))
            {
                _engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
                {
                    Clear = true
                });
            }
        }
    }
}
