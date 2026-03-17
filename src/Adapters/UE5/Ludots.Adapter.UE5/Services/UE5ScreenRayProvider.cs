using System.Numerics;
using Ludots.Platform.Abstractions;

namespace Ludots.Adapter.UE5
{
    /// <summary>
    /// UE5 平台屏幕射线提供者（对标 RaylibScreenRayProvider）。
    ///
    /// 复用 Raylib 侧完全相同的纯 CPU unproject 算法，
    /// 从 <see cref="UE5SharedCameraState"/> 读取相机矩阵与视口尺寸，
    /// 将屏幕像素坐标反投影为世界空间射线，
    /// 供 Ludots 选中/鼠标拾取系统使用。
    /// </summary>
    public sealed class UE5ScreenRayProvider : IScreenRayProvider
    {
        private readonly UE5SharedCameraState _state;

        public UE5ScreenRayProvider(UE5SharedCameraState state) => _state = state;

        public ScreenRay GetRay(Vector2 screenPosition)
        {
            float w = _state.ViewportWidth;
            float h = _state.ViewportHeight;
            if (w <= 0f || h <= 0f)
                return new ScreenRay(Vector3.Zero, Vector3.UnitZ);

            float ndcX =  (screenPosition.X / w) * 2f - 1f;
            float ndcY = 1f - (screenPosition.Y / h) * 2f;

            var cam = _state.ReadCameraState();

            var view = Matrix4x4.CreateLookAt(cam.Position, cam.Target, cam.Up);
            float aspect = w / h;
            float fovRad = cam.FovYDeg * (MathF.PI / 180f);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspect, 0.01f, 1000f);

            var viewProj = view * projection;
            if (!Matrix4x4.Invert(viewProj, out var inv))
                return new ScreenRay(cam.Position, Vector3.Normalize(cam.Target - cam.Position));

            var nearClip = Vector4.Transform(new Vector4(ndcX, ndcY, 0f, 1f), inv);
            var farClip  = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), inv);

            if (MathF.Abs(nearClip.W) < 1e-6f || MathF.Abs(farClip.W) < 1e-6f)
                return new ScreenRay(cam.Position, Vector3.Normalize(cam.Target - cam.Position));

            nearClip /= nearClip.W;
            farClip  /= farClip.W;

            var origin = new Vector3(nearClip.X, nearClip.Y, nearClip.Z);
            var dir    = Vector3.Normalize(new Vector3(farClip.X - nearClip.X,
                                                       farClip.Y - nearClip.Y,
                                                       farClip.Z - nearClip.Z));
            return new ScreenRay(origin, dir);
        }
    }
}
