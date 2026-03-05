using System.Numerics;

namespace Ludots.Core.Gameplay.Camera
{
    /// <summary>
    /// Provides a follow target position for the camera system.
    /// Inspired by Cinemachine's Virtual Camera target concept:
    /// the camera doesn't know what it's following — it just asks for a position.
    ///
    /// Implementations:
    ///   - EntityFollowTarget: tracks a specific entity's WorldPositionCm
    ///   - SelectedEntityFollowTarget: tracks the current SelectedEntity
    ///   - GroupCentroidFollowTarget: tracks the centroid of multiple entities
    ///   - FallbackChainFollowTarget: tries targets in priority order
    ///
    /// Registered on CameraManager via <see cref="CameraManager.FollowTarget"/>.
    /// </summary>
    public interface ICameraFollowTarget
    {
        /// <summary>
        /// Get the current world position (cm) of the follow target.
        /// Returns null if no valid target exists (camera holds last position).
        /// Called once per frame by CameraFollowTargetSystem.
        /// </summary>
        Vector2? GetPosition();
    }
}
