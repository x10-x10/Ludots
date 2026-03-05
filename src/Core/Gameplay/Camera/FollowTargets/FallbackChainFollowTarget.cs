using System.Numerics;

namespace Ludots.Core.Gameplay.Camera.FollowTargets
{
    /// <summary>
    /// Tries follow targets in priority order, returns the first valid position.
    /// Inspired by Cinemachine's Priority system — the camera follows the highest-priority
    /// available target without needing to know what the targets are.
    ///
    /// Example: [SelectedEntityFollowTarget, EntityFollowTarget(hero)]
    ///   → Follow selected unit; if none selected, follow the hero.
    /// </summary>
    public sealed class FallbackChainFollowTarget : ICameraFollowTarget
    {
        private readonly ICameraFollowTarget[] _targets;

        public FallbackChainFollowTarget(params ICameraFollowTarget[] targets)
        {
            _targets = targets;
        }

        public Vector2? GetPosition()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                var pos = _targets[i].GetPosition();
                if (pos.HasValue) return pos;
            }
            return null;
        }
    }
}
