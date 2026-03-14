using Arch.System;
using CameraAcceptanceMod.Runtime;
using Ludots.Core.Engine;
using Ludots.Core.Presentation.Hud;
using System.Diagnostics;

namespace CameraAcceptanceMod.Systems
{
    internal sealed class CameraAcceptancePanelPresentationSystem : ISystem<float>
    {
        private readonly GameEngine _engine;
        private readonly CameraAcceptanceRuntime _runtime;

        public CameraAcceptancePanelPresentationSystem(GameEngine engine, CameraAcceptanceRuntime runtime)
        {
            _engine = engine;
            _runtime = runtime;
        }

        public void Initialize() { }
        public void BeforeUpdate(in float t) { }
        public void AfterUpdate(in float t) { }
        public void Dispose() { }

        public void Update(in float t)
        {
            if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState frameDiagnostics &&
                t > 0f)
            {
                frameDiagnostics.ObserveFrameTime(t * 1000.0);
            }

            long start = Stopwatch.GetTimestamp();
            if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState diagnostics)
            {
                diagnostics.ObserveFrameTime(t * 1000.0);
            }

            _runtime.RefreshPanel(_engine);
            if (_engine.GetService(CameraAcceptanceServiceKeys.DiagnosticsState) is CameraAcceptanceDiagnosticsState diagnosticsAfter)
            {
                diagnosticsAfter.ObservePanelSync((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            }
        }
    }
}
