using Arch.System;
using Ludots.Core.Engine;
using UxPrototypeMod.Runtime;

namespace UxPrototypeMod.Systems;

internal sealed class UxPrototypeSimulationSystem : ISystem<float>
{
    private readonly GameEngine _engine;
    private readonly UxPrototypeRuntime _runtime;

    public UxPrototypeSimulationSystem(GameEngine engine, UxPrototypeRuntime runtime)
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
        _runtime.Update(_engine, dt);
    }

    public void AfterUpdate(in float dt)
    {
    }

    public void Dispose()
    {
    }
}
