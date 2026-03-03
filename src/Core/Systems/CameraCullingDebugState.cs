using System.Numerics;

namespace Ludots.Core.Systems
{
    /// <summary>
    /// Mutable debug snapshot produced by <see cref="CameraCullingSystem"/> each frame.
    /// Stored in GlobalContext to support platform-specific debug views and GM tools.
    /// </summary>
    public sealed class CameraCullingDebugState
    {
        public Vector2 LogicalTargetCm;
        public float LogicalMinX;
        public float LogicalMaxX;
        public float LogicalMinY;
        public float LogicalMaxY;

        public float HighLodDistCm;
        public float MediumLodDistCm;
        public float LowLodDistCm;

        public int QueryCount;
        public int QueryDropped;

        public int VisibleHighCount;
        public int VisibleMediumCount;
        public int VisibleLowCount;
        public int CulledCount;
    }
}
