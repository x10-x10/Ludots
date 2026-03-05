using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Scripting;

namespace Ludots.Core.Gameplay.Camera.FollowTargets
{
    /// <summary>
    /// Tracks the current <see cref="CoreServiceKeys.SelectedEntity"/> from GlobalContext.
    /// For MOBA: follows selected hero. For ARPG: follows the WoW-style target.
    /// Falls back to <see cref="CoreServiceKeys.LocalPlayerEntity"/> if no selection.
    /// </summary>
    public sealed class SelectedEntityFollowTarget : ICameraFollowTarget
    {
        private readonly World _world;
        private readonly Dictionary<string, object> _globals;

        public SelectedEntityFollowTarget(World world, Dictionary<string, object> globals)
        {
            _world = world;
            _globals = globals;
        }

        public Vector2? GetPosition()
        {
            if (TryGetEntityPosition(CoreServiceKeys.SelectedEntity.Name, out var pos))
                return pos;
            if (TryGetEntityPosition(CoreServiceKeys.LocalPlayerEntity.Name, out pos))
                return pos;
            return null;
        }

        private bool TryGetEntityPosition(string key, out Vector2 position)
        {
            position = default;
            if (!_globals.TryGetValue(key, out var obj) || obj is not Entity e) return false;
            if (!_world.IsAlive(e)) return false;
            if (!_world.Has<WorldPositionCm>(e)) return false;
            position = _world.Get<WorldPositionCm>(e).Value.ToVector2();
            return true;
        }
    }
}
