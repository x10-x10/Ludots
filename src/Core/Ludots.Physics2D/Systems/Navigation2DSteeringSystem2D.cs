using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Arch.Buffer;
using Arch.Core;
using Arch.System;
using Ludots.Core.Mathematics.FixedPoint;
using Ludots.Core.Navigation2D.Avoidance;
using Ludots.Core.Navigation2D.Components;
using Ludots.Core.Navigation2D.Runtime;
using Ludots.Core.Physics;
using Ludots.Core.Physics2D.Components;

namespace Ludots.Core.Physics2D.Systems
{
    public sealed class Navigation2DSteeringSystem2D : BaseSystem<World, float>
    {
        private static readonly QueryDescription _needsForceInput = new QueryDescription()
            .WithAll<NavAgent2D, Position2D, Velocity2D, NavKinematics2D>()
            .WithNone<ForceInput2D>();

        private static readonly QueryDescription _needsDesiredVelocity = new QueryDescription()
            .WithAll<NavAgent2D, Position2D, Velocity2D, NavKinematics2D>()
            .WithNone<NavDesiredVelocity2D>();

        private static readonly QueryDescription _agentQuery = new QueryDescription()
            .WithAll<NavAgent2D, Position2D, Velocity2D, NavKinematics2D, ForceInput2D, NavDesiredVelocity2D>();

        private static readonly QueryDescription _flowGoalQuery = new QueryDescription()
            .WithAll<NavFlowGoal2D>();

        private readonly Navigation2DRuntime _runtime;
        private readonly CommandBuffer _commandBuffer = new();

        private const int MaxNeighborsHard = 64;

        public Navigation2DSteeringSystem2D(World world, Navigation2DRuntime runtime) : base(world)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public override void Update(in float deltaTime)
        {
            EnsureSteeringOutputs();

            TryApplyFlowGoal();
            if (_runtime.FlowEnabled)
            {
                for (int f = 0; f < _runtime.FlowCount; f++)
                {
                    _runtime.Flows[f].Step(_runtime.FlowIterationsPerTick);
                }
            }

            BuildAgentSoA();
            var agentSoA = _runtime.AgentSoA;
            var positions = agentSoA.Positions.AsSpan();
            _runtime.CellMap.Build(positions);

            float dt = deltaTime > 1e-6f ? deltaTime : 1e-6f;
            float invDt = 1f / dt;

            var velocities = agentSoA.Velocities.AsSpan();
            var radii = agentSoA.Radii.AsSpan();
            var maxSpeeds = agentSoA.MaxSpeeds.AsSpan();
            var maxAccels = agentSoA.MaxAccels.AsSpan();
            var neighborDistances = agentSoA.NeighborDistances.AsSpan();
            var timeHorizons = agentSoA.TimeHorizons.AsSpan();
            var maxNeighbors = agentSoA.MaxNeighbors.AsSpan();
            var preferredVelocities = agentSoA.PreferredVelocities.AsSpan();
            var outputForces = agentSoA.OutputForces.AsSpan();
            var outputDesiredVelocities = agentSoA.OutputDesiredVelocities.AsSpan();

            Span<int> neighborIdxScratch = stackalloc int[MaxNeighborsHard];
            Span<OrcaSolver2D.Neighbor> neighborScratch = stackalloc OrcaSolver2D.Neighbor[MaxNeighborsHard];
            Span<OrcaSolver2D.OrcaLine> lineScratch = stackalloc OrcaSolver2D.OrcaLine[MaxNeighborsHard];
            Span<OrcaSolver2D.OrcaLine> projectionLineScratch = stackalloc OrcaSolver2D.OrcaLine[OrcaSolver2D.MaxProjectionLines];

            for (int i = 0; i < agentSoA.Count; i++)
            {
                Vector2 pos = positions[i];
                Vector2 vel = velocities[i];
                float radius = radii[i];
                float maxSpeed = maxSpeeds[i];
                float maxAccel = maxAccels[i];
                float neighborDistance = neighborDistances[i];
                float timeHorizon = timeHorizons[i];
                int neighborLimit = maxNeighbors[i];
                Vector2 preferred = preferredVelocities[i];

                int neighborCount = 0;
                if (neighborLimit > 0 && neighborDistance > 0f)
                {
                    neighborCount = _runtime.CellMap.CollectNeighbors(
                        selfIndex: i,
                        selfPos: pos,
                        radius: neighborDistance,
                        positions: positions,
                        neighborsOut: neighborIdxScratch.Slice(0, neighborLimit));
                }

                for (int n = 0; n < neighborCount; n++)
                {
                    int j = neighborIdxScratch[n];
                    neighborScratch[n] = new OrcaSolver2D.Neighbor(positions[j], velocities[j], radii[j]);
                }

                Vector2 newVel = neighborCount > 0
                    ? OrcaSolver2D.ComputeDesiredVelocity(
                        position: pos,
                        velocity: vel,
                        preferredVelocity: preferred,
                        maxSpeed: maxSpeed,
                        radius: radius,
                        timeHorizon: timeHorizon,
                        deltaTime: dt,
                        neighbors: neighborScratch.Slice(0, neighborCount),
                        linesScratch: lineScratch,
                        projectionLinesScratch: projectionLineScratch)
                    : ClampToMaxSpeed(preferred, maxSpeed);

                Vector2 accel = (newVel - vel) * invDt;
                float accelLenSq = accel.LengthSquared();
                float maxAccelSq = maxAccel * maxAccel;
                if (accelLenSq > maxAccelSq && accelLenSq > 1e-12f)
                {
                    accel *= maxAccel / MathF.Sqrt(accelLenSq);
                }

                outputForces[i] = accel;
                outputDesiredVelocities[i] = newVel;
            }

            ApplySteeringOutputs();
        }

