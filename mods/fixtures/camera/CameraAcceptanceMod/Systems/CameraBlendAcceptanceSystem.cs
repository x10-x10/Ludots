using Arch.System;
using Ludots.Core.Engine;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Scripting;

namespace CameraAcceptanceMod.Systems
{
    public sealed class CameraBlendAcceptanceSystem : ISystem<float>
    {
        private readonly GameEngine _engine;

        public CameraBlendAcceptanceSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float t) { }
        public void AfterUpdate(in float t) { }
        public void Dispose() { }

        public void Update(in float t)
        {
            if (!string.Equals(_engine.CurrentMapSession?.MapId.Value, CameraAcceptanceIds.BlendMapId, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_engine.GlobalContext.TryGetValue(CoreServiceKeys.AuthoritativeInput.Name, out var inputObj) ||
                inputObj is not IInputActionReader input)
            {
                return;
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.BlendCutActionId))
            {
                _engine.GlobalContext[CameraAcceptanceIds.ActiveBlendCameraIdKey] = CameraAcceptanceIds.BlendCutCameraId;
            }
            else if (input.PressedThisFrame(CameraAcceptanceIds.BlendLinearActionId))
            {
                _engine.GlobalContext[CameraAcceptanceIds.ActiveBlendCameraIdKey] = CameraAcceptanceIds.BlendLinearCameraId;
            }
            else if (input.PressedThisFrame(CameraAcceptanceIds.BlendSmoothActionId))
            {
                _engine.GlobalContext[CameraAcceptanceIds.ActiveBlendCameraIdKey] = CameraAcceptanceIds.BlendSmoothCameraId;
            }
        }
    }
}
