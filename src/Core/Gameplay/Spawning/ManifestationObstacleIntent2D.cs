using System;
using Ludots.Core.Mathematics;

namespace Ludots.Core.Gameplay.Spawning
{
    public enum ManifestationObstacleShape2D : byte
    {
        Circle = 0,
        Box = 1,
        Polygon = 2
    }

    /// <summary>
    /// Runtime manifestation declares blocker intent here; lower layers materialize
    /// the actual physics collider and/or navigation obstacle components.
    /// </summary>
    public struct ManifestationObstacleIntent2D
    {
        public ManifestationObstacleShape2D Shape;
        public byte SinkPhysicsCollider;
        public byte SinkNavigationObstacle;
        public int RadiusCm;
        public int HalfWidthCm;
        public int HalfHeightCm;
        public int LocalOffsetXCm;
        public int LocalOffsetYCm;
        public int NavRadiusCm;
    }

    public struct ManifestationObstaclePolygon2D
    {
        public const int MaxVertices = 8;

        public byte VertexCount;
        public WorldCmInt2 Vertex0;
        public WorldCmInt2 Vertex1;
        public WorldCmInt2 Vertex2;
        public WorldCmInt2 Vertex3;
        public WorldCmInt2 Vertex4;
        public WorldCmInt2 Vertex5;
        public WorldCmInt2 Vertex6;
        public WorldCmInt2 Vertex7;

        public readonly WorldCmInt2 GetVertex(int index)
        {
            return index switch
            {
                0 => Vertex0,
                1 => Vertex1,
                2 => Vertex2,
                3 => Vertex3,
                4 => Vertex4,
                5 => Vertex5,
                6 => Vertex6,
                7 => Vertex7,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        public void SetVertex(int index, in WorldCmInt2 value)
        {
            switch (index)
            {
                case 0:
                    Vertex0 = value;
                    break;
                case 1:
                    Vertex1 = value;
                    break;
                case 2:
                    Vertex2 = value;
                    break;
                case 3:
                    Vertex3 = value;
                    break;
                case 4:
                    Vertex4 = value;
                    break;
                case 5:
                    Vertex5 = value;
                    break;
                case 6:
                    Vertex6 = value;
                    break;
                case 7:
                    Vertex7 = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    public struct ManifestationObstacleBridge2DState
    {
        public int ShapeDataIndex;
        public int ShapeSignature;
    }
}
