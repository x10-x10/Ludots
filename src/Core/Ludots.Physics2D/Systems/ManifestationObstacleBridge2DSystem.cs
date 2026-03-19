using System;
using Arch.Core;
using Arch.System;
using Ludots.Core.Components;
using Ludots.Core.Gameplay.Spawning;
using Ludots.Core.Mathematics;
using Ludots.Core.Mathematics.FixedPoint;
using Ludots.Core.Navigation2D.Components;
using Ludots.Core.Physics2D.Components;

namespace Ludots.Core.Physics2D.Systems
{
    /// <summary>
    /// Bridges runtime manifestation blocker intent into lower-layer physics and navigation components.
    /// This keeps spell/runtime authoring declarative while collision and nav remain owned by their subsystems.
    /// </summary>
    public sealed class ManifestationObstacleBridge2DSystem : BaseSystem<World, float>
    {
        private static readonly QueryDescription _query = new QueryDescription()
            .WithAll<WorldPositionCm, ManifestationObstacleIntent2D>();

        public ManifestationObstacleBridge2DSystem(World world) : base(world)
        {
        }

        public override void Update(in float dt)
        {
            World.Query(in _query, (Entity entity, ref WorldPositionCm worldPosition, ref ManifestationObstacleIntent2D intent) =>
            {
                Upsert(entity, new Position2D { Value = worldPosition.Value });

                if (World.TryGet(entity, out FacingDirection facing))
                {
                    Upsert(entity, new Rotation2D { Value = Fix64.FromFloat(facing.AngleRad) });
                }

                int signature = ComputeShapeSignature(in intent, entity);
                int shapeDataIndex = EnsureShapeRegistered(entity, in intent, signature);

                if (intent.SinkPhysicsCollider != 0)
                {
                    Upsert(entity, new Collider2D
                    {
                        Type = ToColliderType(intent.Shape),
                        ShapeDataIndex = shapeDataIndex
                    });
                    Upsert(entity, Mass2D.Static);
                    Upsert(entity, Velocity2D.Zero);
                }

                if (intent.SinkNavigationObstacle != 0)
                {
                    Upsert(entity, new NavObstacle2D
                    {
                        Shape = ToNavObstacleShape(intent.Shape),
                        ShapeDataIndex = shapeDataIndex
                    });
                    Upsert(entity, new NavKinematics2D
                    {
                        MaxSpeedCmPerSec = Fix64.Zero,
                        MaxAccelCmPerSec2 = Fix64.Zero,
                        RadiusCm = ResolveNavRadiusCm(entity, in intent, shapeDataIndex),
                        NeighborDistCm = Fix64.Zero,
                        TimeHorizonSec = Fix64.Zero,
                        MaxNeighbors = 0
                    });
                }
            });
        }

        private int EnsureShapeRegistered(Entity entity, in ManifestationObstacleIntent2D intent, int signature)
        {
            if (World.TryGet(entity, out ManifestationObstacleBridge2DState bridgeState) &&
                bridgeState.ShapeSignature == signature &&
                bridgeState.ShapeDataIndex >= 0)
            {
                return bridgeState.ShapeDataIndex;
            }

            int shapeDataIndex = RegisterShape(entity, in intent);
            Upsert(entity, new ManifestationObstacleBridge2DState
            {
                ShapeDataIndex = shapeDataIndex,
                ShapeSignature = signature
            });
            return shapeDataIndex;
        }

        private int RegisterShape(Entity entity, in ManifestationObstacleIntent2D intent)
        {
            return intent.Shape switch
            {
                ManifestationObstacleShape2D.Circle => ShapeDataStorage2D.RegisterCircle(
                    Fix64.FromInt(intent.RadiusCm),
                    Fix64Vec2.FromInt(intent.LocalOffsetXCm, intent.LocalOffsetYCm)),
                ManifestationObstacleShape2D.Box => ShapeDataStorage2D.RegisterBox(
                    Fix64.FromInt(intent.HalfWidthCm),
                    Fix64.FromInt(intent.HalfHeightCm),
                    Fix64Vec2.FromInt(intent.LocalOffsetXCm, intent.LocalOffsetYCm)),
                ManifestationObstacleShape2D.Polygon => RegisterPolygon(entity),
                _ => throw new InvalidOperationException($"Unsupported manifestation obstacle shape '{intent.Shape}'.")
            };
        }

