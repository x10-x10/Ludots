using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Arch.System;
using Ludots.Core.Gameplay.GAS;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Presentation;
using Ludots.Core.Presentation.Camera;
using Ludots.Core.Presentation.Hud;
using Ludots.Core.Scripting;

namespace CoreInputMod.Systems
{
    /// <summary>
    /// Generic skill bar HUD. Renders ability slots for the controlled entity
    /// at the bottom-center of the screen using <see cref="ScreenOverlayBuffer"/>.
    /// Reads GAS data via <see cref="AbilityHudDataProvider"/> — fully decoupled
    /// from any specific game mode.
    /// Key labels are set via GlobalContext[<see cref="SkillBarKeyLabelsKey"/>].
    /// </summary>
    public sealed class SkillBarOverlaySystem : ISystem<float>
    {
        public const string SkillBarKeyLabelsKey = "CoreInputMod.SkillBarKeyLabels";
        public const string SkillBarEnabledKey = "CoreInputMod.SkillBarEnabled";

        private const int SlotW = 52, SlotH = 52, Gap = 6, BottomMargin = 12;

        private static readonly Vector4 SlotBg = new(0.12f, 0.12f, 0.16f, 0.85f);
        private static readonly Vector4 SlotBorder = new(0.4f, 0.4f, 0.5f, 0.6f);
        private static readonly Vector4 Ready = new(0.2f, 0.5f, 0.9f, 0.3f);
        private static readonly Vector4 CdOverlay = new(0f, 0f, 0f, 0.6f);
        private static readonly Vector4 KeyColor = new(0.9f, 0.9f, 0.9f, 0.9f);
        private static readonly Vector4 CdColor = new(1f, 0.85f, 0.3f, 1f);

        private readonly World _world;
        private readonly Dictionary<string, object> _globals;

        public SkillBarOverlaySystem(World world, Dictionary<string, object> globals)
        {
            _world = world;
            _globals = globals;
        }

        public void Initialize() { }

        public void Update(in float dt)
        {
            if (_globals.TryGetValue(SkillBarEnabledKey, out var eb) && eb is bool b && !b) return;
            if (!_globals.TryGetValue(CoreServiceKeys.ScreenOverlayBuffer.Name, out var ov) || ov is not ScreenOverlayBuffer overlay) return;

            var entity = GetControlledEntity();
            if (!_world.IsAlive(entity) || !_world.Has<AbilityStateBuffer>(entity)) return;

            ref var abilities = ref _world.Get<AbilityStateBuffer>(entity);
            if (abilities.Count == 0) return;

            AbilityDefinitionRegistry? reg = null;
            if (_globals.TryGetValue(CoreServiceKeys.AbilityDefinitionRegistry.Name, out var r))
                reg = r as AbilityDefinitionRegistry;

            int tick = 0;
            if (_globals.TryGetValue(CoreServiceKeys.GasClocks.Name, out var c) && c is GasClocks clk)
                tick = clk.FixedFrameNow;

            string[]? keys = _globals.TryGetValue(SkillBarKeyLabelsKey, out var kObj) && kObj is string[] k ? k : null;

            int screenW = 1280, screenH = 720;
            if (_globals.TryGetValue(CoreServiceKeys.ViewController.Name, out var vc) && vc is IViewController v)
            { screenW = (int)v.Resolution.X; screenH = (int)v.Resolution.Y; }

            int total = abilities.Count * SlotW + (abilities.Count - 1) * Gap;
            int x0 = (screenW - total) / 2;
            int y0 = screenH - SlotH - BottomMargin;

            Span<AbilitySlotHudData> slots = stackalloc AbilitySlotHudData[AbilityStateBuffer.CAPACITY];
            int n = AbilityHudDataProvider.GetAllSlots(_world, entity, reg, tick, slots);

            for (int i = 0; i < n; i++)
            {
                int x = x0 + i * (SlotW + Gap);
                ref var s = ref slots[i];
                overlay.AddRect(x, y0, SlotW, SlotH, SlotBg, SlotBorder);

                if (s.IsAvailable && s.AbilityId > 0)
                    overlay.AddRect(x + 1, y0 + 1, SlotW - 2, SlotH - 2, Ready, default);

                if (s.IsOnCooldown)
                {
                    int cdH = (int)(SlotH * s.CooldownFraction);
                    if (cdH > 0) overlay.AddRect(x + 1, y0 + 1, SlotW - 2, cdH, CdOverlay, default);
                    float sec = s.CooldownRemainingTicks / 60f;
                    overlay.AddText(x + SlotW / 2 - 8, y0 + SlotH / 2 - 10,
                        sec >= 1f ? $"{sec:F0}" : $"{sec:F1}", 18, CdColor);
                }

                string label = keys != null && i < keys.Length ? keys[i] : (i + 1).ToString();
                overlay.AddText(x + 4, y0 + SlotH - 18, label, 14, KeyColor);
            }
        }

        private Entity GetControlledEntity()
        {
            if (_globals.TryGetValue(CoreServiceKeys.SelectedEntity.Name, out var s) &&
                s is Entity sel && _world.IsAlive(sel) && _world.Has<AbilityStateBuffer>(sel))
                return sel;
            if (_globals.TryGetValue(CoreServiceKeys.LocalPlayerEntity.Name, out var l) && l is Entity loc)
                return loc;
            return default;
        }

        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }
    }
}
