using System.Numerics;

namespace Ludots.Core.Gameplay.Camera
{
    public enum VirtualCameraTargetSource
    {
        Fixed = 0,
        FollowTarget = 1
    }

    public enum CameraBlendCurve
    {
        Cut = 0,
        Linear = 1,
        SmoothStep = 2
    }

    public sealed class VirtualCameraDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Priority { get; set; }

        public VirtualCameraTargetSource TargetSource { get; set; } = VirtualCameraTargetSource.Fixed;
        public Vector2 FixedTargetCm { get; set; } = Vector2.Zero;

        public float Yaw { get; set; } = 180f;
        public float Pitch { get; set; } = 45f;
        public float DistanceCm { get; set; } = 14142f;
        public float FovYDeg { get; set; } = 60f;

        public float DefaultBlendDuration { get; set; } = 0.25f;
        public CameraBlendCurve BlendCurve { get; set; } = CameraBlendCurve.SmoothStep;

        public bool AllowUserInput { get; set; }
    }
}