        private int RegisterPolygon(Entity entity)
        {
            if (!World.TryGet(entity, out ManifestationObstaclePolygon2D polygon))
            {
                throw new InvalidOperationException("ManifestationObstacleIntent2D with Polygon shape requires ManifestationObstaclePolygon2D.");
            }

            int count = polygon.VertexCount;
            if (count < 3 || count > ManifestationObstaclePolygon2D.MaxVertices)
            {
                throw new InvalidOperationException($"ManifestationObstaclePolygon2D vertex count must be between 3 and {ManifestationObstaclePolygon2D.MaxVertices}.");
            }

            var vertices = new Fix64Vec2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = ToFix64Vec2(polygon.GetVertex(i));
            }

            return ShapeDataStorage2D.RegisterPolygon(vertices);
        }

        private int ComputeShapeSignature(in ManifestationObstacleIntent2D intent, Entity entity)
        {
            var hash = new HashCode();
            hash.Add((byte)intent.Shape);
            hash.Add(intent.RadiusCm);
            hash.Add(intent.HalfWidthCm);
            hash.Add(intent.HalfHeightCm);
            hash.Add(intent.LocalOffsetXCm);
            hash.Add(intent.LocalOffsetYCm);
            hash.Add(intent.NavRadiusCm);

            if (intent.Shape == ManifestationObstacleShape2D.Polygon &&
                World.TryGet(entity, out ManifestationObstaclePolygon2D polygon))
            {
                hash.Add(polygon.VertexCount);
                for (int i = 0; i < polygon.VertexCount; i++)
                {
                    var vertex = polygon.GetVertex(i);
                    hash.Add(vertex.X);
                    hash.Add(vertex.Y);
                }
            }

            return hash.ToHashCode();
        }

        private static Fix64 ResolveNavRadiusCm(Entity entity, in ManifestationObstacleIntent2D intent, int shapeDataIndex)
        {
            if (intent.NavRadiusCm > 0)
            {
                return Fix64.FromInt(intent.NavRadiusCm);
            }

            return intent.Shape switch
            {
                ManifestationObstacleShape2D.Circle when ShapeDataStorage2D.TryGetCircle(shapeDataIndex, out var circle) => circle.Radius,
                ManifestationObstacleShape2D.Box when ShapeDataStorage2D.TryGetBox(shapeDataIndex, out var box) =>
                    Fix64Math.Sqrt(box.HalfWidth * box.HalfWidth + box.HalfHeight * box.HalfHeight),
                ManifestationObstacleShape2D.Polygon when ShapeDataStorage2D.TryGetPolygon(shapeDataIndex, out var polygon) => ResolvePolygonRadius(polygon),
                _ => Fix64.Zero
            };
        }

        private static Fix64 ResolvePolygonRadius(in PolygonShapeData polygon)
        {
            Fix64 maxDistanceSq = Fix64.Zero;
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                Fix64Vec2 delta = polygon.Vertices[i] - polygon.LocalCenter;
                Fix64 distanceSq = delta.LengthSquared();
                if (distanceSq > maxDistanceSq)
                {
                    maxDistanceSq = distanceSq;
                }
            }

            return maxDistanceSq > Fix64.Zero ? Fix64Math.Sqrt(maxDistanceSq) : Fix64.Zero;
        }

        private static ColliderType2D ToColliderType(ManifestationObstacleShape2D shape)
        {
            return shape switch
            {
                ManifestationObstacleShape2D.Circle => ColliderType2D.Circle,
                ManifestationObstacleShape2D.Box => ColliderType2D.Box,
                ManifestationObstacleShape2D.Polygon => ColliderType2D.Polygon,
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
        }

        private static NavObstacleShape2D ToNavObstacleShape(ManifestationObstacleShape2D shape)
        {
            return shape switch
            {
                ManifestationObstacleShape2D.Circle => NavObstacleShape2D.Circle,
                ManifestationObstacleShape2D.Box => NavObstacleShape2D.Box,
                ManifestationObstacleShape2D.Polygon => NavObstacleShape2D.Polygon,
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
        }

        private static Fix64Vec2 ToFix64Vec2(in WorldCmInt2 point)
        {
            return Fix64Vec2.FromInt(point.X, point.Y);
        }

        private void Upsert<T>(Entity entity, in T component)
        {
            if (World.Has<T>(entity))
            {
                World.Set(entity, component);
            }
            else
            {
                World.Add(entity, component);
            }
        }
    }
}
