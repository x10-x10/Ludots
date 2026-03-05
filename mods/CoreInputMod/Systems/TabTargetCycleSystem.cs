using System.Collections.Generic;
using Arch.Core;
using Arch.System;
using Ludots.Core.Components;
using Ludots.Core.Gameplay.Components;
using Ludots.Core.Input.Runtime;
using Ludots.Core.Mathematics;
using Ludots.Core.Scripting;
using Ludots.Core.Spatial;

namespace CoreInputMod.Systems
{
    /// <summary>
    /// WoW-style Tab target cycling. Pressing the configured action (default "TabTarget")
    /// cycles through nearby hostile entities in range, sorted by distance.
    /// Pressing the reverse action (default "TabTargetReverse") cycles backwards.
    ///
    /// Only queries entities with <see cref="WorldPositionCm"/> and a different
    /// <see cref="Team"/> from the local player's team (hostile filter).
    ///
    /// Fully generic — no game-mode specific logic. Enabled via input config.
    /// </summary>
    public sealed class TabTargetCycleSystem : ISystem<float>
    {
        public const string TabTargetActionId = "TabTarget";
        public const string TabTargetReverseActionId = "TabTargetReverse";

        private readonly World _world;
        private readonly Dictionary<string, object> _globals;
        private readonly ISpatialQueryService _spatial;
        private readonly int _searchRadiusCm;

        private int _lastCycleIndex = -1;

        public TabTargetCycleSystem(World world, Dictionary<string, object> globals,
            ISpatialQueryService spatial, int searchRadiusCm = 3000)
        {
            _world = world;
            _globals = globals;
            _spatial = spatial;
            _searchRadiusCm = searchRadiusCm;
        }

        public void Initialize() { }

        public void Update(in float dt)
        {
            if (!_globals.TryGetValue(CoreServiceKeys.InputHandler.Name, out var io) || io is not PlayerInputHandler input) return;

            bool fwd = input.PressedThisFrame(TabTargetActionId);
            bool rev = input.PressedThisFrame(TabTargetReverseActionId);
            if (!fwd && !rev) return;

            if (!_globals.TryGetValue(CoreServiceKeys.LocalPlayerEntity.Name, out var lObj) || lObj is not Entity local) return;
            if (!_world.IsAlive(local) || !_world.Has<WorldPositionCm>(local)) return;

            int localTeam = _world.Has<Team>(local) ? _world.Get<Team>(local).Id : 0;
            var localPos = _world.Get<WorldPositionCm>(local).Value.ToWorldCmInt2();

            Span<Entity> raw = stackalloc Entity[256];
            var result = _spatial.QueryRadius(localPos, _searchRadiusCm, raw);

            // filter to hostile, alive, has position
            Span<Entity> candidates = stackalloc Entity[64];
            Span<long> dists = stackalloc long[64];
            int count = 0;

            for (int i = 0; i < result.Count && count < 64; i++)
            {
                var e = raw[i];
                if (!_world.IsAlive(e)) continue;
                if (e.Id == local.Id) continue;
                if (!_world.Has<WorldPositionCm>(e)) continue;
                if (_world.Has<Team>(e) && _world.Get<Team>(e).Id == localTeam) continue;

                var p = _world.Get<WorldPositionCm>(e).Value.ToWorldCmInt2();
                long dx = p.X - localPos.X, dy = p.Y - localPos.Y;
                long d2 = dx * dx + dy * dy;

                // insertion sort by distance
                int ins = count;
                while (ins > 0 && dists[ins - 1] > d2) ins--;
                for (int j = count; j > ins; j--) { candidates[j] = candidates[j - 1]; dists[j] = dists[j - 1]; }
                candidates[ins] = e;
                dists[ins] = d2;
                count++;
            }

            if (count == 0) return;

            int nextIdx;
            if (fwd)
            {
                nextIdx = (_lastCycleIndex + 1) % count;
            }
            else
            {
                nextIdx = _lastCycleIndex <= 0 ? count - 1 : _lastCycleIndex - 1;
            }

            _lastCycleIndex = nextIdx;
            _globals[CoreServiceKeys.SelectedEntity.Name] = candidates[nextIdx];
        }

        public void BeforeUpdate(in float dt) { }
        public void AfterUpdate(in float dt) { }
        public void Dispose() { }
    }
}
