using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.System;
using Ludots.Core.Config;
using Ludots.Core.Gameplay.GAS.Orders;
using Ludots.Core.Input.Orders;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Mathematics;
using Ludots.Core.Modding;
using Ludots.Core.Presentation.Utils;
using Ludots.Core.Scripting;
using Ludots.Platform.Abstractions;
using CoreInputMod.Systems;

namespace ArpgDemoMod.Presentation
{
    /// <summary>
    /// ARPG order source: WoW-style TargetFirst. Always commands the hero entity.
    /// </summary>
    public sealed class ArpgLocalOrderSourceSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly Dictionary<string, object> _globals;
        private readonly OrderQueue _orders;
        private readonly IModContext _ctx;
        private readonly int _castAbilityTagId;
        private readonly int _stopTagId;
        private InputOrderMappingSystem? _mapping;
        private bool _initialized;

        public ArpgLocalOrderSourceSystem(World world, Dictionary<string, object> globals, OrderQueue orders, IModContext ctx)
        {
            _world = world;
            _globals = globals;
            _orders = orders;
            _ctx = ctx;
            if (_globals.TryGetValue(CoreServiceKeys.GameConfig.Name, out var c) && c is GameConfig cfg)
            {
                _castAbilityTagId = cfg.Constants.OrderTags["castAbility"];
                _stopTagId = cfg.Constants.OrderTags["stop"];
            }
        }

        public void Initialize() { }

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;
            if (!_globals.TryGetValue(CoreServiceKeys.InputHandler.Name, out var io) || io is not PlayerInputHandler input) return;

            using var s = _ctx.VFS.GetStream($"{_ctx.ModId}:assets/Input/input_order_mappings.json");
            var config = InputOrderMappingLoader.LoadFromStream(s);
            _mapping = new InputOrderMappingSystem(input, config);
            _mapping.SetTagKeyResolver(k => k switch { "castAbility" => _castAbilityTagId, "stop" => _stopTagId, _ => 0 });
            _mapping.SetGroundPositionProvider((out Vector3 w) => { w = default; if (TryGround(out var p)) { w = new Vector3(p.X, 0f, p.Y); return true; } return false; });
            _mapping.SetSelectedEntityProvider((out Entity e) => TryGet(CoreServiceKeys.SelectedEntity.Name, out e));
            _mapping.SetHoveredEntityProvider((out Entity e) => TryGet(CoreServiceKeys.HoveredEntity.Name, out e));
            _mapping.SetOrderSubmitHandler((in Order o) => _orders.TryEnqueue(o));
            _globals[SkillBarOverlaySystem.SkillBarKeyLabelsKey] = new[] { "1", "2", "3", "4", "5", "6" };
        }

        public void Update(in float dt)
        {
            Init();
            if (_mapping == null) return;
            if (_globals.TryGetValue(CoreServiceKeys.LocalPlayerEntity.Name, out var l) && l is Entity loc && _world.IsAlive(loc))
            {
                _mapping.SetLocalPlayer(loc, 1);
                _mapping.Update(dt);
            }
        }

        private bool TryGet(string key, out Entity e) { e = default; if (!_globals.TryGetValue(key, out var o) || o is not Entity v || !_world.IsAlive(v)) return false; e = v; return true; }

        private bool TryGround(out WorldCmInt2 w)
        {
            w = default;
            if (!_globals.TryGetValue(CoreServiceKeys.ScreenRayProvider.Name, out var r) || r is not IScreenRayProvider rp) return false;
            if (!_globals.TryGetValue(CoreServiceKeys.InputHandler.Name, out var io) || io is not PlayerInputHandler input) return false;
            return GroundRaycastUtil.TryGetGroundWorldCm(in rp.GetRay(input.ReadAction<Vector2>("PointerPos")), out w);
        }

        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }
    }
}
