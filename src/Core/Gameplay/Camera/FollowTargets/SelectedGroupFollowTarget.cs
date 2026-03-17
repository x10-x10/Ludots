using System.Collections.Generic;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Components;
using Ludots.Core.Input.Selection;
using Ludots.Core.Scripting;

namespace Ludots.Core.Gameplay.Camera.FollowTargets
{
    public sealed class SelectedGroupFollowTarget : ICameraFollowTarget
    {
        private readonly World _world;
        private readonly Dictionary<string, object> _globals;

        public SelectedGroupFollowTarget(World world, Dictionary<string, object> globals)
        {
            _world = world;
            _globals = globals;
        }

        public bool TryGetPosition(out Vector2 positionCm)
        {
            if (TryGetSelectionCentroid(out positionCm))
            {
                return true;
            }

            return TryGetGlobalEntityPosition(CoreServiceKeys.SelectedEntity.Name, out positionCm);
        }

        private bool TryGetSelectionCentroid(out Vector2 positionCm)
        {
            positionCm = default;
            if (!_globals.TryGetValue(CoreServiceKeys.LocalPlayerEntity.Name, out var localObj) ||
                localObj is not Entity selector ||
                !_world.IsAlive(selector) ||
                !_world.Has<SelectionBuffer>(selector))
            {
                return false;
            }

            ref readonly var selection = ref _world.Get<SelectionBuffer>(selector);
            if (selection.Count <= 0)
            {
                return false;
            }

            Vector2 weightedSum = Vector2.Zero;
            float totalWeight = 0f;

            for (int i = 0; i < selection.Count; i++)
            {
                Entity entity = selection.Get(i);
                if (!_world.IsAlive(entity) || !_world.Has<WorldPositionCm>(entity))
                {
                    continue;
                }

                Vector2 entityPosition = _world.Get<WorldPositionCm>(entity).Value.ToVector2();
                float weight = ResolveWeight(entity);
                weightedSum += entityPosition * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            positionCm = weightedSum / totalWeight;
            return true;
        }

        private bool TryGetGlobalEntityPosition(string globalKey, out Vector2 positionCm)
        {
            positionCm = default;
            if (!_globals.TryGetValue(globalKey, out var value) || value is not Entity entity)
            {
                return false;
            }

            if (!_world.IsAlive(entity) || !_world.Has<WorldPositionCm>(entity))
            {
                return false;
            }

            positionCm = _world.Get<WorldPositionCm>(entity).Value.ToVector2();
            return true;
        }

        private float ResolveWeight(Entity entity)
        {
            if (_world.Has<CameraFollowWeight>(entity))
            {
                float configured = _world.Get<CameraFollowWeight>(entity).Value;
                if (configured > 0f)
                {
                    return configured;
                }
            }

            return 1f;
        }
    }
}
