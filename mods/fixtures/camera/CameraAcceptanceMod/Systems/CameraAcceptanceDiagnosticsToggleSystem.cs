using CameraAcceptanceMod.Runtime;
using Ludots.Core.Diagnostics;
using Ludots.Core.Engine;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Scripting;
using Arch.System;

namespace CameraAcceptanceMod.Systems
{
    internal sealed class CameraAcceptanceDiagnosticsToggleSystem : ISystem<float>
    {
        private static readonly LogChannel LogChannel = Log.RegisterChannel("CameraAcceptanceMod");

        private readonly GameEngine _engine;

        public CameraAcceptanceDiagnosticsToggleSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }

        public void Update(in float dt)
        {
            if (!CameraAcceptanceIds.IsAcceptanceMap(_engine.CurrentMapSession?.MapId.Value))
            {
                return;
            }

            if (_engine.GetService(CoreServiceKeys.AuthoritativeInput) is not IInputActionReader input ||
                _engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is not CameraAcceptanceDiagnosticsState diagnostics ||
                _engine.GetService(CoreServiceKeys.RenderDebugState) is not RenderDebugState renderDebug)
            {
                return;
            }

            bool changed = false;
            if (input.PressedThisFrame(CameraAcceptanceIds.TogglePanelActionId))
            {
                renderDebug.DrawSkiaUi = !renderDebug.DrawSkiaUi;
                changed = true;
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.ToggleHudActionId))
            {
                diagnostics.HudEnabled = !diagnostics.HudEnabled;
                changed = true;
            }

            if (input.PressedThisFrame(CameraAcceptanceIds.ToggleTextActionId))
            {
                diagnostics.TextEnabled = !diagnostics.TextEnabled;
                changed = true;
            }

            if (changed)
            {
                Log.Info(in LogChannel,
                    $"CameraAcceptance diagnostics toggles: panel={(renderDebug.DrawSkiaUi ? "ON" : "OFF")} hud={(diagnostics.HudEnabled ? "ON" : "OFF")} text={(diagnostics.TextEnabled ? "ON" : "OFF")}");
            }
        }
    }
}
