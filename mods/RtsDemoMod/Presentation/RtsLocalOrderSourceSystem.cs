using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.System;
using Ludots.Core.Config;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Gameplay.GAS.Orders;
using Ludots.Core.Input.Orders;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Mathematics;
using Ludots.Core.Modding;
using Ludots.Core.Presentation.Utils;
using Ludots.Core.Scripting;
using Ludots.Platform.Abstractions;
using CoreInputMod.Systems;

namespace RtsDemoMod.Presentation
{
    /// <summary>
    /// RTS order source: SC2-style AimCast interaction.
    /// Commands apply to the selected unit's abilities.
    /// </summary>
    public sealed class RtsLocalOrderSourceSystem : ISystem<float>
    {
        private readonly World _world;
        private readonly Dictionary<string, object> _globals;
        private readonly OrderQueue _orders;
        private readonly IModContext _ctx;
        private readonly int _castAbilityTagId;
        private readonly int _stopTagId;
        private InputOrderMappingSystem? _inputOrderMapping;
        private bool _initialized;

        public RtsLocalOrderSourceSystem(World world, Dictionary<string, object> globals, OrderQueue orders, IModContext ctx)
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

        private void InitializeInputOrderMapping()
        {
            if (_initialized) return;
            _initialized = true;
            if (!_globals.TryGetValue(CoreServiceKeys.InputHandler.Name, out var io) || io is not PlayerInputHandler input) return;

            var config = LoadInputOrderMappings();
            _inputOrderMapping = new InputOrderMappingSystem(input, config);
            _inputOrderMapping.SetTagKeyResolver(k => k switch { "castAbility" => _castAbilityTagId, "stop" => _stopTagId, _ => 0 });
            _inputOrderMapping.SetGroundPositionProvider((out Vector3 w) => { w = default; if (TryGetGround(out var p)) { w = new Vector3(p.X, 0f, p.Y); return true; } return false; });
            _inputOrderMapping.SetSelectedEntityProvider((out Entity e) => TryGetSel(out e));
            _inputOrderMapping.SetHoveredEntityProvider((out Entity e) => TryGetHov(out e));
            _inputOrderMapping.SetOrderSubmitHandler((in Order o) => _orders.TryEnqueue(o));
            _globals[SkillBarOverlaySystem.SkillBarKeyLabelsKey] = new[] { "Q", "W", "E" };
        }

        public void Update(in float dt)
        {
            InitializeInputOrderMapping();
            if (_inputOrderMapping == null) return;
            var actor = GetActor();
            if (_world.IsAlive(actor)) { _inputOrderMapping.SetLocalPlayer(actor, 1); _inputOrderMapping.Update(dt); }
        }

        private Entity GetActor()
        {
            if (_globals.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var s) && s is Entity sel && _world.IsAlive(sel))
                if (_world.TryGet(sel, out PlayerOwner o) && o.PlayerId == 1) return sel;
            if (_globals.TryGetValue(CoreServiceKeys.LocalPlayerEntity.Name, out var l) && l is Entity loc) return loc;
            return default;
        }

        private bool TryGetSel(out Entity t) { t = default; if (!_globals.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var o) || o is not Entity e || !_world.IsAlive(e)) return false; t = e; return true; }
        private bool TryGetHov(out Entity t) { t = default; if (!_globals.TryGetValue(CoreServiceKeys.HoveredEntity.Name, out var o) || o is not Entity e || !_world.IsAlive(e)) return false; t = e; return true; }

        private bool TryGetGround(out WorldCmInt2 w)
        {
            w = default;
            if (!_globals.TryGetValue(CoreServiceKeys.ScreenRayProvider.Name, out var r) || r is not IScreenRayProvider rp) return false;
            if (!_globals.TryGetValue(CoreServiceKeys.InputHandler.Name, out var io) || io is not PlayerInputHandler input) return false;
            var mouse = input.ReadAction<Vector2>("PointerPos");
            return GroundRaycastUtil.TryGetGroundWorldCm(in rp.GetRay(mouse), out w);
        }

        private InputOrderMappingConfig LoadInputOrderMappings()
        {
            using var s = _ctx.VFS.GetStream($"{_ctx.ModId}:assets/Input/input_order_mappings.json");
            return InputOrderMappingLoader.LoadFromStream(s);
        }

        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }
    }
}
