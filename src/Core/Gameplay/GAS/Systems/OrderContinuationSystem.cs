using System;
using Arch.Core;
using Arch.System;
using Ludots.Core.Engine;
using Ludots.Core.Gameplay.GAS.Components;
using Ludots.Core.Gameplay.GAS.Orders;

namespace Ludots.Core.Gameplay.GAS.Systems
{
    /// <summary>
    /// Re-submits planned follow-up orders after their trigger order completes.
    /// This keeps move-then-cast and future chained commands generic and queue-safe.
    /// </summary>
    public sealed class OrderContinuationSystem : BaseSystem<World, float>
    {
        private static readonly QueryDescription Query = new QueryDescription()
            .WithAll<OrderBuffer, OrderContinuationBuffer, CompletedOrderSignal>();

        private readonly IClock _clock;
        private readonly OrderTypeRegistry _orderTypeRegistry;
        private readonly OrderRuleRegistry _orderRuleRegistry;
        private readonly int _stepRateHz;

        public OrderContinuationSystem(
            World world,
            IClock clock,
            OrderTypeRegistry orderTypeRegistry,
            OrderRuleRegistry orderRuleRegistry,
            int stepRateHz = 30)
            : base(world)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _orderTypeRegistry = orderTypeRegistry ?? throw new ArgumentNullException(nameof(orderTypeRegistry));
            _orderRuleRegistry = orderRuleRegistry ?? throw new ArgumentNullException(nameof(orderRuleRegistry));
            if (stepRateHz <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepRateHz), stepRateHz, "stepRateHz must be positive.");
            }

            _stepRateHz = stepRateHz;
        }

        public override void Update(in float dt)
        {
            int currentStep = _clock.Now(ClockDomainId.Step);

            World.Query(in Query, (Entity entity, ref OrderBuffer buffer, ref OrderContinuationBuffer continuations, ref CompletedOrderSignal signal) =>
            {
                if (signal.OrderId <= 0 || !continuations.HasEntries)
                {
                    signal = default;
                    return;
                }

                Span<Order> extracted = stackalloc Order[OrderContinuationBuffer.MAX_CONTINUATIONS];
                int count = continuations.Extract(signal.OrderId, extracted);
                signal = default;

                for (int i = 0; i < count; i++)
                {
                    var order = extracted[i];
                    order.Actor = entity;

                    var result = OrderSubmitter.Submit(
                        World,
                        entity,
                        in order,
                        _orderTypeRegistry,
                        _orderRuleRegistry,
                        currentStep,
                        _stepRateHz);

                    if (result != OrderSubmitResult.Blocked)
                    {
                        continue;
                    }

                    var config = _orderTypeRegistry.Get(order.OrderTypeId);
                    if (config.PendingBufferWindowMs <= 0)
                    {
                        continue;
                    }

                    int expireStep = currentStep + (config.PendingBufferWindowMs * _stepRateHz) / 1000;
                    buffer.SetPending(in order, config.Priority, expireStep, currentStep);
                }
            });
        }
    }
}