        private void EnsureSteeringOutputs()
        {
            foreach (ref var chunk in World.Query(in _needsForceInput))
            {
                ref var entityFirst = ref chunk.Entity(0);
                foreach (var index in chunk)
                {
                    var entity = Unsafe.Add(ref entityFirst, index);
                    _commandBuffer.Add(entity, new ForceInput2D { Force = Fix64Vec2.Zero });
                }
            }

            foreach (ref var chunk in World.Query(in _needsDesiredVelocity))
            {
                ref var entityFirst = ref chunk.Entity(0);
                foreach (var index in chunk)
                {
                    var entity = Unsafe.Add(ref entityFirst, index);
                    _commandBuffer.Add(entity, new NavDesiredVelocity2D { ValueCmPerSec = Fix64Vec2.Zero });
                }
            }

            if (_commandBuffer.Size > 0)
            {
                _commandBuffer.Playback(World);
            }
        }

        private void BuildAgentSoA()
        {
            _runtime.AgentSoA.Clear();

            foreach (ref var chunk in World.Query(in _agentQuery))
            {
                chunk.GetSpan<Position2D, Velocity2D, NavKinematics2D>(out var positionsCm, out var velocitiesCm, out var kinematics);

                bool hasGoal = chunk.Has<NavGoal2D>();
                Span<NavGoal2D> goals = default;
                if (hasGoal)
                {
                    goals = chunk.GetSpan<NavGoal2D>();
                }

                bool hasFlowBinding = _runtime.FlowEnabled && chunk.Has<NavFlowBinding2D>();
                Span<NavFlowBinding2D> flowBindings = default;
                if (hasFlowBinding)
                {
                    flowBindings = chunk.GetSpan<NavFlowBinding2D>();
                }

                foreach (var index in chunk)
                {
                    var positionCm = positionsCm[index].Value;
                    var velocityCm = velocitiesCm[index].Linear;
                    var kin = kinematics[index];

                    Vector2 position = positionCm.ToVector2();
                    Vector2 velocity = velocityCm.ToVector2();
                    float maxSpeed = kin.MaxSpeedCmPerSec.ToFloat();
                    Vector2 preferredVelocity = Vector2.Zero;
                    bool hasPreferredVelocity = false;

                    if (hasFlowBinding)
                    {
                        var flow = _runtime.TryGetFlow(flowBindings[index].FlowId);
                        if (flow != null && flow.TrySampleDesiredVelocityCm(positionCm, kin.MaxSpeedCmPerSec, out Fix64Vec2 desiredCm))
                        {
                            preferredVelocity = desiredCm.ToVector2();
                            hasPreferredVelocity = true;
                        }
                    }

                    if (!hasPreferredVelocity && hasGoal)
                    {
                        preferredVelocity = ComputeGoalPreferredVelocity(goals[index], position, maxSpeed);
                    }

                    if (!_runtime.AgentSoA.TryAdd(
                        position,
                        velocity,
                        kin.RadiusCm.ToFloat(),
                        maxSpeed,
                        kin.MaxAccelCmPerSec2.ToFloat(),
                        kin.NeighborDistCm.ToFloat(),
                        kin.TimeHorizonSec.ToFloat(),
                        ClampMaxNeighbors(kin.MaxNeighbors),
                        preferredVelocity))
                    {
                        return;
                    }
                }
            }
        }

