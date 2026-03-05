using System;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Components;

namespace Ludots.Core.Gameplay.Camera.FollowTargets
{
    /// <summary>
    /// Tracks a specific entity's <see cref="WorldPositionCm"/>.
    /// The simplest follow target — equivalent to Cinemachine's "Follow" with a fixed target.
    /// </summary>
    public sealed class EntityFollowTarget : ICameraFollowTarget
    {
        private readonly World _world;
        private Entity _entity;

        public EntityFollowTarget(World world, Entity entity)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _entity = entity;
        }

        public void SetEntity(Entity entity) => _entity = entity;

        public Vector2? GetPosition()
        {
            if (!_world.IsAlive(_entity)) return null;
            if (!_world.Has<WorldPositionCm>(_entity)) return null;
            return _world.Get<WorldPositionCm>(_entity).Value.ToVector2();
        }
    }
}
