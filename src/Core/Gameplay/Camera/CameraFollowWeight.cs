namespace Ludots.Core.Gameplay.Camera
{
    /// <summary>
    /// Optional weight used by camera follow targets that aggregate multiple entities.
    /// Missing or non-positive values fall back to 1.
    /// </summary>
    public struct CameraFollowWeight
    {
        public float Value;
    }
}