        private void ApplySteeringOutputs()
        {
            var outputForces = _runtime.AgentSoA.OutputForces.AsSpan();
            var outputDesiredVelocities = _runtime.AgentSoA.OutputDesiredVelocities.AsSpan();
            int outputIndex = 0;

            foreach (ref var chunk in World.Query(in _agentQuery))
            {
                chunk.GetSpan<ForceInput2D, NavDesiredVelocity2D>(out var forces, out var desiredVelocities);
                foreach (var index in chunk)
                {
                    Vector2 accel = outputForces[outputIndex];
                    Vector2 desiredVelocity = outputDesiredVelocities[outputIndex];
                    forces[index] = new ForceInput2D
                    {
                        Force = Fix64Vec2.FromFloat(accel.X, accel.Y)
                    };
                    desiredVelocities[index] = new NavDesiredVelocity2D
                    {
                        ValueCmPerSec = Fix64Vec2.FromFloat(desiredVelocity.X, desiredVelocity.Y)
                    };
                    outputIndex++;
                }
            }
        }

        private void TryApplyFlowGoal()
        {
            if (_runtime.FlowCount <= 0) return;

            bool has = false;
            foreach (ref var chunk in World.Query(in _flowGoalQuery))
            {
                var goals = chunk.GetSpan<NavFlowGoal2D>();
                foreach (var index in chunk)
                {
                    var goal = goals[index];
                    var flow = _runtime.TryGetFlow(goal.FlowId);
                    if (flow == null) continue;
                    flow.SetGoalPoint(goal.GoalCm, goal.RadiusCm);
                    has = true;
                }
            }

            if (!has)
            {
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampMaxNeighbors(int maxNeighbors)
        {
            if (maxNeighbors <= 0)
            {
                return 0;
            }

            return maxNeighbors <= MaxNeighborsHard ? maxNeighbors : MaxNeighborsHard;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 ClampToMaxSpeed(in Vector2 velocity, float maxSpeed)
        {
            float lenSq = velocity.LengthSquared();
            float maxSpeedSq = maxSpeed * maxSpeed;
            if (lenSq <= maxSpeedSq || lenSq <= 1e-12f)
            {
                return velocity;
            }

            return velocity * (maxSpeed / MathF.Sqrt(lenSq));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 ComputeGoalPreferredVelocity(in NavGoal2D goal, in Vector2 position, float maxSpeed)
        {
            if (goal.Kind != NavGoalKind2D.Point || maxSpeed <= 0f)
            {
                return Vector2.Zero;
            }

            Vector2 delta = goal.TargetCm.ToVector2() - position;
            float lenSq = delta.LengthSquared();
            if (lenSq <= 1e-12f)
            {
                return Vector2.Zero;
            }

            return delta * (maxSpeed / MathF.Sqrt(lenSq));
        }
    }
}
