using Arch.System;
using Ludots.Core.Engine;
using UxPrototypeMod.Runtime;

namespace UxPrototypeMod.Systems;

internal sealed class UxPrototypePanelPresentationSystem : ISystem<float>
{
    private readonly GameEngine _engine;
    private readonly UxPrototypeRuntime _runtime;

    public UxPrototypePanelPresentationSystem(GameEngine engine, UxPrototypeRuntime runtime)
    {
        _engine = engine;
        _runtime = runtime;
    }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in float dt)
    {
    }

    public void Update(in float dt)
    {
        _runtime.RefreshPanel(_engine);
    }

    public void AfterUpdate(in float dt)
    {
    }

    public void Dispose()
    {
    }
}
