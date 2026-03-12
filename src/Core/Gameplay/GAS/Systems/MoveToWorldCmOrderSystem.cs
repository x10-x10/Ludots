using System;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.System;
using Ludots.Core.Components;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Orders;
using Ludots.Core.Gameplay.GAS.Registry;
using Ludots.Core.Mathematics.FixedPoint;

namespace Ludots.Core.Gameplay.GAS.Systems
{
    public sealed class MoveToWorldCmOrderSystem : BaseSystem<World, float>
    {
        private static readonly QueryDescription Query = new QueryDescription()
            .WithAll<WorldPositionCm, OrderBuffer>();

        private readonly OrderTypeRegistry _orderTypeRegistry;
        private readonly int _moveToOrderTypeId;
        private readonly float _defaultSpeedCmPerSec;
        private readonly float _stopRadiusCm;
        private readonly int _moveSpeedAttributeId;

        public MoveToWorldCmOrderSystem(
            World world,
            OrderTypeRegistry orderTypeRegistry,
            int moveToOrderTypeId,
            float defaultSpeedCmPerSec = 600f,
            float stopRadiusCm = 40f) : base(world)
        {
            _orderTypeRegistry = orderTypeRegistry ?? throw new ArgumentNullException(nameof(orderTypeRegistry));
            _moveToOrderTypeId = moveToOrderTypeId;
            _defaultSpeedCmPerSec = Math.Max(0f, defaultSpeedCmPerSec);
            _stopRadiusCm = Math.Max(0f, stopRadiusCm);
            _moveSpeedAttributeId = AttributeRegistry.Register("MoveSpeed");
        }

        public override void Update(in float dt)
        {
            if (_moveToOrderTypeId <= 0 || dt <= 0f)
            {
                return;
            }

            foreach (ref var chunk in World.Query(in Query))
            {
                var positions = chunk.GetSpan<WorldPositionCm>();
                var buffers = chunk.GetSpan<OrderBuffer>();
                ref var entityFirst = ref chunk.Entity(0);

                foreach (var index in chunk)
                {
                    var entity = Unsafe.Add(ref entityFirst, index);
                    if (!World.IsAlive(entity))
                    {
                        continue;
                    }

                    ref var buffer = ref buffers[index];
                    if (!buffer.HasActive || buffer.ActiveOrder.Order.OrderTypeId != _moveToOrderTypeId)
                    {
                        continue;
                    }

                    if (!TryResolveTarget(in buffer.ActiveOrder.Order, out var target))
                    {
                        OrderSubmitter.NotifyOrderComplete(World, entity, _orderTypeRegistry);
                        continue;
                    }

                    float speedCmPerSec = ResolveMoveSpeed(entity);
                    if (speedCmPerSec <= 0f)
                    {
                        continue;
                    }

                    ref var position = ref positions[index];
                    var current = position.Value;
                    bool arrived = WorldMoveCmStepHelper.StepTowards(
                        ref current,
                        target,
                        stepCm: speedCmPerSec * dt,
                        stopRadiusCm: _stopRadiusCm);
                    position.Value = current;

                    if (arrived)
                    {
                        OrderSubmitter.NotifyOrderComplete(World, entity, _orderTypeRegistry);
                    }
                }
            }
        }

        private float ResolveMoveSpeed(Entity entity)
        {
            if (_moveSpeedAttributeId > 0 &&
                World.TryGet(entity, out AttributeBuffer attributes))
            {
                float configured = attributes.GetCurrent(_moveSpeedAttributeId);
                if (configured > 0f)
                {
                    return configured;
                }
            }

            return _defaultSpeedCmPerSec;
        }

        private static bool TryResolveTarget(in Order order, out Fix64Vec2 target)
        {
            target = default;
            ref readonly var spatial = ref order.Args.Spatial;
            if (spatial.Kind != OrderSpatialKind.WorldCm)
            {
                return false;
            }

            if (spatial.Mode == OrderCollectionMode.List && spatial.PointCount > 0)
            {
                unsafe
                {
                    fixed (int* px = spatial.PointX)
                    fixed (int* pz = spatial.PointZ)
                    {
                        int last = spatial.PointCount - 1;
                        target = Fix64Vec2.FromInt(px[last], pz[last]);
                        return true;
                    }
                }
            }

            if (spatial.Mode == OrderCollectionMode.Single)
            {
                target = Fix64Vec2.FromFloat(spatial.WorldCm.X, spatial.WorldCm.Z);
                return true;
            }

            return false;
        }
    }
}
