using System;
using System.Numerics;
using Arch.Core;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Orders;
using Ludots.Core.Mathematics;
using Ludots.Core.Navigation.Pathing;
using Ludots.Core.Presentation.Rendering;

namespace Ludots.Core.Input.Orders
{
    /// <summary>
    /// Emits path overlays for the currently viewed selection by solving each active/queued
    /// move leg through the shared pathing runtime.
    /// </summary>
    public sealed class SelectedMovePathOverlayBridge
    {
        private const int MaxSelectedEntities = 4;
        private const int DefaultMaxPathPoints = 64;
        private const float OverlayY = 0.035f;
        private const float PrimaryLineWidthCm = 28f;
        private const float SecondaryLineWidthCm = 18f;
        private const float WaypointRadiusCm = 26f;

        private static readonly Vector4 PrimaryFill = new(0.22f, 0.86f, 0.98f, 0.18f);
        private static readonly Vector4 PrimaryBorder = new(0.46f, 0.95f, 1.0f, 0.96f);
        private static readonly Vector4 SecondaryFill = new(0.38f, 0.78f, 0.92f, 0.10f);
        private static readonly Vector4 SecondaryBorder = new(0.50f, 0.88f, 0.98f, 0.68f);

        private readonly World _world;
        private readonly IPathService _paths;
        private readonly PathStore _pathStore;
        private readonly GroundOverlayBuffer _overlays;
        private readonly int _moveToOrderTypeId;
        private readonly int[] _pathXcm = new int[DefaultMaxPathPoints];
        private readonly int[] _pathYcm = new int[DefaultMaxPathPoints];
        private int _nextRequestId = 1;

        public SelectedMovePathOverlayBridge(
            World world,
            IPathService paths,
            PathStore pathStore,
            GroundOverlayBuffer overlays,
            int moveToOrderTypeId)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _pathStore = pathStore ?? throw new ArgumentNullException(nameof(pathStore));
            _overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            _moveToOrderTypeId = moveToOrderTypeId;
        }

        public void UpdateViewedSelection(ReadOnlySpan<Entity> selected)
        {
            int emittedEntities = 0;
            for (int i = 0; i < selected.Length && emittedEntities < MaxSelectedEntities; i++)
            {
                Entity entity = selected[i];
                if (!_world.IsAlive(entity) || !_world.Has<OrderBuffer>(entity))
                {
                    continue;
                }

                EmitEntityPath(entity, emittedEntities == 0);
                emittedEntities++;
            }
        }

        private void EmitEntityPath(Entity entity, bool isPrimary)
        {
            if (!OrderWorldSpatialResolver.TryGetEntityWorldCm(_world, entity, out var originWorldCm))
            {
                return;
            }

            ref var buffer = ref _world.Get<OrderBuffer>(entity);
            if (buffer.HasActive &&
                buffer.ActiveOrder.Order.OrderTypeId == _moveToOrderTypeId &&
                OrderWorldSpatialResolver.TryResolveMoveDestination(in buffer.ActiveOrder.Order, out var activeDestination))
            {
                EmitSolvedLeg(entity, originWorldCm, activeDestination, isPrimary);
                originWorldCm = activeDestination;
            }

            for (int i = 0; i < buffer.QueuedCount; i++)
            {
                Order queued = buffer.GetQueued(i).Order;
                if (queued.OrderTypeId != _moveToOrderTypeId ||
                    !OrderWorldSpatialResolver.TryResolveMoveDestination(in queued, out var queuedDestination))
                {
                    continue;
                }

                EmitSolvedLeg(entity, originWorldCm, queuedDestination, isPrimary);
                originWorldCm = queuedDestination;
            }
        }

        private void EmitSolvedLeg(Entity actor, Vector3 startWorldCm, Vector3 goalWorldCm, bool isPrimary)
        {
            if (DistanceCm(startWorldCm, goalWorldCm) <= 0.01f)
            {
                return;
            }

            var request = new PathRequest(
                _nextRequestId++,
                actor,
                PathDomain.Auto,
                PathEndpoint.FromWorldCm((int)MathF.Round(startWorldCm.X), (int)MathF.Round(startWorldCm.Z)),
                PathEndpoint.FromWorldCm((int)MathF.Round(goalWorldCm.X), (int)MathF.Round(goalWorldCm.Z)),
                new PathBudget(maxExpanded: 0, maxPoints: DefaultMaxPathPoints));

            if (!_paths.TrySolve(in request, out var result) ||
                result.Status != PathStatus.Found ||
                !result.Handle.IsValid)
            {
                EmitDirectLeg(startWorldCm, goalWorldCm, isPrimary);
                return;
            }

            try
            {
                if (!_paths.TryCopyPath(in result.Handle, _pathXcm, _pathYcm, out int count) ||
                    count < 2)
                {
                    EmitDirectLeg(startWorldCm, goalWorldCm, isPrimary);
                    return;
                }

                EmitPolyline(count, isPrimary);
            }
            finally
            {
                if (_pathStore.IsAlive(result.Handle))
                {
                    _pathStore.Release(result.Handle);
                }
            }
        }

        private void EmitDirectLeg(Vector3 startWorldCm, Vector3 goalWorldCm, bool isPrimary)
        {
            _pathXcm[0] = (int)MathF.Round(startWorldCm.X);
            _pathYcm[0] = (int)MathF.Round(startWorldCm.Z);
            _pathXcm[1] = (int)MathF.Round(goalWorldCm.X);
            _pathYcm[1] = (int)MathF.Round(goalWorldCm.Z);
            EmitPolyline(count: 2, isPrimary);
        }

        private void EmitPolyline(int count, bool isPrimary)
        {
            Vector4 fill = isPrimary ? PrimaryFill : SecondaryFill;
            Vector4 border = isPrimary ? PrimaryBorder : SecondaryBorder;
            float widthMeters = WorldUnits.CmToM(isPrimary ? PrimaryLineWidthCm : SecondaryLineWidthCm);

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 start = ToVisualMeters(_pathXcm[i], _pathYcm[i]);
                Vector3 end = ToVisualMeters(_pathXcm[i + 1], _pathYcm[i + 1]);
                Vector2 delta = new(end.X - start.X, end.Z - start.Z);
                float length = delta.Length();
                if (length <= 0.0001f)
                {
                    continue;
                }

                _overlays.TryAdd(new GroundOverlayItem
                {
                    Shape = GroundOverlayShape.Line,
                    Center = start,
                    Length = length,
                    Width = widthMeters,
                    Rotation = MathF.Atan2(delta.Y, delta.X),
                    FillColor = fill,
                    BorderColor = border,
                    BorderWidth = 0.02f
                });
            }

            _overlays.TryAdd(new GroundOverlayItem
            {
                Shape = GroundOverlayShape.Circle,
                Center = ToVisualMeters(_pathXcm[count - 1], _pathYcm[count - 1]),
                Radius = WorldUnits.CmToM(WaypointRadiusCm),
                FillColor = fill,
                BorderColor = border,
                BorderWidth = 0.025f
            });
        }

        private static Vector3 ToVisualMeters(int xcm, int ycm)
        {
            return new Vector3(WorldUnits.CmToM(xcm), OverlayY, WorldUnits.CmToM(ycm));
        }

        private static float DistanceCm(Vector3 startWorldCm, Vector3 goalWorldCm)
        {
            float dx = goalWorldCm.X - startWorldCm.X;
            float dz = goalWorldCm.Z - startWorldCm.Z;
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }
    }
}
