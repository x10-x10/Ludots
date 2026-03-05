using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Ludots.Core.Gameplay.GAS.Orders;
using Ludots.Core.Input.Orders;
using Ludots.Core.Modding;
using Ludots.Core.Scripting;
using CoreInputMod.Systems;

namespace RtsDemoMod.Presentation
{
    /// <summary>
    /// RTS order source: SC2-style AimCast interaction.
    /// Commands apply to the selected unit's abilities.
    /// Uses <see cref="LocalOrderSourceHelper"/> for standard Input→Order wiring.
    /// </summary>
    public sealed class RtsLocalOrderSourceSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly LocalOrderSourceHelper _helper;
        private readonly IModContext _ctx;
        private readonly Dictionary<string, object> _globals;
        private InputOrderMappingSystem? _mapping;
        private bool _initialized;

        public RtsLocalOrderSourceSystem(World world, Dictionary<string, object> globals, OrderQueue orders, IModContext ctx)
        {
            _world = world;
            _globals = globals;
            _ctx = ctx;
            _helper = new LocalOrderSourceHelper(world, globals, orders);
        }

        public void Initialize() { }

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _mapping = _helper.TryCreateMapping(_ctx);
            if (_mapping != null)
                _globals[SkillBarOverlaySystem.SkillBarKeyLabelsKey] = new[] { "Q", "W", "E" };
        }

        public void Update(in float dt)
        {
            Init();
            if (_mapping == null) return;
            var actor = _helper.GetControlledActor();
            if (_world.IsAlive(actor))
            {
                _mapping.SetLocalPlayer(actor, 1);
                _mapping.Update(dt);
            }
        }

        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }
    }
}
