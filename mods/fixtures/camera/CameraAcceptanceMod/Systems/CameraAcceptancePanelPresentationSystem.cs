using Arch.System;
using CameraAcceptanceMod.Runtime;
using Ludots.Core.Engine;

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
            _runtime.RefreshPanel(_engine);
        }
    }
}
