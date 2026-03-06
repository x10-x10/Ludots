using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Arch.LowLevel;

namespace Ludots.Core.Navigation2D.Runtime
{
    public sealed unsafe class Navigation2DWorld : IDisposable
    {
        public readonly Navigation2DWorldSettings Settings;

        public UnsafeList<int> EntityIds;
        public UnsafeArray<int> EntityToAgentIndex;
        public UnsafeList<Vector2> Positions;
        public UnsafeList<Vector2> Velocities;
        public UnsafeList<float> Radii;
        public UnsafeList<float> MaxSpeeds;
        public UnsafeList<float> MaxAccels;
        public UnsafeList<float> NeighborDistances;
        public UnsafeList<float> TimeHorizons;
        public UnsafeList<int> MaxNeighbors;
        public UnsafeList<Vector2> PreferredVelocities;
        public UnsafeList<Vector2> OutputForces;
        public UnsafeList<Vector2> OutputDesiredVelocities;
        public UnsafeList<Vector2> GoalPositions;
        public UnsafeList<float> GoalRadii;
        public UnsafeList<float> GoalDistances;
        public UnsafeList<byte> HasPointGoals;
        public UnsafeList<byte> SmartStopFlags;

        public int Count => Positions.Count;

        public Navigation2DWorld(Navigation2DWorldSettings settings)
        {
            Settings = settings;

            int initialEntityCapacity = Math.Max(8, settings.MaxAgents);
            EntityIds = new UnsafeList<int>(settings.MaxAgents);
            EntityToAgentIndex = new UnsafeArray<int>(initialEntityCapacity);
            UnsafeArray.Fill(ref EntityToAgentIndex, -1);

            Positions = new UnsafeList<Vector2>(settings.MaxAgents);
            Velocities = new UnsafeList<Vector2>(settings.MaxAgents);
            Radii = new UnsafeList<float>(settings.MaxAgents);
            MaxSpeeds = new UnsafeList<float>(settings.MaxAgents);
            MaxAccels = new UnsafeList<float>(settings.MaxAgents);
            NeighborDistances = new UnsafeList<float>(settings.MaxAgents);
            TimeHorizons = new UnsafeList<float>(settings.MaxAgents);
            MaxNeighbors = new UnsafeList<int>(settings.MaxAgents);
            PreferredVelocities = new UnsafeList<Vector2>(settings.MaxAgents);
            OutputForces = new UnsafeList<Vector2>(settings.MaxAgents);
            OutputDesiredVelocities = new UnsafeList<Vector2>(settings.MaxAgents);
            GoalPositions = new UnsafeList<Vector2>(settings.MaxAgents);
            GoalRadii = new UnsafeList<float>(settings.MaxAgents);
            GoalDistances = new UnsafeList<float>(settings.MaxAgents);
            HasPointGoals = new UnsafeList<byte>(settings.MaxAgents);
            SmartStopFlags = new UnsafeList<byte>(settings.MaxAgents);
        }

        public bool TryAdd(
            int entityId,
            in Vector2 position,
            in Vector2 velocity,
            float radius,
            float maxSpeed,
            float maxAccel,
            float neighborDistance,
            float timeHorizon,
            int maxNeighbors,
            in Vector2 preferredVelocity,
            bool hasPointGoal,
            in Vector2 goalPosition,
            float goalRadius,
            float goalDistance)
        {
            if (entityId < 0 || Count >= Settings.MaxAgents)
            {
                return false;
            }

            EnsureEntityIndexCapacity(entityId);

            int agentIndex = Count;
            EntityIds.Add(entityId);
            EntityToAgentIndex[entityId] = agentIndex;

            Positions.Add(position);
            Velocities.Add(velocity);
            Radii.Add(radius);
            MaxSpeeds.Add(maxSpeed);
            MaxAccels.Add(maxAccel);
            NeighborDistances.Add(neighborDistance);
            TimeHorizons.Add(timeHorizon);
            MaxNeighbors.Add(maxNeighbors);
            PreferredVelocities.Add(preferredVelocity);
            OutputForces.Add(Vector2.Zero);
            OutputDesiredVelocities.Add(Vector2.Zero);
            GoalPositions.Add(goalPosition);
            GoalRadii.Add(goalRadius);
            GoalDistances.Add(goalDistance);
            HasPointGoals.Add(hasPointGoal ? (byte)1 : (byte)0);
            SmartStopFlags.Add(0);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAgentIndex(int entityId, out int agentIndex)
        {
            if ((uint)entityId >= (uint)EntityToAgentIndex.Length)
            {
                agentIndex = -1;
                return false;
            }

            agentIndex = EntityToAgentIndex[entityId];
            return (uint)agentIndex < (uint)Count;
        }

        public void Clear()
        {
            var entityIds = EntityIds.AsSpan();
            for (int i = 0; i < entityIds.Length; i++)
            {
                int entityId = entityIds[i];
                if ((uint)entityId < (uint)EntityToAgentIndex.Length)
                {
                    EntityToAgentIndex[entityId] = -1;
                }
            }

            EntityIds.Clear();
            Positions.Clear();
            Velocities.Clear();
            Radii.Clear();
            MaxSpeeds.Clear();
            MaxAccels.Clear();
            NeighborDistances.Clear();
            TimeHorizons.Clear();
            MaxNeighbors.Clear();
            PreferredVelocities.Clear();
            OutputForces.Clear();
            OutputDesiredVelocities.Clear();
            GoalPositions.Clear();
            GoalRadii.Clear();
            GoalDistances.Clear();
            HasPointGoals.Clear();
            SmartStopFlags.Clear();
        }

        public void Dispose()
        {
            EntityIds.Dispose();
            EntityToAgentIndex.Dispose();
            Positions.Dispose();
            Velocities.Dispose();
            Radii.Dispose();
            MaxSpeeds.Dispose();
            MaxAccels.Dispose();
            NeighborDistances.Dispose();
            TimeHorizons.Dispose();
            MaxNeighbors.Dispose();
            PreferredVelocities.Dispose();
            OutputForces.Dispose();
            OutputDesiredVelocities.Dispose();
            GoalPositions.Dispose();
            GoalRadii.Dispose();
            GoalDistances.Dispose();
            HasPointGoals.Dispose();
            SmartStopFlags.Dispose();
        }

        private void EnsureEntityIndexCapacity(int entityId)
        {
            if ((uint)entityId < (uint)EntityToAgentIndex.Length)
            {
                return;
            }

            int oldLength = EntityToAgentIndex.Length;
            int newLength = Math.Max(8, oldLength);
            while (newLength <= entityId)
            {
                newLength *= 2;
            }

            EntityToAgentIndex = UnsafeArray.Resize(ref EntityToAgentIndex, newLength);
            EntityToAgentIndex.AsSpan().Slice(oldLength, newLength - oldLength).Fill(-1);
        }
    }
}