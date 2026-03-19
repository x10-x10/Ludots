namespace Ludots.Core.Navigation2D.Components
{
    public enum NavObstacleShape2D : byte
    {
        Circle = 0,
        Box = 1,
        Polygon = 2
    }

    public struct NavObstacle2D
    {
        public NavObstacleShape2D Shape;
        public int ShapeDataIndex;
    }
}
