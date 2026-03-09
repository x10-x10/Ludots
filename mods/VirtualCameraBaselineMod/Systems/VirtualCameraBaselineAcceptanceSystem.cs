using Arch.System;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.Camera;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Scripting;
using System.Numerics;
using VirtualCameraBaselineMod.Input;

namespace VirtualCameraBaselineMod.Systems
{
    public sealed class VirtualCameraBaselineAcceptanceSystem : ISystem<float>
    {
        private readonly GameEngine _engine;
        private PlayerInputHandler? _input;
        private bool _inputContextPushed;

        public VirtualCameraBaselineAcceptanceSystem(GameEngine engine)
        {
            _engine = engine;
        }

        public void Initialize() { }

        public void BeforeUpdate(in float t) { }

        public void Update(in float t)
        {
            if (_input == null &&
                _engine.GlobalContext.TryGetValue(CoreServiceKeys.InputHandler.Name, out var inputObj) &&
                inputObj is PlayerInputHandler input)
            {
                EnsureInputSchema(input);
                _input = input;
                _input.PushContext(VirtualCameraBaselineInputContexts.Acceptance);
                _inputContextPushed = true;
            }

            if (_input == null)
            {
                return;
            }

            if (_input.PressedThisFrame(VirtualCameraBaselineInputActions.ClearIntro))
            {
                _engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
                {
                    Clear = true
                });
            }

            if (_input.PressedThisFrame(VirtualCameraBaselineInputActions.ReplayIntro))
            {
                _engine.SetService(CoreServiceKeys.VirtualCameraRequest, new VirtualCameraRequest
                {
                    Id = VirtualCameraBaselineIds.IntroFocusCameraId,
                    BlendDurationSeconds = 0f
                });
            }

            RenderOverlay();
        }

        public void AfterUpdate(in float t) { }

        public void Dispose()
        {
            if (_input != null && _inputContextPushed)
            {
                _input.PopContext(VirtualCameraBaselineInputContexts.Acceptance);
                _inputContextPushed = false;
            }
        }

        private static void EnsureInputSchema(PlayerInputHandler input)
        {
            if (!input.HasContext(VirtualCameraBaselineInputContexts.Acceptance))
            {
                throw new System.InvalidOperationException($"Missing input context: {VirtualCameraBaselineInputContexts.Acceptance}");
            }

            if (!input.HasAction(VirtualCameraBaselineInputActions.ClearIntro))
            {
                throw new System.InvalidOperationException($"Missing input action: {VirtualCameraBaselineInputActions.ClearIntro}");
            }

            if (!input.HasAction(VirtualCameraBaselineInputActions.ReplayIntro))
            {
                throw new System.InvalidOperationException($"Missing input action: {VirtualCameraBaselineInputActions.ReplayIntro}");
            }
        }

        private void RenderOverlay()
        {
            if (!_engine.GlobalContext.TryGetValue(CoreServiceKeys.ScreenOverlayBuffer.Name, out var overlayObj) ||
                overlayObj is not ScreenOverlayBuffer overlay)
            {
                return;
            }

            var brain = _engine.GameSession.Camera.VirtualCameraBrain;
            bool introActive = brain != null && brain.HasActiveCamera;
            string state = introActive ? "Intro focus active" : "TPS hero follow active";

            int x = 16;
            int y = 16;
            int w = 420;
            int h = 92;
            var bg = new Vector4(0.04f, 0.07f, 0.1f, 0.78f);
            var border = new Vector4(0.55f, 0.85f, 1f, 0.45f);
            var title = new Vector4(0.92f, 0.97f, 1f, 1f);
            var text = new Vector4(0.82f, 0.9f, 0.96f, 1f);
            var hint = new Vector4(1f, 0.9f, 0.55f, 1f);

            overlay.AddRect(x, y, w, h, bg, border);
            overlay.AddText(x + 10, y + 8, "VirtualCamera Baseline", 16, title);
            overlay.AddText(x + 10, y + 32, $"State: {state}", 14, text);
            overlay.AddText(x + 10, y + 54, "Enter: return to TPS hero follow", 13, hint);
            overlay.AddText(x + 10, y + 72, "R: replay intro focus shot", 13, hint);
        }
    }
}
